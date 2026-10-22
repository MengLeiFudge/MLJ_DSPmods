using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BuildBarTool;
using CommonAPI;
using crecheng.DSPModSave;
using FE.Compatibility.Mods;
using FE.Compatibility.Nebula;
using HarmonyLib;
using UnityEngine;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Compatibility;

/// <summary>
/// 加载万物分馏主插件前，检测是否使用其他mod，并对其进行适配。
/// </summary>
[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency(LDBToolPlugin.MODGUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(DSPModSavePlugin.MODGUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CommonAPIPlugin.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(BuildBarToolPlugin.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(NebulaMultiplayerModAPI.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(AutoSorter.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(Auxilaryfunction.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(BuildToolOpt.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CheatEnabler.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CustomCreateBirthStar.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(DeliverySlotsTweaks.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(DspCalculator.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(DspRecipeFinder.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(GenesisBook.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(MoreMegaStructure.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(Multfunction_mod.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(OrbitalRing.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(PackageLogistic.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(SmelterMiner.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(TheyComeFromVoid.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(TowerFactory.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.yummi.dsp.bigtower", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(UxAssist.GUID, BepInDependency.DependencyFlags.SoftDependency)]
public class CheckPlugins : BaseUnityPlugin {
    public const string GUID = PluginInfo.PLUGIN_GUID + ".CheckPlugins";
    public const string NAME = PluginInfo.PLUGIN_NAME + ".CheckPlugins";
    public const string VERSION = PluginInfo.PLUGIN_VERSION;

    private static bool _shown;
    private static string _missingModsList;

    /// <summary>
    /// 是否在游戏加载时禁用提示信息。
    /// </summary>
    public static ConfigEntry<bool> DisableMessageBox;

    #region 日志

    private static ManualLogSource logger;
    public static void LogDebug(object data) => logger.LogDebug(data);
    public static void LogInfo(object data) => logger.LogInfo(data);
    public static void LogWarning(object data) => logger.LogWarning(data);
    public static void LogError(object data) => logger.LogError(data);
    public static void LogFatal(object data) => logger.LogFatal(data);

    #endregion

    public void Awake() {
        logger = Logger;

        LoadTranslationsFromJson();

        //缺少模组提示
        bool dependencyOk = true;
        StringBuilder sb = new StringBuilder();
        if (!Chainloader.PluginInfos.ContainsKey(LDBToolPlugin.MODGUID)) {
            dependencyOk = false;
            sb.Append($"\nLDBTool ({LDBToolPlugin.MODGUID})");
        }
        if (!Chainloader.PluginInfos.ContainsKey(DSPModSavePlugin.MODGUID)) {
            dependencyOk = false;
            sb.Append($"\nDSPModSave ({DSPModSavePlugin.MODGUID})");
        }
        if (!Chainloader.PluginInfos.ContainsKey(CommonAPIPlugin.GUID)) {
            dependencyOk = false;
            sb.Append($"\nCommonAPI ({CommonAPIPlugin.GUID})");
        }
        if (!Chainloader.PluginInfos.ContainsKey(BuildBarToolPlugin.GUID)) {
            dependencyOk = false;
            sb.Append($"\nBuildBarTool ({BuildBarToolPlugin.GUID})");
        }
        if (!Chainloader.PluginInfos.ContainsKey(NebulaMultiplayerModAPI.GUID)) {
            dependencyOk = false;
            sb.Append($"\nNebulaMultiplayerModAPI ({NebulaMultiplayerModAPI.GUID})");
        }
        if (!Chainloader.PluginInfos.ContainsKey(TowerFactory.GUID)) {
            dependencyOk = false;
            sb.Append($"\nTowerFactory ({TowerFactory.GUID})");
        }
        if (!dependencyOk) {
            _missingModsList = sb.ToString();
            new Harmony(GUID).Patch(
                AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
                null,
                new(typeof(CheckPlugins), nameof(ShowMissingModMessage)) { priority = Priority.Last }
            );
            return;
        }

        // BepInEx版本检查
        var bepInExVersion = typeof(Chainloader).Assembly.GetName().Version;
        if (bepInExVersion.Major != 5 || bepInExVersion.Minor != 4 || bepInExVersion.Build != 17) {
            LogWarning($"BepInEx version mismatch! Expected: 5.4.17, Actual: {bepInExVersion}");
            new Harmony(GUID + ".BepInExVersion").Patch(
                AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
                null,
                new(typeof(CheckPlugins), nameof(ShowBepInExVersionWarning)) { priority = Priority.Last }
            );
        }


        AutoSorter.Compatible();
        Auxilaryfunction.Compatible();
        BuildToolOpt.Compatible();
        CheatEnabler.Compatible();
        CustomCreateBirthStar.Compatible();
        DeliverySlotsTweaks.Compatible();
        DspCalculator.Compatible();
        DspRecipeFinder.Compatible();
        GenesisBook.Compatible();
        MoreMegaStructure.Compatible();
        Multfunction_mod.Compatible();
        NebulaMultiplayerModAPI.Compatible();
        OrbitalRing.Compatible();
        PackageLogistic.Compatible();
        SmelterMiner.Compatible();
        TheyComeFromVoid.Compatible();
        TowerFactory.Compatible();
        UxAssist.Compatible();

        new Harmony(GUID).Patch(
            AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
            null,
            new(typeof(CheckPlugins), nameof(OnMainMenuOpen)) { priority = Priority.Last }
        );
    }

    private static void ShowMissingModMessage() {
        UIMessageBox.Show("万物分馏缺少模组标题".Translate(),
            "万物分馏缺少模组内容".Translate() + _missingModsList,
            "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(), UIMessageBox.ERROR,
            null,
            () => {
#if DEBUG
                Application.OpenURL(Path.Combine(FractionateEverything.ModPath, "CHANGELOG.md"));
#else
                Application.OpenURL("FE日志链接".Translate());
#endif
            },
            () => Application.OpenURL("FE交流群链接".Translate())
        );
    }

    private static void ShowBepInExVersionWarning() {
        UIMessageBox.Show("万物分馏BepInEx版本警告标题".Translate(),
            "万物分馏BepInEx版本警告内容".Translate(),
            "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(), UIMessageBox.WARNING,
            null,
            () => {
#if DEBUG
                Application.OpenURL(Path.Combine(FractionateEverything.ModPath, "CHANGELOG.md"));
#else
                Application.OpenURL("FE日志链接".Translate());
#endif
            },
            () => Application.OpenURL("FE交流群链接".Translate())
        );
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
        UIMessageBox.Show("FE标题".Translate(),
            "FE内容".Translate(),
            "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(), UIMessageBox.INFO,
            null,
            () => {
#if DEBUG
                Application.OpenURL(Path.Combine(FractionateEverything.ModPath, "CHANGELOG.md"));
#else
                Application.OpenURL("FE日志链接".Translate());
#endif
            },
            () => Application.OpenURL("FE交流群链接".Translate())
        );
    }
}
