using System;
using System.Collections.Generic;
using FE.Logic.Recipe;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class GachaService {
    public static List<int> CurrentUpItems = [];
    public static string CurrentUpPoolNameKey = "UP池";
    public static bool LimitedPoolUnlocked = false;

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
        FillRecipePool(up);
        up.UpItems.AddRange(CurrentUpItems);
        _pools.Add(up);

        var limited = new GachaPool(GachaPool.PoolIdLimited, "限定池", true);
        FillLimitedPool(limited);
        _pools.Add(limited);
    }

    private static void FillRecipePool(GachaPool pool) {
        foreach (var r in RecipeManager.GetRecipesByMatrix(I电磁矩阵)) pool.PoolC.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I能量矩阵)) pool.PoolC.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I结构矩阵)) pool.PoolB.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I信息矩阵)) pool.PoolA.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I引力矩阵)) pool.PoolA.Add(r.InputID);
        foreach (var r in RecipeManager.GetRecipesByMatrix(I宇宙矩阵)) pool.PoolS.Add(r.InputID);
        pool.PoolS.Add(IFE原版配方核心);

        if (pool.PoolC.Count == 0) pool.PoolC.Add(IFE原版配方核心);
        if (pool.PoolB.Count == 0) pool.PoolB.AddRange(pool.PoolC);
        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.AddRange(pool.PoolA);
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
        for (int id = IFE速度精华; id <= IFE增产精华; id++) pool.PoolS.Add(id);
        if (pool.PoolS.Count == 0) pool.PoolS.Add(IFE原版配方核心);
        pool.PoolA.AddRange(pool.PoolS);
        pool.PoolB.AddRange(pool.PoolS);
        pool.PoolC.AddRange(pool.PoolS);
    }

    public static List<GachaResult> Draw(int poolId, int ticketId, int count) {
        var results = new List<GachaResult>(count);
        GachaPool pool = GetPool(poolId);
        if (pool == null) return results;
        if (pool.RequiresPremiumTicket && ticketId != IFE精选抽卡券) return results;
        if (!TakeItemWithTip(ticketId, count, out int taken)) return results;
        if (taken < count) return results;

        for (int i = 0; i < count; i++) {
            bool hardPity = GachaManager.IsHardPity(poolId);
            float softBonus = GachaManager.GetSoftPityBonus(poolId);
            GachaRarity rarity = RollRarity(pool, softBonus, hardPity);
            bool isUp = false;

            if (poolId == GachaPool.PoolIdUp && rarity == GachaRarity.S) {
                if (GachaManager.UpGuaranteeCount >= 2 || _rng.NextDouble() < 0.5) {
                    isUp = true;
                    GachaManager.UpGuaranteeCount = 0;
                } else {
                    GachaManager.UpGuaranteeCount++;
                }
            }

            int itemId;
            bool isRecipe;
            if (isUp && pool.UpItems.Count > 0) {
                itemId = pool.UpItems[_rng.Next(pool.UpItems.Count)];
                isRecipe = TryReward(itemId);
            } else {
                itemId = pool.PickRandom(rarity, _rng);
                isRecipe = TryReward(itemId);
            }

            GachaManager.RecordDraw(poolId, rarity >= GachaRarity.A);
            results.Add(new GachaResult(itemId, rarity, isUp, isRecipe));
        }

        return results;
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

    private static GachaRarity RollRarity(GachaPool pool, float softPityBonus, bool forceAPlus) {
        if (forceAPlus) {
            float totalAS = pool.RateA + pool.RateS;
            return _rng.NextDouble() < pool.RateS / totalAS ? GachaRarity.S : GachaRarity.A;
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
