using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using static AfterBuildEvent.Utils;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent;

static partial class AfterBuildEvent {
    #region 提取计算器图标资源

    private static void ExportCalcIcons() {
        string dataDir = GetCalcIconDataDir(allowLocalFallback: true);
        if (dataDir == null) {
            Console.WriteLine("未找到计算器 raw 数据，也未找到本地 calc json；请先执行选项 3 生成数据。");
            return;
        }

        RebuildCalcIconsFromGame(dataDir, syncToCalcAssets: IsDspCalcProjectAvailable());
    }

    private static void RebuildCalcIconsFromGame(
        string dataDir,
        bool syncToCalcAssets,
        bool syncGetDspDataToR2 = true) {
        Dictionary<string, string> requiredIconNames = CollectRequiredIconNames(dataDir);
        if (requiredIconNames.Count == 0) {
            Console.WriteLine($"未在计算器数据目录中读取到计算器实际需要的图标：{dataDir}");
            return;
        }

        bool cleanupWorkDir = false;
        PrepareCalcIconExportDirs();
        try {
            CopyStaticCalcIconFallbacks();

            Dictionary<string, MissingCalcIcon> missingIcons = CollectMissingRequiredIconsFromFull(dataDir);
            if (missingIcons.Count == 0) {
                if (syncToCalcAssets) {
                    SyncRequiredIconsToCalcAssets(dataDir);
                }
                Console.WriteLine("本地图标已覆盖所有计算器所需图标。");
                cleanupWorkDir = true;
                return;
            }

            Console.WriteLine($"开始启动游戏提取 {missingIcons.Count} 个缺失图标。");
            foreach (MissingCalcIcon missingIcon in missingIcons.Values) {
                Console.WriteLine(
                    $"缺少图标：{missingIcon.IconName}（{missingIcon.DataFiles.Count} 个数据文件；{string.Join("; ", missingIcon.Examples)}）");
            }

            ExportMissingCalcIconsFromGame(dataDir, missingIcons, syncGetDspDataToR2);
            if (syncToCalcAssets) {
                SyncRequiredIconsToCalcAssets(dataDir);
            } else {
                Console.WriteLine("未检测到计算器项目，跳过同步图标到计算器。");
            }
            missingIcons = CollectMissingRequiredIconsFromFull(dataDir);
            Console.WriteLine($"图标提取结束，剩余缺图：{missingIcons.Count}");
            foreach (MissingCalcIcon missingIcon in missingIcons.Values) {
                Console.WriteLine(
                    $"仍缺图标：{missingIcon.IconName}（{missingIcon.DataFiles.Count} 个数据文件；{string.Join("; ", missingIcon.Examples)}）");
            }
            cleanupWorkDir = missingIcons.Count == 0;
        }
        finally {
            if (cleanupWorkDir) {
                DeleteDirectoryIfExists(CalcIconWorkDir);
                Console.WriteLine($"已清理图标临时目录：{CalcIconWorkDir}");
            } else if (Directory.Exists(CalcIconWorkDir)) {
                Console.WriteLine($"图标流程未完全成功，保留临时目录用于排查：{CalcIconWorkDir}");
            }
        }
    }

    private static string GetCalcIconDataDir(bool allowLocalFallback) {
        if (IsDspCalcProjectAvailable()) {
            return DspCalcRawDataDir;
        }

        Console.WriteLine($"未检测到完整计算器项目：{DspCalcDir}");
        if (allowLocalFallback && Directory.Exists(CalcJsonLocalDir)) {
            Console.WriteLine($"改用本地 calc json 作为图标需求来源：{CalcJsonLocalDir}");
            return CalcJsonLocalDir;
        }

        return null;
    }

    private static bool IsDspCalcProjectAvailable() {
        return File.Exists(Path.Combine(DspCalcDir, "package.json"))
               && Directory.Exists(DspCalcRawDataDir);
    }

    private static void PrepareCalcIconExportDirs() {
        DeleteDirectoryIfExists(CalcIconWorkDir);
        Directory.CreateDirectory(DspCalcFullIconDir);
        Directory.CreateDirectory(CalcIconWorkDir);
    }

    private static void ExportCalcIconsOffline(IReadOnlyDictionary<string, string> requiredIconNames) {
        using CmdProcess cmd = new();
        string assetStudioCli = EnsureAssetStudioCli();
        foreach (CalcIconExportTarget target in GetCalcIconExportTargets()) {
            ExportCalcIconsWithAssetStudio(cmd, assetStudioCli, target, requiredIconNames);
            ExportCalcIconsFromEmbeddedDll(cmd, target, requiredIconNames);
        }
    }

