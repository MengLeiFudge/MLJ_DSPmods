using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Logic.Civilization;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 配方创建、稳定身份索引和已解析配方查询入口。
/// </summary>
public static class RecipeManager {
    private static readonly List<BaseRecipe> RecipeList = [];
    private static readonly Dictionary<string, WorldviewRecipe> StableRecipeIndex = [];
    private static readonly Dictionary<int, WorldviewRecipe> PersistentRecipeIndex = [];
    private static readonly Dictionary<int, List<WorldviewRecipe>> InputRecipeIndex = [];
    private static readonly Dictionary<int, List<WorldviewRecipe>> OutputRecipeIndex = [];
    /// <summary>主线程界面使用的高级配方缓存，按塔型和研究快照版本失效。</summary>
    private static readonly Dictionary<int, (long Version, IReadOnlyList<WorldviewRecipe> Recipes)> UnlockedRecipeCache = [];
    private static readonly HashSet<int> RareMineralIds = [
        I可燃冰,
        I金伯利矿石,
        I分形硅石,
        I光栅石,
        I刺笋结晶,
        I有机晶体,
        I单极磁石,
    ];

    private static readonly BaseRecipe[][] RecipeTypeArr =
        new BaseRecipe[Enum.GetValues(typeof(ERecipe)).Cast<int>().Max() + 1][];

#if DEBUG
    private const string SPRITE_CSV_PATH = @"D:\project\dsp\MLJ_DSPmods\gamedata\fracIconPath.csv";
#endif

    /// <summary>
    /// 获取全部已注册配方。
    /// </summary>
    public static IReadOnlyList<BaseRecipe> AllRecipes => RecipeList;

    /// <summary>
    /// 获取全部当前稳定配方。
    /// </summary>
    public static IEnumerable<WorldviewRecipe> WorldviewRecipes => StableRecipeIndex.Values;

    /// <summary>在LDB预注册阶段建立高级配方展示原型；此时不依赖物品估值和研究状态。</summary>
    public static void RegisterAdvancedRecipeProtos() {
        for (int i = 0; i < RecipeTypeArr.Length; i++) {
            RecipeTypeArr[i] = new BaseRecipe[12000];
        }
        AddRareCatalysisRecipes();
        AddInteractionRecipes();
        AddReplacementRecipes();
        AdvancedRecipeCatalog.RegisterProtos(WorldviewRecipes);
    }

    /// <summary>
    /// 建立基础自动配方、解析方向和高级多输入目录。
    /// </summary>
    public static void AddFracRecipes() {
#if DEBUG
        if (File.Exists(SPRITE_CSV_PATH)) {
            File.Delete(SPRITE_CSV_PATH);
        }
#endif
        ClearCatalog();

        AddBasicMineralRecipes();
        AddRareCatalysisRecipes();
        AddInteractionRecipes();
        AddReplacementRecipes();
        FE.Logic.Civilization.Monument.MonumentCatalog.AddRecipes();
        FilterUnavailableRecipes();
        // 物品分类已完成，置换线索按主产物阶段开放；展示原型预注册不依赖此阶段。
        foreach (WorldviewRecipe recipe in WorldviewRecipes.Where(recipe => recipe.Kind == WorldviewRecipeKind.Replacement)) {
            recipe.ResearchStage = Math.Max(recipe.ResearchStage, GetMatrixStageIndex(itemToMatrix[recipe.PrimaryProductId]) + 1);
        }
        ResearchManager.BuildResearchCatalog();
        AddAnalysisDirections();
        LogInfo($"Added {RecipeList.Count} fractionation recipes.");
    }

    /// <summary>所有运行索引共用一次重建，避免禁用配方仍留在旧塔型或输入查询中。</summary>
    private static void ClearCatalog() {
        UnlockedRecipeCache.Clear();
        RecipeList.Clear();
        StableRecipeIndex.Clear();
        PersistentRecipeIndex.Clear();
        InputRecipeIndex.Clear();
        OutputRecipeIndex.Clear();
        for (int i = 0; i < RecipeTypeArr.Length; i++) { RecipeTypeArr[i] = new BaseRecipe[12000]; }
    }

    /// <summary>按最终来源闭包移除不能启动的配方，随后才创建解析样本目录。</summary>
    private static void FilterUnavailableRecipes() {
        FE.Logic.Items.ItemAvailability.Rebuild(WorldviewRecipes);
        WorldviewRecipe[] candidates = WorldviewRecipes.ToArray();
        ClearCatalog();
        foreach (WorldviewRecipe recipe in candidates) {
            bool available = recipe.Inputs.All(input => FE.Logic.Items.ItemAvailability.IsReachable(input.ItemId))
                && recipe.Products.All(output => LDB.items.Exist(output.ItemId))
                && FE.Logic.Items.ItemAvailability.IsReachable(recipe.AnalysisItemId);
            if (available) { AddRecipe(recipe); }
            else { LogInfo($"跳过无合法来源的分馏配方：{recipe.StableId}"); }
        }
        AdvancedRecipeCatalog.HideUnavailableRecipes();
    }

