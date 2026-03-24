using System;
using System.Collections.Generic;
using FE.Logic.Recipe;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class GachaService {
    public static string CurrentUpPoolNameKey = "UP池";
    public static bool LimitedPoolUnlocked = false;
    public static int UpGroupCount => UpItemGroups.Length;

    private static readonly int[][] UpItemGroups = [
        [IFE交互塔原胚, IFE矿物复制塔原胚, IFE点数聚集塔原胚],
        [IFE转化塔原胚, IFE精馏塔原胚, IFE分馏塔定向原胚],
        [IFE分馏塔增幅芯片, IFE分馏配方核心, IFE残片],
    ];

    private static readonly Random _rng = new();
    private static readonly List<GachaPool> _pools = [];

    public static void InitPools() {
        _pools.Clear();

        var permanentRecipe = new GachaPool(GachaPool.PoolIdPermanentRecipe, "常驻配方池", false);
        FillRecipePool(permanentRecipe);
        _pools.Add(permanentRecipe);

        var permanentBuilding = new GachaPool(GachaPool.PoolIdPermanentBuilding, "常驻建筑池", false);
        FillBuildingPool(permanentBuilding);
        _pools.Add(permanentBuilding);

        var up = new GachaPool(GachaPool.PoolIdUp, CurrentUpPoolNameKey, false);
        FillUpPool(up);
        _pools.Add(up);

        var limited = new GachaPool(GachaPool.PoolIdLimited, "限定池", true);
        FillLimitedPool(limited);
        _pools.Add(limited);
    }

    public static void RefreshUpPool() {
        var up = GetPool(GachaPool.PoolIdUp);
        if (up == null) return;
        up.PoolC.Clear(); up.PoolB.Clear(); up.PoolA.Clear(); up.PoolS.Clear(); up.UpItems.Clear();
        FillUpPool(up);
    }

    private static void FillUpPool(GachaPool pool) {
        pool.PoolC.Add(IFE残片);
        pool.PoolC.Add(IFE残片);
        pool.PoolC.Add(IFE交互塔原胚);
        pool.PoolC.Add(IFE矿物复制塔原胚);

        pool.PoolB.Add(IFE残片);
        pool.PoolB.Add(IFE点数聚集塔原胚);
        pool.PoolB.Add(IFE转化塔原胚);

        pool.PoolA.Add(IFE精馏塔原胚);
        pool.PoolA.Add(IFE分馏塔增幅芯片);

        pool.PoolS.Add(IFE分馏塔定向原胚);
        pool.PoolS.Add(IFE分馏配方核心);

        int groupIndex = GachaManager.CurrentUpGroupIndex;
        if (groupIndex < 0 || groupIndex >= UpItemGroups.Length) {
            groupIndex = 0;
            GachaManager.CurrentUpGroupIndex = 0;
        }
        foreach (int itemId in UpItemGroups[groupIndex]) {
            if (!pool.UpItems.Contains(itemId)) pool.UpItems.Add(itemId);
        }
        CurrentUpPoolNameKey = $"UP池-{groupIndex + 1}";
    }

    private static void FillRecipePool(GachaPool pool) {
        var lowRecipes = new List<int>();
        var midRecipes = new List<int>();
        var highRecipes = new List<int>();

        foreach (var r in RecipeManager.GetRecipesByMatrix(I电磁矩阵)) lowRecipes.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I能量矩阵)) lowRecipes.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I结构矩阵)) midRecipes.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I信息矩阵)) midRecipes.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I引力矩阵)) highRecipes.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I宇宙矩阵)) highRecipes.Add(r.InputID);

        AddWeighted(pool.PoolC, IFE残片, 140);
        AddWeighted(pool.PoolB, IFE残片, 80);
        AddWeighted(pool.PoolA, IFE残片, 40);

        for (int i = 0; i < lowRecipes.Count; i++) pool.PoolC.Add(lowRecipes[i]);
        for (int i = 0; i < lowRecipes.Count; i++) pool.PoolB.Add(lowRecipes[i]);
        for (int i = 0; i < midRecipes.Count; i++) pool.PoolB.Add(midRecipes[i]);
        for (int i = 0; i < midRecipes.Count; i++) pool.PoolA.Add(midRecipes[i]);
        for (int i = 0; i < highRecipes.Count; i++) pool.PoolA.Add(highRecipes[i]);
        for (int i = 0; i < highRecipes.Count; i++) pool.PoolS.Add(highRecipes[i]);
        AddWeighted(pool.PoolS, IFE原版配方核心, 30);

        if (pool.PoolC.Count == 0) pool.PoolC.Add(IFE残片);
        if (pool.PoolB.Count == 0) pool.PoolB.AddRange(pool.PoolC);
        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.Add(IFE原版配方核心);
    }

    private static void FillBuildingPool(GachaPool pool) {
        pool.PoolC.Add(IFE交互塔原胚);
        pool.PoolC.Add(IFE矿物复制塔原胚);
        pool.PoolB.Add(IFE点数聚集塔原胚);
        pool.PoolB.Add(IFE转化塔原胚);
        pool.PoolA.Add(IFE精馏塔原胚);
        pool.PoolS.Add(IFE分馏塔定向原胚);

        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.AddRange(pool.PoolA);
    }

    private static void FillLimitedPool(GachaPool pool) {
        AddWeighted(pool.PoolC, IFE残片, 50);
        pool.PoolB.Add(IFE残片);
        pool.PoolB.Add(IFE残片);
        pool.PoolB.Add(IFE分馏塔增幅芯片);
        pool.PoolA.Add(IFE分馏配方核心);
        pool.PoolA.Add(IFE分馏塔增幅芯片);
        pool.PoolS.Add(IFE分馏塔定向原胚);

        if (pool.PoolC.Count == 0) pool.PoolC.Add(IFE残片);
        if (pool.PoolB.Count == 0) pool.PoolB.AddRange(pool.PoolC);
        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.Add(IFE分馏塔定向原胚);
    }

    public static List<GachaResult> Draw(int poolId, int ticketId, int count) {
        var results = new List<GachaResult>(count);
        if (!GachaPool.IsValidPoolId(poolId)) return results;
        GachaPool pool = GetPool(poolId);
        if (pool == null) return results;
        if (GachaPool.IsLimitedPool(poolId) && !LimitedPoolUnlocked) return results;
        if (pool.RequiresPremiumTicket && ticketId != IFE精选抽卡券) return results;
        if (!TakeItemWithTip(ticketId, count, out _)) return results;

        for (int i = 0; i < count; i++) {
            bool hardPity = GachaManager.IsHardPity(poolId);
            if (hardPity) {
                int pityItemId = GetHardPityItem(poolId, pool);
                bool pityIsUp = GachaPool.IsUpPool(poolId) && pool.UpItems.Contains(pityItemId);
                bool pityIsRecipe = RewardItem(poolId, pityItemId);
                GachaManager.RecordDraw(poolId, true);
                results.Add(new GachaResult(pityItemId, GachaRarity.S, pityIsUp, pityIsRecipe));
                continue;
            }

            float softBonus = GachaManager.GetSoftPityBonus(poolId);
            GachaRarity rarity = RollRarity(pool, softBonus, hardPity);
            bool isUp = false;

            if (GachaPool.IsUpPool(poolId) && rarity == GachaRarity.S) {
                isUp = GachaManager.ShouldGuaranteeUpOnSRoll(_rng.NextDouble());
                GachaManager.RecordUpSResult(isUp);
            }

            int itemId;
            bool isRecipe;
            if (isUp && pool.UpItems.Count > 0) {
                itemId = pool.UpItems[_rng.Next(pool.UpItems.Count)];
                isRecipe = RewardItem(poolId, itemId);
            } else {
                itemId = pool.PickRandom(rarity, _rng);
                isRecipe = RewardItem(poolId, itemId);
            }

            GachaManager.RecordDraw(poolId, rarity == GachaRarity.S);
            results.Add(new GachaResult(itemId, rarity, isUp, isRecipe));
        }

        return results;
    }

    private static int GetHardPityItem(int poolId, GachaPool pool) {
        return poolId switch {
            GachaPool.PoolIdPermanentRecipe => IFE原版配方核心,
            GachaPool.PoolIdPermanentBuilding => IFE分馏塔增幅芯片,
            GachaPool.PoolIdUp => pool.UpItems.Count > 0 ? pool.UpItems[_rng.Next(pool.UpItems.Count)] : IFE分馏塔定向原胚,
            GachaPool.PoolIdLimited => IFE分馏塔定向原胚,
            _ => IFE原版配方核心,
        };
    }

    private static bool RewardItem(int poolId, int itemId) {
        if (GachaPool.IsRecipePool(poolId)) {
            return TryReward(itemId);
        }
        AddItemToModData(itemId, 1, 0, false);
        return false;
    }

    private static void AddWeighted(List<int> pool, int itemId, int weight) {
        for (int i = 0; i < weight; i++) {
            pool.Add(itemId);
        }
    }

    private static bool TryReward(int inputId) {
        if (inputId <= 0) return false;
        foreach (ERecipe recipeType in System.Enum.GetValues(typeof(ERecipe))) {
            var recipe = RecipeManager.GetRecipe<BaseRecipe>(recipeType, inputId);
            if (recipe != null) {
                recipe.RewardThis(true);
                return true;
            }
        }
        AddItemToModData(inputId, 1, 0, false);
        return false;
    }

    private static GachaRarity RollRarity(GachaPool pool, float softPityBonus, bool forceS) {
        if (forceS) {
            return GachaRarity.S;
        }
        double rand = _rng.NextDouble();
        float boostedS = pool.RateS + softPityBonus;
        if (rand < boostedS) return GachaRarity.S;
        rand -= boostedS;
        if (rand < pool.RateA) return GachaRarity.A;
        rand -= pool.RateA;
        if (rand < pool.RateB) return GachaRarity.B;
        return GachaRarity.C;
    }

    public static GachaPool GetPool(int poolId) {
        foreach (var p in _pools)
            if (p.PoolId == poolId) return p;
        return null;
    }

    public static List<GachaPool> GetAllPools() => _pools;
}
