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

    /// <summary>
    /// 建立基础自动配方、解析方向和高级多输入目录。
    /// </summary>
    public static void AddFracRecipes() {
#if DEBUG
        if (File.Exists(SPRITE_CSV_PATH)) {
            File.Delete(SPRITE_CSV_PATH);
        }
#endif
        UnlockedRecipeCache.Clear();
        RecipeList.Clear();
        StableRecipeIndex.Clear();
        PersistentRecipeIndex.Clear();
        InputRecipeIndex.Clear();
        OutputRecipeIndex.Clear();
        for (int i = 0; i < RecipeTypeArr.Length; i++) {
            RecipeTypeArr[i] = new BaseRecipe[12000];
        }

        AddBasicMineralRecipes();
        AddRareCatalysisRecipes();
        AddInteractionRecipes();
        AddReplacementRecipes();
        ResearchManager.BuildResearchCatalog();
        AddAnalysisDirections();
        LogInfo($"Added {RecipeList.Count} fractionation recipes.");
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
            IReadOnlyList<WorldviewRecipeOutput> outputs =
                Fractionators.FractionatorTowerCatalog.IsAdvancedTower(itemId)
                    ? [new WorldviewRecipeOutput(IFE记忆源点, 1f)]
                    : [];
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
                || itemId >= itemValue.Length || itemValue[itemId] >= maxValue) {
                continue;
            }
            AddRecipe(new WorldviewRecipe($"mineral-basic:{itemId}", 200000 + itemId,
                string.Format("{0}自身复制".Translate(), item.name), ERecipe.BasicMineral, WorldviewRecipeKind.BasicMineral, 0.05f,
                [new WorldviewRecipeInput(itemId, 1)], [new WorldviewRecipeOutput(itemId, 2f)]));
        }
    }

    private static void AddRareCatalysisRecipes() {
        AddRareRecipe(30101, I光栅石, [new(I石矿, 10)]);
        AddRareRecipe(30102, I分形硅石, [new(I硅石, 10)]);
        AddRareRecipe(30103, I金伯利矿石, [new(I煤矿, 10), new(I石矿, 10)]);
        AddRareRecipe(30104, I可燃冰, [new(I水, 10), new(I原油, 10)]);
        AddRareRecipe(30105, I刺笋结晶, [new(I植物燃料, 10), new(I煤矿, 10)]);
        AddRareRecipe(30106, I单极磁石, [
            new(I铁矿, 10),
            new(I钛石, 10),
            new(I光栅石, 2),
            new(I分形硅石, 2),
            new(I金伯利矿石, 2),
            new(I可燃冰, 2),
            new(I刺笋结晶, 2),
        ]);
        AddRecipe(new WorldviewRecipe("mineral-memory:1016", 30107,
            "单极磁石记忆重构".Translate(), ERecipe.AdvancedMineral,
            WorldviewRecipeKind.RareCatalysis, 0.2f,
            [new(IFE记忆源点, 1), new(I单极磁石, 1, 0.05f)],
            [new(I单极磁石, 20f)]) { AnalysisItemId = I单极磁石, ResearchStage = 6 });
    }

    private static void AddRareRecipe(int persistentId, int rareItemId,
        IReadOnlyList<WorldviewRecipeInput> commonInputs) {
        ItemProto rare = LDB.items.Select(rareItemId);
        if (rare == null) {
            return;
        }
        List<WorldviewRecipeInput> inputs = [..commonInputs, new(rareItemId, 1, 0.05f)];
        AddRecipe(new WorldviewRecipe($"mineral-rare:{rareItemId}", persistentId,
            string.Format("{0}催化增殖".Translate(), rare.name), ERecipe.AdvancedMineral, WorldviewRecipeKind.RareCatalysis, 0.2f,
            inputs, [new WorldviewRecipeOutput(rareItemId, 1f)]) {
                AnalysisItemId = rareItemId,
                ResearchStage = rareItemId == I单极磁石 ? 5 : 3,
            });
    }

    private static void AddInteractionRecipes() {
        int[] towers = [IFE高级矿物塔, IFE高级交互塔, IFE高级转化塔];
        for (int i = 0; i < towers.Length; i++) {
            int towerId = towers[i];
            ItemProto tower = LDB.items.Select(towerId);
            AddRecipe(new WorldviewRecipe($"interaction-culture:{towerId}", 30201 + i,
                string.Format("培育{0}".Translate(), tower?.name ?? towerId.ToString()),
                ERecipe.AdvancedInteraction,
                WorldviewRecipeKind.TowerCultivation, 0.2f,
                [new WorldviewRecipeInput(IFE通用原胚, 1)], [new WorldviewRecipeOutput(towerId, 1f)]) {
                    AnalysisItemId = IFE通用原胚, ResearchStage = towerId == IFE高级交互塔 ? 0 : 1,
                });
            AddRecipe(new WorldviewRecipe($"interaction-breed:{towerId}", 30211 + i,
                string.Format("{0}原胚繁育".Translate(), tower?.name ?? towerId.ToString()),
                ERecipe.AdvancedInteraction,
                WorldviewRecipeKind.EmbryoBreeding, 0.2f,
                [new WorldviewRecipeInput(towerId, 1), new WorldviewRecipeInput(IFE通用原胚, 1, 0.05f)],
                [new WorldviewRecipeOutput(IFE通用原胚, 1.5f)]) {
                    AnalysisItemId = towerId, ResearchStage = 3,
                });
        }
    }

    private static void AddReplacementRecipes() {
        AddRecipe(new WorldviewRecipe("conversion:fuel-hydrogen-deuterium", 30301,
            "液氢燃料棒重氢置换".Translate(), ERecipe.AdvancedConversion, WorldviewRecipeKind.Replacement, 0.2f,
            [new(I液氢燃料棒, 1), new(I重氢, 10)],
            [new(I氘核燃料棒, 1f), new(I氢, 5f, true)]));
        AddRecipe(new WorldviewRecipe("conversion:fuel-deuterium-antimatter", 30302,
            "氘核燃料棒反物质置换".Translate(), ERecipe.AdvancedConversion, WorldviewRecipeKind.Replacement, 0.2f,
            [new(I氘核燃料棒, 1), new(I反物质, 2), new(I湮灭约束球, 1)],
            [new(I反物质燃料棒, 1f), new(I重氢, 10f, true)]));
        AddRecipe(new WorldviewRecipe("conversion:mining-drill", 30303,
            "采矿机重构为大型采矿机".Translate(), ERecipe.AdvancedConversion, WorldviewRecipeKind.Replacement, 0.2f,
            [new(I采矿机, 1), new(I处理器, 1)],
            [new(I大型采矿机, 1f), new(I齿轮, 2f, true)]));
        AddRecipe(new WorldviewRecipe("conversion:smelter", 30304,
            "电弧熔炉重构为位面熔炉".Translate(), ERecipe.AdvancedConversion, WorldviewRecipeKind.Replacement, 0.2f,
            [new(I电弧熔炉, 1), new(I卡西米尔晶体, 1)],
            [new(I位面熔炉, 1f), new(I磁线圈, 2f, true)]));
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
