namespace FE.Logic.RecipeGrowth;

public readonly struct RecipeDisplaySnapshot {
    public RecipeDisplaySnapshot(
        int level,
        int maxLevel,
        bool isUnlocked,
        bool isMaxed,
        int effectiveLegacyLevel,
        float remainInputRatio,
        float doubleOutputRatio,
        float destroyRatio) {
        Level = level;
        MaxLevel = maxLevel;
        IsUnlocked = isUnlocked;
        IsMaxed = isMaxed;
        EffectiveLegacyLevel = effectiveLegacyLevel;
        RemainInputRatio = remainInputRatio;
        DoubleOutputRatio = doubleOutputRatio;
        DestroyRatio = destroyRatio;
    }

    public int Level { get; }
    public int MaxLevel { get; }
    public bool IsUnlocked { get; }
    public bool IsMaxed { get; }
    public int EffectiveLegacyLevel { get; }
    public float RemainInputRatio { get; }
    public float DoubleOutputRatio { get; }
    public float DestroyRatio { get; }
}