    /// <summary>
    /// 注册配方；旧型配方调用方仍可使用该入口。
    /// </summary>
    public static void AddRecipe<T>(T recipe) where T : BaseRecipe {
        if (recipe == null || recipe.InputID <= 0) {
            return;
        }

        RecipeList.Add(recipe);
        int typeIndex = (int)recipe.RecipeType;
        if (typeIndex > 0 && typeIndex < RecipeTypeArr.Length && recipe.InputID < RecipeTypeArr[typeIndex].Length
            && RecipeTypeArr[typeIndex][recipe.InputID] == null) {
            RecipeTypeArr[typeIndex][recipe.InputID] = recipe;
        }

        if (recipe is WorldviewRecipe worldviewRecipe) {
            if (StableRecipeIndex.ContainsKey(worldviewRecipe.StableId)) {
                throw new InvalidOperationException($"Duplicate FE recipe stable ID: {worldviewRecipe.StableId}");
            }
            StableRecipeIndex.Add(worldviewRecipe.StableId, worldviewRecipe);
            if (PersistentRecipeIndex.ContainsKey(worldviewRecipe.PersistentId)) {
                throw new InvalidOperationException($"Duplicate FE recipe persistent ID: {worldviewRecipe.PersistentId}");
            }
            PersistentRecipeIndex.Add(worldviewRecipe.PersistentId, worldviewRecipe);
            foreach (WorldviewRecipeInput input in worldviewRecipe.Inputs) {
                AddRecipeIndex(InputRecipeIndex, input.ItemId, worldviewRecipe);
            }
            foreach (WorldviewRecipeOutput output in worldviewRecipe.Products) {
                AddRecipeIndex(OutputRecipeIndex, output.ItemId, worldviewRecipe);
            }
        }
    }

    /// <summary>
    /// 按旧塔型与单一输入查询配方。
    /// </summary>
    public static T GetRecipe<T>(ERecipe recipeType, int inputId) where T : BaseRecipe {
        int typeIndex = (int)recipeType;
        if (typeIndex <= 0 || typeIndex >= RecipeTypeArr.Length || inputId <= 0
            || inputId >= RecipeTypeArr[typeIndex].Length) {
            return null;
        }
        return RecipeTypeArr[typeIndex][inputId] as T;
    }

    /// <summary>
    /// 按稳定字符串 ID 查询配方。
    /// </summary>
    public static WorldviewRecipe GetRecipe(string stableId) =>
        stableId != null && StableRecipeIndex.TryGetValue(stableId, out WorldviewRecipe recipe) ? recipe : null;

    /// <summary>
    /// 按稳定整数 ID 查询配方。
    /// </summary>
    public static WorldviewRecipe GetRecipe(int persistentId) =>
        PersistentRecipeIndex.TryGetValue(persistentId, out WorldviewRecipe recipe) ? recipe : null;

    /// <summary>
    /// 查询指定塔当前可选择的高级配方。
    /// </summary>
    /// <param name="buildingItemId">塔物品 ID；仅由主线程界面查询。</param>
    /// <returns>同一研究快照下复用的只读配方列表。</returns>
    public static IReadOnlyList<WorldviewRecipe> GetUnlockedRecipesForTower(int buildingItemId) {
        long version = ResearchManager.AnalysisSnapshotVersion;
        if (UnlockedRecipeCache.TryGetValue(buildingItemId, out var cached) && cached.Version == version) {
            return cached.Recipes;
        }
        ERecipe type = Fractionators.FractionatorTowerCatalog.GetRecipeType(buildingItemId);
        IReadOnlyList<WorldviewRecipe> recipes = StableRecipeIndex.Values
            .Where(recipe => recipe.IsAdvanced && recipe.RecipeType == type
                             && ResearchManager.IsContentUnlocked(recipe.StableId))
            .OrderBy(recipe => recipe.PersistentId)
            .ToArray();
        UnlockedRecipeCache[buildingItemId] = (version, recipes);
        return recipes;
    }

    /// <summary>
    /// 查询使用指定输入的稳定配方。
    /// </summary>
    public static IReadOnlyList<WorldviewRecipe> GetRecipesUsingInput(int itemId) =>
        InputRecipeIndex.TryGetValue(itemId, out List<WorldviewRecipe> recipes) ? recipes : [];

    /// <summary>
    /// 查询产出指定物品的稳定配方。
    /// </summary>
    public static IReadOnlyList<WorldviewRecipe> GetRecipesProducing(int itemId) =>
        OutputRecipeIndex.TryGetValue(itemId, out List<WorldviewRecipe> recipes) ? recipes : [];

