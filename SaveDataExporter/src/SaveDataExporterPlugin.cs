using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SaveDataExporter;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(CommonAPIPlugin.GUID)]
[CommonAPISubmoduleDependency(nameof(CustomKeyBindSystem), nameof(LocalizationModule))]
public class SaveDataExporterPlugin : BaseUnityPlugin {
    private const string ExportKeyId = "ExportSaveStatistics";
    private const int DefaultTimeLevel = 1;
    private const string DefaultTargetItems = "1143,6006";
    private const string OptionWindowMiscContentPath =
        "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5";
    private const string OptionWindowTipLevelLabelPath =
        "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5/labels/tiplevel";
    private const string OptionWindowTipLevelComboPath =
        "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5/comps/ComboBox";
    private const string OptionWindowLabelName = "sde-output-file-name-mode-label";
    private const string OptionWindowComboName = "sde-output-file-name-mode-combo";
    private const float OptionRowStartY = -220f;
    private const float OptionRowStepY = 40f;
    private static readonly string[] MetricNames = ["实际产量", "理论产量", "实际消耗", "理论消耗"];
    private static readonly double[] RateDivisors = [1d, 10d, 60d, 600d, 6000d];
    private static readonly string[] TimeLevelNames = ["1分钟", "10分钟", "1小时", "10小时", "100小时"];

    private static ManualLogSource logger;
    private static ConfigEntry<string> targetItemsEntry;
    private static ConfigEntry<int> timeLevelEntry;
    private static ConfigEntry<string> outputDirectoryEntry;
    private static ConfigEntry<OutputFileNameMode> outputFileNameModeEntry;
    private static Transform outputFileNameModeParent;
    private static UIComboBox outputFileNameModeComboBox;

    public void Awake() {
        logger = Logger;
        targetItemsEntry = Config.Bind(
            "Export",
            "TargetItems",
            DefaultTargetItems,
            "导出的目标物品，支持物品 ID 或物品名，用逗号分隔。默认 1143,6006 表示增产剂 Mk.III 和宇宙矩阵。");
        timeLevelEntry = Config.Bind(
            "Export",
            "TimeLevel",
            DefaultTimeLevel,
            "统计周期：0=1分钟，1=10分钟，2=1小时，3=10小时，4=100小时。");
        outputDirectoryEntry = Config.Bind(
            "Export",
            "OutputDirectory",
            "",
            "导出目录。留空时使用 BepInEx/config/SaveDataExporter。");
        outputFileNameModeEntry = Config.Bind(
            "Miscellaneous",
            "OutputFileNameMode",
            OutputFileNameMode.TimestampedNewFile,
            "输出文件命名模式。TimestampedNewFile=文件名包含导出时间，每次生成新文件；SaveNameOverwrite=固定为 SaveDataExporter_<存档名>.xlsx，已有同名文件时覆盖。");
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll(typeof(SaveDataExporterPlugin));

        CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(new BuiltinKey {
            key = new CombineKey(0, 0, ECombineKeyAction.OnceClick, true),
            conflictGroup = BuiltinKey.USE_KEYBOARD,
            name = ExportKeyId,
            canOverride = true,
        });
        LocalizationModule.RegisterTranslation("KEY" + ExportKeyId, "Export save statistics", "导出存档统计", "");
    }

    public void Update() {
        if (VFInput.inputing) {
            return;
        }

        PressKeyBind keyBind = CustomKeyBindSystem.GetKeyBind(ExportKeyId);
        if (keyBind?.keyValue != true) {
            return;
        }

        ExportCurrentSaveStatistics();
    }

