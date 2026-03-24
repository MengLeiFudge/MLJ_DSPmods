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
        [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔],
    ];

    private enum RecipeCategory {
        Basic,
        Mid,
        High,
    }

    private static readonly (int matrixId, RecipeCategory category)[] RecipeCategoryMap = [
        (I电磁矩阵, RecipeCategory.Basic),
        (I能量矩阵, RecipeCategory.Basic),
        (I结构矩阵, RecipeCategory.Mid),
        (I信息矩阵, RecipeCategory.Mid),
        (I引力矩阵, RecipeCategory.High),
        (I宇宙矩阵, RecipeCategory.High),
    ];

    private static readonly int[] ProtoEmbryos = [
        IFE交互塔原胚,
        IFE矿物复制塔原胚,
        IFE点数聚集塔原胚,
        IFE转化塔原胚,
        IFE精馏塔原胚,
        IFE分馏塔定向原胚,
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
        int groupIndex = GachaManager.CurrentUpGroupIndex;
        if (groupIndex < 0 || groupIndex >= UpItemGroups.Length) {
            groupIndex = 0;
            GachaManager.CurrentUpGroupIndex = 0;
        }
        ResolveUpTargets(groupIndex, out int upMain, out int upSub1, out int upSub2, out int upSub3);
        pool.UpItems.Add(upMain);
        pool.UpItems.Add(upSub1);
        pool.UpItems.Add(upSub2);
        pool.UpItems.Add(upSub3);

        pool.PoolC.AddRange(pool.UpItems);
        pool.PoolB.AddRange(pool.UpItems);
        pool.PoolA.AddRange(pool.UpItems);
        pool.PoolS.AddRange(pool.UpItems);
        GachaManager.SetCurrentUpTargets(upMain, upSub1, upSub2, upSub3);

        if (pool.PoolC.Count == 0) pool.PoolC.Add(IFE交互塔);
        if (pool.PoolB.Count == 0) pool.PoolB.AddRange(pool.PoolC);
        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.AddRange(pool.PoolA);
        CurrentUpPoolNameKey = $"UP池-{groupIndex + 1}";
    }

    private static void FillRecipePool(GachaPool pool) {
        var recipeBuckets = BuildRecipeBuckets();
        List<int> basicRecipes = recipeBuckets[RecipeCategory.Basic];
        List<int> midRecipes = recipeBuckets[RecipeCategory.Mid];
        List<int> highRecipes = recipeBuckets[RecipeCategory.High];

        pool.PoolC.AddRange(basicRecipes);

        pool.PoolB.AddRange(basicRecipes);
        pool.PoolB.AddRange(midRecipes);

        pool.PoolA.AddRange(midRecipes);
        pool.PoolA.AddRange(highRecipes);

        pool.PoolS.AddRange(highRecipes);

        if (pool.PoolC.Count == 0) pool.PoolC.Add(IFE残片);
        if (pool.PoolB.Count == 0) pool.PoolB.AddRange(pool.PoolC);
        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.AddRange(pool.PoolA.Count > 0 ? pool.PoolA : pool.PoolB);
    }

    private static void FillBuildingPool(GachaPool pool) {
        pool.PoolC.AddRange(ProtoEmbryos);
        pool.PoolB.AddRange(ProtoEmbryos);
        pool.PoolA.AddRange(ProtoEmbryos);
        pool.PoolS.AddRange(ProtoEmbryos);

        if (pool.PoolC.Count == 0) pool.PoolC.Add(IFE分馏塔定向原胚);
        if (pool.PoolB.Count == 0) pool.PoolB.AddRange(pool.PoolC);
        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.AddRange(pool.PoolA);
    }

    private static void FillLimitedPool(GachaPool pool) {
        pool.PoolC.Add(IFE原版配方核心);
        pool.PoolB.Add(IFE原版配方核心);
        pool.PoolA.Add(IFE原版配方核心);
        pool.PoolS.Add(IFE原版配方核心);

        if (pool.PoolC.Count == 0) pool.PoolC.Add(IFE原版配方核心);
        if (pool.PoolB.Count == 0) pool.PoolB.AddRange(pool.PoolC);
        if (pool.PoolA.Count == 0) pool.PoolA.AddRange(pool.PoolB);
        if (pool.PoolS.Count == 0) pool.PoolS.AddRange(pool.PoolA);
    }

    private static Dictionary<RecipeCategory, List<int>> BuildRecipeBuckets() {
        var buckets = new Dictionary<RecipeCategory, List<int>> {
            [RecipeCategory.Basic] = [],
            [RecipeCategory.Mid] = [],
            [RecipeCategory.High] = [],
        };

        var dedupe = new HashSet<int>();
        foreach ((int matrixId, RecipeCategory category) in RecipeCategoryMap) {
            foreach (var recipe in RecipeManager.GetRecipesByMatrix(matrixId)) {
                if (recipe == null || recipe.InputID <= 0) continue;
                if (dedupe.Add(recipe.InputID)) {
                    buckets[category].Add(recipe.InputID);
                }
            }
        }

        return buckets;
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
                int pityItemId;
                bool pityIsUp = false;
                if (GachaPool.IsUpPool(poolId)) {
                    pityItemId = RollUpTargetItem(pool, out bool hitMainTarget);
                    GachaManager.RecordUpSResult(hitMainTarget);
                    pityIsUp = true;
                } else {
                    pityItemId = GetHardPityItem(poolId, pool);
                }
                bool pityIsRecipe = RewardItem(poolId, pityItemId);
                GachaManager.RecordDraw(poolId, true);
                results.Add(new GachaResult(pityItemId, GachaRarity.S, pityIsUp, pityIsRecipe));
                continue;
            }

            float softBonus = GachaManager.GetSoftPityBonus(poolId);
            GachaRarity rarity = RollRarity(pool, softBonus, hardPity);

            int itemId;
            bool isRecipe;
            bool isUp = false;
            if (GachaPool.IsUpPool(poolId) && rarity == GachaRarity.S) {
                itemId = RollUpTargetItem(pool, out bool hitMainTarget);
                GachaManager.RecordUpSResult(hitMainTarget);
                isUp = true;
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
            GachaPool.PoolIdPermanentRecipe => PickHardPityFromPool(pool, IFE残片),
            GachaPool.PoolIdPermanentBuilding => PickHardPityFromPool(pool, IFE分馏塔定向原胚),
            GachaPool.PoolIdUp => IFE交互塔,
            GachaPool.PoolIdLimited => IFE原版配方核心,
            _ => IFE原版配方核心,
        };
    }

    private static int RollUpTargetItem(GachaPool pool, out bool hitMainTarget) {
        int mainItemId = GachaManager.UpMainItemId;
        int sub1ItemId = GachaManager.UpSubItemIds[0];
        int sub2ItemId = GachaManager.UpSubItemIds[1];
        int sub3ItemId = GachaManager.UpSubItemIds[2];

        if (mainItemId <= 0) {
            ResolveUpTargets(GachaManager.CurrentUpGroupIndex, out mainItemId, out sub1ItemId, out sub2ItemId, out sub3ItemId);
            GachaManager.SetCurrentUpTargets(mainItemId, sub1ItemId, sub2ItemId, sub3ItemId);
        }

        hitMainTarget = GachaManager.ShouldGuaranteeUpOnSRoll(_rng.NextDouble());
        if (hitMainTarget) {
            return mainItemId;
        }

        double sideRoll = _rng.NextDouble();
        if (sideRoll < 1.0 / 3.0) return sub1ItemId;
        if (sideRoll < 2.0 / 3.0) return sub2ItemId;
        return sub3ItemId;
    }

    private static void ResolveUpTargets(int groupIndex, out int mainItemId, out int sub1ItemId, out int sub2ItemId, out int sub3ItemId) {
        int[] group = groupIndex >= 0 && groupIndex < UpItemGroups.Length ? UpItemGroups[groupIndex] : UpItemGroups[0];
        int fallback = IFE交互塔;

        var uniqueTargets = new List<int>(4);
        for (int i = 0; i < group.Length; i++) {
            int id = group[i];
            if (id <= 0 || uniqueTargets.Contains(id)) {
                continue;
            }
            uniqueTargets.Add(id);
        }

        if (uniqueTargets.Count == 0) {
            uniqueTargets.Add(fallback);
        }

        mainItemId = uniqueTargets[0];

        var sideCandidates = new List<int>(3);
        for (int i = 0; i < uniqueTargets.Count; i++) {
            int id = uniqueTargets[i];
            if (id != mainItemId) {
                sideCandidates.Add(id);
            }
        }

        if (sideCandidates.Count == 0) {
            sideCandidates.Add(mainItemId);
        }

        sub1ItemId = sideCandidates[0];
        sub2ItemId = sideCandidates.Count > 1 ? sideCandidates[1] : sideCandidates[0];
        sub3ItemId = sideCandidates.Count switch {
            > 2 => sideCandidates[2],
            2 => sideCandidates[0],
            _ => sideCandidates[0],
        };
    }

    private static int PickHardPityFromPool(GachaPool pool, int fallbackItemId) {
        if (pool.PoolS.Count > 0) return pool.PoolS[_rng.Next(pool.PoolS.Count)];
        if (pool.PoolA.Count > 0) return pool.PoolA[_rng.Next(pool.PoolA.Count)];
        if (pool.PoolB.Count > 0) return pool.PoolB[_rng.Next(pool.PoolB.Count)];
        if (pool.PoolC.Count > 0) return pool.PoolC[_rng.Next(pool.PoolC.Count)];
        return fallbackItemId;
    }

    private static bool RewardItem(int poolId, int itemId) {
        if (GachaPool.IsRecipePool(poolId)) {
            return TryReward(itemId);
        }
        AddItemToModData(itemId, 1, 0, false);
        return false;
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