    /// <summary>
    /// 切档时清除依赖研究状态的可选配方缓存。
    /// </summary>
    public static void IntoOtherSave() {
        UnlockedRecipeCache.Clear();
    }

    private static void AddAnalysisDirections() {
        foreach (int itemId in ResearchManager.GetAnalyzableItemIds()) {
            ItemProto item = LDB.items.Select(itemId);
            if (item == null || item.ID <= 0 || item.ID >= 12000) {
                continue;
            }
            IReadOnlyList<WorldviewRecipeOutput> outputs = ResearchManager.GetAnalysisProductIds(itemId)
                .Select(id => new WorldviewRecipeOutput(id, 1f)).ToArray();
            AddRecipe(new WorldviewRecipe($"analysis:{item.ID}", 100000 + item.ID,
                string.Format("{0}解析".Translate(), item.name), ERecipe.BasicAnalysis,
                WorldviewRecipeKind.BasicAnalysis, 0.02f,
                [new WorldviewRecipeInput(item.ID, 1)], outputs));
        }
    }

    private static void AddBasicMineralRecipes() {
        HashSet<int> candidates = [I木材, I植物燃料, I铁矿, I铜矿, I硅石, I钛石, I石矿, I煤矿,
            I水, I原油, I硫酸, I氢, I重氢];
        foreach (VeinProto vein in LDB.veins.dataArray) {
            if (vein != null && vein.MiningItem > 0) {
                candidates.Add(vein.MiningItem);
            }
        }
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item != null && item.Type == EItemType.Resource) {
                candidates.Add(item.ID);
            }
        }

        foreach (int itemId in candidates.OrderBy(id => id)) {
            ItemProto item = LDB.items.Select(itemId);
            if (item == null || RareMineralIds.Contains(itemId) || itemId is >= I黑雾矩阵 and <= I能量碎片
                || IsProgressionResource(itemId) || itemId >= itemValue.Length || itemValue[itemId] >= maxValue) {
                continue;
            }
            AddRecipe(new WorldviewRecipe($"mineral-basic:{itemId}", 200000 + itemId,
                string.Format("{0}自身复制".Translate(), item.name), ERecipe.BasicMineral, WorldviewRecipeKind.BasicMineral, 0.04f,
                [new WorldviewRecipeInput(itemId, 1)], [new WorldviewRecipeOutput(itemId, 3f)]));
        }
    }

    /// <summary>注册珍奇催化增殖及对应的源点重构配方，保留已发布的稳定身份。</summary>
    private static void AddRareCatalysisRecipes() {
        AddRareRecipe(30101, I光栅石, 0.02f, 6, [new(I石矿, 3)]);
        AddRareRecipe(30102, I分形硅石, 0.04f, 6, [new(I硅石, 3), new(I石矿, 3)]);
        AddRareRecipe(30103, I金伯利矿石, 0.02f, 6, [new(I煤矿, 3)]);
        AddRareRecipe(30104, I可燃冰, 0.02f, 6, [new(I原油, 3)]);
        AddRareRecipe(30105, I刺笋结晶, 0.04f, 6, [new(I植物燃料, 3), new(I煤矿, 3)]);
        AddRareRecipe(30106, I单极磁石, 0.04f, 3, [new(I分形硅石, 1), new(I有机晶体, 1), new(I刺笋结晶, 1)]);
        AddRareRecipe(30108, I有机晶体, 0.04f, 6, [new(I原油, 3), new(I水, 3)]);

        AddRareMemoryRecipe(30107, I单极磁石, 20);
        AddRareMemoryRecipe(30109, I分形硅石, 60);
        AddRareMemoryRecipe(30110, I有机晶体, 60);
        AddRareMemoryRecipe(30111, I刺笋结晶, 60);
        AddRareMemoryRecipe(30112, I可燃冰, 120);
        AddRareMemoryRecipe(30113, I金伯利矿石, 120);
        AddRareMemoryRecipe(30114, I光栅石, 120);
    }

    /// <summary>以指定原料和同种珍奇催化剂注册稀有增殖配方。</summary>
    /// <param name="persistentId">配方跨存档和联机稳定的整数身份。</param>
    /// <param name="rareItemId">兼作催化剂、解析样本和产物的珍奇物品。</param>
    /// <param name="successProbability">配方基础概率，只控制周转，不承担科技产量成长。</param>
    /// <param name="productCount">每成功轮基础产量。</param>
    /// <param name="rawInputs">成功轮消耗、失败轮回流的主原料。</param>
    private static void AddRareRecipe(int persistentId, int rareItemId, float successProbability, int productCount,
        IReadOnlyList<WorldviewRecipeInput> rawInputs) {
        ItemProto rare = LDB.items.Select(rareItemId);
        if (rare == null) {
            return;
        }
        List<WorldviewRecipeInput> inputs = [..rawInputs, new(rareItemId, 1, 0.05f)];
        AddRecipe(new WorldviewRecipe($"mineral-rare:{rareItemId}", persistentId,
            string.Format("{0}催化增殖".Translate(), rare.name), ERecipe.AdvancedMineral, WorldviewRecipeKind.RareCatalysis, successProbability,
            inputs, [new WorldviewRecipeOutput(rareItemId, productCount)]) {
                AnalysisItemId = rareItemId,
                ResearchStage = rareItemId == I单极磁石 ? 5 : 3,
            });
    }

    /// <summary>以一件源点和同种珍奇催化剂注册白糖阶段的传说重构配方。</summary>
    /// <param name="persistentId">配方跨存档和联机稳定的整数身份。</param>
    /// <param name="rareItemId">兼作催化剂和产物的珍奇物品。</param>
    /// <param name="productCount">每成功轮新产出的数量，催化剂损耗另行结算。</param>
    private static void AddRareMemoryRecipe(int persistentId, int rareItemId, int productCount) {
        ItemProto rare = LDB.items.Select(rareItemId);
        if (rare == null) {
            return;
        }
        AddRecipe(new WorldviewRecipe($"mineral-memory:{rareItemId}", persistentId,
            string.Format("{0}记忆重构".Translate(), rare.name), ERecipe.AdvancedMineral,
            WorldviewRecipeKind.RareCatalysis, 0.08f,
            [new(IFE记忆源点, 1), new(rareItemId, 1, 0.05f)],
            [new(rareItemId, productCount)]) { AnalysisItemId = rareItemId, ResearchStage = 6 });
    }

    private static void AddInteractionRecipes() {
        int[] towers = [IFE高级矿物塔, IFE高级交互塔, IFE高级转化塔];
        for (int i = 0; i < towers.Length; i++) {
            int towerId = towers[i];
            ItemProto tower = LDB.items.Select(towerId);
            string towerName = (towerId switch {
                IFE高级矿物塔 => "高级矿物塔",
                IFE高级交互塔 => "培养塔",
                _ => "转化塔",
            }).Translate();
            AddRecipe(new WorldviewRecipe($"interaction-culture:{towerId}", 30201 + i,
                string.Format("培育{0}".Translate(), tower?.name ?? towerName),
                ERecipe.AdvancedInteraction,
                WorldviewRecipeKind.TowerCultivation, 0.08f,
                [new WorldviewRecipeInput(IFE通用原胚, 1)], [new WorldviewRecipeOutput(towerId, 1f)]) {
                    AnalysisItemId = IFE通用原胚, ResearchStage = towerId == IFE高级交互塔 ? 0 : 1,
                });
            AddRecipe(new WorldviewRecipe($"interaction-breed:{towerId}", 30211 + i,
                string.Format("{0}原胚繁育".Translate(), tower?.name ?? towerName),
                ERecipe.AdvancedInteraction,
                WorldviewRecipeKind.EmbryoBreeding, 0.08f,
                [new WorldviewRecipeInput(towerId, 1), new WorldviewRecipeInput(IFE通用原胚, 1, 0.05f)],
                [new WorldviewRecipeOutput(IFE通用原胚, 1.5f)]) {
                    AnalysisItemId = towerId, ResearchStage = 3,
                });
        }
    }

    private static void AddReplacementRecipes() {
        foreach (ConversionSpec spec in ConversionSpec.CreateAll()) {
            if (spec.Inputs.Any(input => !LDB.items.Exist(input.Item))
                || spec.Outputs.Any(output => !LDB.items.Exist(output.Item))) { continue; }
            string name = string.Format("FE.Conversion.PairName".Translate(),
                LDB.items.Select(spec.Inputs[0].Item).name, LDB.items.Select(spec.Outputs[0].Item).name);
            AddRecipe(new WorldviewRecipe(spec.Key, spec.Id, name, ERecipe.AdvancedConversion,
                WorldviewRecipeKind.Replacement, ConversionSpec.Success,
                spec.Inputs.Select(input => new WorldviewRecipeInput(input.Item, input.Count, input.Consume)).ToArray(),
                spec.Outputs.Select(output => new WorldviewRecipeOutput(output.Item, output.Count, output.Recovery)).ToArray()) {
                ResearchStage = spec.Stage,
            });
        }
    }

    private static void AddRecipeIndex(Dictionary<int, List<WorldviewRecipe>> index, int itemId,
        WorldviewRecipe recipe) {
        if (!index.TryGetValue(itemId, out List<WorldviewRecipe> recipes)) {
            recipes = [];
            index[itemId] = recipes;
        }
        recipes.Add(recipe);
    }
}