    private static void ExportCurrentSaveStatistics() {
        if (!TryGetLoadedGameData(out GameData gameData)) {
            ShowTip("未载入存档，跳过导出");
            logger.LogWarning("未载入存档，跳过导出");
            return;
        }

        try {
            List<ExportItem> exportItems = ResolveTargetItems(targetItemsEntry.Value);
            if (exportItems.Count == 0) {
                ShowTip("没有可导出的目标物品");
                logger.LogWarning($"没有可导出的目标物品：{targetItemsEntry.Value}");
                return;
            }

            int timeLevel = Mathf.Clamp(timeLevelEntry.Value, 0, TimeLevelNames.Length - 1);
            RefreshReferenceSpeeds(gameData);
            ExportDataset dataset = BuildDataset(gameData, exportItems, timeLevel);
            string outputPath = BuildOutputPath(gameData);
            XlsxWorkbookWriter.Save(
                outputPath,
                [
                    BuildWideSheet(dataset),
                    BuildNormalizedSheet(dataset),
                ]);

            ShowTip($"已导出存档统计：{outputPath}");
            logger.LogInfo($"已导出存档统计：{outputPath}");
        }
        catch (Exception ex) {
            ShowTip("导出存档统计失败，详情见日志");
            logger.LogError($"导出存档统计失败：{ex}");
        }
    }

    private static bool TryGetLoadedGameData(out GameData gameData) {
        gameData = GameMain.data;
        return GameMain.isRunning
               && gameData?.galaxy != null
               && gameData.statistics?.production?.factoryStatPool != null;
    }

    private static void RefreshReferenceSpeeds(GameData gameData) {
        ProductionExtraInfoCalculator calculator = gameData.statistics.production.extraInfoCalculator;
        for (int i = 0; i < gameData.factoryCount; i++) {
            calculator.AddFactory(i);
        }
        calculator.CalculateImmediately();
    }

    private static List<ExportItem> ResolveTargetItems(string rawValue) {
        IEnumerable<string> tokens = (rawValue ?? "")
            .Split([',', ';', '，', '；', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0);

        List<ExportItem> items = [];
        HashSet<int> seen = [];
        foreach (string token in tokens) {
            ItemProto proto = ResolveItem(token);
            if (proto == null || !seen.Add(proto.ID)) {
                continue;
            }
            items.Add(new ExportItem(proto.ID, GetItemName(proto)));
        }

        return items;
    }

    private static ItemProto ResolveItem(string token) {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)) {
            return LDB.items.Select(itemId);
        }

