using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 让 FE 科技按解析或已解锁状态显现，并把两个物流交互科技归入科技树第一页。
/// </summary>
public static class TechnologyTreePatch {
    private static readonly MethodInfo IsUpgradePageTechMethod = AccessTools.Method(
        typeof(TechnologyTreePatch), nameof(IsUpgradePageTech));

    /// <summary>启用沙盒工具时立即取消资格，避免短暂启用后关闭绕过按秒检查。</summary>
    /// <param name="value">新的沙盒工具开关。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.sandboxToolsEnabled), MethodType.Setter)]
    private static void GameMain_SandboxToolsEnabled_Postfix(bool value) {
        if (value && GameMain.data != null) { ResearchManager.DisqualifyCompletion(); }
    }

    /// <summary>使用原版一键解锁后，当前存档不再授予正式游玩的完成标记。</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree.Do1KeyUnlock))]
    private static void UITechTree_Do1KeyUnlock_Prefix() => ResearchManager.DisqualifyCompletion();

    /// <summary>
    /// 为 FE 成长科技提供当前等级的增量说明。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    private static bool TechProto_UnlockFunctionText_Prefix(TechProto __instance, ref string __result) {
        if (!GrowthTechnologyManager.TryGetTechnologyUnlockText(__instance, out string text)) {
            return true;
        }
        __result = text;
        return false;
    }

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
    /// FE科技按自身发现或完成资格显现；沙盒沿用同一显隐规则。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.DetermineTechVisible))]
    private static bool UITechNode_DetermineTechVisible_Prefix(UITechNode __instance,
        ref bool __result) {
        TechProto tech = __instance.techProto;
        int techId = tech?.ID ?? 0;
        if (!GrowthTechnologyManager.TryGetTechnologyContentId(techId, out _)) {
            return true;
        }

        bool visible = IsTechnologyVisible(tech);
        tech.IsHiddenTech = !visible;
        __result = visible;
        if (visible) {
            __instance._Open();
        } else {
            __instance._Close();
        }
        return false;
    }

    /// <summary>判断FE节点自身的显现资格，不预告尚未恢复的后续阶段。</summary>
    /// <param name="tech">已注册的FE科技原型。</param>
    /// <returns>节点是否应当显示。</returns>
    private static bool IsTechnologyVisible(TechProto tech) => !tech.IsObsolete
        && (GrowthTechnologyManager.IsTechnologyDiscovered(tech.ID)
            || GameMain.history?.TechUnlocked(tech.ID) == true);

    /// <summary>同步连接到FE科技的箭头与公共线段，隐藏目标不留下向外延伸的线。</summary>
    /// <param name="__instance">刚完成原版连接布局的科技节点。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.UpdateConnDynamic))]
    private static void UITechNode_UpdateConnDynamic_Postfix(UITechNode __instance) {
        TechProto tech = __instance.techProto;
        TechProto[] targets = tech?.postTechArray;
        if (targets == null || __instance.arrows == null || __instance.connGroup == null) { return; }
        bool managed = GrowthTechnologyManager.TryGetTechnologyContentId(tech.ID, out _)
            || targets.Any(target => GrowthTechnologyManager.TryGetTechnologyContentId(target.ID, out _));
        if (!managed) { return; }

        bool anyVisible = false;
        float bottom = __instance.inTrunk ? -22f : __instance.tree.zoomed2 > 0.01f ? -3f : -2f;
        float top = 0f;
        for (int i = 0; i < targets.Length && i < __instance.arrows.Length; i++) {
            var arrow = __instance.arrows[i];
            if (arrow == null) { continue; }
            bool visible = __instance.tree.nodes.TryGetValue(targets[i].ID, out UITechNode targetNode)
                && (GrowthTechnologyManager.TryGetTechnologyContentId(targets[i].ID, out _)
                    ? IsTechnologyVisible(targets[i])
                    : targetNode.active && arrow.gameObject.activeSelf);
            arrow.gameObject.SetActive(visible);
            if (!visible) { continue; }
            anyVisible = true;
            float y = arrow.anchoredPosition.y;
            if (y > 0f) { y -= __instance.inTrunk ? 10f : 0f; }
            if (y < 0f) { y -= __instance.inTrunk ? 12f : 2f; }
            bottom = System.Math.Min(bottom, y);
            top = System.Math.Max(top, y);
        }
        __instance.connGroup.gameObject.SetActive(anyVisible);
        if (!anyVisible) { return; }
        __instance.outputLine.gameObject.SetActive(true);
        __instance.expandLine.anchoredPosition = new UnityEngine.Vector2(0f, top);
        __instance.expandLine.sizeDelta = new UnityEngine.Vector2(
            __instance.tree.zoomed2 > 0.01f ? 3f : 2f, top - bottom);
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

    private static bool IsUpgradePageTech(int techId) =>
        techId >= 2000 && techId is not TFE行星内物流交互 and not TFE星际物流交互;

    private static bool LoadsInt(CodeInstruction instruction, int value) => value switch {
        0 => instruction.opcode == OpCodes.Ldc_I4_0,
        1 => instruction.opcode == OpCodes.Ldc_I4_1,
        _ => instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int operand && operand == value,
    };
}
