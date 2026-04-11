using FE.Logic.Recipe;

namespace FE.Logic.RecipeGrowth;

public readonly struct RecipeStatsSnapshot {
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

    public RecipeFamily Family { get; }
    public ERecipe RecipeType { get; }
    public int MatrixId { get; }
    public int TotalCount { get; }
    public int UnlockedCount { get; }
    public int MaxedCount { get; }
    public int TotalLevel { get; }
    public int TotalGrowthExp { get; }
    public int TotalPityProgress { get; }
}
