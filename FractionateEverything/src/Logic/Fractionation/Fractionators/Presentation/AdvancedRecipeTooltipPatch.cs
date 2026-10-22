using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

using HarmonyLib;
using UnityEngine.UI;

using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;

using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>用运行配方显示成功轮均值和催化需求，不改变原型、结算或普通配方提示。</summary>
public static class AdvancedRecipeTooltipPatch {
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
    private static void UIItemTip_SetTip_Prefix(int itemId, ref bool isRecipe) {
        // 配方以负ID进入原版提示；也覆盖从普通UIButton打开、未显式传isRecipe的入口。
        if (itemId < 0 && AdvancedRecipeCatalog.GetRecipe(-itemId) != null) { isRecipe = true; }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIItemTip), nameof(UIItemTip.SetTip))]
    private static IEnumerable<CodeInstruction> UIItemTip_SetTip_Transpiler(IEnumerable<CodeInstruction> instructions) {
        MethodInfo unlocked = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.TechUnlocked), [typeof(int)]);
        MethodInfo setText = AccessTools.PropertySetter(typeof(Text), nameof(Text.text));
        MethodInfo showStats = AccessTools.Method(typeof(AdvancedRecipeTooltipPatch), nameof(ShouldShowStatistics));
        MethodInfo writeStats = AccessTools.Method(typeof(AdvancedRecipeTooltipPatch), nameof(SetStatistics));
        bool recipeBonus = false;
        int gates = 0;
        int texts = 0;
        foreach (CodeInstruction instruction in instructions) {
            if (instruction.opcode == OpCodes.Ldstr && Equals(instruction.operand, "增产公式描述1")) {
                recipeBonus = true;
            }
            MethodInfo replacement = null;
            if (instruction.Calls(unlocked)) {
                replacement = showStats;
                gates++;
            } else if (recipeBonus && instruction.Calls(setText)) {
                replacement = writeStats;
                recipeBonus = false;
                texts++;
            }
            if (replacement == null) {
                yield return instruction;
                continue;
            }
            // 在原版计算preferredHeight之前换入数值区，保留换行、窗口高度及屏幕边界修正。
            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)10);
            yield return new CodeInstruction(OpCodes.Call, replacement);
        }
        if (gates != 1 || texts != 1) {
            throw new InvalidOperationException("FE高级配方提示未找到唯一的增产说明入口，无法应用配方数值显示。");
        }
    }

    private static bool ShouldShowStatistics(GameHistoryData history, int techId, int itemId, bool isRecipe) =>
        isRecipe && itemId < 0 && AdvancedRecipeCatalog.GetRecipe(-itemId) != null || history.TechUnlocked(techId);

    /// <summary>普通提示沿用原文；高级配方复用原版数值区及文字样式。</summary>
    private static void SetStatistics(Text text, string original, int itemId, bool isRecipe) {
        WorldviewRecipe recipe = isRecipe && itemId < 0 ? AdvancedRecipeCatalog.GetRecipe(-itemId) : null;
        if (recipe == null) {
            text.text = original;
            return;
        }
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
        text.text = statistics.ToString();
    }

    private static int GetBuildingId(WorldviewRecipe recipe) => recipe.RecipeType switch {
        ERecipe.AdvancedMineral => IFE高级矿物塔,
        ERecipe.AdvancedInteraction => IFE高级交互塔,
        ERecipe.AdvancedConversion => IFE高级转化塔,
        _ => throw new InvalidOperationException($"非高级配方进入高级提示：{recipe.PersistentId}"),
    };
}
