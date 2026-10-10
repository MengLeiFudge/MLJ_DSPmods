using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>按当前解锁状态展示五塔的制造来源，不修改共享物品原型或生产资格。</summary>
public static class TowerRecipeTooltipPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIItemTip), nameof(UIItemTip.SetTip))]
    private static IEnumerable<CodeInstruction> UIItemTip_SetTip_Transpiler(IEnumerable<CodeInstruction> instructions) {
        Dictionary<FieldInfo, MethodInfo> getters = new() {
            [AccessTools.Field(typeof(ItemProto), nameof(ItemProto.recipes))] =
                AccessTools.Method(typeof(TowerRecipeTooltipPatch), nameof(GetVisibleRecipes)),
            [AccessTools.Field(typeof(ItemProto), nameof(ItemProto.maincraft))] =
                AccessTools.Method(typeof(TowerRecipeTooltipPatch), nameof(GetVisibleMaincraft)),
            [AccessTools.Field(typeof(ItemProto), nameof(ItemProto.handcraft))] =
                AccessTools.Method(typeof(TowerRecipeTooltipPatch), nameof(GetVisibleHandcraft)),
        };
        HashSet<FieldInfo> matched = [];
        foreach (CodeInstruction instruction in instructions) {
            if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field
                && getters.TryGetValue(field, out MethodInfo getter)) {
                // 原指令的标签和异常块保持原位；读取共享原型改为只读的显示投影。
                instruction.opcode = OpCodes.Call;
                instruction.operand = getter;
                matched.Add(field);
            }
            yield return instruction;
        }
        if (matched.Count != getters.Count) {
            throw new InvalidOperationException("FE五塔物品提示未找到完整的配方/制造来源读取，无法应用解锁显示规则。");
        }
    }

    /// <summary>基础塔按实际制造资格过滤；高级塔通过稳定目录找回已解锁的培育来源。</summary>
    private static List<RecipeProto> GetVisibleRecipes(ItemProto item) {
        if (!FractionatorTowerCatalog.IsActiveFractionator(item.ID)) { return item.recipes; }
        GameHistoryData history = GameMain.history;
        List<RecipeProto> recipes = [];
        if (history == null) { return recipes; }
        if (FractionatorTowerCatalog.IsBasicTower(item.ID)) {
            if (item.recipes != null) {
                foreach (RecipeProto recipe in item.recipes) {
                    if (history.RecipeUnlocked(recipe.ID)) { recipes.Add(recipe); }
                }
            }
            return recipes;
        }
        foreach (WorldviewRecipe recipe in RecipeManager.GetRecipesProducing(item.ID)) {
            if (recipe.Kind != WorldviewRecipeKind.TowerCultivation
                || !ResearchManager.IsContentUnlocked(recipe.StableId)) { continue; }
            RecipeProto proto = LDB.recipes.Select(AdvancedRecipeCatalog.GetProtoId(recipe.PersistentId));
            if (proto != null) { recipes.Add(proto); }
        }
        return recipes;
    }

    /// <summary>制造来源与当前可见配方一致，高级塔仅指向高级交互塔的培育配方。</summary>
    private static RecipeProto GetVisibleMaincraft(ItemProto item) {
        if (FractionatorTowerCatalog.IsAdvancedTower(item.ID)) {
            List<RecipeProto> recipes = GetVisibleRecipes(item);
            return recipes.Count > 0 ? recipes[0] : null;
        }
        return FractionatorTowerCatalog.IsBasicTower(item.ID) && !IsUnlocked(item.maincraft)
            ? null : item.maincraft;
    }

    /// <summary>锁定的基础塔不显示手工制造标签，高级塔始终沿用禁止手工制造的规则。</summary>
    private static RecipeProto GetVisibleHandcraft(ItemProto item) {
        if (FractionatorTowerCatalog.IsAdvancedTower(item.ID)) { return null; }
        return FractionatorTowerCatalog.IsBasicTower(item.ID) && !IsUnlocked(item.handcraft)
            ? null : item.handcraft;
    }

    /// <summary>读取当前存档的实际制造资格，不缓存跨存档的解锁状态。</summary>
    private static bool IsUnlocked(RecipeProto recipe) =>
        recipe != null && GameMain.history?.RecipeUnlocked(recipe.ID) == true;
}
