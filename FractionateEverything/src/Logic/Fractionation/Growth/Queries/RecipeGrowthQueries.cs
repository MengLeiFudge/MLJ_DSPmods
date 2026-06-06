using System.Collections.Generic;
using System.Linq;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Gacha;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 配方成长状态的只读查询和显示快照生成逻辑。
/// </summary>
public static class RecipeGrowthQueries {
    /// <summary>
    /// 配方加工倍率缓存。
    /// </summary>
    private readonly struct ProcessingRatioCache(
        int level,
        bool isUnlocked,
        bool canApplyProcessingProgress,
        float remainInputRatio,
        float doubleOutputRatio) {
        /// <summary>
        /// 读取或设置该分馏塔建筑的成长等级。
        /// </summary>
        public readonly int Level = level;
        /// <summary>
        /// 判断配方是否已解锁。
        /// </summary>
        public readonly bool IsUnlocked = isUnlocked;
        /// <summary>
        /// 判断配方是否能从分馏加工中获得成长进度。
        /// </summary>
        public readonly bool CanApplyProcessingProgress = canApplyProcessingProgress;
        /// <summary>
        /// 获取成长系统提供的输入保留概率。
        /// </summary>
        public readonly float RemainInputRatio = remainInputRatio;
        /// <summary>
        /// 获取成长系统提供的双倍输出概率。
        /// </summary>
        public readonly float DoubleOutputRatio = doubleOutputRatio;
    }

    private static readonly Dictionary<BaseRecipe, ProcessingRatioCache> processingRatioCache = [];

    /// <summary>
    /// 读取配方当前等级。
    /// </summary>
    public static int GetLevel(BaseRecipe recipe) {
        if (processingRatioCache.TryGetValue(recipe, out ProcessingRatioCache cache)) {
            return cache.Level;
        }
        return RecipeGrowthManager.Store.GetOrCreate(recipe).Level;
    }

