using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 配方统计页面使用的成长与产出快照。
/// </summary>
public readonly struct RecipeStatsSnapshot {
    /// <summary>
    /// 初始化 RecipeStatsSnapshot 的新实例。
    /// </summary>
    public RecipeStatsSnapshot(
        RecipeFamily family,
        ERecipe recipeType,
        int matrixId,
        int totalCount,
        int unlockedCount,
        int maxedCount,
        int totalLevel,
        int totalGrowthExp,
        int totalPityProgress) {
        Family = family;
        RecipeType = recipeType;
        MatrixId = matrixId;
        TotalCount = totalCount;
        UnlockedCount = unlockedCount;
        MaxedCount = maxedCount;
        TotalLevel = totalLevel;
        TotalGrowthExp = totalGrowthExp;
        TotalPityProgress = totalPityProgress;
    }

    /// <summary>
    /// 获取该配方所属的成长家族。
    /// </summary>
    public RecipeFamily Family { get; }
    /// <summary>
    /// 获取该配方所属的分馏配方类型。
    /// </summary>
    public ERecipe RecipeType { get; }
    /// <summary>
    /// 获取该统计快照对应的矩阵阶段 ID。
    /// </summary>
    public int MatrixId { get; }
    /// <summary>
    /// 获取该统计分组包含的配方总数。
    /// </summary>
    public int TotalCount { get; }
    /// <summary>
    /// 获取该统计分组已解锁的配方数量。
    /// </summary>
    public int UnlockedCount { get; }
    /// <summary>
    /// 获取该统计分组已满级的配方数量。
    /// </summary>
    public int MaxedCount { get; }
    /// <summary>
    /// 获取该统计分组累计等级。
    /// </summary>
    public int TotalLevel { get; }
    /// <summary>
    /// 获取该统计分组累计成长经验。
    /// </summary>
    public int TotalGrowthExp { get; }
    /// <summary>
    /// 获取该统计分组累计保底进度。
    /// </summary>
    public int TotalPityProgress { get; }
}
