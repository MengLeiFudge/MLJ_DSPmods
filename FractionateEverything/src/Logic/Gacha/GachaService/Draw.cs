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
            results.Add(new GachaResult(itemId, rarity, focusMatchType, reward.RewardType, reward.RewardItemId,
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
            GachaPool.PoolIdProtoLoop => IFE分馏塔定向原胚,
            _ => IFE残片,
        };
    }

    private static GachaRewardResolution ResolveReward(int poolId, int itemId) {
        if (GachaPool.IsRecipePool(poolId)) {
            return ResolveRecipeReward(itemId);
        }

        AddItemToModData(itemId, 1, 0, false);
        return new GachaRewardResolution(GachaRewardType.ItemGranted, itemId, 1);
    }

    private static GachaRewardResolution ResolveRecipeReward(int inputId) {
        if (inputId <= 0) {
            return new GachaRewardResolution(GachaRewardType.None, 0, 0);
        }

        EnsureRecipeRewardIndex();

        if (!recipeRewardIndex.TryGetValue(inputId, out BaseRecipe recipe)) {
            AddItemToModData(inputId, 1, 0, false);
            return new GachaRewardResolution(GachaRewardType.ItemGranted, inputId, 1);
        }

        bool wasLocked = !RecipeGrowthQueries.IsUnlocked(recipe);
        RecipeGrowthResult growthResult =
            RecipeGrowthExecutor.ApplyDrawReward(recipe, RecipeGrowthManager.BuildContext(manual: true));

        if (growthResult.FragmentReward > 0) {
            int fragmentReward = growthResult.FragmentReward;
            AddItemToModData(IFE残片, fragmentReward, 0, true);
            return new GachaRewardResolution(GachaRewardType.DuplicateRecipeFragments, IFE残片, fragmentReward);
        }

        return new GachaRewardResolution(wasLocked ? GachaRewardType.RecipeUnlock : GachaRewardType.RecipeUpgrade, 0,
            RecipeGrowthQueries.GetLevel(recipe));
    }

    private static void EnsureRecipeRewardIndex() {
        int recipeCount = RecipeManager.AllRecipes.Count;
        if (recipeRewardIndexRecipeCount == recipeCount) {
            return;
        }

        recipeRewardIndex.Clear();
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            if (!IsOpeningLineRecipe(recipe) || recipeRewardIndex.ContainsKey(recipe.InputID)) {
                continue;
            }

            recipeRewardIndex.Add(recipe.InputID, recipe);
        }

        recipeRewardIndexRecipeCount = recipeCount;
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
