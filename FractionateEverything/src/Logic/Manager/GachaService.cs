using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Recipe;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public readonly struct GachaFocusDefinition(
    GachaFocusType focusType,
    string nameKey,
    string descKey) {
    public GachaFocusType FocusType { get; } = focusType;
    public string NameKey { get; } = nameKey;
    public string DescKey { get; } = descKey;
}

public readonly struct GachaGrowthOffer(
    int pointCost,
    int fragmentCost,
    int outputId,
    int outputCount,
    GachaFocusType focusType = GachaFocusType.Balanced) {
    public int PointCost { get; } = pointCost;
    public int FragmentCost { get; } = fragmentCost;
    public int OutputId { get; } = outputId;
    public int OutputCount { get; } = outputCount;
    public GachaFocusType FocusType { get; } = focusType;
}

public static class GachaService {
    private static readonly Random rng = new();
    private static readonly List<GachaPool> pools = [];

    private static int cachedMatrixId;
    private static GachaFocusType cachedFocus = GachaFocusType.Balanced;

    private static readonly GachaFocusDefinition[] focusDefinitions = [
        new(GachaFocusType.Balanced, "聚焦-平衡发展", "聚焦描述-平衡发展"),
        new(GachaFocusType.MineralExpansion, "聚焦-复制扩张", "聚焦描述-复制扩张"),
        new(GachaFocusType.ConversionLeap, "聚焦-转化跃迁", "聚焦描述-转化跃迁"),
        new(GachaFocusType.LogisticsInteraction, "聚焦-交互物流", "聚焦描述-交互物流"),
        new(GachaFocusType.EmbryoCycle, "聚焦-原胚循环", "聚焦描述-原胚循环"),
        new(GachaFocusType.ProcessOptimization, "聚焦-工艺优化", "聚焦描述-工艺优化"),
        new(GachaFocusType.RectificationEconomy, "聚焦-精馏经济", "聚焦描述-精馏经济"),
    ];

    public static IReadOnlyList<GachaFocusDefinition> FocusDefinitions => focusDefinitions;

    public static void InitPools() {
        cachedMatrixId = GetCurrentProgressMatrixId();
        cachedFocus = GachaManager.CurrentFocus;

        pools.Clear();

        var openingPool = new GachaPool(GachaPool.PoolIdOpeningLine, "开线池");
        FillOpeningLinePool(openingPool);
        pools.Add(openingPool);

        var protoPool = new GachaPool(GachaPool.PoolIdProtoLoop, "原胚闭环池");
        FillProtoLoopPool(protoPool);
        pools.Add(protoPool);

        var growthPool = new GachaPool(GachaPool.PoolIdGrowth, "成长池");
        FillGrowthPool(growthPool);
        pools.Add(growthPool);

        var focusPool = new GachaPool(GachaPool.PoolIdFocus, "流派聚焦");
        FillFocusPool(focusPool);
        pools.Add(focusPool);
    }

    private static void EnsurePoolsFresh() {
        int currentMatrixId = GetCurrentProgressMatrixId();
        GachaFocusType currentFocus = GachaManager.CurrentFocus;
        if (pools.Count == GachaPool.PoolCount
            && cachedMatrixId == currentMatrixId
            && cachedFocus == currentFocus) {
            return;
        }

        InitPools();
    }

    public static int GetCurrentDrawMatrixId() {
        return GetCurrentProgressMatrixId();
    }

    public static int GetDrawMatrixCost(int poolId, int drawCount) {
        if (!GachaPool.IsDrawPool(poolId) || drawCount <= 0) {
            return 0;
        }

        int singleCost = poolId switch {
            GachaPool.PoolIdOpeningLine => 1,
            GachaPool.PoolIdProtoLoop => 1,
            _ => 0,
        };
        return singleCost * drawCount;
    }

    public static int GetFocusSwitchFragmentCost(GachaFocusType targetFocus) {
        return targetFocus == GachaManager.CurrentFocus ? 0 : 120;
    }

    public static bool TryChangeFocus(GachaFocusType targetFocus) {
        int fragmentCost = GetFocusSwitchFragmentCost(targetFocus);
        if (fragmentCost > 0 && !TakeItemWithTip(IFE残片, fragmentCost, out _)) {
            return false;
        }

        GachaManager.SetFocus(targetFocus);
        return true;
    }

    public static IReadOnlyList<GachaGrowthOffer> GetGrowthOffers() {
        var offers = new List<GachaGrowthOffer> {
            new(5, 0, IFE残片, 50),
            new(10, 10, GetCurrentDrawMatrixId(), 4),
            new(20, 15, GetFocusedEmbryoReward(), 1, GachaManager.CurrentFocus),
            new(36, 30, IFE分馏塔定向原胚, 1, GachaFocusType.EmbryoCycle),
        };
        return offers;
    }

