using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using DSP_Battle;
using HarmonyLib;

namespace FE.Compatibility;

public static class TheyComeFromVoid {
    public const string GUID = "com.ckcz123.DSP_Battle";
    public static bool Enable;
    public static Assembly assembly;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.TheyComeFromVoid");
        harmony.PatchAll(typeof(TheyComeFromVoid));
        //任务链可使用所有来源物品
        harmony.Patch(AccessTools.Method(typeof(EventSystem), nameof(EventSystem.RefreshRequestMeetData)),
            transpiler: new(typeof(Utils.Utils), nameof(Utils.Utils.GetItemCount_Transpiler)));
        //任务链可使用所有来源物品
        harmony.Patch(AccessTools.Method(typeof(EventSystem), nameof(EventSystem.Decision)),
            transpiler: new(typeof(Utils.Utils), nameof(Utils.Utils.TakeTailItems_Transpiler)));
        //元驱动刷新可使用所有来源物品
        harmony.Patch(AccessTools.Method(typeof(UIRelic), nameof(UIRelic.RollNewAlternateRelics)),
            transpiler: new(typeof(Utils.Utils), nameof(Utils.Utils.TakeTailItems_Transpiler)));
        CheckPlugins.LogInfo("TheyComeFromVoid Compat finish.");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DSP_Battle.Utils), nameof(DSP_Battle.Utils.UIItemUp))]
    private static bool Utils_UIItemUp_Prefix(ref int itemId) {
        if (itemId == 8035) {
            itemId = 9514;
        }
        return true;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(BattleProtos), nameof(BattleProtos.AddTranslate))]
    private static IEnumerable<CodeInstruction> BattleProtos_AddTranslate_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var matcher = new CodeMatcher(instructions, generator);
        // 查找 ldc.i4 8035 指令并替换为 ldc.i4 9514
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldc_I4, 8035)
        );
        if (matcher.IsValid) {
            matcher.SetOperandAndAdvance(9514);
            CheckPlugins.LogInfo("TheyComeFromVoid: Replaced ldc.i4 8035 with ldc.i4 9514 in UIButton.LateUpdate");
        }
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 元驱动刷新可使用所有来源物品
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIRelic), nameof(UIRelic.CheckEnoughMatrixToRoll))]
    private static bool UIRelic_CheckEnoughMatrixToRoll_Prefix(ref bool __result) {
        if (Relic.rollCount <= 0) {
            return true;
        }
        int need = Relic.basicMatrixCost << Relic.rollCount;
        __result = Utils.Utils.GetItemTotalCount(5201) >= need;
        return false;
    }

    /// <summary>
    /// 授权点重置可使用所有来源物品
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UISkillPointsWindow), nameof(UISkillPointsWindow.CheckCanReset))]
    private static bool UISkillPointsWindow_CheckCanReset_Prefix(out int need, ref bool __result) {
        need = 0;
        int confirmedAssigned = SkillPoints.skillLevelL.Sum() + SkillPoints.skillLevelR.Sum();
        if (confirmedAssigned <= 0) {
            __result = false;
            return false;
        }
        need = confirmedAssigned * 10;
        if (confirmedAssigned > 100) {
            need += (confirmedAssigned - 100) * 40;
        }
        if (need > 5000) {
            need = 5000;
        }
        __result = Utils.Utils.GetItemTotalCount(6006) >= need;
        return false;
    }

    /// <summary>
    /// 授权点重置可使用所有来源物品
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UISkillPointsWindow), nameof(UISkillPointsWindow.OnResetAllClick))]
    private static bool UISkillPointsWindow_OnResetAllClick_Prefix() {
        //由于原方法TakeTailItems是内置隐式方法，所以无法使用Transpiler，只能Prefix拦截
        if (!UISkillPointsWindow.CheckCanReset(out int need)) {
            return false;
        }
        UIMessageBox.Show("重置技能点确认标题".Translate(), string.Format("重置技能点确认警告".Translate(), (object)need),
            "否".Translate(), "是".Translate(), 1, () => { }, () => {
                int itemId = 6006;
                int inc = 0;
                //GameMain.mainPlayer.package.TakeTailItems(ref itemId, ref need, out inc);
                Utils.Utils.TakeItemWithTip(itemId, need, out inc, false);
                SkillPoints.ResetAll();
                UISkillPointsWindow.RefreshResetButton();
            });
        return false;
    }
}
