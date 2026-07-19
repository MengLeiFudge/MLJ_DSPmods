using System.Collections.Generic;
using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Achievements;

/// <summary>
/// 保存首版文明成就定义，不把条件和奖励闭包放入 UI。
/// </summary>
public static class AchievementCatalog {
    private static readonly List<AchievementDefinition> definitions = [];

    public static IReadOnlyList<AchievementDefinition> All => definitions;

    public static void Initialize() {
        definitions.Clear();
        definitions.Add(new("first-protocol", "文明成就-首项协议", AchievementConditionType.CompletedProtocols, 1,
            AchievementRewardType.RecipeTypeSuccessRate, 0.01f, ERecipe.MineralCopy));
        definitions.Add(new("first-stage", "文明成就-完整阶段", AchievementConditionType.CompletedStages, 1,
            AchievementRewardType.RecipeTypeSuccessRate, 0.01f, ERecipe.Conversion));
        definitions.Add(new("first-tech", "文明成就-首次科技投入", AchievementConditionType.SpentTechPoints, 1,
            AchievementRewardType.AllRecipeSuccessRate, 0.005f));
        definitions.Add(new("fractionation-1000", "文明成就-千次分馏", AchievementConditionType.FractionationSuccesses,
            1000, AchievementRewardType.AllRecipeSuccessRate, 0.005f));
    }
}
