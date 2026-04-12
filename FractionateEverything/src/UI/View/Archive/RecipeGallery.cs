using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.Archive;

public static class RecipeGallery {
    // 行数：配方类型+矩阵7种+总计    列数：矩阵类型+配方3种+总计
    private const int MatrixCount = 7;
    private const int RecipeCount = 3;
    private static RectTransform window;
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtGridTitle;
    private static readonly Text[,] recipeUnlockInfoText = new Text[MatrixCount + 2, RecipeCount + 2];
    private static int[] Matrixes = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I黑雾矩阵];

    public static void AddTranslations() {
        Register("配方图鉴", "Recipe Gallery");
        Register("配方解锁情况",
            $"The recipe gallery shows the current totals of {"Maxed".WithColor(7)}/{"Unlocked".WithColor(4)}/{"Total".WithColor(1)}:",
            $"配方图鉴当前展示的是 {"满级".WithColor(7)}/{"已解锁".WithColor(4)}/{"总数".WithColor(1)} 三项汇总：");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        header = PageLayout.CreatePageHeader(wnd, tab, "配方图鉴", "", "recipe-gallery-header");
        RectTransform gridCard = PageLayout.CreateContentCard(tab, "recipe-gallery-grid-card", 0f,
            PageLayout.HeaderHeight + PageLayout.Gap, PageLayout.DesignWidth, 665f, true);
        float y = 18f;
        txtGridTitle = PageLayout.AddCardTitle(wnd, gridCard, 18f, 14f, "配方解锁情况", 16, "recipe-gallery-grid-title");
        y = 58f;
        for (int i = 0; i < MatrixCount + 2; i++) {
            for (int j = 0; j < RecipeCount + 2; j++) {
                (float, float) position = GetPosition(j, RecipeCount + 2);
                recipeUnlockInfoText[i, j] = wnd.AddText2(position.Item1, y, gridCard, "动态刷新");
                recipeUnlockInfoText[i, j].supportRichText = true;
            }
            y += 36f;
        }
        recipeUnlockInfoText[0, 0].text = "";
        for (int j = 1; j <= RecipeCount; j++) {
            recipeUnlockInfoText[0, j].text = RecipeTypeShortNames[j - 1];
        }
        recipeUnlockInfoText[0, RecipeCount + 1].text = "总计".Translate();
        for (int i = 1; i <= MatrixCount; i++) {
            recipeUnlockInfoText[i, 0].text = LDB.items.Select(Matrixes[i - 1]).name.Replace(" Matrix", "");
        }
        recipeUnlockInfoText[MatrixCount + 1, 0].text = "总计".Translate();
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

        int[,] fullUpgradeCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        int[,] unlockCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        int[,] totalCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        var counts = RecipeGrowthQueries.GetGalleryCounts(Matrixes, RecipeTypes);
        for (int i = 0; i < MatrixCount; i++) {
            for (int j = 0; j < RecipeCount; j++) {
                int matrixID = Matrixes[i];
                ERecipe type = RecipeTypes[j];
                (int unlocked, int maxed, int total) = counts[(matrixID, type)];
                totalCountArr[i, j] = total;
                totalCountArr[MatrixCount, j] += total;
                totalCountArr[i, RecipeCount] += total;
                totalCountArr[MatrixCount, RecipeCount] += total;
                unlockCountArr[i, j] = unlocked;
                unlockCountArr[MatrixCount, j] += unlocked;
                unlockCountArr[i, RecipeCount] += unlocked;
                unlockCountArr[MatrixCount, RecipeCount] += unlocked;
                fullUpgradeCountArr[i, j] = maxed;
                fullUpgradeCountArr[MatrixCount, j] += maxed;
                fullUpgradeCountArr[i, RecipeCount] += maxed;
                fullUpgradeCountArr[MatrixCount, RecipeCount] += maxed;
            }
        }

        for (int i = 0; i < MatrixCount + 1; i++) {
            for (int j = 0; j < RecipeCount + 1; j++) {
                recipeUnlockInfoText[i + 1, j + 1].text =
                    $"{fullUpgradeCountArr[i, j].ToString().WithColor(7)}"
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
