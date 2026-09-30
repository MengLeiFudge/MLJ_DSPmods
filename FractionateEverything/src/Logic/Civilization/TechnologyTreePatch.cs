using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 让 FE 科技按解析状态显现、保护沙盒探索门槛，并把两个物流交互科技归入科技树第一页。
/// </summary>
public static class TechnologyTreePatch {
    private static readonly FieldInfo PublishedField = AccessTools.Field(
        typeof(TechProto), nameof(TechProto.Published));
    private static readonly MethodInfo IsSandboxUnlockEligibleMethod = AccessTools.Method(
        typeof(TechnologyTreePatch), nameof(IsSandboxUnlockEligible));
    private static readonly MethodInfo IsUpgradePageTechMethod = AccessTools.Method(
        typeof(TechnologyTreePatch), nameof(IsUpgradePageTech));

    /// <summary>
    /// 两个物流交互科技保留稳定的 2061/2062 ID，但在界面中属于建筑科技第一页。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.page), MethodType.Getter)]
    private static void TechProto_Page_Postfix(TechProto __instance, ref int __result) {
        if (__instance.ID is TFE行星内物流交互 or TFE星际物流交互) {
            __result = 0;
        }
    }

    /// <summary>
    /// 科技方向解析完成后同时显示整行；研究顺序仍由节点本身的前置关系限制。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.DetermineTechVisible))]
    private static bool UITechNode_DetermineTechVisible_Prefix(UITechNode __instance,
        ref bool __result) {
        int techId = __instance.techProto?.ID ?? 0;
        if (!GrowthTechnologyManager.TryGetTechnologyContentId(techId, out _)) {
            return true;
        }

        bool discovered = GrowthTechnologyManager.IsTechnologyDiscovered(techId);
        __instance.techProto.IsHiddenTech = !discovered;
        __result = discovered;
        if (__result) {
            __instance._Open();
        } else {
            __instance._Close();
        }
        return false;
    }

    /// <summary>
    /// 原版科技树有六处直接按 ID 2000 分页，需要与 TechProto.page 使用同一分类规则。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree._OnOpen))]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree.OnPageChanged))]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree._OnUpdate))]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree._OnLateUpdate))]
    private static IEnumerable<CodeInstruction> UITechTree_PageSelection_Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) {
        List<CodeInstruction> codes = instructions.ToList();
        List<int> matches = [];
        for (int i = 0; i <= codes.Count - 5; i++) {
            if (LoadsInt(codes[i], 2000)
                && codes[i + 1].opcode is var lessThan
                && (lessThan == OpCodes.Blt || lessThan == OpCodes.Blt_S)
                && LoadsInt(codes[i + 2], 1)
                && codes[i + 3].opcode is var branch
                && (branch == OpCodes.Br || branch == OpCodes.Br_S)
                && LoadsInt(codes[i + 4], 0)) {
                matches.Add(i);
            }
        }

        int expected = __originalMethod.Name is nameof(UITechTree._OnUpdate)
            or nameof(UITechTree._OnLateUpdate) ? 2 : 1;
        if (matches.Count != expected) {
            LogError($"科技树分页补丁 {__originalMethod.Name} 预期匹配 {expected} 处，实际 {matches.Count} 处。");
            return codes;
        }

        for (int matchIndex = matches.Count - 1; matchIndex >= 0; matchIndex--) {
            int index = matches[matchIndex];
            CodeInstruction replacement = new(OpCodes.Call, IsUpgradePageTechMethod);
            replacement.labels.AddRange(codes[index].labels);
            replacement.blocks.AddRange(codes[index].blocks);
            codes[index] = replacement;
            codes.RemoveRange(index + 1, 4);
        }
        return codes;
    }

    /// <summary>
    /// 沙盒批量解锁和对应完成状态只计算已完成探索解析的 FE 科技；原版科技保持原行为。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree._OnUpdate))]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree.Do1KeyUnlock))]
    [HarmonyPatch(typeof(GoalLogic), nameof(GoalLogic.CheckIsSanboxAllTechUnlocked))]
    [HarmonyPatch(typeof(GoalTools), nameof(GoalTools.CheckSandbox1KeyUnlockTech))]
    private static IEnumerable<CodeInstruction> SandboxUnlockEligibility_Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) {
        List<CodeInstruction> codes = instructions.ToList();
        List<int> matches = [];
        for (int index = 0; index < codes.Count; index++) {
            if (codes[index].LoadsField(PublishedField)) {
                matches.Add(index);
            }
        }
        if (matches.Count != 1) {
            LogError($"沙盒科技资格补丁 {__originalMethod.Name} 预期匹配 1 处，实际 {matches.Count} 处。");
            return codes;
        }

        int match = matches[0];
        CodeInstruction replacement = new(OpCodes.Call, IsSandboxUnlockEligibleMethod);
        replacement.labels.AddRange(codes[match].labels);
        replacement.blocks.AddRange(codes[match].blocks);
        codes[match] = replacement;
        return codes;
    }

    private static bool IsSandboxUnlockEligible(TechProto tech) =>
        tech.Published
        && (!GrowthTechnologyManager.TryGetTechnologyContentId(tech.ID, out _)
            || GrowthTechnologyManager.IsTechnologyDiscovered(tech.ID));

    private static bool IsUpgradePageTech(int techId) =>
        techId >= 2000 && techId is not TFE行星内物流交互 and not TFE星际物流交互;

    private static bool LoadsInt(CodeInstruction instruction, int value) => value switch {
        0 => instruction.opcode == OpCodes.Ldc_I4_0,
        1 => instruction.opcode == OpCodes.Ldc_I4_1,
        _ => instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int operand && operand == value,
    };
}
