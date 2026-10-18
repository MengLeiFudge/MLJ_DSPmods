using System;
using System.Collections.Generic;
using System.Linq;

using CommonAPI.Systems;

using FE.Logic.Civilization;

using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>把高级塔运行配方映射到原版选择器；展示原型不承担生产或研究结算。</summary>
public static class AdvancedRecipeCatalog {
    private static readonly Dictionary<int, int> PersistentIds = [];
    private static GameHistoryData projectedHistory;
    private static long projectedVersion = long.MinValue;

    /// <summary>在LDB预注册阶段创建按塔型分行的配方；运行身份与原型身份相互独立。</summary>
    /// <param name="recipes">已经建立、尚不依赖物品估值的高级配方目录。</param>
    public static void RegisterProtos(IEnumerable<WorldviewRecipe> recipes) {
        Dictionary<ERecipe, int> types = [];
        Dictionary<ERecipe, int> columns = [];
        foreach (WorldviewRecipe recipe in recipes.Where(recipe => recipe.IsAdvanced).OrderBy(recipe => recipe.PersistentId)) {
            if (!types.TryGetValue(recipe.RecipeType, out int type)) {
                type = AssemblerRecipeSystem.RegisterRecipeType($"{PluginInfo.PLUGIN_GUID}:{recipe.RecipeType}");
                types.Add(recipe.RecipeType, type);
            }
            int id = GetProtoId(recipe.PersistentId);
            if (LDB.recipes.Exist(id)) {
                throw new InvalidOperationException($"FE advanced recipe proto ID conflict: {id}");
            }
            columns.TryGetValue(recipe.RecipeType, out int column);
            columns[recipe.RecipeType] = ++column;
            int row = recipe.RecipeType switch {
                ERecipe.AdvancedMineral => 2,
                ERecipe.AdvancedInteraction => 4,
                ERecipe.AdvancedConversion => 5,
                _ => throw new InvalidOperationException($"Unsupported advanced recipe type: {recipe.RecipeType}"),
            };
            // 名称和说明在 Awake 统一加载；此阶段晚于语言键索引建立，不能再注册翻译。
            string nameKey = $"FE.AdvancedRecipe.{id}";
            string descriptionKey = nameKey + ".Description";
            // 原版数量字段仅支持整数；繁育按两轮的物料比例展示，真实概率与催化剂规则写在说明中。
            int scale = recipe.ProductArray.Any(output => output.Count != Math.Floor(output.Count)) ? 2 : 1;

            RecipeProto proto = ProtoRegistry.RegisterRecipe(id, type, 60,
                recipe.InputArray.Select(input => input.ItemId).ToArray(),
                recipe.InputArray.Select(input => input.Count * scale).ToArray(),
                recipe.ProductArray.Select(output => output.ItemId).ToArray(),
                recipe.ProductArray.Select(output => (int)Math.Round(output.Count * scale)).ToArray(),
                descriptionKey, 0, tab分馏 * 1000 + row * 100 + column, nameKey, "");
            proto.Handcraft = false;
            proto.Explicit = true;
            proto.NonProductive = true;
            proto.IconPath = "";
            PersistentIds.Add(id, recipe.PersistentId);
        }
    }

    /// <summary>在全部原型加载后移除物品的展示配方关联，避免普通物品提示泄露未解锁配方或改变主制造来源。</summary>
    public static void DetachFromItemRecipes() {
        foreach (ItemProto item in LDB.items.dataArray) {
            item.recipes?.RemoveAll(recipe => IsAdvancedProto(recipe.ID));
            item.makes?.RemoveAll(recipe => IsAdvancedProto(recipe.ID));
            if (item.maincraft == null || !IsAdvancedProto(item.maincraft.ID)) { continue; }
            item.maincraft = null;
            item.maincraftProductCount = 0;
            int firstResultIndex = int.MaxValue;
            if (item.recipes != null) {
                foreach (RecipeProto recipe in item.recipes) {
                    int index = Array.IndexOf(recipe.Results, item.ID);
                    if (index < 0 || index >= firstResultIndex) { continue; }
                    firstResultIndex = index;
                    item.maincraft = recipe;
                    item.maincraftProductCount = recipe.ResultCounts[index];
                }
            }
            item.FindPreTech();
            item.rawMats = null;
            item.ComputeRawMats();
        }
    }

    /// <summary>最终来源过滤后隐藏失效展示原型；身份仍保留用于排除普通生产和旧存档查找。</summary>
    internal static void HideUnavailableRecipes() {
        foreach (var pair in PersistentIds) {
            if (RecipeManager.GetRecipe(pair.Value) != null) { continue; }
            RecipeProto proto = LDB.recipes.Select(pair.Key);
            if (proto != null) { proto.GridIndex = 0; }
        }
    }

    /// <summary>辨认高级塔展示原型，供估值和普通配方调整排除。</summary>
    public static bool IsAdvancedProto(int recipeId) => PersistentIds.ContainsKey(recipeId);

    /// <summary>由展示原型查找当前运行配方，不保留初始化阶段的临时配方对象。</summary>
    public static WorldviewRecipe GetRecipe(int protoId) =>
        PersistentIds.TryGetValue(protoId, out int persistentId) ? RecipeManager.GetRecipe(persistentId) : null;

    /// <summary>将稳定运行身份映射到图标索引范围内的原型身份；未知身份返回0。</summary>
    public static int GetProtoId(int persistentId) => persistentId switch {
        >= 30101 and <= 30107 => RFE高级矿物展示起始 + persistentId - 30101,
        >= 30108 and <= 30114 => RFE高级矿物扩展展示起始 + persistentId - 30108,
        >= 30201 and <= 30203 => RFE高级培育展示起始 + persistentId - 30201,
        >= 30211 and <= 30213 => RFE高级繁育展示起始 + persistentId - 30211,
        >= 30301 and <= 30304 => RFE高级转化展示起始 + persistentId - 30301,
        _ => 0,
    };

    /// <summary>在主线程把研究解锁投影到原版集合；读档强制同步，常规帧按快照版本跳过。</summary>
    /// <param name="force">导入结束时重建投影，不依赖上一份原版集合内容。</param>
    public static void SyncUnlocks(bool force = false) {
        GameHistoryData history = GameMain.history;
        if (history?.recipeUnlocked == null) { return; }
        long version = ResearchManager.AnalysisSnapshotVersion;
        if (!force && ReferenceEquals(projectedHistory, history) && projectedVersion == version) { return; }
        foreach (var pair in PersistentIds) {
            WorldviewRecipe recipe = RecipeManager.GetRecipe(pair.Value);
            if (recipe != null && ResearchManager.IsContentUnlocked(recipe.StableId)) {
                history.recipeUnlocked.Add(pair.Key);
            } else {
                history.recipeUnlocked.Remove(pair.Key);
            }
        }
        projectedHistory = history;
        projectedVersion = version;
    }
}
