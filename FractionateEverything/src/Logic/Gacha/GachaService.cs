using System;
using System.Collections.Generic;
using FE.Logic.Fractionation.Recipes;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取聚焦选项的类型和文案键。
/// </summary>
public readonly struct GachaFocusDefinition(
    GachaFocusType focusType,
    string nameKey,
    string descKey) {
    public GachaFocusType FocusType { get; } = focusType;
    public string NameKey { get; } = nameKey;
    public string DescKey { get; } = descKey;
}

/// <summary>
/// 成长商店单个报价的成本、奖励和类别。
/// </summary>
public readonly struct GachaGrowthOffer(
    int pointCost,
    int fragmentCost,
    int outputId,
    int outputCount,
    GachaFocusType focusType = GachaFocusType.Balanced,
    int extraCostItemId = 0,
    int extraCostCount = 0,
    GachaGrowthOfferKind offerKind = GachaGrowthOfferKind.ItemGrant,
    ERecipe recipeType = 0) {
    public int PointCost { get; } = pointCost;
    public int FragmentCost { get; } = fragmentCost;
    public int OutputId { get; } = outputId;
    public int OutputCount { get; } = outputCount;
    public GachaFocusType FocusType { get; } = focusType;
    public int ExtraCostItemId { get; } = extraCostItemId;
    public int ExtraCostCount { get; } = extraCostCount;
    public GachaGrowthOfferKind OfferKind { get; } = offerKind;
    public ERecipe RecipeType { get; } = recipeType;
}

/// <summary>
/// 成长商店报价的结算行为分类。
/// </summary>
public enum GachaGrowthOfferKind {
    ItemGrant = 0,
    DarkFogCatchup = 1,
    DarkFogRecipeGrowth = 2,
}

/// <summary>
/// 抽取奖励解析后的物品与数量。
/// </summary>
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
public static partial class GachaService {
    private static readonly Random rng = new();
    private static readonly List<GachaPool> pools = [];
    private static readonly GachaPool[] poolsById = new GachaPool[GachaPool.PoolCount];
    private static readonly Dictionary<int, BaseRecipe> recipeRewardIndex = [];
    private static int recipeRewardIndexRecipeCount;

    private static int cachedMatrixId;
    private static GachaFocusType cachedFocus = GachaFocusType.Balanced;
    private static GachaMode cachedMode = GachaMode.Normal;
    private static int cachedOpeningRecipeStateHash;


    public static bool IsSpeedrunMode => GachaManager.IsSpeedrunMode;
}
