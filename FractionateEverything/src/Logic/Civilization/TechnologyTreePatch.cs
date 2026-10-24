using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 按资格预告 FE 灰色问号、恢复后展示真实科技，并把两个物流交互科技归入第一页。
/// </summary>
public static class TechnologyTreePatch {
    private static readonly MethodInfo IsUpgradePageTechMethod = AccessTools.Method(
        typeof(TechnologyTreePatch), nameof(IsUpgradePageTech));
    // 新版节点增加了前置科技明细组，旧版引用中不存在该字段。
    private static readonly FieldInfo PreTechGroupField = AccessTools.Field(typeof(UITechNode), "preTechGroup");

    /// <summary>问号控件随原版节点生命周期回收，不参与研究或存档。</summary>
    private sealed class TechnologyPreview {
        /// <summary>覆盖在科技图标位置的匿名问号。</summary>
        public Text QuestionMark;
        /// <summary>是否正在覆盖原版展示。</summary>
        public bool Applied;
        /// <summary>覆盖前的图标启用状态，恢复时还原。</summary>
        public bool IconEnabled;
        /// <summary>原版大缩放标题的颜色。</summary>
        public Color TitleColor;
    }
    private static readonly ConditionalWeakTable<UITechNode, TechnologyPreview> Previews = new();

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

    /// <summary>物流集装科技同时提升FE塔输出堆叠，保留原版及其他模组的效果说明。</summary>
    /// <param name="__instance">当前科技原型。</param>
    /// <param name="__result">原版生成的解锁说明。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    private static void TechProto_UnlockFunctionText_Postfix(TechProto __instance, ref string __result) {
        if (__instance.ID >= T运输站集装物流 && __instance.ID <= T运输站集装物流 + 2) {
            __result += "\r\n" + "FE.Growth.StationStack".Translate();
        }
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
    /// FE 科技在恢复后显示真实内容，当前直接后继资格满足时显示灰色问号。
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

    /// <summary>按已恢复、已完成或当前可预告的直接后继资格显示 FE 节点。</summary>
    /// <param name="tech">已注册的FE科技原型。</param>
    /// <returns>节点是否应当显示。</returns>
    private static bool IsTechnologyVisible(TechProto tech) => !tech.IsObsolete
        && tech.ID is not TFE终极探索 and not TFE终极仓储
        && (GrowthTechnologyManager.IsTechnologyDiscovered(tech.ID)
            || GameMain.history?.TechUnlocked(tech.ID) == true
            || GrowthTechnologyManager.IsTechnologyPreview(tech.ID));

    /// <summary>原版刷新信息或布局后重新覆盖匿名展示，恢复资格时完整还原原版节点。</summary>
    /// <param name="__instance">刚完成原版刷新的科技节点。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.UpdateInfoComplete))]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.UpdateInfoDynamic))]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.UpdateLayoutDynamic))]
    private static void UITechNode_UpdatePreview_Postfix(UITechNode __instance) {
        if (__instance.techProto == null) { return; }
        bool preview = GrowthTechnologyManager.IsTechnologyPreview(__instance.techProto.ID);
        if (!preview) {
            if (!Previews.TryGetValue(__instance, out TechnologyPreview previous) || !previous.Applied) { return; }
            previous.Applied = false;
            previous.QuestionMark.gameObject.SetActive(false);
            __instance.techIcon.enabled = previous.IconEnabled;
            __instance.titleText2.color = previous.TitleColor;
            __instance.UpdateInfoComplete();
            __instance.UpdateInfoDynamic(true);
            __instance.UpdateLayoutDynamic(true, true);
            return;
        }
        TechnologyPreview state = Previews.GetValue(__instance, CreatePreview);
        if (!state.Applied) {
            state.IconEnabled = __instance.techIcon.enabled;
            state.TitleColor = __instance.titleText2.color;
            state.Applied = true;
        }
        state.QuestionMark.gameObject.SetActive(true);
        __instance.techIcon.enabled = false;
        __instance.titleText.text = "?";
        __instance.titleText2.text = "?";
        __instance.titleText.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        __instance.titleText2.color = __instance.titleText.color;
        __instance.bgImage.color = __instance.bgColor0;
        __instance.levelText.text = string.Empty;
        __instance.percentText.text = string.Empty;
        __instance.techDescText.text = string.Empty;
        __instance.unlockText.text = string.Empty;
        __instance.unlockGroup.gameObject.SetActive(false);
        __instance.techDescText.gameObject.SetActive(false);
        if (PreTechGroupField?.GetValue(__instance) is RectTransform preTechGroup) {
            preTechGroup.gameObject.SetActive(false);
        }
        __instance.prerequisiteGroup.gameObject.SetActive(false);
        __instance.costGroup.gameObject.SetActive(false);
        __instance.pCostGroup.gameObject.SetActive(false);
        __instance.costBg.gameObject.SetActive(false);
        __instance.startGroup.gameObject.SetActive(false);
        __instance.progressGroup.gameObject.SetActive(false);
        __instance.unlockDirectButton.gameObject.SetActive(false);
        __instance.developTipText.gameObject.SetActive(false);
        __instance.researchingText.gameObject.SetActive(false);
        __instance.startCanvas.interactable = false;
        __instance.progressCanvas.interactable = false;
    }

    /// <summary>创建随科技图标缩放的问号，只读取原版字体，不改共享科技原型。</summary>
    /// <param name="node">持有控件的原版节点。</param>
    /// <returns>由弱引用表持有的节点展示状态。</returns>
    private static TechnologyPreview CreatePreview(UITechNode node) {
        GameObject gameObject = new("fe-tech-question", typeof(RectTransform), typeof(Text));
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.SetParent(node.techIcon.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text question = gameObject.GetComponent<Text>();
        question.font = node.titleText.font;
        question.fontSize = 42;
        question.alignment = TextAnchor.MiddleCenter;
        question.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        question.text = "?";
        question.raycastTarget = false;
        return new TechnologyPreview { QuestionMark = question };
    }

    /// <summary>问号节点不能打开研究操作、元数据买断或沙盒单项解锁，原版一键解锁保持。</summary>
    /// <param name="__instance">玩家当前操作的科技节点。</param>
    /// <returns>仅真实节点继续原版操作。</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.OnPointerDown))]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.OnPointerEnter))]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.DoStartTech))]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.DoBuyoutTech))]
    [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.OnUnlockDirectButton))]
    private static bool UITechNode_PreviewAction_Prefix(UITechNode __instance) =>
        !GrowthTechnologyManager.IsTechnologyPreview(__instance.techProto?.ID ?? 0);

    /// <summary>同步连接到 FE 科技的箭头与公共线段，隐藏目标不留下向外延伸的线。</summary>
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
            bool visible = targets[i].page == tech.page
                && __instance.tree.nodes.TryGetValue(targets[i].ID, out UITechNode targetNode)
                && targetNode.active
                && (GrowthTechnologyManager.TryGetTechnologyContentId(targets[i].ID, out _)
                    ? IsTechnologyVisible(targets[i])
                    : arrow.gameObject.activeSelf);
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