    private static string EnsureAssetStudioCli() {
        if (File.Exists(AssetStudioCliPath)) {
            return AssetStudioCliPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(AssetStudioZipPath) ?? ".");
        Console.WriteLine($"下载 AssetStudio CLI：{AssetStudioDownloadUrl}");
        DownloadAssetStudioZip();

        Console.WriteLine($"解压 AssetStudio CLI：{AssetStudioToolDir}");
        Directory.CreateDirectory(AssetStudioToolDir);
        new FastZip().ExtractZip(AssetStudioZipPath, AssetStudioToolDir, null);
        if (!File.Exists(AssetStudioCliPath)) {
            throw new FileNotFoundException("AssetStudio CLI 解压后未找到可执行文件", AssetStudioCliPath);
        }

        return AssetStudioCliPath;
    }

    private static void DownloadAssetStudioZip() {
        using Process process = Process.Start(new ProcessStartInfo("curl.exe",
            $"-L --retry 3 --connect-timeout 20 --max-time 300 --fail -o \"{AssetStudioZipPath}\" \"{AssetStudioDownloadUrl}\"") {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(AssetStudioZipPath)) {
            throw new InvalidOperationException($"curl.exe 下载 AssetStudio 失败，错误码 {process.ExitCode}");
        }
    }

    private static void ExportCalcIconsWithAssetStudio(
        CmdProcess cmd,
        string assetStudioCli,
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames) {
        List<string> inputPaths = GetAssetStudioInputPaths(target).Distinct().ToList();
        if (inputPaths.Count == 0) {
            return;
        }

        int totalCopied = 0;
        foreach (string inputPath in inputPaths) {
            List<string> nameFilters = GetAssetStudioNameFilters(target, requiredIconNames);
            for (int i = 0; i < nameFilters.Count; i++) {
                string outputDir = Path.Combine(
                    CalcIconWorkDir,
                    "assetstudio",
                    target.TargetMod,
                    MakeSafePathSegment(Path.GetFileName(inputPath)),
                    $"batch-{i + 1}");
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"开始离线提取 Texture2D：{target.TargetMod} <- {inputPath}");
                string arguments =
                    $"\"{inputPath}\" \"{outputDir}\" --game Normal --types Texture2D --image_format Png --group_assets ByType";
                if (!string.IsNullOrWhiteSpace(nameFilters[i])) {
                    arguments += $" --names \"{nameFilters[i]}\"";
                }

                int exitCode = cmd.Run(assetStudioCli, arguments, Path.GetDirectoryName(assetStudioCli));
                if (exitCode != 0) {
                    Console.WriteLine($"AssetStudio 提取失败：{target.TargetMod}，错误码 {exitCode}");
                    continue;
                }

                totalCopied += CopyRequiredPngIcons(outputDir, target, requiredIconNames, require80x80: true);
            }
        }

        if (totalCopied > 0) {
            Console.WriteLine($"{target.TargetMod} AssetStudio 离线图标：复制 {totalCopied}");
        }
    }

    private static List<string> GetAssetStudioNameFilters(
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames) {
        // 原版资源会连带扫描 sharedassets，必须按名字过滤，避免每轮导出数千张无关贴图。
        if (!target.TargetMod.Equals("Vanilla", StringComparison.OrdinalIgnoreCase)) {
            return [""];
        }

        const int maxPatternLength = 3000;
        List<string> result = [];
        List<string> currentNames = [];
        int currentLength = 4;
        foreach (string iconName in requiredIconNames.Values
                     .Select(Regex.Escape)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) {
            int nextLength = currentLength + iconName.Length + 1;
            if (currentNames.Count > 0 && nextLength > maxPatternLength) {
                result.Add($"^({string.Join("|", currentNames)})$");
                currentNames.Clear();
                currentLength = 4;
            }

            currentNames.Add(iconName);
            currentLength += iconName.Length + 1;
        }

        if (currentNames.Count > 0) {
            result.Add($"^({string.Join("|", currentNames)})$");
        }
        return result.Count == 0 ? [""] : result;
    }

    private static IEnumerable<string> GetAssetStudioInputPaths(CalcIconExportTarget target) {
        foreach (string relativePath in target.AssetStudioGameDataRelativePaths) {
            string inputPath = ResolveExistingPathRespectingOld(Path.Combine(DSPGameDir, relativePath));
            if (inputPath != null) {
                yield return inputPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(target.AssetStudioPackageName)) {
            string pluginDir = Path.Combine(R2PluginsDir, target.AssetStudioPackageName);
            foreach (string relativePath in target.AssetStudioRelativePaths) {
                string inputPath = ResolveExistingPathRespectingOld(Path.Combine(pluginDir, relativePath));
                if (inputPath != null) {
                    yield return inputPath;
                }
            }
        }
    }

    private static void ExportCalcIconsFromEmbeddedDll(
        CmdProcess cmd,
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames) {
        if (string.IsNullOrWhiteSpace(target.EmbeddedDllPackageName)
            || string.IsNullOrWhiteSpace(target.EmbeddedDllRelativePath)) {
            return;
        }

        string sourceDll = ResolveExistingPathRespectingOld(
            Path.Combine(R2PluginsDir, target.EmbeddedDllPackageName, target.EmbeddedDllRelativePath));
        if (sourceDll == null) {
            Console.WriteLine(
                $"未找到 {target.TargetMod} embedded PNG DLL：{Path.Combine(R2PluginsDir, target.EmbeddedDllPackageName, target.EmbeddedDllRelativePath)}");
            return;
        }

        string workDll = PrepareDllPathForTool(sourceDll, target.TargetMod);
        string outputDir = Path.Combine(CalcIconWorkDir, "ilspy", target.TargetMod);
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"开始离线提取 embedded PNG：{target.TargetMod} <- {sourceDll}");
        int exitCode = cmd.Run("ilspycmd", $"-p --nested-directories -o \"{outputDir}\" \"{workDll}\"");
        if (exitCode != 0) {
            Console.WriteLine($"ilspycmd 提取 embedded PNG 失败：{target.TargetMod}，错误码 {exitCode}");
            return;
        }

        int copied = CopyRequiredPngIcons(outputDir, target, requiredIconNames, require80x80: true);
        Console.WriteLine($"{target.TargetMod} embedded PNG 离线图标：复制 {copied}");
    }

