using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using HarmonyLib;
using UnityEngine;

namespace SaveDataExporter;

/// <summary>
/// SaveDataExporter 插件入口：生命周期、配置、可配置快捷键触发与导出编排。
/// </summary>
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(CommonAPIPlugin.GUID)]
[CommonAPISubmoduleDependency(nameof(CustomKeyBindSystem), nameof(LocalizationModule))]
public class SaveDataExporterPlugin : BaseUnityPlugin {
    /// <summary>
    /// CustomKeyBindSystem 注册名；默认未绑定任何键位，由玩家在设置中配置。
    /// </summary>
    private const string ExportKeyId = "ExportSaveStatistics";

    /// <summary>
    /// 插件日志源，供导出流程状态记录与错误输出使用。
    /// </summary>
    private static ManualLogSource logger;

    /// <summary>
    /// 绑定配置、注册可配置快捷键、应用杂项设置页补丁。
    /// </summary>
    public void Awake() {
        logger = Logger;
        ExportSettings.Bind(Config);
        OutputFileNameOptionWindow.Initialize(logger);

        Harmony harmony = new(PluginInfo.PLUGIN_GUID);
        harmony.PatchAll(typeof(OutputFileNameOptionWindow));

        // 默认 CombineKey(0,0,...)：未绑定任何键，避免占用键位；玩家在按键设置页自行绑定。
        CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(new BuiltinKey {
            key = new CombineKey(0, 0, ECombineKeyAction.OnceClick, true),
            conflictGroup = BuiltinKey.USE_KEYBOARD,
            name = ExportKeyId,
            canOverride = true,
        });
        LocalizationModule.RegisterTranslation("KEY" + ExportKeyId, "Export save statistics", "导出存档统计", "");
    }

    /// <summary>
    /// 每帧检测已配置快捷键；输入框占用时跳过，命中后导出当前存档统计。
    /// </summary>
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

    /// <summary>
    /// 唯一导出主流程：校验存档 → 解析目标 → 采集统计 → 解析路径 → 构建工作表 → 写出 xlsx。
    /// </summary>
    private static void ExportCurrentSaveStatistics() {
        if (!SaveStatisticsCollector.TryGetLoadedGameData(out GameData gameData)) {
            ShowTip("未载入存档，跳过导出");
            logger.LogWarning("未载入存档，跳过导出");
            return;
        }

        try {
            List<ExportItem> exportItems = SaveStatisticsCollector.ResolveTargetItems(ExportSettings.TargetItems);
            if (exportItems.Count == 0) {
                ShowTip("没有可导出的目标物品");
                logger.LogWarning($"没有可导出的目标物品：{ExportSettings.TargetItems}");
                return;
            }

            int timeLevel = Mathf.Clamp(ExportSettings.TimeLevel, 0, SaveStatisticsCollector.TimeLevelCount - 1);
            SaveStatisticsCollector.RefreshReferenceSpeeds(gameData);
            ExportDataset dataset = SaveStatisticsCollector.BuildDataset(gameData, exportItems, timeLevel);
            string outputPath = ExportPathResolver.BuildOutputPath(gameData);
            XlsxWorkbookWriter.Save(
                outputPath,
                [
                    ExportSheetBuilder.BuildWideSheet(dataset),
                    ExportSheetBuilder.BuildNormalizedSheet(dataset),
                ]);

            ShowTip($"已导出存档统计：{outputPath}");
            logger.LogInfo($"已导出存档统计：{outputPath}");
        }
        catch (Exception ex) {
            ShowTip("导出存档统计失败，详情见日志");
            logger.LogError($"导出存档统计失败：{ex}");
        }
    }

    /// <summary>
    /// 通过 UIRealtimeTip 向玩家弹出提示；UI 未初始化时静默捕获异常，避免中断导出。
    /// </summary>
    /// <param name="text">提示文本。</param>
    private static void ShowTip(string text) {
        try {
            UIRealtimeTip.Popup(text, sound: false);
        }
        catch {
            // 主页或 UI 未初始化时只保留日志，不让提示路径影响导出主流程。
        }
    }
}
