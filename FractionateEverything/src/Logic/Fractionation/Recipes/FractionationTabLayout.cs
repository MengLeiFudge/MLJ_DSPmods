using System;
using System.Linq;

using FE.Compatibility.Mods;

using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>在最终可用配方确定后统一安排分馏页；展示顺序不改变配方所属机器或建造栏。</summary>
internal static class FractionationTabLayout {
    internal static readonly int[] ResourceItems = [IFE通用原胚, IFE残片, IFE记忆源点, IFE实体元数据, IFE丰碑结晶];

    internal static void Apply() {
        int columns = GenesisBook.Enable ? 17 : 14;
        int rows = GenesisBook.Enable ? 7 : 8;
        int[] firstRow = [IFE通用原胚, IFE残片, IFE记忆源点];
        for (int i = 0; i < firstRow.Length; i++) { PlaceItem(firstRow[i], Grid(1, i + 1)); }
        PlaceItem(IFE行星内物流交互站, Grid(1, 7));
        PlaceItem(IFE星际物流交互站, Grid(1, 8));

        WorldviewRecipe[] recipes = RecipeManager.WorldviewRecipes.Where(recipe => recipe.IsAdvanced)
            .OrderBy(recipe => recipe.PersistentId).ToArray();
        // 矿物上下两行逐列对应；所有培育、繁育留在交互塔行。
        PlaceItem(IFE基础矿物塔, Grid(4, 1));
        PlaceItem(IFE基础解析塔, Grid(4, 2));
        var minerals = recipes.Where(recipe => recipe.RecipeType == ERecipe.AdvancedMineral)
            .GroupBy(recipe => recipe.PrimaryProductId).OrderBy(group => group.Key).ToArray();
        if (minerals.Length > columns - 1) { throw new InvalidOperationException("分馏页矿物目录超出两行容量。"); }
        for (int i = 0; i < minerals.Length; i++) {
            foreach (WorldviewRecipe recipe in minerals[i]) {
                PlaceRecipe(recipe.PersistentId, Grid(recipe.ContainsInput(IFE记忆源点) ? 3 : 2, i + 2));
            }
        }
        WorldviewRecipe[] interaction = recipes.Where(recipe => recipe.RecipeType == ERecipe.AdvancedInteraction).ToArray();
        for (int i = 0; i < interaction.Length; i++) {
            PlaceRecipe(interaction[i].PersistentId, Grid(4, i + 3));
            if (interaction[i].Kind == WorldviewRecipeKind.TowerCultivation) {
                PlaceItem(interaction[i].PrimaryProductId, Grid(4, i + 3));
            }
        }
        WorldviewRecipe[] conversion = recipes.Where(recipe => recipe.Kind == WorldviewRecipeKind.Replacement).ToArray();
        int conversionRows = (conversion.Length + columns - 1) / columns;
        if (conversionRows > 3) { throw new InvalidOperationException("分馏页转化配方不得超过三行。"); }
        for (int i = 0; i < conversion.Length; i++) {
            PlaceRecipe(conversion[i].PersistentId, Grid(5 + i / columns, i % columns + 1));
        }
        int row = 5 + conversionRows;
        PlaceItem(IFE实体元数据, Grid(row, 1));
        PlaceItem(IFE丰碑结晶, Grid(row, 2));
        int offset = 2;
        foreach (WorldviewRecipe recipe in recipes.Where(recipe => recipe.Kind == WorldviewRecipeKind.Monument)
                     .OrderBy(recipe => AdvancedRecipeCatalog.GetProtoId(recipe.PersistentId))) {
            int grid = Grid(row + offset / columns, offset % columns + 1);
            if ((grid % 1000) / 100 > rows) { throw new InvalidOperationException("分馏页丰碑配方超出当前网格行数。"); }
            PlaceRecipe(recipe.PersistentId, grid);
            PlaceItem(recipe.PrimaryProductId, grid);
            offset++;
        }
    }

    private static int Grid(int row, int column) => tab分馏 * 1000 + row * 100 + column;

    private static void PlaceItem(int itemId, int grid) {
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) { return; }
        item.GridIndex = grid;
        if (item.maincraft != null) { item.maincraft.GridIndex = grid; }
    }

    private static void PlaceRecipe(int persistentId, int grid) {
        RecipeProto proto = LDB.recipes.Select(AdvancedRecipeCatalog.GetProtoId(persistentId));
        if (proto != null && RecipeManager.GetRecipe(persistentId) != null) { proto.GridIndex = grid; }
    }
}
