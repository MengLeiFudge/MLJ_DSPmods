using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
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

    #region Logger

    private static ManualLogSource logger;
    public static void LogDebug(object data) => logger.LogDebug(data);
    public static void LogInfo(object data) => logger.LogInfo(data);
    public static void LogWarning(object data) => logger.LogWarning(data);
    public static void LogError(object data) => logger.LogError(data);
    public static void LogFatal(object data) => logger.LogFatal(data);

    #endregion

    #region 添加蓝图

    private const string intro =
        "BPBOOK:0,1,2322,2326,2323,0,0,0,%E9%87%8C%E9%9D%A2%E6%9C%89%E5%87%A0%E4%B8%AA%E5%AE%9E%E7%94%A8%E7%9A%84%E5%B0%8F%E8%93%9D%E5%9B%BE%EF%BC%8C%E5%B8%8C%E6%9C%9B%E5%AE%83%E4%BB%AC%E8%83%BD%E5%B8%AE%E5%88%B0%E4%BD%A0~%0A%0A%E2%80%94%E2%80%94%E6%9D%A5%E8%87%AA%E4%B8%87%E7%89%A9%E5%88%86%E9%A6%8F%E4%BD%9C%E8%80%85%E7%9A%84%E5%85%B3%E7%88%B1%0A%0AThere%20are%20a%20couple%20of%20useful%20little%20blueprints%20in%20there%20and%20I%20hope%20they%20help%20you~%0A%0A--%20From%20the%20author%20of%20Fractionate%20Everything%20with%20love";

    private const string bp能量枢纽 =
        "BLUEPRINT:0,32,2209,0,601,0,0,0,638571732974124216,0.10.30.22292,%E8%83%BD%E9%87%8F%E6%9E%A2%E7%BA%BD,%E8%BE%93%E5%85%A5%E5%88%9D%E5%A7%8B%E5%A2%9E%E4%BA%A7%E5%89%82%E3%80%81%E8%93%84%E7%94%B5%E5%99%A8%E5%8D%B3%E5%8F%AF%E6%8C%81%E7%BB%AD%E8%BF%90%E8%A1%8C%EF%BC%8C%E4%B9%8B%E5%90%8E%E6%97%A0%E9%9C%80%E8%BE%93%E5%85%A5%E3%80%82%0A%0AEnter%20the%20initial%20proliferator%20and%20accumulator%20for%20continuous%20operation%2C%20after%20which%20no%20input%20is%20required.\"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\"88D105F9FFC6215AC6686914C3C377C8";

    private const string bp能量枢纽拓展 =
        "BLUEPRINT:0,32,2209,0,602,0,0,0,638571596487037974,0.10.30.22292,%E8%83%BD%E9%87%8F%E6%9E%A2%E7%BA%BD%E6%8B%93%E5%B1%95,%E5%8F%AF%E6%A0%B9%E6%8D%AE%E9%9C%80%E8%A6%81%E6%8B%93%E5%B1%95%E8%83%BD%E9%87%8F%E6%9E%A2%E7%BA%BD%E7%9A%84%E6%95%B0%E7%9B%AE%E3%80%82%0A%0AThe%20number%20of%20Energy%20Exchangers%20can%20be%20expanded%20as%20needed.\"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\"B4AF15BE9329781B71DACBCBC7B34C30";

    private const string bp升转化通用 =
        "BLUEPRINT:0,20,2322,2323,2323,0,0,0,638555528517002608,0.10.30.22292,%E5%8D%87%E9%99%8D%E7%BA%A7%E9%80%9A%E7%94%A8,%E4%BC%A0%E9%80%81%E5%B8%A6%E5%8F%AF%E9%9A%8F%E6%84%8F%E5%8D%87%E9%99%8D%E7%BA%A7%EF%BC%8C%E9%85%8D%E9%80%81%E5%99%A8%E9%9C%80%E7%82%B9%E5%87%BB%E6%99%BA%E8%83%BD%E7%8C%9C%E6%B5%8B%E3%80%82%0A%E5%8F%AF%E6%97%A0%E9%99%90%E4%B8%B2%E8%81%94%EF%BC%8C%E4%BE%BF%E4%BA%8E%E6%8B%93%E5%B1%95%E3%80%82%0A%0AThe%20conveyor%20belt%20can%20be%20raised%20and%20lowered%20at%20will%2C%20and%20the%20dispenser%20needs%20to%20be%20clicked%20for%20intelligent%20guessing.%0AUnlimited%20tandem%20connection%20is%20available%20for%20easy%20expansion.\"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\"A4C5C3CF91C0EC73E70A5C5E30B3837E";

    /// <summary>
    /// 添加部分蓝图至蓝图库。
    /// </summary>
    private static void AddBlueprints() {
        string FEBlueprintsDir = Path.Combine(GameConfig.blueprintFolder, "FEBlueprints".Translate());
        if (!Directory.Exists(FEBlueprintsDir)) {
            Directory.CreateDirectory(FEBlueprintsDir);
        }
        string introPath = Path.Combine(FEBlueprintsDir, "_intro_");
        if (!File.Exists(introPath)) {
            File.WriteAllText(introPath, intro);
        }
        string bp能量枢纽Path = Path.Combine(FEBlueprintsDir, "bp能量枢纽".Translate());
        if (!File.Exists(bp能量枢纽Path)) {
            File.WriteAllText(bp能量枢纽Path, bp能量枢纽);
        }
        string bp能量枢纽拓展Path = Path.Combine(FEBlueprintsDir, "bp能量枢纽拓展".Translate());
        if (!File.Exists(bp能量枢纽拓展Path)) {
            File.WriteAllText(bp能量枢纽拓展Path, bp能量枢纽拓展);
        }
        string bp升转化通用Path = Path.Combine(FEBlueprintsDir, "bp升转化通用".Translate());
        if (!File.Exists(bp升转化通用Path)) {
            File.WriteAllText(bp升转化通用Path, bp升转化通用);
        }
    }

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
        //UIMessageBox.Show会直接返回，所以这里只能显示一个弹窗。弹窗1消失后再展示弹窗2。
        if (!FractionateEverything.disableMessageBox) {
            ShowMessageBox();
        } else if (FractionateEverything.isVersionChanged) {
            ShowMessageBoxLatestVersion();
        }
        FractionateEverything.SetConfig();
        _shown = true;
    }

    private static void ShowMessageBox() {
        UIMessageBox.Show(
            "FE标题".Translate(), "FE信息".Translate(),
            "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(),
            UIMessageBox.INFO,
            ShowMessageBoxLatestVersion, ResponseFE日志, ResponseFE交流群
        );
    }

    private static void ResponseFE日志() {
#if DEBUG
        Application.OpenURL(Path.Combine(FractionateEverything.ModPath, "CHANGELOG.md"));
#else
        Application.OpenURL("FE日志链接".Translate());
#endif
        ShowMessageBoxLatestVersion();
    }

    private static void ResponseFE交流群() {
        Application.OpenURL("FE交流群链接".Translate());
        ShowMessageBoxLatestVersion();
    }

    private static void ShowMessageBoxLatestVersion() {
        const string version = PluginInfo.PLUGIN_VERSION;
        if ((version + "信息").Translate() != version + "信息") {
            UIMessageBox.Show(
                (version + "标题").Translate(), (version + "信息").Translate(),
                "确定".Translate(), "FE日志".Translate(), "FE交流群".Translate(),
                UIMessageBox.INFO,
                null, ResponseFE日志, ResponseFE交流群
            );
        }
    }
}
