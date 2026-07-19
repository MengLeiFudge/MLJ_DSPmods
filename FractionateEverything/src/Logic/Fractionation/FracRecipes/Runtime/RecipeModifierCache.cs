using System.Collections.Generic;

namespace FE.Logic.Fractionation.FracRecipes.Runtime;

/// <summary>
/// 保存文明成就投影到分馏热路径的固定配方加成。
/// </summary>
public static class RecipeModifierCache {
    private static readonly Dictionary<ERecipe, float> successRateBonusByType = [];
    private static float allRecipeSuccessRateBonus;

    public static void Reset() {
        successRateBonusByType.Clear();
        allRecipeSuccessRateBonus = 0f;
    }

    public static void AddSuccessRateBonus(ERecipe recipeType, float bonus) {
        if (bonus <= 0f) {
            return;
        }
        successRateBonusByType.TryGetValue(recipeType, out float current);
        successRateBonusByType[recipeType] = current + bonus;
    }

    public static void AddAllRecipeSuccessRateBonus(float bonus) {
        if (bonus > 0f) {
            allRecipeSuccessRateBonus += bonus;
        }
    }

    public static float GetSuccessRateBonus(BaseRecipe recipe) {
        if (recipe == null) {
            return allRecipeSuccessRateBonus;
        }
        successRateBonusByType.TryGetValue(recipe.RecipeType, out float typeBonus);
        return allRecipeSuccessRateBonus + typeBonus;
    }
}
