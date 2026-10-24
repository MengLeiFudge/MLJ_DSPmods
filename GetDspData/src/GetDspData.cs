using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CommonAPI;
using crecheng.DSPModSave;
using GetDspData.Export;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using xiaoye97;
using static BepInEx.BepInDependency.DependencyFlags;
using static GetDspData.Utils.Utils;

namespace GetDspData;
//item.UnlockKey
//UnlockKey>0：跟随解锁，例如蓄电器（满）是跟随蓄电器解锁的
//UnlockKey=0：由科技解锁
//UnlockKey=-1：直接解锁
//UnlockKey=-2：由黑雾掉落

//LDB.ItemName 等价于 item.name，item.name 等价于 item.Name.Translate()
//name: 推进器  name.Translate: <0xa0>-<0xa0>推进器  Name: 推进器2  Name.Translate: 推进器
//name: Thruster  name.Translate: Thruster  Name: 推进器2  Name.Translate: Thruster
//name: 制造台<0xa0>Mk.I  name.Translate: 制造台<0xa0>Mk.I  Name: 制造台 Mk.I  Name.Translate: 制造台<0xa0>Mk.I
//name: Assembling Machine Mk.I  name.Translate: Assembling Machine Mk.I  Name: 制造台 Mk.I  Name.Translate: Assembling Machine Mk.I

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LDBToolPlugin.MODGUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(MoreMegaStructureGUID, SoftDependency)]
[BepInDependency(TheyComeFromVoidGUID, SoftDependency)]
[BepInDependency(GenesisBookGUID, SoftDependency)]
[BepInDependency(OrbitalRingGUID, SoftDependency)]
[BepInDependency(FractionateEverythingGUID, SoftDependency)]
public class GetDspData : BaseUnityPlugin {
    private const string SolutionDir = @"D:\project\dsp\MLJ_DSPmods";
    private const string IconExportRequestPath =
        SolutionDir + @"\gamedata\calc-icon-export-request.json";

    #region 日志

    private static ManualLogSource logger;