    /// <summary>
    /// 读取配方当前等级。
    /// </summary>
    public static int GetLevel(RecipeKey key) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
        return recipe == null ? 0 : GetLevel(recipe);
    }

    /// <summary>
    /// 判断配方是否已解锁。
    /// </summary>
    public static bool IsUnlocked(BaseRecipe recipe) {
        return GetProcessingCache(recipe).IsUnlocked;
    }

    /// <summary>
    /// 读取配方当前等级。
    /// </summary>
    public static bool IsUnlocked(RecipeKey key) => GetLevel(key) > 0;

    /// <summary>
    /// 判断配方是否已达到最高等级。
    /// </summary>
    public static bool IsMaxed(BaseRecipe recipe) {
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        return GetLevel(recipe) >= rule.MaxLevel;
    }

    /// <summary>
    /// 判断配方是否已达到最高等级。
    /// </summary>
    public static bool IsMaxed(RecipeKey key) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
        return recipe != null && IsMaxed(recipe);
    }

    /// <summary>
    /// 读取配方最高等级。
    /// </summary>
    public static int GetMaxLevel(BaseRecipe recipe) {
        return RecipeGrowthRules.GetRule(recipe).MaxLevel;
    }

    /// <summary>
    /// 读取面向旧版显示和兼容逻辑的有效等级。
    /// </summary>
    public static int GetEffectiveLegacyLevel(BaseRecipe recipe) {
        return RecipeGrowthRules.GetEffectiveLegacyLevel(recipe, GetLevel(recipe));
    }

    /// <summary>
    /// 读取配方成长带来的输入保留概率。
    /// </summary>
    public static float GetRemainInputRatio(BaseRecipe recipe) {
        return GetProcessingCache(recipe).RemainInputRatio;
    }

    /// <summary>
    /// 读取配方成长带来的双倍输出概率。
    /// </summary>
    public static float GetDoubleOutputRatio(BaseRecipe recipe) {
        return GetProcessingCache(recipe).DoubleOutputRatio;
    }

    /// <summary>
    /// 判断配方是否能从分馏加工中获得成长进度。
    /// </summary>
    public static bool CanApplyProcessingProgress(BaseRecipe recipe) {
        return GetProcessingCache(recipe).CanApplyProcessingProgress;
    }

    /// <summary>
    /// 一次性读取配方加工保留输入和双倍输出概率。
    /// </summary>
    public static void GetProcessingRatios(BaseRecipe recipe, out float remainInputRatio, out float doubleOutputRatio) {
        ProcessingRatioCache cache = GetProcessingCache(recipe);
        remainInputRatio = cache.RemainInputRatio;
        doubleOutputRatio = cache.DoubleOutputRatio;
    }

    private static ProcessingRatioCache GetProcessingCache(BaseRecipe recipe) {
        if (processingRatioCache.TryGetValue(recipe, out ProcessingRatioCache cache)) {
            return cache;
        }
        // 生产热路径只需要两个概率，缓存后避免每次分馏都重复查规则族和存档状态。
        int level = RecipeGrowthManager.Store.GetOrCreate(recipe).Level;
        RecipeGrowthRule rule = RecipeGrowthRules.GetRule(recipe);
        int legacyLevel = RecipeGrowthRules.GetEffectiveLegacyLevel(recipe, level);
        cache = new ProcessingRatioCache(
            level,
            level > 0,
            level > 0 && level < rule.MaxLevel && (rule.UsesGrowthExp || rule.UsesPity),
            legacyLevel * 0.08f,
            legacyLevel * 0.05f + GachaGalleryBonusManager.GetDoubleBonus(recipe.RecipeType));
        processingRatioCache[recipe] = cache;
        return cache;
    }

    /// <summary>
    /// 清除指定配方的加工概率缓存。
    /// </summary>
    public static void InvalidateProcessingCache(BaseRecipe recipe) {
        if (recipe != null) {
            processingRatioCache.Remove(recipe);
        }
    }

    /// <summary>
    /// 清除全部配方加工概率缓存。
    /// </summary>
    public static void ClearProcessingCache() {
        processingRatioCache.Clear();
    }

    /// <summary>
    /// 构建配方展示快照。
    /// </summary>
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

    /// <summary>
    /// 构建配方展示快照。
    /// </summary>
    public static RecipeDisplaySnapshot GetSnapshot(RecipeKey key) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
        return recipe == null ? default : GetSnapshot(recipe);
    }

    /// <summary>
    /// 按配方家族构建展示快照列表。
    /// </summary>
    public static List<RecipeDisplaySnapshot> GetSnapshotsByFamily(RecipeFamily family) {
        return RecipeManager.AllRecipes
            .Where(recipe => RecipeGrowthRules.GetFamily(recipe) == family)
            .Select(GetSnapshot)
            .ToList();
    }

    /// <summary>
    /// 构建配方家族统计快照列表。
    /// </summary>
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

    /// <summary>
    /// 构建黑雾配方进度快照列表。
    /// </summary>
    public static List<DarkFogRecipeProgressSnapshot> GetDarkFogProgressSnapshots() {
        List<DarkFogRecipeProgressSnapshot> snapshots = [];
        RecipeGrowthContext context = RecipeGrowthManager.BuildContext();
        int stageIndex = RecipeGrowthCatchup.GetDarkFogStageIndex(context.DarkFogStage);
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
            if (family is not RecipeFamily.MineralCopyDarkFog and not RecipeFamily.ConversionDarkFogChain) {
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
            int remainPct = Mathf.RoundToInt(effectiveLegacyLevel * 8f);
            int doublePct = Mathf.RoundToInt(effectiveLegacyLevel * 5f);
            string maxSuffix = level >= maxLevel ? "  MAX".WithColor(Gold) : string.Empty;
            descriptions[level] =
                $"Lv{level}  {"不消耗原料".Translate()}{remainPct}%  {"翻倍产出".Translate()}{doublePct}%{maxSuffix}";
        }
        return descriptions;
    }

    /// <summary>
    /// 统计指定配方类型中已解锁配方数量。
    /// </summary>
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

    /// <summary>
    /// 统计指定配方类型中已满级配方数量。
    /// </summary>
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

    /// <summary>
    /// 统计图鉴按矩阵阶段和配方类型分组的解锁数量。
    /// </summary>
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
