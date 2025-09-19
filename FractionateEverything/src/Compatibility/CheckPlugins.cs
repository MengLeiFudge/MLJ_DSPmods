using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BuildBarTool;
using CommonAPI;
using crecheng.DSPModSave;
using HarmonyLib;
using UnityEngine;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Compatibility;

/// <summary>
/// 加载万物分馏主插件前，检测是否使用其他mod，并对其进行适配。
/// </summary>
[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency(LDBToolPlugin.MODGUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(BuildBarToolPlugin.GUID)]
[BepInDependency(BuildToolOpt.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CheatEnabler.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(CustomCreateBirthStar.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(DeliverySlotsTweaks.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(GenesisBook.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(MoreMegaStructure.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(Multfunction_mod.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(NebulaMultiplayerModAPI.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(OrbitalRing.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(SmelterMiner.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(TheyComeFromVoid.GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(UxAssist.GUID, BepInDependency.DependencyFlags.SoftDependency)]
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

    public static void AddTranslations() {
        Register("FE标题", "Fractionate Everything Mod Tips", "万物分馏提示");
        Register("FE信息",
            "Thank you for using Fractionation Everything! This mod adds 7 different functioning fractionators, 2 interaction stations, and nearly 1,000 fractionation recipes.\n"
            + $"After researching the 'Fractionation data centre' tech, press {"Shift + F".WithColor(Orange)} (can be changed on the settings page) to call out the panel.\n"
            + "This mod has been compatible with some large mods, such as Genesis Book, They Come From Void, and More Mega Structure.\n"
            + $"If you have any issues or ideas about the mod, please feedback to {"Github Issue".WithColor(Blue)}.\n"
            + "Have fun with fractionation!".WithColor(Orange),
            "感谢你使用万物分馏！该Mod添加了7种不同功能的分馏塔，2个物流交互站，以及近千个分馏配方。\n"
            + $"研究“分馏数据中心”科技后，按下 {"Shift + F".WithColor(Orange)} （可在设置页面修改）即可呼出面板。\n"
            + $"该Mod已对部分大型Mod进行了兼容，例如创世之书（Genesis Book）、深空来敌（They Come From Void）、更多巨构（More Mega Structure）。\n"
            + $"如果你在游玩时遇到了任何问题，或者有宝贵的意见或建议，欢迎加入{"万物分馏MOD交流群".WithColor(Blue)}反馈。\n"
            + "尽情享受分馏的乐趣吧！".WithColor(Orange));
        Register("FE交流群", "Feedback on Github", "加入交流群");
        Register("FE交流群链接",
            "https://github.com/MengLeiFudge/MLJ_DSPmods",
            "https://qm.qq.com/q/zzicz6j9zW");
        Register("FE日志", "Update Log", "更新日志");
        Register("FE日志链接",
            "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/",
            "https://thunderstore.io/c/dyson-sphere-program/p/MengLei/FractionateEverything/changelog/");
    }

    public void Awake() {
        logger = Logger;

        AddTranslations();

        BuildToolOpt.Compatible();
        CheatEnabler.Compatible();
        CustomCreateBirthStar.Compatible();
        DeliverySlotsTweaks.Compatible();
        GenesisBook.Compatible();
        MoreMegaStructure.Compatible();
        Multfunction_mod.Compatible();
        NebulaMultiplayerModAPI.Compatible();
        OrbitalRing.Compatible();
        SmelterMiner.Compatible();
        TheyComeFromVoid.Compatible();
        UxAssist.Compatible();

        new Harmony(GUID).Patch(
            AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
            null,
            new(typeof(CheckPlugins), nameof(OnMainMenuOpen)) { priority = Priority.Last }
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
            "FE信息".Translate(),
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
