using System;
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
    private static MethodInfo relicCountMethod;
    private static FieldInfo meritRankField;
    private static FieldInfo skillLevelLField;
    private static FieldInfo skillLevelRField;
    private static FieldInfo eventRecorderField;
    private static FieldInfo eventProtoIdField;

    public static int GetRelicCount() {
        return !Enable || relicCountMethod == null ? 0 : InvokeIntMethod(relicCountMethod);
    }

    public static int GetMeritRank() {
        return !Enable || meritRankField == null ? 0 : GetStaticIntFieldValue(meritRankField);
    }

    public static int GetAssignedSkillPointCount() {
        if (!Enable) {
            return 0;
        }
        return SumStaticIntArray(skillLevelLField) + SumStaticIntArray(skillLevelRField);
    }

    public static bool HasActiveEventChain() {
        if (!Enable || eventRecorderField == null || eventProtoIdField == null) {
            return false;
        }
        object recorder = eventRecorderField.GetValue(null);
        return recorder != null && GetInstanceIntFieldValue(eventProtoIdField, recorder) > 0;
    }

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.TheyComeFromVoid");
        harmony.PatchAll(typeof(TheyComeFromVoid));
        //Compatible必定执行，所以此方法中不能出现深空的类，否则会报错
        CacheApiMembers();
        PatchMethods(harmony);
        CheckPlugins.LogInfo("TheyComeFromVoid Compat finish.");
    }

    private static void CacheApiMembers() {
        // 通过字符串反射隔离可选模组依赖，避免未安装深空来敌时触发 CLR 解析外部程序集。
        Type relicType = assembly?.GetType("DSP_Battle.Relic");
        Type rankType = assembly?.GetType("DSP_Battle.Rank");
        Type skillPointsType = assembly?.GetType("DSP_Battle.SkillPoints");
        Type eventSystemType = assembly?.GetType("DSP_Battle.EventSystem");

        relicCountMethod = AccessTools.Method(relicType, "GetRelicCount");
        meritRankField = AccessTools.Field(rankType, "rank");
        skillLevelLField = AccessTools.Field(skillPointsType, "skillLevelL");
        skillLevelRField = AccessTools.Field(skillPointsType, "skillLevelR");
        eventRecorderField = AccessTools.Field(eventSystemType, "recorder");

        Type recorderType = eventRecorderField?.FieldType;
        eventProtoIdField = AccessTools.Field(recorderType, "protoId");
        if (relicCountMethod == null
            || meritRankField == null
            || skillLevelLField == null
            || skillLevelRField == null
            || eventRecorderField == null
            || eventProtoIdField == null) {
            CheckPlugins.LogWarning("TheyComeFromVoid: 关键反射入口缺失，黑雾增强层数据将自动回退为默认值。");
        }
    }

    private static int InvokeIntMethod(MethodInfo method) {
        try {
            return method.Invoke(null, null) is int value ? value : 0;
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"TheyComeFromVoid: 调用 {method.Name} 失败，已回退为 0。{ex}");
            return 0;
        }
    }

    private static int GetStaticIntFieldValue(FieldInfo field) {
        try {
            return field.GetValue(null) is int value ? value : 0;
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"TheyComeFromVoid: 读取字段 {field.Name} 失败，已回退为 0。{ex}");
            return 0;
        }
    }

    private static int GetInstanceIntFieldValue(FieldInfo field, object instance) {
        try {
            return field.GetValue(instance) is int value ? value : 0;
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"TheyComeFromVoid: 读取实例字段 {field.Name} 失败，已回退为 0。{ex}");
            return 0;
        }
    }

    private static int SumStaticIntArray(FieldInfo field) {
        try {
            return field?.GetValue(null) is int[] values ? values.Sum() : 0;
        }
        catch (Exception ex) {
            string fieldName = field?.Name ?? "null";
            CheckPlugins.LogWarning($"TheyComeFromVoid: 读取数组字段 {fieldName} 失败，已回退为 0。{ex}");
            return 0;
        }
    }

    private static void PatchMethods(Harmony harmony) {
        //任务链可使用所有来源物品
        harmony.Patch(AccessTools.Method(typeof(EventSystem), nameof(EventSystem.RefreshRequestMeetData)),
            transpiler: new(typeof(Utils.Utils), nameof(Utils.Utils.GetItemCount_Transpiler)));
        //任务链可使用所有来源物品
        harmony.Patch(AccessTools.Method(typeof(EventSystem), nameof(EventSystem.Decision)),
            transpiler: new(typeof(Utils.Utils), nameof(Utils.Utils.TakeTailItems_Transpiler)));
        //元驱动刷新可使用所有来源物品
        harmony.Patch(AccessTools.Method(typeof(UIRelic), nameof(UIRelic.RollNewAlternateRelics)),
            transpiler: new(typeof(Utils.Utils), nameof(Utils.Utils.TakeTailItems_Transpiler)));
        // BattleProtos 在原版 DLL 中不是稳定 public 类型，改为运行时反射查找，兼容直接引用 R2 原始 DLL。
        MethodInfo battleProtosAddTranslate =
            AccessTools.Method(assembly?.GetType("DSP_Battle.BattleProtos"), "AddTranslate");
        if (battleProtosAddTranslate != null) {
            harmony.Patch(battleProtosAddTranslate,
                transpiler: new(typeof(TheyComeFromVoid), nameof(BattleProtos_AddTranslate_Transpiler)));
        } else {
            CheckPlugins.LogWarning("TheyComeFromVoid: 未找到 BattleProtos.AddTranslate，跳过对应翻译补丁。");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DSP_Battle.Utils), nameof(DSP_Battle.Utils.UIItemUp))]
    private static bool Utils_UIItemUp_Prefix(ref int itemId) {
        if (itemId == 8035) {
            itemId = 9514;
        }
        return true;
    }

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