    private static string PrepareDllPathForTool(string sourceDll, string targetMod) {
        if (sourceDll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            return sourceDll;
        }

        string outputDir = Path.Combine(CalcIconWorkDir, "dll-input", targetMod);
        Directory.CreateDirectory(outputDir);
        string fileName = Path.GetFileName(sourceDll);
        if (fileName.EndsWith(".old", StringComparison.OrdinalIgnoreCase)) {
            fileName = fileName.Substring(0, fileName.Length - ".old".Length);
        }
        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            fileName += ".dll";
        }

        string targetDll = Path.Combine(outputDir, fileName);
        File.Copy(sourceDll, targetDll, true);
        return targetDll;
    }

    private static string ResolveExistingPathRespectingOld(string basePath) {
        if (File.Exists(basePath)) {
            return basePath;
        }

        string oldPath = basePath + ".old";
        if (File.Exists(oldPath)) {
            return oldPath;
        }

        return null;
    }

    private static int CopyRequiredPngIcons(
        string sourceDir,
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames,
        bool require80x80) {
        string outputDir = Path.Combine(DspCalcFullIconDir, target.TargetMod);
        Directory.CreateDirectory(outputDir);

        int copied = 0;
        int skippedSize = 0;
        int skippedExisting = 0;
        foreach (string sourceFile in Directory.GetFiles(sourceDir, "*.png", SearchOption.AllDirectories)) {
            if (require80x80 && !IsPng80x80(sourceFile)) {
                skippedSize++;
                continue;
            }

            string sourceIconName = GetSourceIconName(sourceFile, target.SourcePrefix);
            string key = NormalizeIconNameForMatch(sourceIconName);
            if (!requiredIconNames.TryGetValue(key, out string requiredIconName)) {
                continue;
            }

            string targetFile = Path.Combine(outputDir, $"{SanitizeFileName(requiredIconName)}.png");
            if (File.Exists(targetFile)) {
                skippedExisting++;
                continue;
            }

            File.Copy(sourceFile, targetFile, false);
            copied++;
        }

        if (skippedSize > 0 || skippedExisting > 0) {
            Console.WriteLine($"{target.TargetMod} 离线图标跳过：非 80x80 {skippedSize}，已有 {skippedExisting}");
        }
        return copied;
    }

    private static bool IsPng80x80(string filePath) {
        byte[] header = new byte[24];
        using FileStream stream = File.OpenRead(filePath);
        if (stream.Read(header, 0, header.Length) < header.Length) {
            return false;
        }

        return header[0] == 0x89
               && header[1] == 0x50
               && header[2] == 0x4E
               && header[3] == 0x47
               && ReadBigEndianInt32(header, 16) == 80
               && ReadBigEndianInt32(header, 20) == 80;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) {
        return (bytes[offset] << 24)
               | (bytes[offset + 1] << 16)
               | (bytes[offset + 2] << 8)
               | bytes[offset + 3];
    }

    private static void DeleteDirectoryIfExists(string dir) {
        if (!Directory.Exists(dir)) {
            return;
        }

        Directory.Delete(dir, true);
    }

    private static string MakeSafePathSegment(string value) {
        foreach (char invalidChar in Path.GetInvalidFileNameChars()) {
            value = value.Replace(invalidChar, '_');
        }
        return string.IsNullOrWhiteSpace(value) ? "input" : value;
    }

    private static void CopyStaticCalcIconFallbacks() {
        CopyStaticCalcIconFallback("Vanilla", "伊卡洛斯", Path.Combine(DspCalcIconAssetsDir, "Vanilla", "伊卡洛斯.png"));
        CopyStaticCalcIconFallback("Vanilla", "行星基地", Path.Combine(DspCalcIconAssetsDir, "Vanilla", "行星基地.png"));
        CopyStaticCalcIconFallback("Vanilla", "巨构星际组装厂", Path.Combine(DspCalcIconAssetsDir, "Vanilla", "巨构星际组装厂.png"));
    }

    private static void CopyStaticCalcIconFallback(string targetMod, string iconName, string sourceFile) {
        if (!File.Exists(sourceFile)) {
            return;
        }

        string outputDir = Path.Combine(DspCalcFullIconDir, targetMod);
        Directory.CreateDirectory(outputDir);
        string targetFile = Path.Combine(outputDir, $"{SanitizeFileName(iconName)}.png");
        if (File.Exists(targetFile) && new FileInfo(targetFile).Length == new FileInfo(sourceFile).Length) {
            return;
        }

        File.Copy(sourceFile, targetFile, true);
        Console.WriteLine($"同步静态兜底图标：{targetMod}/{iconName}");
    }

    private static List<CalcIconExportTarget> GetCalcIconExportTargets() {
        return [
            new() {
                TargetMod = "Vanilla",
                AssetStudioGameDataRelativePaths = [
                    @"DSPGAME_Data\resources.assets",
                    @"DSPGAME_Data\sharedassets0.assets",
                ],
                EnabledMods = [],
                LowerPriorityMods = [],
            },
            new() {
                TargetMod = "MoreMegaStructure",
                AssetStudioPackageName = "jinxOAO-MoreMegaStructure",
                AssetStudioRelativePaths = ["mmstabicon"],
                EnabledMods = ["jinxOAO-MoreMegaStructure"],
                LowerPriorityMods = ["Vanilla"],
            },
            new() {
                TargetMod = "TheyComeFromVoid",
                AssetStudioPackageName = "ckcz123-TheyComeFromVoid",
                AssetStudioRelativePaths = ["dspbattletex"],
                EnabledMods = ["jinxOAO-MoreMegaStructure", "ckcz123-TheyComeFromVoid"],
                LowerPriorityMods = ["Vanilla", "MoreMegaStructure"],
            },
            new() {
                TargetMod = "GenesisBook",
                AssetStudioPackageName = "HiddenCirno-GenesisBook",
                EmbeddedDllPackageName = "HiddenCirno-GenesisBook",
                EmbeddedDllRelativePath = "ProjectGenesis.dll",
                SourcePrefix = "ProjectGenesis.assets.sprite.",
                EnabledMods = ["jinxOAO-MoreMegaStructure", "HiddenCirno-GenesisBook"],
                LowerPriorityMods = ["Vanilla", "MoreMegaStructure"],
            },
            new() {
                TargetMod = "OrbitalRing",
                AssetStudioPackageName = "ProfessorCat-OrbitalRing",
                EmbeddedDllPackageName = "ProfessorCat-OrbitalRing",
                EmbeddedDllRelativePath = "ProjectOrbitalRing.dll",
                SourcePrefix = "ProjectOrbitalRing.assets.sprite.",
                EnabledMods = ["jinxOAO-MoreMegaStructure", "ProfessorCat-OrbitalRing"],
                LowerPriorityMods = ["Vanilla", "MoreMegaStructure"],
            },
            new() {
                TargetMod = "FractionateEverything",
                AssetStudioPackageName = "MengLei-FractionateEverything",
                AssetStudioRelativePaths = ["fe"],
                EnabledMods = ["MengLei-FractionateEverything"],
                LowerPriorityMods = ["Vanilla"],
            },
        ];
    }

    private static Dictionary<string, string> CollectRequiredIconNames(string dataDir) {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dataDir)) {
            return result;
        }

        foreach (string jsonFile in Directory.GetFiles(dataDir, "*.json")) {
            JObject root;
            try {
                root = JObject.Parse(File.ReadAllText(jsonFile));
            }
            catch (Exception ex) {
                Console.WriteLine($"读取计算器 json 失败：{jsonFile}，{ex.Message}");
                continue;
            }

            foreach ((_, string iconName, _) in EnumerateRequiredCalcIcons(root)) {
                if (string.IsNullOrWhiteSpace(iconName)) {
                    continue;
                }

                string key = NormalizeIconNameForMatch(iconName);
                if (!result.ContainsKey(key)) {
                    result.Add(key, iconName);
                }
            }
        }

        return result;
    }

    private static IEnumerable<(int ItemId, string IconName, string ItemName)>
        EnumerateRequiredCalcIcons(JObject root) {
        Dictionary<int, JObject> itemById = [];
        foreach (JObject item in (root["items"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()) {
            int? id = item.Value<int?>("ID");
            if (id.HasValue && !itemById.ContainsKey(id.Value)) {
                itemById.Add(id.Value, item);
            }
        }

        HashSet<int> requiredItemIds = [];
        foreach (JObject recipe in (root["recipes"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()) {
            AddIds(requiredItemIds, recipe["Items"]);
            AddIds(requiredItemIds, recipe["Results"]);
            AddIds(requiredItemIds, recipe["Factories"]);
        }

        foreach (int id in requiredItemIds) {
            if (!itemById.TryGetValue(id, out JObject item)) {
                continue;
            }

            string iconName = item.Value<string>("IconName");
            if (string.IsNullOrWhiteSpace(iconName)) {
                continue;
            }

            yield return (id, iconName, item.Value<string>("Name") ?? id.ToString());
        }
    }

    private static void AddIds(HashSet<int> target, JToken token) {
        if (token is not JArray array) {
            return;
        }

        foreach (JToken item in array) {
            int? id = item.Value<int?>();
            if (id.HasValue) {
                target.Add(id.Value);
            }
        }
    }

    private static void ExportMissingCalcIconsFromGame(
        string dataDir,
        Dictionary<string, MissingCalcIcon> missingIcons,
        bool syncGetDspDataToR2) {
        using CmdProcess cmd = new();
        LoadModInfos();
        ModInfo getDspData = GetModInfo("MengLei-GetDspData");
        ModInfo errorAnalyzer = GetModInfo("starfi5h-ErrorAnalyzer");
        if (getDspData == null || errorAnalyzer == null) {
            Console.WriteLine("未找到 MengLei-GetDspData 或 starfi5h-ErrorAnalyzer，无法启动游戏兜底提取图标！");
            return;
        }

        KillDspGameAndWaitForExit();
        if (syncGetDspDataToR2) {
            SyncGetDspDataToR2ForIconExport();
        } else {
            Console.WriteLine("本轮选项 3 已在 JSON 阶段同步 GetDspData，图标阶段跳过重复同步。");
        }
        PrepareR2Doorstop();
        HashSet<string> handledTargets = new(StringComparer.OrdinalIgnoreCase);
        try {
            while (missingIcons.Count > 0) {
                CalcIconExportTarget target = GetNextMissingIconTarget(missingIcons, handledTargets);
                if (target == null) {
                    break;
                }

                handledTargets.Add(target.TargetMod);
                if (!TryBuildIconExportModList(target, getDspData, errorAnalyzer, out List<string> enabledModNames)) {
                    continue;
                }

                Console.WriteLine($"开始启动游戏提取 {target.TargetMod} 图标...");
                WriteIconExportRequest(target, missingIcons);
                if (File.Exists(IconExportMarkerPath)) {
                    File.Delete(IconExportMarkerPath);
                }

                KillDspGameAndWaitForExit();
                OnlyEnableInputMods(enabledModNames);
                cmd.Exec(RunDSP);
                if (!WaitForFile(IconExportMarkerPath, TimeSpan.FromMinutes(5))) {
                    Console.WriteLine($"等待 {target.TargetMod} 图标导出超时，跳过。");
                    continue;
                }

                Console.WriteLine(File.ReadAllText(IconExportMarkerPath));
                missingIcons = CollectMissingRequiredIconsFromFull(dataDir);
            }
        }
        finally {
            if (File.Exists(IconExportRequestPath)) {
                File.Delete(IconExportRequestPath);
            }
            EnableModsByConfig();
            KillDspGameAndWaitForExit();
        }
    }

    private static void SyncGetDspDataToR2ForIconExport() {
        string sourceDll = GetProjectOutputPath("GetDspData", BuildConfiguration, "GetDspData.dll");
        string sourceJsonDll = Path.Combine(SolutionFullDir, "lib", "Newtonsoft.Json.dll");
        CopyToR2RespectingOld(sourceDll, Path.Combine(R2PluginsDir, "MengLei-GetDspData", "GetDspData.dll"));
        CopyToR2RespectingOld(sourceJsonDll, Path.Combine(R2PluginsDir, "MengLei-GetDspData", "Newtonsoft.Json.dll"),
            false);
        Console.WriteLine("已同步图标导出用 GetDspData 到 R2");
    }

    private static void KillDspGameAndWaitForExit() {
        Console.WriteLine("终止游戏进程...");
        try {
            using Process process = Process.Start(new ProcessStartInfo("taskkill.exe", "/F /IM DSPGAME.exe") {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process?.WaitForExit(10000);
        }
        catch (Exception ex) {
            Console.WriteLine($"执行 taskkill 失败：{ex.Message}");
        }

        if (!WaitForProcessExit("DSPGAME", TimeSpan.FromSeconds(30))) {
            Console.WriteLine("等待 DSPGAME.exe 退出超时，后续复制如仍被占用会继续重试。");
        }
        Thread.Sleep(1000);
    }

    private static bool WaitForProcessExit(string processName, TimeSpan timeout) {
        DateTime deadline = DateTime.Now + timeout;
        while (DateTime.Now < deadline) {
            Process[] processes = Process.GetProcessesByName(processName);
            try {
                if (processes.Length == 0) {
                    return true;
                }
            }
            finally {
                foreach (Process process in processes) {
                    process.Dispose();
                }
            }
            Thread.Sleep(500);
        }
        return false;
    }

    private static CalcIconExportTarget GetNextMissingIconTarget(
        Dictionary<string, MissingCalcIcon> missingIcons,
        HashSet<string> handledTargets) {
        HashSet<string> candidateTargets = new(StringComparer.OrdinalIgnoreCase);
        foreach (MissingCalcIcon missingIcon in missingIcons.Values) {
            candidateTargets.UnionWith(missingIcon.CandidateTargets);
        }

        return GetCalcIconExportTargets()
            .FirstOrDefault(target =>
                candidateTargets.Contains(target.TargetMod) && !handledTargets.Contains(target.TargetMod));
    }

    private static bool TryBuildIconExportModList(
        CalcIconExportTarget target,
        ModInfo getDspData,
        ModInfo errorAnalyzer,
        out List<string> enabledModNames) {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        AddModAndDependencies(names, getDspData.name);
        AddModAndDependencies(names, errorAnalyzer.name);

        foreach (string modName in target.EnabledMods) {
            ModInfo modInfo = GetModInfo(modName);
            if (modInfo == null) {
                Console.WriteLine($"mods.yml 中未找到模组信息：{modName}，跳过 {target.TargetMod} 图标提取。");
                enabledModNames = [];
                return false;
            }

            AddModAndDependencies(names, modInfo.name);
        }

        enabledModNames = names.ToList();
        return true;
    }

    private static void AddModAndDependencies(HashSet<string> names, string modName) {
        names.Add(modName);
        foreach (string dependency in GetDependencies(modName)) {
            names.Add(dependency);
        }
    }

    private static void WriteIconExportRequest(
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, MissingCalcIcon> missingIcons) {
        JArray requestedIconNames = new();
        foreach (MissingCalcIcon missingIcon in missingIcons.Values
                     .Where(missingIcon => missingIcon.CandidateTargets.Contains(target.TargetMod))) {
            requestedIconNames.Add(missingIcon.IconName);
        }

        JArray existingIconDirs = new() {
            Path.Combine(DspCalcFullIconDir, target.TargetMod),
        };
        foreach (string lowerPriorityMod in target.LowerPriorityMods) {
            existingIconDirs.Add(Path.Combine(DspCalcFullIconDir, lowerPriorityMod));
        }

        JObject request = new() {
            { "TargetMod", target.TargetMod },
            { "OutputDir", Path.Combine(DspCalcFullIconDir, target.TargetMod) },
            { "LowerPriorityDirs", existingIconDirs },
            { "IconNames", requestedIconNames },
            { "MarkerPath", IconExportMarkerPath },
        };
        Directory.CreateDirectory(Path.GetDirectoryName(IconExportRequestPath) ?? ".");
        File.WriteAllText(IconExportRequestPath, request.ToString(), Encoding.UTF8);
    }

    private static bool WaitForFile(string filePath, TimeSpan timeout) {
        DateTime deadline = DateTime.Now + timeout;
        while (DateTime.Now < deadline) {
            if (File.Exists(filePath)) {
                return true;
            }
            Thread.Sleep(500);
        }
        return false;
    }

    private static void SyncRequiredIconsToCalcAssets(string dataDir) {
        if (!IsDspCalcProjectAvailable()) {
            Console.WriteLine("未检测到完整计算器项目，跳过同步图标到计算器。");
            return;
        }

        Dictionary<string, Dictionary<string, string>> requiredIconCopies = BuildRequiredCalcIconAssetCopies(dataDir);
        int removedStale = RemoveStaleCalcIconAssets(requiredIconCopies);
        int copied = 0;
        int skippedSameSize = 0;
        foreach (KeyValuePair<string, Dictionary<string, string>> modPair in requiredIconCopies) {
            string sourceMod = modPair.Key;
            Dictionary<string, string> iconFiles = modPair.Value;
            string outputDir = Path.Combine(DspCalcIconAssetsDir, sourceMod);
            Directory.CreateDirectory(outputDir);
            foreach (KeyValuePair<string, string> iconPair in iconFiles) {
                string iconName = iconPair.Key;
                string sourceFile = iconPair.Value;
                string targetFile = Path.Combine(outputDir, $"{SanitizeFileName(iconName)}.png");
                if (File.Exists(targetFile) && new FileInfo(targetFile).Length == new FileInfo(sourceFile).Length) {
                    skippedSameSize++;
                    continue;
                }

                File.Copy(sourceFile, targetFile, true);
                copied++;
            }
        }

        int requiredCount = requiredIconCopies.Values.Sum(iconFiles => iconFiles.Count);
        Console.WriteLine($"同步计算器所需图标：需要 {requiredCount}，复制 {copied}，已有同尺寸 {skippedSameSize}，删除多余 {removedStale}");
    }

    private static int RemoveStaleCalcIconAssets(Dictionary<string, Dictionary<string, string>> requiredIconCopies) {
        if (!Directory.Exists(DspCalcIconAssetsDir)) {
            return 0;
        }

        HashSet<string> requiredFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Dictionary<string, string>> modPair in requiredIconCopies) {
            string sourceMod = modPair.Key;
            foreach (string iconName in modPair.Value.Keys) {
                requiredFiles.Add(Path.GetFullPath(Path.Combine(DspCalcIconAssetsDir, sourceMod,
                    $"{SanitizeFileName(iconName)}.png")));
            }
        }

        int removed = 0;
        foreach (string iconFile in Directory.GetFiles(DspCalcIconAssetsDir, "*.png", SearchOption.AllDirectories)) {
            if (requiredFiles.Contains(Path.GetFullPath(iconFile))) {
                continue;
            }

            File.Delete(iconFile);
            removed++;
        }
        return removed;
    }

    private static Dictionary<string, Dictionary<string, string>> BuildRequiredCalcIconAssetCopies(string dataDir) {
        Dictionary<string, Dictionary<string, string>> fullIconIndex = BuildIconFileIndex(DspCalcFullIconDir);
        Dictionary<int, HashSet<string>> iconAliases = BuildCalcIconAliases(dataDir);
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dataDir)) {
            return result;
        }

        foreach (string jsonFile in Directory.GetFiles(dataDir, "*.json")) {
            JObject root = JObject.Parse(File.ReadAllText(jsonFile));
            List<string> lookupOrder = GetIconLookupOrder(Path.GetFileName(jsonFile));
            string targetMod = lookupOrder.FirstOrDefault(modName => modName != "Vanilla") ?? "Vanilla";
            foreach ((int itemId, string iconName, _) in EnumerateRequiredCalcIcons(root)) {
                if (string.IsNullOrWhiteSpace(iconName)) {
                    continue;
                }

                if (!TryFindIconFile(
                        fullIconIndex,
                        lookupOrder,
                        iconAliases,
                        itemId,
                        iconName,
                        out string sourceMod,
                        out string sourceFile)) {
                    continue;
                }

                string copyTargetMod = lookupOrder.Contains(sourceMod, StringComparer.OrdinalIgnoreCase)
                    ? sourceMod
                    : targetMod;

                // 计算器按 raw 文件启用模组顺序解析图标；跨模组兜底复用时也必须复制到当前 raw 的目标模组目录。
                if (!result.TryGetValue(copyTargetMod, out Dictionary<string, string> requiredIconFiles)) {
                    requiredIconFiles = new(StringComparer.OrdinalIgnoreCase);
                    result.Add(copyTargetMod, requiredIconFiles);
                }
                requiredIconFiles[iconName] = sourceFile;
            }
        }

        return result;
    }

    private static Dictionary<string, MissingCalcIcon> CollectMissingRequiredIconsFromFull(string dataDir) {
        Dictionary<string, Dictionary<string, string>> fullIconIndex = BuildIconFileIndex(DspCalcFullIconDir);
        Dictionary<int, HashSet<string>> iconAliases = BuildCalcIconAliases(dataDir);
        Dictionary<string, MissingCalcIcon> result = new(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dataDir)) {
            return result;
        }

        foreach (string jsonFile in Directory.GetFiles(dataDir, "*.json")) {
            JObject root;
            try {
                root = JObject.Parse(File.ReadAllText(jsonFile));
            }
            catch (Exception ex) {
                Console.WriteLine($"读取计算器 json 失败：{jsonFile}，{ex.Message}");
                continue;
            }

            string fileName = Path.GetFileName(jsonFile);
            List<string> lookupOrder = GetIconLookupOrder(fileName);
            List<string> candidateTargets = GetCandidateTargets(fileName);
            foreach ((int itemId, string iconName, string itemName) in EnumerateRequiredCalcIcons(root)) {
                if (string.IsNullOrWhiteSpace(iconName)) {
                    continue;
                }

                if (TryFindIconFile(
                        fullIconIndex,
                        lookupOrder,
                        iconAliases,
                        itemId,
                        iconName,
                        out _,
                        out _)) {
                    continue;
                }

                if (!result.TryGetValue(iconName, out MissingCalcIcon missingIcon)) {
                    missingIcon = new() {
                        IconName = iconName,
                    };
                    result.Add(iconName, missingIcon);
                }
                missingIcon.CandidateTargets.UnionWith(candidateTargets);
                missingIcon.DataFiles.Add(fileName);
                if (missingIcon.Examples.Count < 3) {
                    missingIcon.Examples.Add($"{fileName}: {itemName}");
                }
            }
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> BuildIconFileIndex(string sourceDir) {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (CalcIconExportTarget target in GetCalcIconExportTargets()) {
            string modDir = Path.Combine(sourceDir, target.TargetMod);
            Dictionary<string, string> iconFiles = new(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(modDir)) {
                foreach (string filePath in Directory.GetFiles(modDir, "*.png")) {
                    iconFiles[Path.GetFileNameWithoutExtension(filePath)] = filePath;
                }
            }
            result[target.TargetMod] = iconFiles;
        }
        return result;
    }

    private static Dictionary<int, HashSet<string>> BuildCalcIconAliases(string dataDir) {
        Dictionary<int, HashSet<string>> result = [];
        if (!Directory.Exists(dataDir)) {
            return result;
        }

        foreach (string jsonFile in Directory.GetFiles(dataDir, "*.json")) {
            JObject root;
            try {
                root = JObject.Parse(File.ReadAllText(jsonFile));
            }
            catch (Exception ex) {
                Console.WriteLine($"读取计算器 json 失败：{jsonFile}，{ex.Message}");
                continue;
            }

            foreach (JObject item in (root["items"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()) {
                int? itemId = item.Value<int?>("ID");
                string iconName = item.Value<string>("IconName");
                if (!itemId.HasValue || string.IsNullOrWhiteSpace(iconName)) {
                    continue;
                }

                if (!result.TryGetValue(itemId.Value, out HashSet<string> aliases)) {
                    aliases = new(StringComparer.OrdinalIgnoreCase);
                    result.Add(itemId.Value, aliases);
                }
                aliases.Add(iconName);
            }
        }

        return result;
    }

    private static bool TryFindIconFile(
        IReadOnlyDictionary<string, Dictionary<string, string>> fullIconIndex,
        IReadOnlyList<string> lookupOrder,
        IReadOnlyDictionary<int, HashSet<string>> iconAliases,
        int itemId,
        string iconName,
        out string sourceMod,
        out string sourceFile) {
        List<string> candidateIconNames = [iconName];
        if (iconAliases.TryGetValue(itemId, out HashSet<string> aliases)) {
            foreach (string alias in aliases) {
                if (!candidateIconNames.Contains(alias, StringComparer.OrdinalIgnoreCase)) {
                    candidateIconNames.Add(alias);
                }
            }
        }

        // 先按当前 JSON 的启用模组优先级查，避免跨模组同名图误覆盖。
        foreach (string candidateIconName in candidateIconNames) {
            if (TryFindIconFileInMods(fullIconIndex, lookupOrder, candidateIconName, out sourceMod, out sourceFile)) {
                return true;
            }
        }

        // 再从完整图池兜底复用同名/同 ID 别名图，处理不同模组组合导出的 IconName 不一致。
        string[] allMods = GetCalcIconExportTargets()
            .Select(target => target.TargetMod)
            .ToArray();
        foreach (string candidateIconName in candidateIconNames) {
            if (TryFindIconFileInMods(fullIconIndex, allMods, candidateIconName, out sourceMod, out sourceFile)) {
                return true;
            }
        }

        sourceMod = null;
        sourceFile = null;
        return false;
    }

    private static bool TryFindIconFileInMods(
        IReadOnlyDictionary<string, Dictionary<string, string>> fullIconIndex,
        IEnumerable<string> modNames,
        string iconName,
        out string sourceMod,
        out string sourceFile) {
        string sanitizedIconName = SanitizeFileName(iconName);
        foreach (string modName in modNames) {
            if (fullIconIndex.TryGetValue(modName, out Dictionary<string, string> iconFiles)
                && iconFiles.TryGetValue(sanitizedIconName, out sourceFile)) {
                sourceMod = modName;
                return true;
            }
        }

        sourceMod = null;
        sourceFile = null;
        return false;
    }

    private static List<string> GetIconLookupOrder(string jsonFileName) {
        List<string> enabledMods = GetEnabledCalcModsFromFileName(jsonFileName);
        enabledMods.Reverse();
        enabledMods.Add("Vanilla");
        return enabledMods;
    }

    private static List<string> GetCandidateTargets(string jsonFileName) {
        List<string> enabledMods = GetEnabledCalcModsFromFileName(jsonFileName);
        return enabledMods.Count == 0 ? ["Vanilla"] : enabledMods;
    }

    private static List<string> GetEnabledCalcModsFromFileName(string jsonFileName) {
        return GetCalcIconExportTargets()
            .Select(target => target.TargetMod)
            .Where(modName => modName != "Vanilla" && jsonFileName.Contains(modName))
            .ToList();
    }

    private static string GetSourceIconName(string sourceFile, string sourcePrefix) {
        string fileName = Path.GetFileNameWithoutExtension(sourceFile);
        if (fileName.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)) {
            return fileName.Substring(sourcePrefix.Length);
        }
        return fileName;
    }

    private static string NormalizeIconNameForMatch(string iconName) {
        StringBuilder builder = new(iconName.Length);
        foreach (char ch in iconName) {
            if (char.IsLetterOrDigit(ch)) {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }
        return builder.ToString();
    }

    private static string SanitizeFileName(string fileName) {
        foreach (char invalidChar in Path.GetInvalidFileNameChars()) {
            fileName = fileName.Replace(invalidChar, '_');
        }
        return fileName.Trim();
    }

    #endregion
}
