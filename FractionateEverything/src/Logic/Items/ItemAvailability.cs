using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace FE.Logic.Items;

/// <summary>由最终原型与可采集来源求物料可达闭包；不把解锁进度或循环配方当成来源。</summary>
public static class ItemAvailability {
    private static readonly HashSet<int> ExternalSources = [];
    private static HashSet<int> reachable = [];

    /// <summary>外部模组在最终配方初始化前声明非原型机制提供的合法实物来源。</summary>
    public static void RegisterExternalSource(int itemId) {
        if (itemId <= 0 || itemId >= 12000) { throw new ArgumentOutOfRangeException(nameof(itemId)); }
        ExternalSources.Add(itemId);
    }

    /// <summary>查询物品是否有可从独立来源启动的生产路径。</summary>
    public static bool IsReachable(int itemId) => reachable.Contains(itemId);

    /// <summary>从主题采集、黑雾掉落、FE奖励和外部显式来源启动，交替展开普通与高级配方。</summary>
    internal static void Rebuild(IEnumerable<WorldviewRecipe> advancedRecipes) {
        HashSet<int> available = [];
        void Add(int id) { if (id > 0 && id < 12000 && LDB.items.Exist(id)) { available.Add(id); } }
        void AddVein(int id) { Add(LDB.veins.Select(id)?.MiningItem ?? 0); }
        foreach (int id in ExternalSources) { Add(id); }
        // FE脚本奖励及献祭提供的真实来源，不依赖本目录中的自复制配方。
        foreach (int id in new[] { IFE通用原胚, IFE残片, IFE记忆源点, IFE基础矿物塔, IFE基础解析塔, I沙土 }) { Add(id); }
        // 原版特殊恒星可追加光栅、分形、刺笋与单极矿，不要求主题本身列出。
        foreach (int vein in new[] { 9, 10, 12, 14 }) { AddVein(vein); }
        foreach (ThemeProto theme in LDB.themes.dataArray) {
            Add(theme.WaterItemId);
            for (int i = 0; i < (theme.GasItems?.Length ?? 0); i++) {
                if (i < (theme.GasSpeeds?.Length ?? 0) && theme.GasSpeeds[i] > 0f) { Add(theme.GasItems[i]); }
            }
            for (int i = 0; i < (theme.VeinSpot?.Length ?? 0); i++) {
                if (theme.VeinSpot[i] > 0) { AddVein(i + 1); }
            }
            for (int i = 0; i < (theme.RareVeins?.Length ?? 0); i++) {
                if (i * 4 + 1 < (theme.RareSettings?.Length ?? 0)
                    && (theme.RareSettings[i * 4] > 0f || theme.RareSettings[i * 4 + 1] > 0f)) { AddVein(theme.RareVeins[i]); }
            }
            foreach (int[] group in new[] { theme.Vegetables0, theme.Vegetables1, theme.Vegetables2,
                         theme.Vegetables3, theme.Vegetables4, theme.Vegetables5 }) {
                if (group == null) { continue; }
                foreach (int id in group) {
                    VegeProto plant = LDB.veges.Select(id);
                    for (int i = 0; i < (plant?.MiningItem?.Length ?? 0); i++) {
                        if (i < plant.MiningCount.Length && i < plant.MiningChance.Length
                            && plant.MiningCount[i] > 0 && plant.MiningChance[i] > 0f) { Add(plant.MiningItem[i]); }
                    }
                }
            }
        }
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item.EnemyDropRange.y > 0.00005f && item.EnemyDropCount > 0f) { Add(item.ID); }
        }
        RecipeProto[] ordinary = LDB.recipes.dataArray.Where(recipe => recipe.Type != ERecipeType.None
            && !AdvancedRecipeCatalog.IsAdvancedProto(recipe.ID) && recipe.Items != null && recipe.Results != null).ToArray();
        WorldviewRecipe[] advanced = advancedRecipes.ToArray();
        bool changed;
        do {
            int before = available.Count;
            foreach (RecipeProto recipe in ordinary) {
                if (!recipe.Items.All(available.Contains)) { continue; }
                for (int i = 0; i < recipe.Results.Length; i++) {
                    if (i < recipe.ResultCounts.Length && recipe.ResultCounts[i] > 0) { Add(recipe.Results[i]); }
                }
            }
            foreach (WorldviewRecipe recipe in advanced) {
                if (!recipe.Inputs.All(input => available.Contains(input.ItemId))) { continue; }
                foreach (WorldviewRecipeOutput output in recipe.Products) { if (output.Count > 0) { Add(output.ItemId); } }
            }
            foreach (ItemProto item in LDB.items.dataArray) {
                if (available.Contains(item.ID) && item.prefabDesc?.powerProductId > 0) { Add(item.prefabDesc.powerProductId); }
            }
            changed = before != available.Count;
        } while (changed);
        reachable = available;
    }
}
