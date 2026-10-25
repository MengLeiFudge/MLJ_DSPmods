using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;
using UnityEngine.UI;

using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;

using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>用运行配方显示成功轮数值与原版加速说明，保持普通配方提示。</summary>
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
        List<CodeInstruction> code = instructions.ToList();
        MethodInfo unlocked = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.TechUnlocked), [typeof(int)]);
        MethodInfo setText = AccessTools.PropertySetter(typeof(Text), nameof(Text.text));
        MethodInfo showBonus = AccessTools.Method(typeof(AdvancedRecipeTooltipPatch), nameof(ShouldShowRecipeBonus));
        MethodInfo writeBonus = AccessTools.Method(typeof(AdvancedRecipeTooltipPatch), nameof(SetRecipeBonus));
        bool recipeBonus = false;
        int gates = 0;
        int texts = 0;
        for (int i = 0; i < code.Count; i++) {
            CodeInstruction instruction = code[i];
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

    private static int GetBuildingId(WorldviewRecipe recipe) => recipe.RecipeType switch {
        ERecipe.AdvancedMineral => IFE高级矿物塔,
        ERecipe.AdvancedInteraction => IFE高级交互塔,
        ERecipe.AdvancedConversion => IFE高级转化塔,
        _ => throw new InvalidOperationException($"非高级配方进入高级提示：{recipe.PersistentId}"),
    };
}
