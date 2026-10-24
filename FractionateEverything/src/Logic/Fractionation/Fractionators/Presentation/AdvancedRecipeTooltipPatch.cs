using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation;

using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>用运行配方显示成功轮均值和催化需求，不改变原型、结算或普通配方提示。</summary>
public static class AdvancedRecipeTooltipPatch {
    private static readonly ConditionalWeakTable<UIItemTip, Text> Statistics = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIRecipeEntry), nameof(UIRecipeEntry.SetRecipe))]
    private static void UIRecipeEntry_SetRecipe_Postfix(UIRecipeEntry __instance, RecipeProto recipe) {
        WorldviewRecipe advanced = AdvancedRecipeCatalog.GetRecipe(recipe.ID);
        if (advanced == null) { return; }
        int buildingId = GetBuildingId(advanced);
        int slot = 0;
        // 原版依次排列产物和输入，最多显示七个图标；仅改数字，不改共享整数原型。
        foreach (WorldviewRecipeOutput output in advanced.ProductArray) {
            if (slot >= 7 || slot >= __instance.countTexts.Length) { break; }
            __instance.countTexts[slot++].text = AdvancedFractionatorRuntime
                .GetOutputPerSuccess(advanced, output).ToString("0.###");
        }
        foreach (WorldviewRecipeInput input in advanced.InputArray) {
            if (slot >= 7 || slot >= __instance.countTexts.Length) { break; }
            float consumed = input.Count * AdvancedFractionatorRuntime.GetInputConsumeProbability(input, buildingId);
            __instance.countTexts[slot++].text = consumed.ToString("0.###");
        }
        __instance.timeText.text = AdvancedFractionatorRuntime.GetSuccessProbability(advanced,
            ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId)).ToString("0.#%");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIItemTip), nameof(UIItemTip.SetTip))]
    private static void UIItemTip_SetTip_Prefix(UIItemTip __instance, int itemId, ref bool isRecipe) {
        // 替代提示模组可能跳过原方法，因此在入口先隐藏上次的图表说明。
        if (Statistics.TryGetValue(__instance, out Text statistics)) { statistics.gameObject.SetActive(false); }
        // 配方以负ID进入原版提示；也覆盖从普通UIButton打开、未显式传isRecipe的入口。
        if (itemId < 0 && AdvancedRecipeCatalog.GetRecipe(-itemId) != null) { isRecipe = true; }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIItemTip), nameof(UIItemTip.SetTip))]
    private static IEnumerable<CodeInstruction> UIItemTip_SetTip_Transpiler(IEnumerable<CodeInstruction> instructions) {
        List<CodeInstruction> code = instructions.ToList();
        MethodInfo unlocked = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.TechUnlocked), [typeof(int)]);
        MethodInfo setText = AccessTools.PropertySetter(typeof(Text), nameof(Text.text));
        MethodInfo setSize = AccessTools.PropertySetter(typeof(RectTransform), nameof(RectTransform.sizeDelta));
        MethodInfo showBonus = AccessTools.Method(typeof(AdvancedRecipeTooltipPatch), nameof(ShouldShowRecipeBonus));
        MethodInfo writeBonus = AccessTools.Method(typeof(AdvancedRecipeTooltipPatch), nameof(SetRecipeBonus));
        MethodInfo finishLayout = AccessTools.Method(typeof(AdvancedRecipeTooltipPatch), nameof(SetTooltipSize));
        // 最后一次sizeDelta赋值确定完整高度，随后原版统一修正屏幕边界。
        int finalSize = code.FindLastIndex(instruction => instruction.Calls(setSize));
        if (finalSize < 0) { throw new InvalidOperationException("FE高级配方提示未找到最终窗口尺寸入口。"); }
        bool recipeBonus = false;
        int gates = 0;
        int texts = 0;
        for (int i = 0; i < code.Count; i++) {
            CodeInstruction instruction = code[i];
            if (i == finalSize) {
                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call, finishLayout);
                continue;
            }
            if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, "增产公式描述1")) { recipeBonus = true; }
            MethodInfo replacement = null;
            if (instruction.Calls(unlocked)) {
                replacement = showBonus;
                gates++;
            } else if (recipeBonus && instruction.Calls(setText)) {
                replacement = writeBonus;
                recipeBonus = false;
                texts++;
            }
            if (replacement == null) {
                yield return instruction;
                continue;
            }
            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)10);
            yield return new CodeInstruction(OpCodes.Call, replacement);
        }
        if (gates != 1 || texts != 1) {
            throw new InvalidOperationException("FE高级配方提示未找到唯一的增产说明入口。");
        }
    }

    private static bool ShouldShowRecipeBonus(GameHistoryData history, int techId, int itemId, bool isRecipe) =>
        isRecipe && itemId < 0 && AdvancedRecipeCatalog.GetRecipe(-itemId) != null || history.TechUnlocked(techId);

    /// <summary>增产剂区域只显示原版加速生产说明，配方数字由下方图表承担。</summary>
    private static void SetRecipeBonus(Text text, string original, int itemId, bool isRecipe) {
        text.text = isRecipe && itemId < 0 && AdvancedRecipeCatalog.GetRecipe(-itemId) != null
            ? "+\u00a0" + "增产公式描述2".Translate() : original;
    }

    /// <summary>在配方图下方单列均值口径与催化持有要求，先扩高，再由原版调整屏幕边界。</summary>
    private static void SetTooltipSize(RectTransform rect, Vector2 size, UIItemTip tip, int itemId) {
        WorldviewRecipe recipe = itemId < 0 ? AdvancedRecipeCatalog.GetRecipe(-itemId) : null;
        if (recipe != null) {
            Text text = Statistics.GetValue(tip, owner => {
                Text label = UnityEngine.Object.Instantiate(owner.descText, owner.trans);
                label.name = "fe-recipe-statistics";
                Localizer localizer = label.GetComponent<Localizer>();
                if (localizer != null) { localizer.enabled = false; }
                label.raycastTarget = false;
                label.alignment = TextAnchor.UpperLeft;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                return label;
            });
            text.gameObject.SetActive(true);
            text.text = GetRecipeStatistics(recipe);
            RectTransform labelRect = RectTransformUtils.NormalizeRectWithTopLeft(text, 12f, size.y - 8f, tip.trans);
            labelRect.sizeDelta = new Vector2(size.x - 24f, 20f);
            float height = Mathf.Ceil(text.preferredHeight);
            labelRect.sizeDelta = new Vector2(size.x - 24f, height);
            size.y += height + 4f;
        }
        rect.sizeDelta = size;
    }

    private static string GetRecipeStatistics(WorldviewRecipe recipe) {
        var statistics = new StringBuilder("FE.AdvancedRecipe.AverageQuantities".Translate());
        int buildingId = GetBuildingId(recipe);
        bool hasAuxiliary = false;
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability >= 0.9999f) { continue; }
            if (!hasAuxiliary) {
                statistics.Append('\n').Append("FE.AdvancedRecipe.MinimumAuxiliary".Translate());
                hasAuxiliary = true;
            }
            statistics.Append('\n').AppendFormat("FE.AdvancedRecipe.AuxiliaryRequirement".Translate(),
                LDB.items.Select(input.ItemId).name, input.Count,
                AdvancedFractionatorRuntime.GetInputConsumeProbability(input, buildingId));
        }
        return statistics.ToString();
    }

    private static int GetBuildingId(WorldviewRecipe recipe) => recipe.RecipeType switch {
        ERecipe.AdvancedMineral => IFE高级矿物塔,
        ERecipe.AdvancedInteraction => IFE高级交互塔,
        ERecipe.AdvancedConversion => IFE高级转化塔,
        _ => throw new InvalidOperationException($"非高级配方进入高级提示：{recipe.PersistentId}"),
    };
}
