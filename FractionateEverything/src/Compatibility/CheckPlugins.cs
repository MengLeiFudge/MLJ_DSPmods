using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FE.Compatibility;

/// <summary>
/// 加载万物分馏主插件前，检测是否使用其他mod，并对其进行适配。
/// </summary>
[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency(MoreMegaStructure.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(TheyComeFromVoid.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(GenesisBook.GUID, BepInDependency.DependencyFlags.SoftDependency)]
public class CheckPlugins : BaseUnityPlugin {
    public const string GUID = PluginInfo.PLUGIN_GUID + ".CheckPlugins";
    public const string NAME = PluginInfo.PLUGIN_NAME + ".CheckPlugins";
    public const string VERSION = PluginInfo.PLUGIN_VERSION;

    private static bool _shown;

    /// <summary>
    /// 是否在游戏加载时禁用提示信息。
    /// </summary>
    public static ConfigEntry<bool> DisableMessageBox;

    #region Logger

    private static ManualLogSource logger;
    public static void LogDebug(object data) => logger.LogDebug(data);
    public static void LogInfo(object data) => logger.LogInfo(data);
    public static void LogWarning(object data) => logger.LogWarning(data);
    public static void LogError(object data) => logger.LogError(data);
    public static void LogFatal(object data) => logger.LogFatal(data);

    #endregion

    public void Awake() {
        logger = Logger;
        new Harmony(GUID).Patch(
            AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
            null,
            new(typeof(CheckPlugins), nameof(OnMainMenuOpen)) { priority = Priority.Last }
        );

        MoreMegaStructure.Compatible();
        TheyComeFromVoid.Compatible();
        GenesisBook.Compatible();
    }

    public static void OnMainMenuOpen() {
        if (_shown) return;
        if (!DisableMessageBox.Value) {
            ShowMessageBox();
            DisableMessageBox.Value = true;
            FractionateEverything.SaveConfig();
        }
        _shown = true;
    }

    private static void ShowMessageBox() {
        UIMessageBox.Show(
            "FE标题".Translate(), "FE信息".Translate(),
            "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(),
            UIMessageBox.INFO,
            null, () => {
#if DEBUG
                Application.OpenURL(Path.Combine(FractionateEverything.ModPath, "CHANGELOG.md"));
#else
                Application.OpenURL("FE日志链接".Translate());
#endif
            }, () => Application.OpenURL("FE交流群链接".Translate())
        );
    }
}
