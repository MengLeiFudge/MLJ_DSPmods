using System.Collections.Generic;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.FracRecipes;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取执行、保底推进与奖励结算逻辑。
/// </summary>
public static partial class GachaService {
    public static List<GachaResult> Draw(int poolId, int resourceItemId, int count) {
        if (count <= 0) {
            return [];
        }

        EnsurePoolsFresh();

        var results = new List<GachaResult>(count);
        if (!GachaPool.IsDrawPool(poolId)) {
            return results;
        }

        GachaPool pool = GetPool(poolId);
        if (pool == null || !GachaPool.CanUseDrawResource(poolId, resourceItemId)) {
            return results;
        }

        int totalCost = GetDrawMatrixCost(poolId, count);
        if (!TakeItemWithTip(resourceItemId, totalCost, out _)) {
            return results;
        }

        for (int i = 0; i < count; i++) {
            bool hardPity = GachaManager.IsHardPity(poolId);
            GachaRarity rarity = RollRarity(pool, GachaManager.GetCurrentSRate(poolId, pool.RateS), hardPity);
            int itemId = hardPity ? GetHardPityItem(poolId, pool) : pool.PickRandom(rarity, rng);
            GachaFocusMatchType focusMatchType = GetFocusMatchType(poolId, itemId);
            GachaRewardResolution reward = ResolveReward(poolId, itemId);

            GachaManager.RecordDraw(poolId, rarity == GachaRarity.S);
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, 1);
            int displayItemId = reward.DisplayItemId > 0 ? reward.DisplayItemId : itemId;
            results.Add(new GachaResult(displayItemId, rarity, focusMatchType, reward.RewardType, reward.RewardItemId,
                reward.RewardCount, wasHardPity: hardPity));
        }