        string normalizedToken = NormalizeName(token);
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item == null) {
                continue;
            }

            if (NormalizeName(item.name) == normalizedToken
                || NormalizeName(item.Name) == normalizedToken
                || NormalizeName(GetItemName(item)) == normalizedToken) {
                return item;
            }
        }

        return null;
    }

    private static string NormalizeName(string value) {
        return (value ?? "")
            .Replace("\u00a0", "")
            .Replace(" ", "")
            .Trim()
            .ToLowerInvariant();
    }

    private static string GetItemName(ItemProto item) {
        string translatedName = item.Name?.Translate();
        if (!string.IsNullOrWhiteSpace(translatedName) && translatedName != item.Name) {
            return translatedName.Replace('\u00a0', ' ');
        }
        return (item.name ?? item.Name ?? item.ID.ToString(CultureInfo.InvariantCulture)).Replace('\u00a0', ' ');
    }

    private static ExportDataset BuildDataset(GameData gameData, IReadOnlyList<ExportItem> items, int timeLevel) {
        List<PlanetExportRow> planets = [];
        GalaxyData galaxy = gameData.galaxy;
        for (int starIndex = 0; starIndex < galaxy.starCount; starIndex++) {
            StarData star = galaxy.stars[starIndex];
            if (star == null) {
                continue;
            }

            for (int planetIndex = 0; planetIndex < star.planetCount; planetIndex++) {
                PlanetData planet = star.planets[planetIndex];
                if (planet == null) {
                    continue;
                }

                Dictionary<int, ItemStatistics> statistics = [];
                foreach (ExportItem item in items) {
                    statistics[item.ItemId] = GetPlanetItemStatistics(gameData, planet, item.ItemId, timeLevel);
                }

                planets.Add(new PlanetExportRow(
                    star.displayName,
                    planet.displayName,
                    planetIndex,
                    statistics));
            }
        }

        return new ExportDataset(
            DateTime.Now,
            gameData.gameName,
            TimeLevelNames[timeLevel],
            items,
            planets);
    }

    private static ItemStatistics GetPlanetItemStatistics(GameData gameData, PlanetData planet, int itemId, int timeLevel) {
        if (planet.factoryIndex < 0 || planet.factoryIndex >= gameData.statistics.production.factoryStatPool.Length) {
            return ItemStatistics.Zero;
        }

        FactoryProductionStat factoryStat = gameData.statistics.production.factoryStatPool[planet.factoryIndex];
        if (factoryStat?.productIndices == null || itemId < 0 || itemId >= factoryStat.productIndices.Length) {
            return ItemStatistics.Zero;
        }

        int productIndex = factoryStat.productIndices[itemId];
        if (productIndex <= 0 || productIndex >= factoryStat.productCursor) {
            return ItemStatistics.Zero;
        }

        ProductStat productStat = factoryStat.productPool[productIndex];
        if (productStat?.total == null) {
            return ItemStatistics.Zero;
        }

        double divisor = RateDivisors[timeLevel];
        int productionLevel = timeLevel + 1;
        int consumptionLevel = productionLevel + 7;
        return new ItemStatistics(
            productStat.total[productionLevel] / divisor,
            productStat.refProductSpeed,
            productStat.total[consumptionLevel] / divisor,
            productStat.refConsumeSpeed);
    }

    private static XlsxSheet BuildWideSheet(ExportDataset dataset) {
        ExportItem firstItem = dataset.Items[0];
        List<XlsxRow> rows = [
            Row("导出时间", dataset.ExportTime.ToString("yyyy-MM-dd-HH-mm-ss"), "", "", "", "", "导出数据的时间"),
            Row("导出目标", firstItem.Name, "", "", "", "", "导出统计信息的目标产物，这个模板应该只导出单种比较好"),
            Row("统计周期", dataset.TimeLevelName, "", "", "", "", "导出的统计周期，参考游戏内统计面板的统计周期"),
            Row("导出内容", string.Join("、", MetricNames), "", "", "", "", "导出的统计信息，主要是这四种"),
        ];

        List<object> header = ["星系"];
        for (int i = 1; i <= 6; i++) {
            header.Add($"星球{i}");
            header.AddRange(MetricNames);
        }
        rows.Add(Row());
        rows.Add(new XlsxRow(header));

        foreach (IGrouping<string, PlanetExportRow> starGroup in dataset.Planets.GroupBy(row => row.StarName)) {
            List<object> cells = [starGroup.Key];
            foreach (PlanetExportRow planet in starGroup.OrderBy(row => row.PlanetIndex).Take(6)) {
                ItemStatistics stats = planet.Statistics[firstItem.ItemId];
                cells.Add(planet.PlanetName);
                cells.Add(stats.ActualProduction);
                cells.Add(stats.ReferenceProduction);
                cells.Add(stats.ActualConsumption);
                cells.Add(stats.ReferenceConsumption);
            }

            while (cells.Count < header.Count) {
                int planetBlockStart = cells.Count % 5;
                cells.Add(planetBlockStart == 1 ? "/" : "");
            }
            rows.Add(new XlsxRow(cells));
        }

        return new XlsxSheet("星球信息导出模板1", rows);
    }

    private static XlsxSheet BuildNormalizedSheet(ExportDataset dataset) {
        List<XlsxRow> rows = [
            Row("导出时间", dataset.ExportTime.ToString("yyyy-MM-dd-HH-mm-ss"), "", "导出数据的时间"),
            Row("导出目标", string.Join("、", dataset.Items.Select(item => item.Name)), "", "导出统计信息的目标产物，可多选"),
            Row("统计周期", dataset.TimeLevelName, "", "导出的统计周期，参考游戏内统计面板的统计周期"),
            Row("导出内容", string.Join("、", MetricNames), "", "导出的统计信息，如前"),
        ];

        List<object> header = ["星系", "星球"];
        foreach (ExportItem item in dataset.Items) {
            header.Add($"{item.Name}-实际产量");
            header.Add($"{item.Name}-理论产量");
            header.Add($"{item.Name}-实际消耗");
            header.Add($"{item.Name}-理论消耗");
        }

        rows.Add(Row());
        rows.Add(new XlsxRow(header));
        foreach (PlanetExportRow planet in dataset.Planets) {
            List<object> cells = [planet.StarName, planet.PlanetName];
            foreach (ExportItem item in dataset.Items) {
                ItemStatistics stats = planet.Statistics[item.ItemId];
                cells.Add(stats.ActualProduction);
                cells.Add(stats.ReferenceProduction);
                cells.Add(stats.ActualConsumption);
                cells.Add(stats.ReferenceConsumption);
            }
            rows.Add(new XlsxRow(cells));
        }

        return new XlsxSheet("星球信息导出模板2", rows);
    }

    private static XlsxRow Row(params object[] cells) {
        return new XlsxRow(cells);
    }

    private static string BuildOutputPath(GameData gameData) {
        string outputDirectory = outputDirectoryEntry.Value;
        if (string.IsNullOrWhiteSpace(outputDirectory)) {
            string modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Paths.PluginPath;
            outputDirectory = Path.Combine(
                Directory.GetParent(modDirectory)?.Parent?.FullName ?? Paths.ConfigPath,
                "config",
                "SaveDataExporter");
        }

        Directory.CreateDirectory(outputDirectory);
        string saveName = SanitizeFileName(string.IsNullOrWhiteSpace(gameData.gameName) ? "save" : gameData.gameName);
        string fileName = GetOutputFileNameMode() == OutputFileNameMode.SaveNameOverwrite
            ? $"SaveDataExporter_{saveName}.xlsx"
            : $"SaveDataExporter_{saveName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return Path.Combine(outputDirectory, fileName);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), "_OnOpen")]
    [HarmonyAfter(new string[] { "org.LoShin.GenesisBook", "org.ProfessorCat305.OrbitalRing" })]
    private static void UIOptionWindow_OnOpen_Postfix(UIOptionWindow __instance) {
        EnsureOptionWindowControl(__instance);
        ResetOptionWindowControl();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnRevertButtonClick))]
    private static void UIOptionWindow_OnRevertButtonClick_Postfix(int idx) {
        if (idx == 4) {
            ResetOptionWindowControl();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnApplyClick))]
    private static void UIOptionWindow_OnApplyClick_Postfix() {
        if (outputFileNameModeComboBox == null) {
            return;
        }

        outputFileNameModeEntry.Value = IndexToOutputFileNameMode(outputFileNameModeComboBox.itemIndex);
    }

    private static void EnsureOptionWindowControl(UIOptionWindow optionWindow) {
        Transform parent = GetOptionWindowMiscContent(optionWindow);
        if (parent == null) {
            return;
        }

        Transform existing = FindDirectChild(parent, OptionWindowComboName);
        if (existing != null) {
            outputFileNameModeParent = parent;
            outputFileNameModeComboBox = existing.GetComponentInChildren<UIComboBox>();
            if (outputFileNameModeComboBox == null) {
                logger.LogWarning("原版设置-杂项页面下拉框已存在但组件缺失，无法刷新导出文件命名模式。");
                return;
            }

            RefreshOptionWindowControlText();
            return;
        }

        GameObject labelPrefab = GameObject.Find(OptionWindowTipLevelLabelPath);
        GameObject comboPrefab = GameObject.Find(OptionWindowTipLevelComboPath);
        if (labelPrefab == null || comboPrefab == null) {
            logger.LogWarning("未找到原版设置-杂项页面的提示等级控件，无法添加导出文件命名模式下拉框。");
            return;
        }

        float rowY = GetNextOptionRowY(parent);
        outputFileNameModeParent = parent;
        GameObject labelObject = UnityEngine.Object.Instantiate(labelPrefab, parent);
        labelObject.name = OptionWindowLabelName;
        DestroyLocalizer(labelObject);
        ((RectTransform)labelObject.transform).anchoredPosition = new Vector2(30f, rowY);

        GameObject comboObject = UnityEngine.Object.Instantiate(comboPrefab, parent);
        comboObject.name = OptionWindowComboName;
        DestroyLocalizer(comboObject);
        ((RectTransform)comboObject.transform).anchoredPosition = new Vector2(340f, rowY);

        outputFileNameModeComboBox = comboObject.GetComponentInChildren<UIComboBox>();
        if (outputFileNameModeComboBox == null) {
            logger.LogWarning("原版设置-杂项页面下拉框克隆失败，无法添加导出文件命名模式。");
            return;
        }

        ((UnityEventBase)outputFileNameModeComboBox.onItemIndexChange).RemoveAllListeners();
        ((RectTransform)((Component)outputFileNameModeComboBox).transform).sizeDelta = new Vector2(430f, 30f);
        RefreshOptionWindowControlText();
    }

    private static Transform GetOptionWindowMiscContent(UIOptionWindow optionWindow) {
        Transform tipLevelTransform = optionWindow?.tipLevelComp == null
            ? null
            : ((Component)optionWindow.tipLevelComp).transform;
        Transform parent = tipLevelTransform?.parent?.parent;
        if (parent != null) {
            return parent;
        }

        return GameObject.Find(OptionWindowMiscContentPath)?.transform;
    }

    private static Transform FindDirectChild(Transform parent, string objectName) {
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.name == objectName) {
                return child;
            }
        }

        return null;
    }

    private static float GetNextOptionRowY(Transform parent) {
        float rowY = OptionRowStartY;
        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            if (child.name is OptionWindowLabelName or OptionWindowComboName || child is not RectTransform rect) {
                continue;
            }

            float childY = rect.anchoredPosition.y;
            if (childY <= OptionRowStartY + 0.1f) {
                rowY = Math.Min(rowY, childY - OptionRowStepY);
            }
        }

        return rowY;
    }

    private static void RefreshOptionWindowControlText() {
        if (outputFileNameModeComboBox == null) {
            return;
        }

        Transform labelTransform = outputFileNameModeParent == null
            ? null
            : FindDirectChild(outputFileNameModeParent, OptionWindowLabelName);
        if (labelTransform != null && labelTransform.TryGetComponent(out Text labelText)) {
            labelText.text = GetOutputFileNameModeLabel();
        }

        outputFileNameModeComboBox.Items.Clear();
        outputFileNameModeComboBox.Items.AddRange(GetOutputFileNameModeOptions());
        outputFileNameModeComboBox.ItemsData.Clear();
        outputFileNameModeComboBox.translated = true;
        outputFileNameModeComboBox.UpdateItems();
        ResetOptionWindowControl();
    }

    private static void ResetOptionWindowControl() {
        if (outputFileNameModeComboBox != null) {
            outputFileNameModeComboBox.itemIndex = OutputFileNameModeToIndex(GetOutputFileNameMode());
        }
    }

    private static void DestroyLocalizer(GameObject obj) {
        Localizer localizer = obj.GetComponent<Localizer>();
        if (localizer != null) {
            UnityEngine.Object.DestroyImmediate(localizer);
        }
    }

    private static string GetOutputFileNameModeLabel() {
        return IsChineseLanguage()
            ? "导出文件命名模式"
            : "Export file naming";
    }

    private static List<string> GetOutputFileNameModeOptions() {
        return IsChineseLanguage()
            ? ["含导出时间（每次新文件）", "固定存档名（覆盖同名文件）"]
            : ["Timestamped new file", "Save-name overwrite"];
    }

    private static bool IsChineseLanguage() {
        return Localization.CurrentLanguageLCID is 2052 or 1028 or 3076;
    }

    private static OutputFileNameMode GetOutputFileNameMode() {
        OutputFileNameMode mode = outputFileNameModeEntry.Value;
        if (Enum.IsDefined(typeof(OutputFileNameMode), mode)) {
            return mode;
        }

        outputFileNameModeEntry.Value = OutputFileNameMode.TimestampedNewFile;
        return OutputFileNameMode.TimestampedNewFile;
    }

    private static int OutputFileNameModeToIndex(OutputFileNameMode mode) {
        return mode == OutputFileNameMode.SaveNameOverwrite ? 1 : 0;
    }

    private static OutputFileNameMode IndexToOutputFileNameMode(int index) {
        return index == 1 ? OutputFileNameMode.SaveNameOverwrite : OutputFileNameMode.TimestampedNewFile;
    }

    private static string SanitizeFileName(string value) {
        string invalidChars = new(Path.GetInvalidFileNameChars());
        StringBuilder builder = new(value.Length);
        foreach (char c in value) {
            builder.Append(invalidChars.IndexOf(c) >= 0 ? '_' : c);
        }
        return builder.ToString();
    }

    private static void ShowTip(string text) {
        try {
            UIRealtimeTip.Popup(text, sound: false);
        }
        catch {
            // 主页或 UI 未初始化时只保留日志，不让提示路径影响导出主流程。
        }
    }

    private sealed class ExportItem {
        public ExportItem(int itemId, string name) {
            ItemId = itemId;
            Name = name;
        }

        public int ItemId { get; }
        public string Name { get; }
    }

    private sealed class ExportDataset {
        public ExportDataset(
            DateTime exportTime,
            string saveName,
            string timeLevelName,
            IReadOnlyList<ExportItem> items,
            IReadOnlyList<PlanetExportRow> planets) {
            ExportTime = exportTime;
            SaveName = saveName;
            TimeLevelName = timeLevelName;
            Items = items;
            Planets = planets;
        }

        public DateTime ExportTime { get; }
        public string SaveName { get; }
        public string TimeLevelName { get; }
        public IReadOnlyList<ExportItem> Items { get; }
        public IReadOnlyList<PlanetExportRow> Planets { get; }
    }

    private sealed class PlanetExportRow {
        public PlanetExportRow(
            string starName,
            string planetName,
            int planetIndex,
            IReadOnlyDictionary<int, ItemStatistics> statistics) {
            StarName = starName;
            PlanetName = planetName;
            PlanetIndex = planetIndex;
            Statistics = statistics;
        }

        public string StarName { get; }
        public string PlanetName { get; }
        public int PlanetIndex { get; }
        public IReadOnlyDictionary<int, ItemStatistics> Statistics { get; }
    }

    private readonly struct ItemStatistics {
        public static readonly ItemStatistics Zero = new(0, 0, 0, 0);

        public ItemStatistics(
            double actualProduction,
            double referenceProduction,
            double actualConsumption,
            double referenceConsumption) {
            ActualProduction = actualProduction;
            ReferenceProduction = referenceProduction;
            ActualConsumption = actualConsumption;
            ReferenceConsumption = referenceConsumption;
        }

        public double ActualProduction { get; }
        public double ReferenceProduction { get; }
        public double ActualConsumption { get; }
        public double ReferenceConsumption { get; }
    }

    private enum OutputFileNameMode {
        TimestampedNewFile = 0,
        SaveNameOverwrite = 1,
    }
}

