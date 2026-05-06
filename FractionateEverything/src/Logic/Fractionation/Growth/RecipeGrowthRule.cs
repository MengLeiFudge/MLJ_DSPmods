namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 单个配方家族的成长模式、上限和倍率配置。
/// </summary>
public readonly struct RecipeGrowthRule {
    public RecipeGrowthRule(
        RecipeFamily family,
        RecipeGrowthMode growthMode,
        int maxLevel,
        int defaultLevel,
        int techBaselineLevel,
        int drawUnlockLevel,
        bool fixedMaxReward,
        bool usesGrowthExp,
        bool usesPity) {
        Family = family;
        GrowthMode = growthMode;
        MaxLevel = maxLevel;
        DefaultLevel = defaultLevel;
        TechBaselineLevel = techBaselineLevel;
        DrawUnlockLevel = drawUnlockLevel;
        FixedMaxReward = fixedMaxReward;
        UsesGrowthExp = usesGrowthExp;
        UsesPity = usesPity;
    }

    public RecipeFamily Family { get; }
    public RecipeGrowthMode GrowthMode { get; }
    public int MaxLevel { get; }
    public int DefaultLevel { get; }
    public int TechBaselineLevel { get; }
    public int DrawUnlockLevel { get; }
    public bool FixedMaxReward { get; }
    public bool UsesGrowthExp { get; }
    public bool UsesPity { get; }
}
