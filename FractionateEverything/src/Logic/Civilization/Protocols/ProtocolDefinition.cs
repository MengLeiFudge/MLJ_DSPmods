using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Protocols;

/// <summary>
/// 描述一项远古协议恢复的运行配方和所属文明阶段。
/// </summary>
public sealed class ProtocolDefinition(
    RecipeKey recipeKey,
    string stageKey,
    bool countsTowardStageCompletion = true) {
    public RecipeKey RecipeKey { get; } = recipeKey;
    public string StageKey { get; } = stageKey;
    public bool CountsTowardStageCompletion { get; } = countsTowardStageCompletion;
}
