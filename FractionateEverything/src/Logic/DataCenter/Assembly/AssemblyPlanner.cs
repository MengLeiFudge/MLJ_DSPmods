using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Fractionation.Recipes;

namespace FE.Logic.DataCenter.Assembly;

/// <summary>需求树上某条路径的用户决定；只取库存不允许该节点自行展开制造。</summary>
public sealed class AssemblyChoice {
    public bool StockOnly;
    public int RecipeId;
}

/// <summary>一次规划的只读需求行，数量全部为物品件数。</summary>
public sealed class AssemblyDemand {
    public string Path;
    public int Depth;
    public int ItemId;
    public long Required;
    public long Stock;
    public long Planned;
    public long Missing;
    public int RecipeId;
    public bool StockOnly;
    public bool Cycle;
}

/// <summary>规划只登记当前真实库存；尚未制造的产物不进入预留账本。</summary>
internal sealed class AssemblyPlan {
    public readonly List<AssemblyDemand> Rows = [];
    public readonly Dictionary<int, long> Reserved = [];
    public bool TooComplex;
}

/// <summary>确定性的配方选择与需求展开，支持明确配方、默认配方及已解锁工艺候选。</summary>
public static class AssemblyPlanner {
    private const int MaximumDepth = 32;
    private const int MaximumRows = 512;
    private const long MaximumMaterial = 1000000000L;
    private static Dictionary<int, RecipeProto[]> candidates = [];

    /// <summary>最终原型加载后构建工艺索引；资格在每次规划时检查，避免解锁缓存过期。</summary>
    public static void Initialize() {
        candidates = LDB.recipes.dataArray.Where(Supported)
            .SelectMany(recipe => recipe.Results.Distinct().Select(item => (item, recipe)))
            .GroupBy(pair => pair.item).ToDictionary(group => group.Key, group => group.Select(pair => pair.recipe).OrderBy(recipe => recipe.ID).ToArray());
    }

    private static bool Supported(RecipeProto recipe) {
        if (recipe == null || AdvancedRecipeCatalog.IsAdvancedProto(recipe.ID)
            || recipe.Type is not (ERecipeType.Assemble or ERecipeType.Smelt or ERecipeType.Chemical or ERecipeType.Refine or ERecipeType.Particle)
            || recipe.Items == null || recipe.Results == null || recipe.ItemCounts == null || recipe.ResultCounts == null
            || recipe.Items.Length is 0 or > 128 || recipe.Results.Length is 0 or > 128 || recipe.Items.Length != recipe.ItemCounts.Length
            || recipe.Results.Length != recipe.ResultCounts.Length || recipe.TimeSpend <= 0) { return false; }
        return recipe.Items.Concat(recipe.Results).All(id => id > 0 && id < 12000 && LDB.items.Exist(id))
            && recipe.ItemCounts.All(n => n > 0) && recipe.ResultCounts.All(n => n > 0);
    }

    public static RecipeProto[] GetCandidates(int itemId) => candidates.TryGetValue(itemId, out RecipeProto[] recipes)
        ? recipes.Where(recipe => GameMain.history?.RecipeUnlocked(recipe.ID) == true).ToArray() : [];

    internal static RecipeProto SelectRecipe(int itemId, AssemblyChoice choice) {
        RecipeProto[] recipes = GetCandidates(itemId);
        if (choice?.RecipeId > 0) { return recipes.FirstOrDefault(recipe => recipe.ID == choice.RecipeId); }
        int preferred = LDB.items.Select(itemId)?.maincraft?.ID ?? 0;
        return recipes.FirstOrDefault(recipe => recipe.ID == preferred) ?? recipes.FirstOrDefault();
    }

    internal static long Read(Dictionary<int, long> values, int id) => values.TryGetValue(id, out long value) ? value : 0;
    internal static void Add(Dictionary<int, long> values, int id, long count) {
        if (count < 0 || Read(values, id) > MaximumMaterial - count) { throw new OverflowException("装配物料需求超过规划范围。"); }
        values[id] = Read(values, id) + count;
    }

