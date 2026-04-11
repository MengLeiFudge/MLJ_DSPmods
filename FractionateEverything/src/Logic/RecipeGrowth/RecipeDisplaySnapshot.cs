using FE.Logic.Recipe;

namespace FE.Logic.RecipeGrowth;

public readonly struct RecipeDisplaySnapshot {
    public RecipeDisplaySnapshot(
        ERecipe recipeType,
        int inputId,
        RecipeFamily family,
        int level,
        int maxLevel,
        bool isUnlocked,
        bool isMaxed,
        int effectiveLegacyLevel,
        int growthExp,
        int pityProgress,
        string[] levelDescriptions,
        float remainInputRatio,
        float doubleOutputRatio,
        float destroyRatio) {
        RecipeType = recipeType;
        InputId = inputId;
        Family = family;
        Level = level;
        MaxLevel = maxLevel;
        IsUnlocked = isUnlocked;
        IsMaxed = isMaxed;
        EffectiveLegacyLevel = effectiveLegacyLevel;
        GrowthExp = growthExp;
        PityProgress = pityProgress;
        LevelDescriptions = levelDescriptions;
        RemainInputRatio = remainInputRatio;
        DoubleOutputRatio = doubleOutputRatio;
        DestroyRatio = destroyRatio;
    }

    public ERecipe RecipeType { get; }
    public int InputId { get; }
    public RecipeFamily Family { get; }
    public int Level { get; }
    public int MaxLevel { get; }
    public bool IsUnlocked { get; }
    public bool IsMaxed { get; }
    public int EffectiveLegacyLevel { get; }
    public int GrowthExp { get; }
    public int PityProgress { get; }
    public string[] LevelDescriptions { get; }
    public float RemainInputRatio { get; }
    public float DoubleOutputRatio { get; }
    public float DestroyRatio { get; }
}
