using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using ProjectGenesis.Patches;
using static FE.Utils.Utils;

namespace FE.Compatibility;

public static class GenesisBook {
    public const string GUID = "org.LoShin.GenesisBook";
    public static bool Enable;
    public static Assembly assembly;

    public static int tab精炼;
    public static int tab化工;
    public static int tab防御;

    #region 创世ERecipeType拓展

    public const ERecipeType 基础制造 = ERecipeType.Assemble;
    public const ERecipeType 标准制造 = (ERecipeType)9;
    public const ERecipeType 高精度加工 = (ERecipeType)10;

    #endregion

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        tab精炼 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab1");
        tab化工 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab2");
        tab防御 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab3");
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.GenesisBook");
        harmony.PatchAll(typeof(GenesisBook));
        CheckPlugins.LogInfo("GenesisBook Compat finish.");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(FastStartOptionPatches), nameof(FastStartOptionPatches.SetForNewGame))]
    private static IEnumerable<CodeInstruction> FastStartOptionPatches_SetForNewGame_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        //if (!GameMain.data.history.TechUnlocked(proto.ID) && NeedFastUnlock(proto.Items))
        //变为
        //if (IsFracTech(proto.ID) && !GameMain.data.history.TechUnlocked(proto.ID) && NeedFastUnlock(proto.Items))
        var matcher = new CodeMatcher(instructions);
        //寻找: GameMain.data.history.TechUnlocked(proto.ID)
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldsfld),// GameMain.data
            new CodeMatch(OpCodes.Ldfld),// .history
            new CodeMatch(OpCodes.Ldloc_3),// proto
            new CodeMatch(OpCodes.Ldfld),// .ID
            new CodeMatch(OpCodes.Callvirt)// TechUnlocked
        );
        if (matcher.IsInvalid) {
            CheckPlugins.LogError("Failed to find TechUnlocked call pattern");
            return instructions;
        }
        //找到要跳转的标签
        var matcher2 = matcher.Clone();
        matcher2.MatchForward(false, new CodeMatch(OpCodes.Brtrue));
        if (matcher2.IsInvalid) {
            CheckPlugins.LogError("Failed to find Brtrue opcode");
            return instructions;
        }
        // 在 GameMain.data.history.TechUnlocked 调用之前插入我们的检查
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_3),// proto
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TechProto), "ID")),// proto.ID
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(GenesisBook), nameof(IsFracTech))),// IsFracTech(proto.ID)
            new CodeInstruction(matcher2.Opcode, matcher2.Operand)// 如果是分馏科技，直接跳过
        );
        return matcher.InstructionEnumeration();
    }

    public static bool IsFracTech(int id) {
        return id >= TFE分馏数据中心 && id <= TFE超值礼包9;
    }

    // /// <summary>
    // /// 修复开启“科技探索”时，分馏塔的科技不能显示的问题
    // /// </summary>
    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(InitialTechPatches), nameof(InitialTechPatches.RefreshNode))]
    // private static bool InitialTechPatches_RefreshNode_Prefix(ref UITechTree __instance) {
    //     GameHistoryData history = GameMain.history;
    //     foreach ((int techId, UITechNode node) in __instance.nodes) {
    //         TechProto tech = node?.techProto;
    //         CheckPlugins.LogInfo($"RefreshNode[start]: techId{techId}, TechProto{tech}");
    //         if (techId > 1999 || node == null || tech.IsHiddenTech) {
    //             CheckPlugins.LogWarning($"RefreshNode[continue]: techId{techId}, TechProto{tech} ");
    //             continue;
    //         }
    //         bool techUnlocked = history.TechUnlocked(techId);
    //         bool active = techUnlocked;
    //         if (tech.PreTechs.Length > 0) {
    //             active |= tech.PreTechs.Any(history.TechUnlocked);
    //         } else if (tech.PreTechsImplicit.Length > 0) {
    //             active |= tech.PreTechsImplicit.Any(history.TechUnlocked);
    //         } else {
    //             active = true;
    //         }
    //         node.gameObject.SetActive(active);
    //         if (tech.postTechArray.Length > 0) {
    //             node.connGroup.gameObject.SetActive(techUnlocked);
    //         }
    //         CheckPlugins.LogInfo($"RefreshNode[end]: techId{techId}, TechProto{tech}");
    //     }
    //     return false;
    // }
}
