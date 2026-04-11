using System.Collections.Generic;
using FE.Logic.Manager;
using FE.Logic.Recipe;

namespace FE.Logic.RecipeGrowth;

public static class RecipeGrowthQueries {
    public static int GetLevel(BaseRecipe recipe) {
        return RecipeGrowthManager.Store.GetOrCreate(recipe).Level;
    }

    public static bool IsUnlocked(BaseRecipe recipe) {
        return GetLevel(recipe) > 0;
    }

    public static bool IsMaxed(BaseRecipe recipe) {
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        return GetLevel(recipe) >= rule.MaxLevel;
    }

    public static int GetMaxLevel(BaseRecipe recipe) {
        return RecipeGrowthRules.GetRule(recipe).MaxLevel;
    }

    public static int GetEffectiveLegacyLevel(BaseRecipe recipe) {
        return RecipeGrowthRules.GetEffectiveLegacyLevel(recipe, GetLevel(recipe));
    }

    public static RecipeDisplaySnapshot GetSnapshot(BaseRecipe recipe) {
        int level = GetLevel(recipe);
        int legacyLevel = GetEffectiveLegacyLevel(recipe);
        float destroyRatio = 0.04f;
        destroyRatio -= GachaGalleryBonusManager.GetDestroyReduction(recipe.RecipeType);
        if (destroyRatio < 0f) {
            destroyRatio = 0f;
        }

        return new RecipeDisplaySnapshot(
            level,
            GetMaxLevel(recipe),
            IsUnlocked(recipe),
            IsMaxed(recipe),
            legacyLevel,
            legacyLevel * 0.08f,
            legacyLevel * 0.05f + GachaGalleryBonusManager.GetDoubleBonus(recipe.RecipeType),
            destroyRatio
        );
    }

    public static int GetUnlockedCount(params ERecipe[] types) {
        int count = 0;
        foreach (ERecipe type in types) {
            foreach (BaseRecipe recipe in RecipeManager.GetRecipesByType(type)) {
                if (IsUnlocked(recipe)) {
                    count++;
                }
            }
        }
        return count;
    }

    public static int GetMaxedCount(params ERecipe[] types) {
        int count = 0;
        foreach (ERecipe type in types) {
            foreach (BaseRecipe recipe in RecipeManager.GetRecipesByType(type)) {
                if (IsMaxed(recipe)) {
                    count++;
                }
            }
        }
        return count;
    }

    public static Dictionary<(int matrixId, ERecipe recipeType), (int unlocked, int maxed, int total)> GetGalleryCounts(
        IReadOnlyList<int> matrixIds,
        IReadOnlyList<ERecipe> recipeTypes
    ) {
        Dictionary<(int matrixId, ERecipe recipeType), (int unlocked, int maxed, int total)> result = [];
        foreach (int matrixId in matrixIds) {
            foreach (ERecipe recipeType in recipeTypes) {
                int unlocked = 0;
                int maxed = 0;
                int total = 0;
                foreach (BaseRecipe recipe in RecipeManager.GetRecipesByType(recipeType)) {
                    if (recipe.MatrixID != matrixId) {
                        continue;
                    }
                    total++;
                    if (IsUnlocked(recipe)) {
                        unlocked++;
                    }
                    if (IsMaxed(recipe)) {
                        maxed++;
                    }
                }
                result[(matrixId, recipeType)] = (unlocked, maxed, total);
            }
        }
        return result;
    }
}
