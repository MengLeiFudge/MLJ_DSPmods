using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FE.Logic.Fractionation.Recipes;
using static FE.Logic.Civilization.ResearchManager;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>当前配方加载后构造的研究目录；不保存探索、解锁或解析进度。</summary>
internal sealed class ResearchCatalog {
    private readonly Dictionary<string, ResearchContentDefinition> definitions = [];
    private readonly Dictionary<int, List<string>> directionPools = [];
    private readonly Dictionary<string, List<int>> contentDirections = [];
    private readonly Dictionary<int, WorldviewRecipe[]> sampleRecipes = [];
    private WorldviewRecipe[] preparationRecipes = [];

    internal IReadOnlyDictionary<string, ResearchContentDefinition> Definitions { get; }
    internal IReadOnlyDictionary<int, IReadOnlyList<string>> Directions { get; }
    internal IReadOnlyDictionary<string, IReadOnlyList<int>> ContentDirections { get; }
    internal IReadOnlyDictionary<int, IReadOnlyList<WorldviewRecipe>> SampleRecipes { get; }
    internal IReadOnlyList<WorldviewRecipe> PreparationRecipes { get; }

    /// <summary>空目录用于原型尚未加载的生命周期阶段。</summary>
    internal static ResearchCatalog Empty { get; } = new(false);

    /// <summary>按原型顺序建立全部索引，再以只读视图发布。</summary>
    internal ResearchCatalog(bool populate = true) {
        if (populate) { Build(); }
        Definitions = new ReadOnlyDictionary<string, ResearchContentDefinition>(definitions);
        Directions = new ReadOnlyDictionary<int, IReadOnlyList<string>>(directionPools
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value.AsReadOnly()));
        ContentDirections = new ReadOnlyDictionary<string, IReadOnlyList<int>>(contentDirections
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value.AsReadOnly()));
        SampleRecipes = new ReadOnlyDictionary<int, IReadOnlyList<WorldviewRecipe>>(sampleRecipes
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<WorldviewRecipe>)System.Array.AsReadOnly(pair.Value)));
        PreparationRecipes = System.Array.AsReadOnly(preparationRecipes);
    }

    private void Build() {
        foreach (WorldviewRecipe recipe in RecipeManager.WorldviewRecipes) {
            if (recipe.Kind == WorldviewRecipeKind.BasicAnalysis) {
                continue;
            }

            int tier = recipe.ResearchStage;
            AddContentDefinition(new ResearchContentDefinition(recipe.StableId, recipe.DisplayName, tier));
            AddToPool(recipe.AnalysisItemId, recipe.StableId);
        }

        AddContentDefinition(new ResearchContentDefinition(AdvancedMineralTraitId,
            "FE高级矿物特质".Translate(), 4));
        AddContentDefinition(new ResearchContentDefinition(AdvancedInteractionTraitId,
            "FE高级交互特质".Translate(), 4));
        AddContentDefinition(new ResearchContentDefinition(AdvancedConversionTraitId,
            "FE高级转化特质".Translate(), 4));
        AddToPool(IFE高级矿物塔, AdvancedMineralTraitId);
        AddToPool(IFE高级交互塔, AdvancedInteractionTraitId);
        AddToPool(IFE高级转化塔, AdvancedConversionTraitId);
        AddStageContentDefinitions();
        BuildUnlockCatalog();
    }

    private void AddContentDefinition(ResearchContentDefinition definition) {
        definitions[definition.Id] = definition;
    }

    private void AddTechnologyContentDefinitions() {
        AddTechnologyContent(GrowthTechnologyManager.StackTechnologyContentId,
            "FE分馏堆叠成长".Translate(), 0);
        AddTechnologyContent(GrowthTechnologyManager.PlanetaryInteractionTechnologyContentId,
            "T行星内物流交互".Translate(), 2);
        AddTechnologyContent(GrowthTechnologyManager.SubspaceEfficiencyTechnologyContentId,
            "FE亚空间能效".Translate(), 2);
        AddTechnologyContent(GrowthTechnologyManager.InterstellarInteractionTechnologyContentId,
            "T星际物流交互".Translate(), 3);
    }

    private void AddTechnologyContent(string contentId, string displayName, int tier) {
        AddContentDefinition(new ResearchContentDefinition(contentId, displayName, tier));
        AddToPool(IFE记忆源点, contentId);
    }

    private void AddToPool(int itemId, string contentId) {
        if (itemId <= 0) {
            return;
        }
        if (!directionPools.TryGetValue(itemId, out List<string> pool)) {
            pool = [];
            directionPools[itemId] = pool;
        }
        if (pool.Contains(contentId)) {
            return;
        }
        pool.Add(contentId);
        if (!contentDirections.TryGetValue(contentId, out List<int> directions)) {
            directions = [];
            contentDirections[contentId] = directions;
        }
        directions.Add(itemId);
    }

    /// <summary>将阶段内容注册到目录，物流方向由任务直接恢复。</summary>
    private void AddStageContentDefinitions() {
        AddTechnologyContentDefinitions();
        for (int stage = 1; stage <= 6; stage++) {
            AddTechnologyContent(ResearchManager.StageContentId(stage),
                string.Format("{0}文明记录".Translate(), LDB.items.Select(ResearchManager.GetStageMatrix(stage))?.name), stage);
        }
    }

    /// <summary>建立百科残页、实物样本和置换制样目录，不改变任何游玩进度。</summary>
    private void BuildUnlockCatalog() {
        for (int i = 1; i <= 3; i++) {
            AddContentDefinition(new ResearchContentDefinition($"encyclopedia:{i}", "FE.Encyclopedia.Title".Translate(), 0));
        }
        sampleRecipes.Clear();
        foreach (var group in RecipeManager.WorldviewRecipes
                     .Where(recipe => recipe.Kind != WorldviewRecipeKind.BasicAnalysis
                         && recipe.UnlockTier == RecipeUnlockTier.Rare && recipe.AnalysisItemId > 0)
                     .GroupBy(recipe => recipe.AnalysisItemId)) {
            sampleRecipes.Add(group.Key, group.OrderBy(recipe => recipe.PersistentId).ToArray());
        }
        preparationRecipes = RecipeManager.WorldviewRecipes
            .Where(recipe => recipe.Kind == WorldviewRecipeKind.Replacement)
            .OrderBy(recipe => recipe.PersistentId).ToArray();
    }
}