    public static void LogDebug(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Debug]{data}");
        } else {
            logger.LogDebug(data);
        }
    }

    public static void LogInfo(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Info]{data}");
        } else {
            logger.LogInfo(data);
        }
    }

    public static void LogWarning(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Warning]{data}");
        } else {
            logger.LogWarning(data);
        }
    }

    public static void LogError(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Error]{data}");
        } else {
            logger.LogError(data);
        }
    }

    public static void LogFatal(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Fatal]{data}");
        } else {
            logger.LogFatal(data);
        }
    }

    #endregion

    private static string dir = null;
    public const string MoreMegaStructureGUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool MoreMegaStructureEnable;
    public const string TheyComeFromVoidGUID = "com.ckcz123.DSP_Battle";
    public static bool TheyComeFromVoidEnable;
    public const string GenesisBookGUID = "org.LoShin.GenesisBook";
    public const string GBMSHarmonyPatchID = "ProjectGenesis.Compatibility.Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool GenesisBookEnable;
    public const string OrbitalRingGUID = "org.ProfessorCat305.OrbitalRing";
    public const string ORMSHarmonyPatchID = "ProjectOrbitalRing.Compatibility.Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool OrbitalRingEnable;
    public const string FractionateEverythingGUID = "com.menglei.dsp.fe";
    public static bool FractionateEverythingEnable;

    public void Awake() {
        logger = Logger;

        if (!Directory.Exists(SolutionDir)) {
            return;
        }
        dir = $@"{SolutionDir}\gamedata";
        if (!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
        MoreMegaStructureEnable = Chainloader.PluginInfos.ContainsKey(MoreMegaStructureGUID);
        TheyComeFromVoidEnable = Chainloader.PluginInfos.ContainsKey(TheyComeFromVoidGUID);
        GenesisBookEnable = Chainloader.PluginInfos.ContainsKey(GenesisBookGUID);
        OrbitalRingEnable = Chainloader.PluginInfos.ContainsKey(OrbitalRingGUID);
        FractionateEverythingEnable = Chainloader.PluginInfos.ContainsKey(FractionateEverythingGUID);

        Harmony harmony = new(PluginInfo.PLUGIN_GUID);
        harmony.Patch(
            AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
            null,
            new(typeof(GetDspData), nameof(WriteDataToFile)) {
                after = [
                    LDBToolPlugin.MODGUID,
                    MoreMegaStructureGUID,
                    TheyComeFromVoidGUID,
                    GenesisBookGUID,
                    GBMSHarmonyPatchID,
                    OrbitalRingGUID,
                    ORMSHarmonyPatchID,
                    FractionateEverythingGUID,
                ],
                priority = Priority.Last,
            }
        );
    }

    /// <summary>
    /// 在全部依赖模组加载完成后导出开发与计算器所需数据文件。
    /// </summary>
    private static void WriteDataToFile() {
        if (Localization.CurrentLanguageLCID != Localization.LCID_ZHCN) {
            Localization.CurrentLanguageLCID = Localization.LCID_ZHCN;
        }

        //如果当前语言索引非中文，直接返回
        if (Localization.CurrentLanguageLCID != Localization.LCID_ZHCN) {
            LogWarning("当前语言非中文，不生成数据文件");
            return;
        }

        //冲突的mod同时启用时，直接返回
        if (GenesisBookEnable && OrbitalRingEnable) {
            LogWarning("创世与星环同时启用");
            return;
        }

        //检测兼容补丁是否已执行完毕
        if (MoreMegaStructureEnable) {
            if (GenesisBookEnable && !Harmony.HasAnyPatches(GBMSHarmonyPatchID)) {
                LogFatal("未能正常patch");
                return;
            }
            if (OrbitalRingEnable && !Harmony.HasAnyPatches(ORMSHarmonyPatchID)) {
                LogFatal("未能正常patch");
                return;
            }
        }

        InitMiningData();

        try {
            var (fullName, shortName) = ResolveAndLogEnabledModCombination();
            string baseDir = EnsureExportDirectory(shortName);
            ProtoDataExporter.WriteProtoIdFile(baseDir);
            if (FractionateEverythingEnable) {
                ProtoDataExporter.SaveFEItemValue(baseDir);
            }
            ProtoDataExporter.WriteGameDataCsv(baseDir);
            string dirCalc = EnsureCalculatorJsonDirectory();
            JObject dataObj = CalculatorDataExporter.BuildCalculatorData();
            CalculatorDataExporter.WriteCalculatorJson(dirCalc, fullName, dataObj);
            IconExporter.ExportRequestedIcons(IconExportRequestPath);
        }
        catch (Exception ex) {
            LogError(ex.ToString());
        }
    }

    /// <summary>
    /// 根据当前启用的计算器相关模组拼接导出命名，并记录启用日志。
    /// </summary>
    /// <returns>fullName 为完整模组名下划线组合；shortName 为短名组合或 Vanilla。</returns>
    private static (string fullName, string shortName) ResolveAndLogEnabledModCombination() {
        string modFullStr = "";
        string modShortStr = "";
        (bool enabled, string fullName, string shortName)[] calcMods = [
            (MoreMegaStructureEnable, "MoreMegaStructure", "MS"),
            (TheyComeFromVoidEnable, "TheyComeFromVoid", "VD"),
            (GenesisBookEnable, "GenesisBook", "GB"),
            (OrbitalRingEnable, "OrbitalRing", "OR"),
            (FractionateEverythingEnable, "FractionateEverything", "FE"),
        ];
        foreach (var calcMod in calcMods) {
            if (calcMod.enabled) {
                LogInfo($"已启用{calcMod.fullName}");
                modFullStr += "_" + calcMod.fullName;
                modShortStr += "_" + calcMod.shortName;
            }
        }
        modShortStr = modShortStr == "" ? "Vanilla" : modShortStr.Substring(1);
        modFullStr = modFullStr == "" ? "Vanilla" : modFullStr.Substring(1);
        return (modFullStr, modShortStr);
    }

    /// <summary>
    /// 确保当前模组组合的导出根目录存在。
    /// </summary>
    /// <param name="modShortStr">短名组合目录名。</param>
    /// <returns>导出根目录绝对路径。</returns>
    private static string EnsureExportDirectory(string modShortStr) {
        string baseDir = $@"{dir}\{modShortStr}";
        if (!Directory.Exists(baseDir)) {
            Directory.CreateDirectory(baseDir);
        }
        return baseDir;
    }

    /// <summary>
    /// 确保计算器 JSON 输出目录存在。
    /// </summary>
    /// <returns>calc json 目录绝对路径。</returns>
    private static string EnsureCalculatorJsonDirectory() {
        string dirCalc = $@"{dir}\calc json";
        if (!Directory.Exists(dirCalc)) {
            Directory.CreateDirectory(dirCalc);
        }
        return dirCalc;
    }

}
