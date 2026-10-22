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
        int[] firstRow = [IFE基础解析塔, IFE基础矿物塔, IFE通用原胚, IFE残片, IFE记忆源点];
        for (int i = 0; i < firstRow.Length; i++) { PlaceItem(firstRow[i], Grid(1, i + 1)); }
        PlaceItem(IFE行星内物流交互站, Grid(1, 7));
        PlaceItem(IFE星际物流交互站, Grid(1, 8));

        WorldviewRecipe[] recipes = RecipeManager.WorldviewRecipes.Where(recipe => recipe.IsAdvanced)
            .OrderBy(recipe => recipe.PersistentId).ToArray();
        int row = 2;
        // 培育与繁育仍由交互塔执行，这里只把它们放到对应塔型的行首。
        PlaceTower(IFE高级矿物塔, ERecipe.AdvancedMineral, 30201);
        PlaceTower(IFE高级交互塔, ERecipe.AdvancedInteraction, 30202);
        PlaceTower(IFE高级转化塔, ERecipe.AdvancedConversion, 30203);

        PlaceItem(IFE实体元数据, Grid(row, 1));
        PlaceItem(IFE丰碑结晶, Grid(row, 2));
        int offset = 2;
        foreach (WorldviewRecipe recipe in recipes.Where(recipe => recipe.Kind == WorldviewRecipeKind.Monument)) {
            int grid = Grid(row + offset / columns, offset % columns + 1);
            if ((grid % 1000) / 100 > rows) { throw new InvalidOperationException("分馏页丰碑配方超出当前网格行数。"); }
            PlaceRecipe(recipe.PersistentId, grid);
            PlaceItem(recipe.PrimaryProductId, grid);
            offset++;
        }

        void PlaceTower(int itemId, ERecipe type, int cultureId) {
            PlaceItem(itemId, Grid(row, 1));
            PlaceRecipe(cultureId, Grid(row, 1));
            PlaceRecipe(cultureId + 10, Grid(row, 2));
            int index = 2;
            foreach (WorldviewRecipe recipe in recipes.Where(recipe => recipe.RecipeType == type
                         && recipe.Kind is not (WorldviewRecipeKind.TowerCultivation or WorldviewRecipeKind.EmbryoBreeding or WorldviewRecipeKind.Monument))) {
                PlaceRecipe(recipe.PersistentId, Grid(row + index / columns, index % columns + 1));
                index++;
            }
            row += (index + columns - 1) / columns;
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
