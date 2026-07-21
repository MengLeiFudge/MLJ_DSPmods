using System.Collections.Generic;
using FE.Logic.Civilization.Configuration;
using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Protocols;

/// <summary>
/// 根据当前配方目录生成不可变的基础协议定义。
/// </summary>
public static class ProtocolCatalog {
    private static readonly List<ProtocolDefinition> definitions = [];
    private static readonly Dictionary<RecipeKey, ProtocolDefinition> definitionsByRecipe = [];
    private static readonly Dictionary<string, List<ProtocolDefinition>> definitionsByStage = [];

    public static IReadOnlyList<ProtocolDefinition> All => definitions;

    public static void Initialize() {
        definitions.Clear();
        definitionsByRecipe.Clear();
        definitionsByStage.Clear();

        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        if (profile == null) {
            return;
        }

        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            if (!recipe.RequiresProtocolRecovery) {
                continue;
            }

            int stageOrder = GetProtocolStageOrder(recipe);
            bool countsTowardStageCompletion = stageOrder >= 0;
            MatrixStageDefinition stage = profile.GetStageByOrder(stageOrder) ?? profile.GetStageByOrder(0);
            if (stage == null) {
                continue;
            }

            // 黑雾协议在实际接触对应掉落后加入电磁检索池，但不阻塞六阶段主线。
            var definition = new ProtocolDefinition(RecipeKey.FromRecipe(recipe), stage.StageKey,
                countsTowardStageCompletion && recipe.CountsTowardStageCompletion);
            definitions.Add(definition);
            definitionsByRecipe[definition.RecipeKey] = definition;
            if (!definitionsByStage.TryGetValue(stage.StageKey, out List<ProtocolDefinition> stageDefinitions)) {
                stageDefinitions = [];
                definitionsByStage[stage.StageKey] = stageDefinitions;
            }
            stageDefinitions.Add(definition);
        }
    }

    public static ProtocolDefinition Get(RecipeKey recipeKey) =>
        definitionsByRecipe.TryGetValue(recipeKey, out ProtocolDefinition definition) ? definition : null;

    public static IReadOnlyList<ProtocolDefinition> GetByStage(string stageKey) =>
        stageKey != null && definitionsByStage.TryGetValue(stageKey, out List<ProtocolDefinition> stageDefinitions)
            ? stageDefinitions
            : [];

    public static bool IsStageComplete(string stageKey) {
        IReadOnlyList<ProtocolDefinition> stageDefinitions = GetByStage(stageKey);
        foreach (ProtocolDefinition definition in stageDefinitions) {
            if (!definition.CountsTowardStageCompletion) {
                continue;
            }
            if (!ProtocolProgressStore.IsComplete(definition.RecipeKey)) {
                return false;
            }
        }
        return true;
    }

    public static bool HasRequiredProtocols(string stageKey) {
        foreach (ProtocolDefinition definition in GetByStage(stageKey)) {
            if (definition.CountsTowardStageCompletion) {
                return true;
            }
        }
        return false;
    }

    public static int GetCompletedStageCount() {
        int count = 0;
        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        if (profile == null) {
            return 0;
        }
        int currentStageOrder = FE.Logic.Items.ItemManager.GetCurrentProgressStageIndex();
        foreach (MatrixStageDefinition stage in profile.Stages) {
            if (stage.Order <= currentStageOrder && IsStageComplete(stage.StageKey)) {
                count++;
            }
        }
        return count;
    }

    private static int GetProtocolStageOrder(BaseRecipe recipe) {
        if (recipe.ProtocolStageOrder >= 0) {
            return recipe.ProtocolStageOrder;
        }

        int stageOrder = FE.Logic.Items.ItemManager.GetMatrixStageIndex(recipe.MatrixID);
        if (recipe.RecipeType != ERecipe.Conversion) {
            return stageOrder;
        }

        foreach (OutputInfo output in recipe.OutputMain) {
            int outputStage = FE.Logic.Items.ItemManager.GetMatrixStageIndex(output.OutputID);
            if (outputStage >= 0) {
                stageOrder = System.Math.Max(stageOrder, outputStage);
            }
        }
        foreach (OutputInfo output in recipe.OutputAppend) {
            int outputStage = FE.Logic.Items.ItemManager.GetMatrixStageIndex(output.OutputID);
            if (outputStage >= 0) {
                stageOrder = System.Math.Max(stageOrder, outputStage);
            }
        }
        return stageOrder;
    }
}
