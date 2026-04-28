using System.Collections.Generic;
using System.Linq;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using static FE.Utils.Utils;

namespace FE.Logic.RecipeGrowth;

public static class RecipeGrowthQueries {
    public static int GetLevel(BaseRecipe recipe) {
        return RecipeGrowthManager.Store.GetOrCreate(recipe).Level;
    }

    public static int GetLevel(RecipeKey key) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
        return recipe == null ? 0 : GetLevel(recipe);
    }

    public static bool IsUnlocked(BaseRecipe recipe) {
        return GetLevel(recipe) > 0;
    }

    public static bool IsUnlocked(RecipeKey key) => GetLevel(key) > 0;

    public static bool IsMaxed(BaseRecipe recipe) {
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        return GetLevel(recipe) >= rule.MaxLevel;
    }

    public static bool IsMaxed(RecipeKey key) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
        return recipe != null && IsMaxed(recipe);
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
        RecipeGrowthState state = RecipeGrowthManager.Store.GetOrCreate(recipe);
        RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
        float destroyRatio = 0.04f;
        destroyRatio -= GachaGalleryBonusManager.GetDestroyReduction(recipe.RecipeType);
        if (destroyRatio < 0f) {
            destroyRatio = 0f;
        }

        return new RecipeDisplaySnapshot(
            recipe.RecipeType,
            recipe.InputID,
            family,
            level,
            GetMaxLevel(recipe),
            IsUnlocked(recipe),
            IsMaxed(recipe),
            legacyLevel,
            state.GrowthExp,
            state.PityProgress,
            BuildLevelDescriptions(recipe),
            legacyLevel * 0.08f,
            legacyLevel * 0.05f + GachaGalleryBonusManager.GetDoubleBonus(recipe.RecipeType),
            destroyRatio
        );
    }

    public static RecipeDisplaySnapshot GetSnapshot(RecipeKey key) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
        return recipe == null ? default : GetSnapshot(recipe);
    }

    public static List<RecipeDisplaySnapshot> GetSnapshotsByFamily(RecipeFamily family) {
        return RecipeManager.AllRecipes
            .Where(recipe => RecipeGrowthRules.GetFamily(recipe) == family)
            .Select(GetSnapshot)
            .ToList();
    }

    public static List<RecipeStatsSnapshot> GetFamilyStatsSnapshots() {
        Dictionary<RecipeFamily, List<BaseRecipe>> groups = RecipeManager.AllRecipes
            .GroupBy(RecipeGrowthRules.GetFamily)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<RecipeStatsSnapshot> snapshots = [];
        foreach (KeyValuePair<RecipeFamily, List<BaseRecipe>> pair in groups) {
            RecipeFamily family = pair.Key;
            List<BaseRecipe> recipes = pair.Value;
            if (family == RecipeFamily.Unknown) {
                continue;
            }
            int total = recipes.Count;
            int unlocked = 0;
            int maxed = 0;
            int totalLevel = 0;
            int totalGrowthExp = 0;
            int totalPity = 0;
            foreach (BaseRecipe recipe in recipes) {
                RecipeDisplaySnapshot snapshot = GetSnapshot(recipe);
                if (snapshot.IsUnlocked) {
                    unlocked++;
                }
                if (snapshot.IsMaxed) {
                    maxed++;
                }
                totalLevel += snapshot.Level;
                totalGrowthExp += snapshot.GrowthExp;
                totalPity += snapshot.PityProgress;
            }
            BaseRecipe first = recipes[0];
            snapshots.Add(new RecipeStatsSnapshot(family, first.RecipeType, first.MatrixID, total, unlocked, maxed,
                totalLevel, totalGrowthExp, totalPity));
        }
        return snapshots;
    }

    public static List<DarkFogRecipeProgressSnapshot> GetDarkFogProgressSnapshots() {
        List<DarkFogRecipeProgressSnapshot> snapshots = [];
        RecipeGrowthContext context = RecipeGrowthManager.BuildContext();
        int stageIndex = RecipeGrowthCatchup.GetDarkFogStageIndex(context.DarkFogStage);
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
            if (family is not RecipeFamily.MineralCopyDarkFog and not RecipeFamily.ConversionMaterialDarkFog) {
                continue;
            }
            RecipeDisplaySnapshot snapshot = GetSnapshot(recipe);
            int tier = RecipeGrowthCatchup.GetDarkFogTier(recipe.InputID);
            snapshots.Add(new DarkFogRecipeProgressSnapshot(
                recipe.RecipeType,
                recipe.InputID,
                snapshot.Level,
                snapshot.MaxLevel,
                snapshot.GrowthExp,
                snapshot.PityProgress,
                tier,
                RecipeGrowthCatchup.GetDarkFogProcessMultiplier(stageIndex, tier),
                RecipeGrowthCatchup.GetDarkFogCatchupMultiplier(stageIndex, tier),
                snapshot.IsUnlocked,
                snapshot.IsMaxed
            ));
        }
        return snapshots;
    }

    private static string[] BuildLevelDescriptions(BaseRecipe recipe) {
        int maxLevel = GetMaxLevel(recipe);
        string[] descriptions = new string[maxLevel + 1];
        descriptions[0] = $"Lv0  {"未解锁".Translate()}";
        for (int level = 1; level <= maxLevel; level++) {
            int effectiveLegacyLevel = RecipeGrowthRules.GetEffectiveLegacyLevel(recipe, level);
            int remainPct = UnityEngine.Mathf.RoundToInt(effectiveLegacyLevel * 8f);
            int doublePct = UnityEngine.Mathf.RoundToInt(effectiveLegacyLevel * 5f);
            string maxSuffix = level >= maxLevel ? "  MAX".WithColor(Gold) : string.Empty;
            descriptions[level] =
                $"Lv{level}  {"不消耗原料".Translate()}{remainPct}%  {"翻倍产出".Translate()}{doublePct}%{maxSuffix}";
        }
        return descriptions;
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
