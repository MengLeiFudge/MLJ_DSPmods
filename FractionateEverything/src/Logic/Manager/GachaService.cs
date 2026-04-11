using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using UnityEngine;
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
    GachaFocusType focusType = GachaFocusType.Balanced,
    int extraCostItemId = 0,
    int extraCostCount = 0) {
    public int PointCost { get; } = pointCost;
    public int FragmentCost { get; } = fragmentCost;
    public int OutputId { get; } = outputId;
    public int OutputCount { get; } = outputCount;
    public GachaFocusType FocusType { get; } = focusType;
    public int ExtraCostItemId { get; } = extraCostItemId;
    public int ExtraCostCount { get; } = extraCostCount;
}

internal readonly struct GachaRewardResolution(
    GachaRewardType rewardType,
    int rewardItemId,
    int rewardCount) {
    public GachaRewardType RewardType { get; } = rewardType;
    public int RewardItemId { get; } = rewardItemId;
    public int RewardCount { get; } = rewardCount;
}

/// <summary>
/// 抽卡域的唯一业务入口。
/// 这里只维护池构建、聚焦偏置、成长报价和奖励结算，不直接处理任何 UI 文案。
/// </summary>
public static class GachaService {
    private static readonly System.Random rng = new();
    private static readonly List<GachaPool> pools = [];
    private static readonly GachaPool[] poolsById = new GachaPool[GachaPool.PoolCount];
    private static readonly Dictionary<int, BaseRecipe> recipeRewardIndex = [];
    private static int recipeRewardIndexRecipeCount;

    private static int cachedMatrixId;
    private static GachaFocusType cachedFocus = GachaFocusType.Balanced;
    private static GachaMode cachedMode = GachaMode.Normal;
    private static int cachedOpeningRecipeStateHash;

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
    public static bool IsSpeedrunMode => GachaManager.IsSpeedrunMode;

    public static string GetFocusName(GachaFocusType focusType) {
        foreach (var focus in focusDefinitions) {
            if (focus.FocusType == focusType) {
                return focus.NameKey.Translate();
            }
        }
        return focusType.ToString();
    }

    public static void InitPools() {
        EnsureRecipeRewardIndex();
        cachedMatrixId = GetCurrentProgressMatrixId();
        cachedFocus = GachaManager.CurrentFocus;
        cachedMode = GachaManager.CurrentMode;
        cachedOpeningRecipeStateHash = GetOpeningRecipeStateHash();

        pools.Clear();
        Array.Clear(poolsById, 0, poolsById.Length);

        var openingPool = new GachaPool(GachaPool.PoolIdOpeningLine, GetPoolNameKey(GachaPool.PoolIdOpeningLine));
        FillOpeningLinePool(openingPool);
        RegisterPool(openingPool);

        var protoPool = new GachaPool(GachaPool.PoolIdProtoLoop, GetPoolNameKey(GachaPool.PoolIdProtoLoop));
        FillProtoLoopPool(protoPool);
        RegisterPool(protoPool);

        var growthPool = new GachaPool(GachaPool.PoolIdGrowth, GetPoolNameKey(GachaPool.PoolIdGrowth));
        FillGrowthPool(growthPool);
        RegisterPool(growthPool);

        var focusPool = new GachaPool(GachaPool.PoolIdFocus, GetPoolNameKey(GachaPool.PoolIdFocus));
        FillFocusPool(focusPool);
        RegisterPool(focusPool);
    }

    private static void EnsurePoolsFresh() {
        int currentMatrixId = GetCurrentProgressMatrixId();
        GachaFocusType currentFocus = GachaManager.CurrentFocus;
        GachaMode currentMode = GachaManager.CurrentMode;
        int currentRecipeStateHash = GetOpeningRecipeStateHash();
        if (pools.Count == GachaPool.PoolCount
            && cachedMatrixId == currentMatrixId
            && cachedFocus == currentFocus
            && cachedMode == currentMode
            && cachedOpeningRecipeStateHash == currentRecipeStateHash) {
            return;
        }

        InitPools();
    }