        return results;
    }

    private static int GetHardPityItem(int poolId, GachaPool pool) {
        if (pool.PoolS.Count > 0) {
            return pool.PoolS[rng.Next(pool.PoolS.Count)];
        }

        return poolId switch {
            GachaPool.PoolIdOpeningLine => IFE残片,
            GachaPool.PoolIdProtoLoop => GetFocusedEmbryoReward(),
            _ => IFE残片,
        };
    }

    private static GachaRewardResolution ResolveReward(int poolId, int itemId) {
        if (GachaPool.IsRecipePool(poolId)) {
            return ResolveRecipeReward(itemId);
        }
        if (GachaPool.IsProtoLoopPool(poolId)) {
            return ResolveProtoLoopReward(itemId);
        }

        AddItemToModData(itemId, 1, 0, false);
        return new GachaRewardResolution(GachaRewardType.ItemGranted, itemId, 1);
    }

    private static GachaRewardResolution ResolveProtoLoopReward(int itemId) {
        AddItemToModData(itemId, 1, 0, false);
        if (!TryGetProtoLoopDrawUnit(itemId, out GachaDrawUnit unit)) {
            return new GachaRewardResolution(GachaRewardType.ItemGranted, itemId, 1);
        }

        BaseRecipe recipe = SelectDrawUnitTargetRecipe(unit);
        if (recipe == null) {
            return new GachaRewardResolution(GachaRewardType.ItemGranted, itemId, 1);
        }

        if (IsDrawUnitFullyUnlocked(unit)
            && GachaManager.TryAddDrawUnitResonance(unit.Key, out int resonanceLevel)) {
            RecipeGrowthQueries.ClearProcessingCache();
            InitPools();
            return new GachaRewardResolution(GachaRewardType.DrawUnitResonance, 0, resonanceLevel, unit.DisplayItemId);
        }

        bool wasLocked = !RecipeGrowthQueries.IsUnlocked(recipe);
        RecipeGrowthResult growthResult =
            RecipeGrowthExecutor.ApplyDrawReward(recipe, RecipeGrowthManager.BuildContext(manual: true));
        GachaRewardType rewardType = wasLocked
            ? GachaRewardType.RecipeUnlock
            : growthResult.StateChanged ? GachaRewardType.RecipeUpgrade : GachaRewardType.RecipeProgress;
        return new GachaRewardResolution(rewardType, 0, RecipeGrowthQueries.GetLevel(recipe), unit.DisplayItemId);
    }

    private static GachaRewardResolution ResolveRecipeReward(int inputId) {
        if (inputId <= 0) {
            return new GachaRewardResolution(GachaRewardType.None, 0, 0);
        }

        EnsureRecipeRewardIndex();

        if (!recipeRewardIndex.TryGetValue(inputId, out GachaDrawUnit unit)) {
            AddItemToModData(inputId, 1, 0, false);
            return new GachaRewardResolution(GachaRewardType.ItemGranted, inputId, 1);
        }

        BaseRecipe recipe = SelectDrawUnitTargetRecipe(unit);
        if (recipe == null) {
            AddItemToModData(inputId, 1, 0, false);
            return new GachaRewardResolution(GachaRewardType.ItemGranted, inputId, 1);
        }

        bool wasLocked = !RecipeGrowthQueries.IsUnlocked(recipe);
        if (IsDrawUnitFullyUnlocked(unit) && GachaManager.TryAddDrawUnitResonance(unit.Key, out int resonanceLevel)) {
            RecipeGrowthQueries.ClearProcessingCache();
            InitPools();
            return new GachaRewardResolution(GachaRewardType.DrawUnitResonance, 0, resonanceLevel, unit.DisplayItemId);
        }

        RecipeGrowthResult growthResult =
            RecipeGrowthExecutor.ApplyDrawReward(recipe, RecipeGrowthManager.BuildContext(manual: true));

        if (growthResult.FragmentReward > 0) {
            int fragmentReward = growthResult.FragmentReward;
            AddItemToModData(IFE残片, fragmentReward, 0, true);
            return new GachaRewardResolution(GachaRewardType.DuplicateRecipeFragments, IFE残片, fragmentReward,
                unit.DisplayItemId);
        }

        GachaRewardType rewardType = wasLocked
            ? GachaRewardType.RecipeUnlock
            : growthResult.StateChanged ? GachaRewardType.RecipeUpgrade : GachaRewardType.RecipeProgress;
        return new GachaRewardResolution(rewardType, 0, RecipeGrowthQueries.GetLevel(recipe), unit.DisplayItemId);
    }

    private static void EnsureRecipeRewardIndex(bool force = false) {
        int recipeCount = RecipeManager.AllRecipes.Count;
        if (!force && recipeRewardIndexRecipeCount == recipeCount) {
            return;
        }
        if (isRebuildingRecipeRewardIndex) {
            return;
        }

        isRebuildingRecipeRewardIndex = true;
        try {
            recipeRewardIndex.Clear();
            foreach (GachaDrawUnit unit in GetRewardDrawUnits()) {
                if (!unit.Key.IsValid || unit.DisplayItemId <= 0 || recipeRewardIndex.ContainsKey(unit.DisplayItemId)) {
                    continue;
                }

                recipeRewardIndex.Add(unit.DisplayItemId, unit);
            }

            recipeRewardIndexRecipeCount = recipeCount;
        } finally {
            isRebuildingRecipeRewardIndex = false;
        }
    }

    private static BaseRecipe SelectDrawUnitTargetRecipe(GachaDrawUnit unit) {
        BaseRecipe fallback = null;
        BaseRecipe bestProgressTarget = null;
        foreach (RecipeKey key in unit.RecipeKeys) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(key.RecipeType, key.InputId);
            if (recipe == null) {
                continue;
            }

            fallback ??= recipe;
            if (!RecipeGrowthQueries.IsUnlocked(recipe)) {
                return recipe;
            }

            if (!RecipeGrowthQueries.IsMaxed(recipe)
                && (bestProgressTarget == null
                    || RecipeGrowthQueries.GetLevel(recipe) < RecipeGrowthQueries.GetLevel(bestProgressTarget)
                    || RecipeGrowthQueries.GetLevel(recipe) == RecipeGrowthQueries.GetLevel(bestProgressTarget)
                    && recipe.InputID < bestProgressTarget.InputID)) {
                bestProgressTarget = recipe;
            }
        }

        return bestProgressTarget ?? fallback;
    }

    private static GachaRarity RollRarity(GachaPool pool, float currentSRate, bool forceS) {
        if (forceS) {
            return GachaRarity.S;
        }

        double value = rng.NextDouble();
        if (value < currentSRate) {
            return GachaRarity.S;
        }

        value -= currentSRate;
        if (value < pool.RateA) {
            return GachaRarity.A;
        }

        value -= pool.RateA;
        if (value < pool.RateB) {
            return GachaRarity.B;
        }

        return GachaRarity.C;
    }
}