internal sealed class XlsxSheet {
    public XlsxSheet(string name, IReadOnlyList<XlsxRow> rows) {
        Name = name;
        Rows = rows;
    }

    public string Name { get; }
    public IReadOnlyList<XlsxRow> Rows { get; }
}

internal sealed class XlsxRow {
    public XlsxRow(IReadOnlyList<object> cells) {
        Cells = cells;
    }

    public IReadOnlyList<object> Cells { get; }
}

internal static class XlsxWorkbookWriter {
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static void Save(string path, IReadOnlyList<XlsxSheet> sheets) {
        using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create);

        AddEntry(archive, "[Content_Types].xml", BuildContentTypes(sheets.Count));
        AddEntry(archive, "_rels/.rels", BuildRootRelationships());
        AddEntry(archive, "docProps/app.xml", BuildAppProperties());
        AddEntry(archive, "docProps/core.xml", BuildCoreProperties());
        AddEntry(archive, "xl/workbook.xml", BuildWorkbook(sheets));
        AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Count));
        for (int i = 0; i < sheets.Count; i++) {
            AddEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildWorksheet(sheets[i]));
        }
    }

    private static void AddEntry(ZipArchive archive, string path, string content) {
        ZipArchiveEntry entry = archive.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using StreamWriter writer = new(stream, Utf8NoBom);
        writer.Write(content);
    }

    private static string BuildContentTypes(int sheetCount) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        builder.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        builder.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
        builder.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
        for (int i = 1; i <= sheetCount; i++) {
            builder.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        }
        builder.Append("""<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>""");
        builder.Append("""<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>""");
        builder.Append("</Types>");
        return builder.ToString();
    }

    private static string BuildRootRelationships() {
        return """
               <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
               <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                 <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                 <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
                 <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
               </Relationships>
               """;
    }

    private static string BuildAppProperties() {
        return """
               <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
               <Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
                 <Application>SaveDataExporter</Application>
               </Properties>
               """;
    }

    private static string BuildCoreProperties() {
        string now = XmlConvert.ToString(DateTime.UtcNow, XmlDateTimeSerializationMode.Utc);
        return $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <dc:creator>SaveDataExporter</dc:creator>
                  <cp:lastModifiedBy>SaveDataExporter</cp:lastModifiedBy>
                  <dcterms:created xsi:type="dcterms:W3CDTF">{now}</dcterms:created>
                  <dcterms:modified xsi:type="dcterms:W3CDTF">{now}</dcterms:modified>
                </cp:coreProperties>
                """;
    }

    private static string BuildWorkbook(IReadOnlyList<XlsxSheet> sheets) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");
        for (int i = 0; i < sheets.Count; i++) {
            builder.Append($"""<sheet name="{EscapeAttribute(sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        }
        builder.Append("</sheets></workbook>");
        return builder.ToString();
    }

    private static string BuildWorkbookRelationships(int sheetCount) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (int i = 1; i <= sheetCount; i++) {
            builder.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
        }
        builder.Append("</Relationships>");
        return builder.ToString();
    }

    private static string BuildWorksheet(XlsxSheet sheet) {
        StringBuilder builder = new();
        builder.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        builder.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++) {
            XlsxRow row = sheet.Rows[rowIndex];
            int excelRow = rowIndex + 1;
            builder.Append($"""<row r="{excelRow}">""");
            for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++) {
                AppendCell(builder, excelRow, columnIndex + 1, row.Cells[columnIndex]);
            }
            builder.Append("</row>");
        }
        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static void AppendCell(StringBuilder builder, int row, int column, object value) {
        string reference = ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
        if (value == null) {
            builder.Append($"""<c r="{reference}"/>""");
            return;
        }

        switch (value) {
            case int intValue:
                builder.Append($"""<c r="{reference}"><v>{intValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case long longValue:
                builder.Append($"""<c r="{reference}"><v>{longValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case float floatValue:
                builder.Append($"""<c r="{reference}"><v>{floatValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case double doubleValue:
                builder.Append($"""<c r="{reference}"><v>{doubleValue.ToString("0.###", CultureInfo.InvariantCulture)}</v></c>""");
                break;
            case decimal decimalValue:
                builder.Append($"""<c r="{reference}"><v>{decimalValue.ToString(CultureInfo.InvariantCulture)}</v></c>""");
                break;
            default:
                builder.Append($"""<c r="{reference}" t="inlineStr"><is><t>{EscapeText(value.ToString() ?? "")}</t></is></c>""");
                break;
        }
    }

    private static string ColumnName(int column) {
        StringBuilder builder = new();
        while (column > 0) {
            column--;
            builder.Insert(0, (char)('A' + column % 26));
            column /= 26;
        }
        return builder.ToString();
    }

    private static string EscapeText(string value) {
        return SecurityElementEscape(value);
    }

    private static string EscapeAttribute(string value) {
        return SecurityElementEscape(value);
    }

    private static string SecurityElementEscape(string value) {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