    /// <summary>根节点代表新制造的数量；子节点优先使用库存，副产物按实际工艺贡献后续需求。</summary>
    internal static AssemblyPlan Build(int itemId, int count, Dictionary<string, AssemblyChoice> choices,
        Dictionary<int, long> available) {
        var result = new AssemblyPlan();
        var stock = new Dictionary<int, long>(available);
        var virtualStock = new Dictionary<int, long>();
        var ancestry = new HashSet<int>();
        void Expand(int item, long need, string path, int depth) {
            if (depth > MaximumDepth || result.Rows.Count >= MaximumRows || need > MaximumMaterial) { result.TooComplex = true; return; }
            choices.TryGetValue(path, out AssemblyChoice choice);
            var row = new AssemblyDemand { Path = path, Depth = depth, ItemId = item, Required = need, StockOnly = choice?.StockOnly == true };
            result.Rows.Add(row);
            long virtualUsed = depth == 0 || row.StockOnly ? 0 : Math.Min(need, Read(virtualStock, item));
            virtualStock[item] = Read(virtualStock, item) - virtualUsed;
            need -= virtualUsed;
            row.Planned = virtualUsed;
            row.Stock = depth == 0 && !row.StockOnly ? 0 : Math.Min(need, Read(stock, item));
            stock[item] = Read(stock, item) - row.Stock;
            if (row.Stock > 0) { Add(result.Reserved, item, row.Stock); }
            need -= row.Stock;
            RecipeProto recipe = row.StockOnly ? null : SelectRecipe(item, choice);
            row.RecipeId = recipe?.ID ?? 0;
            if (need <= 0) { return; }
            if (recipe == null || ancestry.Contains(item)) {
                row.Missing = need; row.Cycle = ancestry.Contains(item); return;
            }
            row.Planned += need;
            long yield = recipe.Results.Select((id, i) => id == item ? (long)recipe.ResultCounts[i] : 0).Sum();
            long turns = checked((need + yield - 1) / yield);
            ancestry.Add(item);
            for (int i = 0; i < recipe.Items.Length; i++) { Expand(recipe.Items[i], checked(turns * recipe.ItemCounts[i]), path + "." + i, depth + 1); }
            ancestry.Remove(item);
            for (int i = 0; i < recipe.Results.Length; i++) {
                long amount = checked(turns * recipe.ResultCounts[i]);
                // 同一目标的重复结果先合计，避免每槽都减去同一份需求。
                if (recipe.Results[i] == item) { continue; }
                Add(virtualStock, recipe.Results[i], amount);
            }
            Add(virtualStock, item, checked(turns * yield - need));
        }
        try { Expand(itemId, count, "0", 0); }
        catch (OverflowException) { result.TooComplex = true; }
        if (result.TooComplex) { result.Reserved.Clear(); }
        return result;
    }

    /// <summary>以一轮根配方为目标深度优先寻找下一轮可运行工艺，避免先堆满整批中间物。</summary>
    internal static (RecipeProto Recipe, bool Root) Next(int itemId, Dictionary<string, AssemblyChoice> choices,
        Dictionary<int, long> owned, out bool blocked) {
        bool unavailable = false;
        var ancestry = new HashSet<int>();
        (RecipeProto Recipe, bool Root) Find(int item, string path, int depth) {
            if (depth > MaximumDepth || !ancestry.Add(item)) { unavailable = true; return default; }
            choices.TryGetValue(path, out AssemblyChoice choice);
            RecipeProto recipe = choice?.StockOnly == true ? null : SelectRecipe(item, choice);
            if (recipe == null) { unavailable = true; ancestry.Remove(item); return default; }
            var remaining = new Dictionary<int, long>(owned);
            for (int i = 0; i < recipe.Items.Length; i++) {
                int input = recipe.Items[i];
                long available = Read(remaining, input);
                if (available >= recipe.ItemCounts[i]) { remaining[input] = available - recipe.ItemCounts[i]; continue; }
                var child = Find(input, path + "." + i, depth + 1);
                ancestry.Remove(item); return child;
            }
            ancestry.Remove(item); return (recipe, depth == 0);
        }
        var result = Find(itemId, "0", 0); blocked = unavailable; return result;
    }
}
