namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 单个配方家族的成长模式、上限和倍率配置。
/// </summary>
public readonly struct RecipeGrowthRule {
    /// <summary>
    /// 初始化 RecipeGrowthRule 的新实例。
    /// </summary>
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

    /// <summary>
    /// 获取该配方所属的成长家族。
    /// </summary>
    public RecipeFamily Family { get; }
    /// <summary>
    /// 获取该规则使用的成长模式。
    /// </summary>
    public RecipeGrowthMode GrowthMode { get; }
    /// <summary>
    /// 获取该规则或快照允许的最高等级。
    /// </summary>
    public int MaxLevel { get; }
    /// <summary>
    /// 获取该规则在默认状态下使用的等级。
    /// </summary>
    public int DefaultLevel { get; }
    /// <summary>
    /// 获取该规则由科技进度提供的基础等级。
    /// </summary>
    public int TechBaselineLevel { get; }
    /// <summary>
    /// 获取该规则由抽取解锁提供的等级。
    /// </summary>
    public int DrawUnlockLevel { get; }
    /// <summary>
    /// 判断该规则是否使用固定满级奖励。
    /// </summary>
    public bool FixedMaxReward { get; }
    /// <summary>
    /// 判断该规则是否使用成长经验。
    /// </summary>
    public bool UsesGrowthExp { get; }
    /// <summary>
    /// 判断该规则是否使用保底进度。
    /// </summary>
    public bool UsesPity { get; }
}
