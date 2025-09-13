﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.Statistic;

public static class RecipeGallery {
    private static RectTransform window;
    private static RectTransform tab;

    private static Text recipeUnlockTitleText;
    //矩阵7种（竖），配方6种（横）
    private static Text[,] recipeUnlockInfoText = new Text[9, 8];
    private static int[] Matrixes = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I黑雾矩阵];

    public static void AddTranslations() {
        Register("配方图鉴", "Recipe Gallery");

        Register("配方解锁情况",
            $"The recipe unlock status is as follows ({"Full Upgrade".WithColor(Orange)}/{"Max Echo".WithColor(Red)}/{"Unlocked".WithColor(Blue)}/Total):",
            $"配方解锁情况如下（{"完全升级".WithColor(Orange)}/{"最大回响".WithColor(Red)}/{"已解锁".WithColor(Blue)}/总数）：");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "配方图鉴");
        float x = 0f;
        float y = 18f;
        recipeUnlockTitleText = wnd.AddText2(x, y, tab, "配方解锁情况");
        recipeUnlockTitleText.supportRichText = true;
        y += 36f;
        for (int i = 0; i < 9; i++) {
            for (int j = 0; j < 8; j++) {
                (float, float) position = GetPosition(j, 8);
                recipeUnlockInfoText[i, j] = wnd.AddText2(position.Item1, y, tab, "动态刷新");
                recipeUnlockInfoText[i, j].supportRichText = true;
            }
            y += 36f;
        }
        recipeUnlockInfoText[0, 0].text = "";
        for (int i = 1; i <= 7; i++) {
            recipeUnlockInfoText[i, 0].text = LDB.items.Select(Matrixes[i - 1]).name.Replace(" Matrix", "");
        }
        recipeUnlockInfoText[8, 0].text = "总计".Translate();
        for (int j = 1; j <= 6; j++) {
            recipeUnlockInfoText[0, j].text = RecipeTypeShortNames[j - 1];
        }
        recipeUnlockInfoText[0, 7].text = "总计".Translate();
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        int[,] fullUpgradeCountArr = new int[9, 8];
        int[,] maxEchoCountArr = new int[9, 8];
        int[,] unlockCountArr = new int[9, 8];
        int[,] totalCountArr = new int[9, 8];
        for (int i = 1; i <= 7; i++) {
            for (int j = 1; j <= 6; j++) {
                int matrixID = Matrixes[i - 1];
                ERecipe type = (ERecipe)j;
                List<BaseRecipe> recipes = GetRecipesByType(type)
                    .Where(r => itemToMatrix[r.InputID] == matrixID).ToList();
                totalCountArr[i, j] = recipes.Count;
                totalCountArr[8, j] += recipes.Count;
                totalCountArr[i, 7] += recipes.Count;
                totalCountArr[8, 7] += recipes.Count;
                recipes = recipes.Where(r => r.Unlocked).ToList();
                unlockCountArr[i, j] = recipes.Count;
                unlockCountArr[8, j] += recipes.Count;
                unlockCountArr[i, 7] += recipes.Count;
                unlockCountArr[8, 7] += recipes.Count;
                recipes = recipes.Where(r => r.IsMaxEcho).ToList();
                maxEchoCountArr[i, j] = recipes.Count;
                maxEchoCountArr[8, j] += recipes.Count;
                maxEchoCountArr[i, 7] += recipes.Count;
                maxEchoCountArr[8, 7] += recipes.Count;
                recipes = recipes.Where(r => r.FullUpgrade).ToList();
                fullUpgradeCountArr[i, j] = recipes.Count;
                fullUpgradeCountArr[8, j] += recipes.Count;
                fullUpgradeCountArr[i, 7] += recipes.Count;
                fullUpgradeCountArr[8, 7] += recipes.Count;
            }
        }
        for (int i = 1; i <= 8; i++) {
            for (int j = 1; j <= 7; j++) {
                recipeUnlockInfoText[i, j].text =
                    $"{fullUpgradeCountArr[i, j].ToString().WithColor(Orange)}"
                    + $"/{maxEchoCountArr[i, j].ToString().WithColor(Red)}"
                    + $"/{unlockCountArr[i, j].ToString().WithColor(Blue)}"
                    + $"/{totalCountArr[i, j]}";
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
