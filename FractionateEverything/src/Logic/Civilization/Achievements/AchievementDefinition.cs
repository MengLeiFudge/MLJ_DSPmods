using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Achievements;

public enum AchievementConditionType {
    CompletedProtocols,
    CompletedStages,
    SpentTechPoints,
    FractionationSuccesses,
}

public enum AchievementRewardType {
    RecipeTypeSuccessRate,
    AllRecipeSuccessRate,
}

/// <summary>
/// 定义单存档成就的类型化条件和固定运行奖励。
/// </summary>
public sealed class AchievementDefinition(
    string achievementKey,
    string displayNameKey,
    AchievementConditionType conditionType,
    long target,
    AchievementRewardType rewardType,
    float rewardValue,
    ERecipe recipeType = 0) {
    public string AchievementKey { get; } = achievementKey;
    public string DisplayNameKey { get; } = displayNameKey;
    public AchievementConditionType ConditionType { get; } = conditionType;
    public long Target { get; } = target;
    public AchievementRewardType RewardType { get; } = rewardType;
    public float RewardValue { get; } = rewardValue;
    public ERecipe RecipeType { get; } = recipeType;
}