    private static void FillOpeningLinePool(GachaPool pool) {
        var allRecipes = GetOpeningRecipes();
        int currentStageIndex = GetCurrentProgressStageIndex();

        var previousStageRecipes = new List<int>();
        var currentStageRecipes = new List<int>();
        var lockedCurrentStageRecipes = new List<int>();

        foreach (BaseRecipe recipe in allRecipes) {
            int stageIndex = GetMatrixStageIndex(recipe.MatrixID);
            int itemId = recipe.InputID;
            if (stageIndex < currentStageIndex) {
                AddWeighted(previousStageRecipes, itemId, GetRecipeWeight(recipe, currentStageIndex));
                continue;
            }

            if (stageIndex == currentStageIndex) {
                int weight = GetRecipeWeight(recipe, currentStageIndex);
                AddWeighted(currentStageRecipes, itemId, weight);
                if (!recipe.Unlocked) {
                    AddWeighted(lockedCurrentStageRecipes, itemId, weight + 1);
                }
            }
        }

        pool.PoolC.Add(IFE残片);

        if (previousStageRecipes.Count > 0) {
            pool.PoolB.AddRange(previousStageRecipes);
        } else {
            pool.PoolB.AddRange(currentStageRecipes);
        }
        if (pool.PoolB.Count == 0) {
            pool.PoolB.Add(IFE残片);
        }

        if (currentStageRecipes.Count > 0) {
            pool.PoolA.AddRange(currentStageRecipes);
        }
        if (pool.PoolA.Count == 0) {
            pool.PoolA.AddRange(pool.PoolB);
        }

        if (lockedCurrentStageRecipes.Count > 0) {
            pool.PoolS.AddRange(lockedCurrentStageRecipes);
        } else if (currentStageRecipes.Count > 0) {
            pool.PoolS.AddRange(currentStageRecipes);
        } else {
            pool.PoolS.AddRange(pool.PoolA);
        }
    }

    private static void FillProtoLoopPool(GachaPool pool) {
        AddWeighted(pool.PoolC, IFE交互塔原胚, GetEmbryoWeight(IFE交互塔原胚));
        AddWeighted(pool.PoolC, IFE矿物复制塔原胚, GetEmbryoWeight(IFE矿物复制塔原胚));

        AddWeighted(pool.PoolB, IFE点数聚集塔原胚, GetEmbryoWeight(IFE点数聚集塔原胚));
        AddWeighted(pool.PoolB, IFE交互塔原胚, GetEmbryoWeight(IFE交互塔原胚));

        AddWeighted(pool.PoolA, IFE转化塔原胚, GetEmbryoWeight(IFE转化塔原胚));
        AddWeighted(pool.PoolA, IFE精馏塔原胚, GetEmbryoWeight(IFE精馏塔原胚));

        AddWeighted(pool.PoolS, IFE分馏塔定向原胚, GetEmbryoWeight(IFE分馏塔定向原胚) + 1);
        AddWeighted(pool.PoolS, GetFocusedEmbryoReward(), GetEmbryoWeight(GetFocusedEmbryoReward()));

        if (pool.PoolB.Count == 0) {
            pool.PoolB.AddRange(pool.PoolC);
        }
        if (pool.PoolA.Count == 0) {
            pool.PoolA.AddRange(pool.PoolB);
        }
        if (pool.PoolS.Count == 0) {
            pool.PoolS.AddRange(pool.PoolA);
        }
    }

    private static void FillGrowthPool(GachaPool pool) {
        pool.PoolC.Add(IFE残片);
        pool.PoolB.Add(GetCurrentDrawMatrixId());
        pool.PoolA.Add(GetFocusedEmbryoReward());
        pool.PoolS.Add(IFE分馏塔定向原胚);
    }

    private static void FillFocusPool(GachaPool pool) {
        foreach (var focus in focusDefinitions) {
            pool.PoolC.Add((int)focus.FocusType);
        }
    }

    private static List<BaseRecipe> GetOpeningRecipes() {
        var recipes = new List<BaseRecipe>();
        recipes.AddRange(RecipeManager.GetRecipesByType(ERecipe.MineralCopy));
        recipes.AddRange(RecipeManager.GetRecipesByType(ERecipe.Conversion));
        return recipes
            .Where(recipe => recipe != null && recipe.InputID > 0 && recipe.MatrixID != I黑雾矩阵)
            .Where(recipe => GetMatrixStageIndex(recipe.MatrixID) <= GetCurrentProgressStageIndex())
            .ToList();
    }

