using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
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
            string nameKey = $"FE.AdvancedRecipe.{id}";
            string descriptionKey = nameKey + ".Description";
            // 原版数量字段仅支持整数；繁育按两轮的物料比例展示，真实概率与催化剂规则写在说明中。
            int scale = recipe.ProductArray.Any(output => output.Count != Math.Floor(output.Count)) ? 2 : 1;
            LocalizationModule.RegisterTranslation(nameKey, GetRecipeName(recipe, true), GetRecipeName(recipe, false), "");
            LocalizationModule.RegisterTranslation(descriptionKey, GetDescription(recipe, true), GetDescription(recipe, false), "");

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

    /// <summary>辨认高级塔展示原型，供估值和普通配方调整排除。</summary>
    public static bool IsAdvancedProto(int recipeId) => PersistentIds.ContainsKey(recipeId);

    /// <summary>由展示原型查找当前运行配方，不保留初始化阶段的临时配方对象。</summary>
    public static WorldviewRecipe GetRecipe(int protoId) =>
        PersistentIds.TryGetValue(protoId, out int persistentId) ? RecipeManager.GetRecipe(persistentId) : null;

    /// <summary>将稳定运行身份映射到图标索引范围内的原型身份；未知身份返回0。</summary>
    public static int GetProtoId(int persistentId) => persistentId switch {
        >= 30101 and <= 30107 => RFE高级矿物展示起始 + persistentId - 30101,
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

    /// <summary>直接构造两种语言，避免预注册时把当前语言烘焙进另一语言的原型文本。</summary>
    private static string GetRecipeName(WorldviewRecipe recipe, bool english) {
        string product = GetItemName(recipe.ProductArray[0].ItemId, english);
        return recipe.PersistentId switch {
            >= 30101 and <= 30106 => english ? $"{product} Catalytic Proliferation" : $"{product}催化增殖",
            30107 => english ? "Unipolar Magnet Memory Reconstruction" : "单极磁石记忆重构",
            >= 30201 and <= 30203 => english ? $"Cultivate {product}" : $"培育{product}",
            >= 30211 and <= 30213 => english
                ? $"{GetItemName(recipe.InputArray[0].ItemId, true)} Embryo Breeding"
                : $"{GetItemName(recipe.InputArray[0].ItemId, false)}原胚繁育",
            30301 => english ? "Hydrogen Fuel Rod Deuterium Replacement" : "液氢燃料棒重氢置换",
            30302 => english ? "Deuteron Fuel Rod Antimatter Replacement" : "氘核燃料棒反物质置换",
            30303 => english ? "Mining Machine Reconstruction" : "采矿机重构为大型采矿机",
            30304 => english ? "Arc Smelter Reconstruction" : "电弧熔炉重构为位面熔炉",
            _ => throw new InvalidOperationException($"Unknown advanced recipe name: {recipe.PersistentId}"),
        };
    }

    /// <summary>说明成功概率、催化剂和物料比例；数量不受启动时的游戏语言影响。</summary>
    private static string GetDescription(WorldviewRecipe recipe, bool english) {
        string inputs = string.Join("\n", recipe.InputArray.Select(input => english
            ? $"{GetItemName(input.ItemId, true)} × {input.Count} · Consumption chance {input.ConsumeProbability:P0}"
            : $"{GetItemName(input.ItemId, false)} × {input.Count} · 消耗概率 {input.ConsumeProbability:P0}"));
        string outputs = string.Join("\n", recipe.ProductArray.Select(output => english
            ? $"{GetItemName(output.ItemId, true)} · Average per successful cycle {output.Count:0.##}"
            : $"{GetItemName(output.ItemId, false)} · 每成功轮平均 {output.Count:0.##}"));
        string rules = english
            ? $"Success chance per cycle: {recipe.CycleSuccessProbability:P0}. Failed inputs flow back; catalysts are consumed probabilistically on successful cycles only.\n"
                + "Processing speed depends on incoming cargo, stacking, the matrix being digested and durability. Diagram quantities show material ratios.\n"
            : $"每轮成功率 {recipe.CycleSuccessProbability:P0}。失败原料回流，催化剂仅在成功轮次按概率消耗。\n"
                + "处理速率随传入货物、堆叠、消化中的矩阵与耐久变化；图中物料数量表示比例。\n";
        return rules + inputs + "\n" + outputs;
    }

    /// <summary>高级配方物料的双语名称；不依赖预注册时尚未加载的模组物品或当前语言。</summary>
    private static string GetItemName(int itemId, bool english) {
        (string chinese, string englishName) = itemId switch {
            // 模组名称与 Locale/translations.json 的 zhCN/enUS 一致；注释保留各自的来源键。
            IFE高级矿物塔 => ("高级矿物塔", "Advanced Mineral Tower"), // 高级矿物塔
            IFE高级交互塔 => ("高级交互塔", "Advanced Interaction Tower"), // 培养塔
            IFE高级转化塔 => ("高级转化塔", "Advanced Conversion Tower"), // 转化塔
            IFE通用原胚 => ("通用原胚", "Universal Tower Embryo"), // 通用原胚
            IFE记忆源点 => ("源点", "Memory"), // 记忆源点
            I光栅石 => ("光栅石", "Optical Grating Crystal"),
            I分形硅石 => ("分形硅石", "Fractal Silicon"),
            I金伯利矿石 => ("金伯利矿石", "Kimberlite Ore"),
            I可燃冰 => ("可燃冰", "Fire Ice"),
            I刺笋结晶 => ("刺笋结晶", "Spiniform Stalagmite Crystal"),
            I单极磁石 => ("单极磁石", "Unipolar Magnet"),
            I石矿 => ("石矿", "Stone"),
            I硅石 => ("硅石", "Silicon Ore"),
            I煤矿 => ("煤矿", "Coal"),
            I水 => ("水", "Water"),
            I原油 => ("原油", "Crude Oil"),
            I植物燃料 => ("植物燃料", "Plant Fuel"),
            I铁矿 => ("铁矿", "Iron Ore"),
            I钛石 => ("钛石", "Titanium Ore"),
            I液氢燃料棒 => ("液氢燃料棒", "Hydrogen Fuel Rod"),
            I氘核燃料棒 => ("氘核燃料棒", "Deuteron Fuel Rod"),
            I反物质燃料棒 => ("反物质燃料棒", "Antimatter Fuel Rod"),
            I重氢 => ("重氢", "Deuterium"),
            I氢 => ("氢", "Hydrogen"),
            I反物质 => ("反物质", "Antimatter"),
            I湮灭约束球 => ("湮灭约束球", "Annihilation Constraint Sphere"),
            I采矿机 => ("采矿机", "Mining Machine"),
            I大型采矿机 => ("大型采矿机", "Advanced Mining Machine"),
            I处理器 => ("处理器", "Processor"),
            I齿轮 => ("齿轮", "Gear"),
            I电弧熔炉 => ("电弧熔炉", "Arc Smelter"),
            I位面熔炉 => ("位面熔炉", "Plane Smelter"),
            I卡西米尔晶体 => ("卡西米尔晶体", "Casimir Crystal"),
            I磁线圈 => ("磁线圈", "Magnetic Coil"),
            _ => throw new InvalidOperationException($"Unknown advanced recipe item: {itemId}"),
        };
        return english ? englishName : chinese;
    }
}
