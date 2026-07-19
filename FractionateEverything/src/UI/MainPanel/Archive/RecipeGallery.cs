using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.FracRecipes.Runtime;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Logic.Fractionation.FracRecipes.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Archive;

/// <summary>
/// 分馏配方图鉴与完成度统计页面。
/// </summary>
public static class RecipeGallery {
    // 行数：表头+矩阵7种+总计    列数：矩阵类型+配方4种+总计
    private const int MatrixCount = 7;
    private const int RecipeCount = 4;
    private static RectTransform window;
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtGridTitle;
    private static readonly Text[,] recipeUnlockInfoText = new Text[MatrixCount + 2, RecipeCount + 2];
    private static int[] Matrixes = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I黑雾矩阵];

    public static void AddTranslations() {
        Register("配方图鉴", "Recipe Gallery");
        Register("配方解锁情况",
            $"The recipe gallery shows the current totals of {"Complete".WithColor(7)}/{"Available".WithColor(4)}/{"Total".WithColor(1)}:",
            $"配方图鉴当前展示的是 {"完成".WithColor(7)}/{"可用".WithColor(4)}/{"总数".WithColor(1)} 三项汇总：");
        Register("总计", "Total");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("配方图鉴", objectName: "recipe-gallery-header", pos: (0, 0), onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "recipe-gallery-grid-card", strong: true,
                        rows: [Px(32f), 1],
                        rowGap: PageLayout.InnerGap,
                        children: [
                            CardTitleNode("配方解锁情况", 16, onBuilt: text => txtGridTitle = text,
                                pos: (0, 0), objectName: "recipe-gallery-grid-title"),
                            Grid(pos: (1, 0),
                                rows: BuildRecipeGalleryRows(),
                                cols: BuildRecipeGalleryCols(),
                                rowGap: 4f,
                                columnGap: PageLayout.InnerGap,
                                children: BuildRecipeGalleryCells()),
                        ]),
                ]));
        recipeUnlockInfoText[0, 0].text = "";
        for (int j = 1; j <= RecipeCount; j++) {
            recipeUnlockInfoText[0, j].text = RecipeTypes[j - 1].GetShortName();
        }
        recipeUnlockInfoText[0, RecipeCount + 1].text = "总计".Translate();
        for (int i = 1; i <= MatrixCount; i++) {
            recipeUnlockInfoText[i, 0].text = LDB.items.Select(Matrixes[i - 1]).name.Replace(" Matrix", "");
        }
        recipeUnlockInfoText[MatrixCount + 1, 0].text = "总计".Translate();
    }

    private static IReadOnlyList<LayoutTrack> BuildRecipeGalleryRows() {
        var rows = new List<LayoutTrack>();
        for (int i = 0; i < MatrixCount + 2; i++) {
            rows.Add(1);
        }

        return rows;
    }

    private static IReadOnlyList<LayoutTrack> BuildRecipeGalleryCols() {
        var cols = new List<LayoutTrack>();
        for (int i = 0; i < RecipeCount + 2; i++) {
            cols.Add(i == 0 ? Fr(2) : Fr(1));
        }

        return cols;
    }

    private static IReadOnlyList<LayoutNode> BuildRecipeGalleryCells() {
        var cells = new List<LayoutNode>();
        for (int i = 0; i < MatrixCount + 2; i++) {
            for (int j = 0; j < RecipeCount + 2; j++) {
                int row = i;
                int col = j;
                cells.Add(TextNode("动态刷新", onBuilt: text => recipeUnlockInfoText[row, col] = text,
                    pos: (row, col), objectName: $"recipe-gallery-cell-{row}-{col}"));
            }
        }

        return cells;
    }

    private static bool IsPageVisible() {
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.None) return false;
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.Analysis) {
            return tab != null && tab.gameObject.activeInHierarchy;
        }
        return tab != null && tab.gameObject.activeSelf;
    }

    public static void UpdateUI() {
        if (!IsPageVisible()) {
            return;
        }

        header.Title.text = "配方图鉴".Translate().WithColor(Orange);
        header.Summary.text = "配方解锁情况".Translate().WithColor(White);
        txtGridTitle.text = "配方解锁情况".Translate().WithColor(Orange);

        int[,] completeCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        int[,] unlockCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        int[,] totalCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        for (int i = 0; i < MatrixCount; i++) {
            for (int j = 0; j < RecipeCount; j++) {
                int matrixID = Matrixes[i];
                ERecipe type = RecipeTypes[j];
                int unlocked = 0;
                int complete = 0;
                int total = 0;
                foreach (BaseRecipe recipe in RecipeManager.GetRecipesByMatrix(matrixID)) {
                    if (recipe.RecipeType != type) {
                        continue;
                    }

                    total++;
                    RecipeKey recipeKey = RecipeKey.FromRecipe(recipe);
                    ProtocolDefinition definition = ProtocolCatalog.Get(recipeKey);
                    bool available = RecipeAvailabilityStore.IsAvailable(recipeKey);
                    if (available) {
                        unlocked++;
                    }

                    if (definition == null ? available : ProtocolProgressStore.IsComplete(recipeKey)) {
                        complete++;
                    }
                }

                totalCountArr[i, j] = total;
                totalCountArr[MatrixCount, j] += total;
                totalCountArr[i, RecipeCount] += total;
                totalCountArr[MatrixCount, RecipeCount] += total;
                unlockCountArr[i, j] = unlocked;
                unlockCountArr[MatrixCount, j] += unlocked;
                unlockCountArr[i, RecipeCount] += unlocked;
                unlockCountArr[MatrixCount, RecipeCount] += unlocked;
                completeCountArr[i, j] = complete;
                completeCountArr[MatrixCount, j] += complete;
                completeCountArr[i, RecipeCount] += complete;
                completeCountArr[MatrixCount, RecipeCount] += complete;
            }
        }

        for (int i = 0; i < MatrixCount + 1; i++) {
            for (int j = 0; j < RecipeCount + 1; j++) {
                recipeUnlockInfoText[i + 1, j + 1].text =
                    $"{completeCountArr[i, j].ToString().WithColor(7)}"
                    + $"/{unlockCountArr[i, j].ToString().WithColor(4)}"
                    + $"/{totalCountArr[i, j].ToString().WithColor(1)}";
            }
        }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion
}