    private static int GetRecipeWeight(BaseRecipe recipe, int currentStageIndex) {
        int weight = 1;
        if (recipe.RecipeType == ERecipe.MineralCopy && GachaManager.CurrentFocus == GachaFocusType.MineralExpansion) {
            weight += 2;
        }
        if (recipe.RecipeType == ERecipe.Conversion && GachaManager.CurrentFocus == GachaFocusType.ConversionLeap) {
            weight += 2;
        }
        if (GachaManager.CurrentFocus == GachaFocusType.ProcessOptimization
            && GetMatrixStageIndex(recipe.MatrixID) == currentStageIndex) {
            weight += 1;
        }
        return weight;
    }

    private static int GetEmbryoWeight(int itemId) {
        int weight = 1;
        switch (GachaManager.CurrentFocus) {
            case GachaFocusType.MineralExpansion when itemId == IFE矿物复制塔原胚:
                weight += 2;
                break;
            case GachaFocusType.ConversionLeap when itemId == IFE转化塔原胚:
                weight += 2;
                break;
            case GachaFocusType.LogisticsInteraction when itemId == IFE交互塔原胚:
                weight += 2;
                break;
            case GachaFocusType.EmbryoCycle when itemId == IFE分馏塔定向原胚:
                weight += 3;
                break;
            case GachaFocusType.ProcessOptimization when itemId == IFE点数聚集塔原胚:
                weight += 2;
                break;
            case GachaFocusType.RectificationEconomy when itemId == IFE精馏塔原胚:
                weight += 2;
                break;
        }
        return weight;
    }

    private static int GetFocusedEmbryoReward() {
        return GachaManager.CurrentFocus switch {
            GachaFocusType.MineralExpansion => IFE矿物复制塔原胚,
            GachaFocusType.ConversionLeap => IFE转化塔原胚,
            GachaFocusType.LogisticsInteraction => IFE交互塔原胚,
            GachaFocusType.EmbryoCycle => IFE分馏塔定向原胚,
            GachaFocusType.ProcessOptimization => IFE点数聚集塔原胚,
            GachaFocusType.RectificationEconomy => IFE精馏塔原胚,
            _ => IFE交互塔原胚,
        };
    }

    private static void AddWeighted(List<int> target, int itemId, int weight) {
        if (itemId <= 0) {
            return;
        }

        int count = Math.Max(1, weight);
        for (int i = 0; i < count; i++) {
            target.Add(itemId);
        }
    }

    public static List<GachaResult> Draw(int poolId, int ticketId, int count) {
        if (count <= 0) {
            return [];
        }

        EnsurePoolsFresh();

        var results = new List<GachaResult>(count);
        if (!GachaPool.IsDrawPool(poolId)) {
            return results;
        }

        GachaPool pool = GetPool(poolId);
        if (pool == null || !GachaPool.CanUseTicket(poolId, ticketId)) {
            return results;
        }

        int totalCost = GetDrawMatrixCost(poolId, count);
        if (!TakeItemWithTip(ticketId, totalCost, out _)) {
            return results;
        }

        for (int i = 0; i < count; i++) {
            bool hardPity = GachaManager.IsHardPity(poolId);
            GachaRarity rarity = RollRarity(pool, GachaManager.GetCurrentSRate(poolId, pool.RateS), hardPity);
            int itemId = hardPity ? GetHardPityItem(poolId, pool) : pool.PickRandom(rarity, rng);
            bool isRecipe = RewardItem(poolId, itemId);

            GachaManager.RecordDraw(poolId, rarity == GachaRarity.S);
            GachaManager.AddPoolPoints(poolId, 1);
            results.Add(new GachaResult(itemId, rarity, false, isRecipe, wasHardPity: hardPity));
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

    private static bool RewardItem(int poolId, int itemId) {
        if (GachaPool.IsRecipePool(poolId)) {
            return TryRewardRecipe(itemId);
        }

        AddItemToModData(itemId, 1, 0, false);
        return false;
    }

    private static bool TryRewardRecipe(int inputId) {
        if (inputId <= 0) {
            return false;
        }

        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(ERecipe.MineralCopy, inputId)
                            ?? RecipeManager.GetRecipe<BaseRecipe>(ERecipe.Conversion, inputId);
        if (recipe == null) {
            AddItemToModData(inputId, 1, 0, false);
            return false;
        }

        recipe.RewardThis(true);
        return true;
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

    public static GachaPool GetPool(int poolId) {
        EnsurePoolsFresh();
        return pools.FirstOrDefault(pool => pool.PoolId == poolId);
    }

    public static List<GachaPool> GetAllPools() {
        EnsurePoolsFresh();
        return pools;
    }
}
