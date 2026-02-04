using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.Statistic;

public static class RecipeGallery {
    //行数：配方类型+矩阵7种+总计    列数：矩阵类型+配方3种+总计
    private const int MatrixCount = 7;
    private const int RecipeCount = 3;
    private static RectTransform window;
    private static RectTransform tab;
    private static readonly Text[,] recipeUnlockInfoText = new Text[MatrixCount + 2, RecipeCount + 2];
    private static int[] Matrixes = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I黑雾矩阵];

    public static void AddTranslations() {
        Register("配方图鉴", "Recipe Gallery");

        Register("配方解锁情况",
            $"The recipe unlock status is as follows ({"Full Upgrade".WithColor(7)}/{"Unlocked".WithColor(4)}/{"Total".WithColor(1)}):",
            $"配方解锁情况如下（{"完全升级".WithColor(7)}/{"已解锁".WithColor(4)}/{"总数".WithColor(1)}）：");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "配方图鉴");
        float x = 0f;
        float y = 18f;
        wnd.AddText2(x, y, tab, "配方解锁情况").supportRichText = true;
        y += 36f;
        for (int i = 0; i < MatrixCount + 2; i++) {
            for (int j = 0; j < RecipeCount + 2; j++) {
                (float, float) position = GetPosition(j, RecipeCount + 2);
                recipeUnlockInfoText[i, j] = wnd.AddText2(position.Item1, y, tab, "动态刷新");
                recipeUnlockInfoText[i, j].supportRichText = true;
            }
            y += 36f;
        }
        //左上角
        recipeUnlockInfoText[0, 0].text = "";
        //第一行，配方类型
        for (int j = 1; j <= RecipeCount; j++) {
            recipeUnlockInfoText[0, j].text = RecipeTypeShortNames[j - 1];
        }
        recipeUnlockInfoText[0, RecipeCount + 1].text = "总计".Translate();
        //第一列，矩阵类型
        for (int i = 1; i <= MatrixCount; i++) {
            recipeUnlockInfoText[i, 0].text = LDB.items.Select(Matrixes[i - 1]).name.Replace(" Matrix", "");
        }
        recipeUnlockInfoText[MatrixCount + 1, 0].text = "总计".Translate();
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        int[,] fullUpgradeCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        int[,] unlockCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        int[,] totalCountArr = new int[MatrixCount + 1, RecipeCount + 1];
        for (int i = 0; i < MatrixCount; i++) {
            for (int j = 0; j < RecipeCount; j++) {
                int matrixID = Matrixes[i];
                var type = (ERecipe)(j + 1);
                List<BaseRecipe> recipes = GetRecipesByType(type)
                    .Where(r => r.MatrixID == matrixID).ToList();
                totalCountArr[i, j] = recipes.Count;
                totalCountArr[MatrixCount, j] += recipes.Count;
                totalCountArr[i, RecipeCount] += recipes.Count;
                totalCountArr[MatrixCount, RecipeCount] += recipes.Count;
                recipes = recipes.Where(r => r.Unlocked).ToList();
                unlockCountArr[i, j] = recipes.Count;
                unlockCountArr[MatrixCount, j] += recipes.Count;
                unlockCountArr[i, RecipeCount] += recipes.Count;
                unlockCountArr[MatrixCount, RecipeCount] += recipes.Count;
                recipes = recipes.Where(r => r.FullUpgrade).ToList();
                fullUpgradeCountArr[i, j] = recipes.Count;
                fullUpgradeCountArr[MatrixCount, j] += recipes.Count;
                fullUpgradeCountArr[i, RecipeCount] += recipes.Count;
                fullUpgradeCountArr[MatrixCount, RecipeCount] += recipes.Count;
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
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() { }

    #endregion
}
