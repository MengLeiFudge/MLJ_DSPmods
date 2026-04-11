using FE.Logic.Recipe;

namespace FE.Logic.RecipeGrowth;

public readonly struct DarkFogRecipeProgressSnapshot {
    public DarkFogRecipeProgressSnapshot(
        ERecipe recipeType,
        int inputId,
        int level,
        int maxLevel,
        int growthExp,
        int pityProgress,
        int tier,
        float processMultiplier,
        float catchupMultiplier,
        bool isUnlocked,
        bool isMaxed) {
        RecipeType = recipeType;
        InputId = inputId;
        Level = level;
        MaxLevel = maxLevel;
        GrowthExp = growthExp;
        PityProgress = pityProgress;
        Tier = tier;
        ProcessMultiplier = processMultiplier;
        CatchupMultiplier = catchupMultiplier;
        IsUnlocked = isUnlocked;
        IsMaxed = isMaxed;
    }

    public ERecipe RecipeType { get; }
    public int InputId { get; }
    public int Level { get; }
    public int MaxLevel { get; }
    public int GrowthExp { get; }
    public int PityProgress { get; }
    public int Tier { get; }
    public float ProcessMultiplier { get; }
    public float CatchupMultiplier { get; }
    public bool IsUnlocked { get; }
    public bool IsMaxed { get; }
}