    private static void RegisterPool(GachaPool pool) {
        pools.Add(pool);
        if (GachaPool.IsValidPoolId(pool.PoolId)) {
            poolsById[pool.PoolId] = pool;
        }
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
            GachaPool.PoolIdProtoLoop => IsSpeedrunMode ? 1 : 1,
            _ => 0,
        };
        return singleCost * drawCount;
    }

    public static int GetFocusSwitchFragmentCost(GachaFocusType targetFocus) {
        if (IsSpeedrunMode) {
            return 0;
        }
        return targetFocus == GachaManager.CurrentFocus ? 0 : 120;
    }

    public static bool TryChangeFocus(GachaFocusType targetFocus) {
        if (targetFocus == GachaManager.CurrentFocus) {
            return true;
        }
        int fragmentCost = GetFocusSwitchFragmentCost(targetFocus);
        if (fragmentCost > 0 && !TakeItemWithTip(IFE残片, fragmentCost, out _)) {
            return false;
        }

        GachaManager.SetFocus(targetFocus);
        return true;
    }

    public static IReadOnlyList<GachaGrowthOffer> GetGrowthOffers() {
        IReadOnlyList<GachaGrowthOffer> baseOffers = IsSpeedrunMode
            ? BuildSpeedrunGrowthOffers()
            : BuildNormalGrowthOffers();
        List<GachaGrowthOffer> adjusted = new(baseOffers.Count);
        foreach (GachaGrowthOffer offer in baseOffers) {
            adjusted.Add(ApplyFocusOfferModifier(offer));
        }
        return adjusted;
    }

    public static bool TryExchangeGrowthOffer(GachaGrowthOffer offer, out int rewardItemId, out int rewardCount) {
        rewardItemId = 0;
        rewardCount = 0;

        if (offer.PointCost > 0 && !GachaManager.TryConsumePoolPoints(GachaPool.PoolIdGrowth, offer.PointCost)) {
            return false;
        }
        if (offer.FragmentCost > 0 && !TakeItemWithTip(IFE残片, offer.FragmentCost, out _)) {
            if (offer.PointCost > 0) {
                GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            }
            return false;
        }
        if (offer.ExtraCostItemId > 0 && !TakeItemWithTip(offer.ExtraCostItemId, offer.ExtraCostCount, out _)) {
            if (offer.PointCost > 0) {
                GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            }
            if (offer.FragmentCost > 0) {
                AddItemToModData(IFE残片, offer.FragmentCost, 0, true);
            }
            return false;
        }

        if (IsDarkFogCatchupOffer(offer)) {
            int appliedRecipeCount = RecipeGrowthExecutor.ApplyDarkFogCatchupByItem(
                offer.OutputId,
                offer.OutputCount,
                RecipeGrowthManager.BuildContext(manual: true));
            rewardItemId = offer.OutputId;
            rewardCount = appliedRecipeCount > 0 ? offer.OutputCount : 0;
            return true;
        }

        AddItemToModData(offer.OutputId, offer.OutputCount, 0, true);
        rewardItemId = offer.OutputId;
        rewardCount = offer.OutputCount;
        return true;
    }

    private static GachaGrowthOffer ApplyFocusOfferModifier(GachaGrowthOffer offer) {
        if (!IsFocusedGrowthOffer(offer)) {
            return offer;
        }

        float discountFactor = GetFocusedOfferDiscountFactor();
        int pointCost = Mathf.Max(1, Mathf.RoundToInt(offer.PointCost * discountFactor));
        int fragmentCost = offer.FragmentCost <= 0
            ? 0
            : Mathf.Max(0, Mathf.RoundToInt(offer.FragmentCost * discountFactor));
        int outputCount = offer.OutputCount;

        if (IsCoreGrowthReward(offer)) {
            outputCount += 1;
            if (offer.OutputId == IFE残片) {
                outputCount += 10;
            }
        }

        return new GachaGrowthOffer(pointCost, fragmentCost, offer.OutputId, outputCount, offer.FocusType,
            offer.ExtraCostItemId, offer.ExtraCostCount);
    }

    public static bool IsFocusedGrowthOffer(GachaGrowthOffer offer) {
        return offer.FocusType != GachaFocusType.Balanced && offer.FocusType == GachaManager.CurrentFocus;
    }

    public static float GetFocusedOfferDiscountFactor() {
        return IsSpeedrunMode ? 0.85f : 0.80f;
    }

    public static bool IsCoreGrowthReward(GachaGrowthOffer offer) {
        return offer.OutputId == GetFocusedEmbryoReward()
               || offer.OutputId == IFE分馏塔定向原胚
               || offer.OutputId == IFE残片;
    }

    public static bool IsDarkFogCatchupOffer(GachaGrowthOffer offer) {
        return offer.ExtraCostItemId == I黑雾矩阵
               && !DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputId);
    }

    private static IReadOnlyList<GachaGrowthOffer> BuildNormalGrowthOffers() {
        var offers = new List<GachaGrowthOffer> {
            new(5, 0, IFE残片, 50),
            new(10, 10, GetCurrentDrawMatrixId(), 4),
            new(20, 15, GetFocusedEmbryoReward(), 1, GachaManager.CurrentFocus),
            new(36, 30, IFE分馏塔定向原胚, 1, GachaFocusType.EmbryoCycle),
        };

        AppendBlackFogOffers(offers);
        return offers;
    }

    private static IReadOnlyList<GachaGrowthOffer> BuildSpeedrunGrowthOffers() {
        var offers = new List<GachaGrowthOffer> {
            new(4, 0, GetCurrentDrawMatrixId(), 6),
            new(8, 6, GetFocusedEmbryoReward(), 1, GachaManager.CurrentFocus),
            new(15, 10, IFE分馏塔定向原胚, 1, GachaFocusType.EmbryoCycle),
        };

        AppendBlackFogOffers(offers, pointBaseOffset: -4, fragmentBaseOffset: -4);
        return offers;
    }

    private static void AppendBlackFogOffers(List<GachaGrowthOffer> offers, int pointBaseOffset = 0, int fragmentBaseOffset = 0) {
        if (!DarkFogCombatManager.IsGrowthOfferUnlocked()) {
            return;
        }

        EDarkFogCombatStage stage = DarkFogCombatManager.GetCurrentStage();
        int enhancedNodeCount = DarkFogCombatManager.GetEnhancedNodeCount();

        if (stage >= EDarkFogCombatStage.Signal) {
            offers.Add(new(18 + pointBaseOffset, 12 + fragmentBaseOffset, I能量碎片,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.Signal),
                GachaFocusType.RectificationEconomy, I黑雾矩阵, 1));
        }
        if (stage >= EDarkFogCombatStage.GroundSuppression) {
            offers.Add(new(26 + pointBaseOffset, 16 + fragmentBaseOffset, I物质重组器,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.GroundSuppression),
                GachaFocusType.ConversionLeap, I黑雾矩阵, 2));
            offers.Add(new(30 + pointBaseOffset, 18 + fragmentBaseOffset, I硅基神经元,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.GroundSuppression),
                GachaFocusType.ProcessOptimization, I黑雾矩阵, 2));
        }
        if (stage >= EDarkFogCombatStage.StellarHunt) {
            offers.Add(new(38 + pointBaseOffset, 24 + fragmentBaseOffset, I负熵奇点,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.StellarHunt),
                GachaFocusType.RectificationEconomy, I黑雾矩阵, 3));
        }
        if (stage >= EDarkFogCombatStage.Singularity) {
            offers.Add(new(45 + pointBaseOffset, 30 + fragmentBaseOffset, I核心素,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.Singularity),
                GachaFocusType.EmbryoCycle, I黑雾矩阵, 4));
        }
        if (DarkFogCombatManager.IsEnhancedLayerEnabled() && stage >= EDarkFogCombatStage.Singularity) {
            if (enhancedNodeCount >= 2) {
                offers.Add(new(48 + pointBaseOffset, 32 + fragmentBaseOffset, IFE分馏塔定向原胚, 1,
                    GachaFocusType.EmbryoCycle, I黑雾矩阵, 4));
            }
        }
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
                if (!RecipeGrowthQueries.IsUnlocked(recipe)) {
                    AddWeighted(lockedCurrentStageRecipes, itemId, weight + 1);
                }
            }
        }

        if (IsSpeedrunMode) {
            List<int> targetRecipes = currentStageRecipes.Count > 0 ? currentStageRecipes :
                previousStageRecipes.Count > 0 ? previousStageRecipes : lockedCurrentStageRecipes;
            if (targetRecipes.Count == 0) {
                targetRecipes = [IFE残片];
            }
            pool.PoolC.AddRange(targetRecipes);
            pool.PoolB.AddRange(targetRecipes);
            pool.PoolA.AddRange(targetRecipes);
            pool.PoolS.AddRange(lockedCurrentStageRecipes.Count > 0 ? lockedCurrentStageRecipes : targetRecipes);
            return;
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
        List<int> weightedEmbryos = [];
        for (int itemId = IFE交互塔原胚; itemId <= IFE精馏塔原胚; itemId++) {
            AddWeighted(weightedEmbryos, itemId, GetEmbryoWeight(itemId));
        }
        AddWeighted(weightedEmbryos, IFE分馏塔定向原胚, GetEmbryoWeight(IFE分馏塔定向原胚));

        pool.PoolC.AddRange(weightedEmbryos);
        pool.PoolB.AddRange(weightedEmbryos);
        pool.PoolA.AddRange(weightedEmbryos);
        pool.PoolS.AddRange(weightedEmbryos);
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

    /// <summary>
    /// 开线池当前只消费“生产型”配方。
    /// 工具/解锁型与特殊成长型配方继续走科技、原胚闭环或成长页，不混入随机开线入口。
    /// </summary>
    private static bool IsOpeningLineRecipe(BaseRecipe recipe) {
        return recipe != null
               && recipe.RecipeType is ERecipe.MineralCopy or ERecipe.Conversion
               && recipe.GrowthRole == ERecipeGrowthRole.Production
               && recipe.InputID > 0
               && recipe.MatrixID != I黑雾矩阵;
    }

    private static int GetOpeningRecipeStateHash() {
        int hash = 17;
        unchecked {
            foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
                if (!IsOpeningLineRecipe(recipe)) {
                    continue;
                }

                hash = hash * 31 + recipe.InputID;
                hash = hash * 31 + (int)recipe.RecipeType;
                hash = hash * 31 + RecipeGrowthQueries.GetLevel(recipe);
                hash = hash * 31 + recipe.MatrixID;
            }
        }
        return hash;
    }

    private static List<BaseRecipe> GetOpeningRecipes() {
        int currentStageIndex = GetCurrentProgressStageIndex();
        var recipes = new List<BaseRecipe>();
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            if (!IsOpeningLineRecipe(recipe) || GetMatrixStageIndex(recipe.MatrixID) > currentStageIndex) {
                continue;
            }

            recipes.Add(recipe);
        }
        return recipes;
    }

    private static int GetRecipeWeight(BaseRecipe recipe, int currentStageIndex) {
        RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
        float weight = family switch {
            RecipeFamily.MineralCopyNormal => IsSpeedrunMode ? 120f : 100f,
            RecipeFamily.ConversionMaterialNormal => IsSpeedrunMode ? 120f : 100f,
            RecipeFamily.ConversionBuilding => IsSpeedrunMode ? 32f : 40f,
            _ => 1f,
        };

        int recipeStageIndex = GetMatrixStageIndex(recipe.MatrixID);
        if (!RecipeGrowthQueries.IsUnlocked(recipe)) {
            weight *= IsSpeedrunMode ? 1.8f : 1.5f;
        }
        if (recipeStageIndex == currentStageIndex) {
            weight *= IsSpeedrunMode ? 1.5f : 1.3f;
        } else if (IsSpeedrunMode && recipeStageIndex == currentStageIndex - 1 && !RecipeGrowthQueries.IsMaxed(recipe)) {
            weight *= 1.25f;
        }

        weight *= GetOpeningRecipeFocusMultiplier(recipe, currentStageIndex);

        if (RecipeGrowthQueries.IsMaxed(recipe)) {
            weight *= IsSpeedrunMode ? 0.20f : 0.35f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(weight));
    }

    private static int GetEmbryoWeight(int itemId) {
        float weight;
        if (itemId == IFE分馏塔定向原胚) {
            weight = IsSpeedrunMode ? 80f : 65f;
        } else if (itemId == GetFocusedEmbryoReward()) {
            weight = IsSpeedrunMode ? 115f : 100f;
        } else {
            weight = IsSpeedrunMode ? 85f : 80f;
        }

        if (!IsSpeedrunMode
            && GachaManager.CurrentFocus == GachaFocusType.RectificationEconomy
            && itemId == IFE精馏塔原胚) {
            weight *= 1.3f;
        }

        return Mathf.Max(1, Mathf.RoundToInt(weight));
    }

    private static float GetOpeningRecipeFocusMultiplier(BaseRecipe recipe, int currentStageIndex) {
        GachaFocusType focus = GachaManager.CurrentFocus;
        if (focus == GachaFocusType.Balanced) {
            return 1f;
        }

        float mainMultiplier = IsSpeedrunMode ? 1.6f : 1.4f;
        float sideMultiplier = IsSpeedrunMode ? 1.3f : 1.2f;

        if (focus == GachaFocusType.ProcessOptimization && GetMatrixStageIndex(recipe.MatrixID) == currentStageIndex) {
            return sideMultiplier;
        }
        if (focus == GachaFocusType.LogisticsInteraction && IsLogisticsRecipe(recipe.InputID)) {
            return mainMultiplier;
        }
        if (focus == GachaFocusType.EmbryoCycle && !RecipeGrowthQueries.IsUnlocked(recipe)) {
            return sideMultiplier;
        }

        return recipe.RecipeType switch {
            ERecipe.MineralCopy when focus == GachaFocusType.MineralExpansion => mainMultiplier,
            ERecipe.Conversion when focus == GachaFocusType.ConversionLeap => mainMultiplier,
            _ => 1f,
        };
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
        RecipeGrowthResult growthResult = RecipeGrowthExecutor.ApplyDrawReward(recipe, RecipeGrowthManager.BuildContext(manual: true));

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

    public static string GetModeNameKey() {
        return IsSpeedrunMode ? "速通模式" : "常规模式";
    }

    public static string GetPoolNameKey(int poolId) {
        if (IsSpeedrunMode) {
            return poolId switch {
                GachaPool.PoolIdOpeningLine => "阶段箱池",
                GachaPool.PoolIdProtoLoop => "简化原胚池",
                GachaPool.PoolIdGrowth => "简化成长池",
                GachaPool.PoolIdFocus => "速通聚焦层",
                _ => "阶段箱池",
            };
        }

        return poolId switch {
            GachaPool.PoolIdOpeningLine => "开线池",
            GachaPool.PoolIdProtoLoop => "原胚闭环池",
            GachaPool.PoolIdGrowth => "成长池",
            GachaPool.PoolIdFocus => "流派聚焦",
            _ => "开线池",
        };
    }

    public static string GetPoolDescKey(int poolId) {
        if (IsSpeedrunMode) {
            return poolId switch {
                GachaPool.PoolIdOpeningLine => "阶段箱池说明",
                GachaPool.PoolIdProtoLoop => "简化原胚池说明",
                GachaPool.PoolIdGrowth => "简化成长池说明",
                GachaPool.PoolIdFocus => "速通聚焦层说明",
                _ => "阶段箱池说明",
            };
        }

        return poolId switch {
            GachaPool.PoolIdOpeningLine => "开线池说明",
            GachaPool.PoolIdProtoLoop => "原胚闭环池说明",
            GachaPool.PoolIdGrowth => "成长池说明",
            GachaPool.PoolIdFocus => "流派聚焦说明",
            _ => "开线池说明",
        };
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
        return GachaPool.IsValidPoolId(poolId) ? poolsById[poolId] : null;
    }

    public static List<GachaPool> GetAllPools() {
        EnsurePoolsFresh();
        return [.. pools];
    }

    public static int GetDisplayPoolPoints() {
        return GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth);
    }

    private static GachaFocusMatchType GetFocusMatchType(int poolId, int itemId) {
        if (GachaManager.CurrentFocus == GachaFocusType.Balanced) {
            return GachaFocusMatchType.None;
        }
        if (GachaPool.IsRecipePool(poolId)) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(ERecipe.MineralCopy, itemId)
                                ?? RecipeManager.GetRecipe<BaseRecipe>(ERecipe.Conversion, itemId);
            if (recipe == null) {
                return GachaFocusMatchType.None;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.LogisticsInteraction && IsLogisticsRecipe(recipe.InputID)) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.EmbryoCycle && !RecipeGrowthQueries.IsUnlocked(recipe)) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.ProcessOptimization
                && GetMatrixStageIndex(recipe.MatrixID) == GetCurrentProgressStageIndex()) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.RectificationEconomy && RecipeGrowthQueries.IsMaxed(recipe)) {
                return GachaFocusMatchType.Side;
            }
            return recipe.RecipeType switch {
                ERecipe.MineralCopy when GachaManager.CurrentFocus == GachaFocusType.MineralExpansion => GachaFocusMatchType.Main,
                ERecipe.Conversion when GachaManager.CurrentFocus == GachaFocusType.ConversionLeap => GachaFocusMatchType.Main,
                _ => GachaFocusMatchType.None,
            };
        }
        if (GachaPool.IsProtoLoopPool(poolId)) {
            if (itemId == GetFocusedEmbryoReward()) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.EmbryoCycle && itemId == IFE分馏塔定向原胚) {
                return GachaFocusMatchType.Side;
            }
        }
        return GachaFocusMatchType.None;
    }

    private static bool IsLogisticsRecipe(int inputId) {
        return inputId switch {
            I配送运输机 or I物流运输机 or I星际物流运输船
                or I物流配送器 or I行星内物流运输站 or I星际物流运输站 or I轨道采集器
                or I传送带 or I高速传送带 or I极速传送带
                or I四向分流器 or I流速监测器 or I自动集装机
                or I分拣器 or I高速分拣器 or I极速分拣器 or I集装分拣器
                or I小型储物仓 or I大型储物仓 or I储液罐 => true,
            _ => false,
        };
    }
}
