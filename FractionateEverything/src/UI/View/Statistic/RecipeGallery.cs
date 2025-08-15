using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.Statistic;

public static class RecipeGallery {
    public static RectTransform _windowTrans;

    public static Text recipeUnlockTitleText;
    //矩阵7种（竖），配方6种（横）
    public static Text[,] recipeUnlockInfoText = new Text[9, 8];
    public static int[] Matrixes = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I黑雾矩阵];

    public static ConfigEntry<int> RecipeTypeEntry;
    public static string[] RecipeTypeNames;
    public static ERecipe[] RecipeTypes = [
        ERecipe.BuildingTrain, ERecipe.MineralCopy, ERecipe.QuantumCopy,
        ERecipe.Alchemy, ERecipe.Deconstruction, ERecipe.Conversion,
    ];

    public static void AddTranslations() { }

    public static void LoadConfig(ConfigFile configFile) {
        RecipeTypeNames = new string[RecipeTypes.Length];
        for (int i = 0; i < RecipeTypeNames.Length; i++) {
            RecipeTypeNames[i] = RecipeTypes[i].GetShortName();
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        var tab = wnd.AddTab(trans, "配方图鉴");
        float x = 0f;
        float y = 10f;
        recipeUnlockTitleText = wnd.AddText2(x, y, tab,
            $"配方解锁情况如下（{"满回响".WithColor(Orange)}/{"已解锁".WithColor(Blue)}/总数）：", 15, "text-recipe-unlock-title");
        recipeUnlockTitleText.supportRichText = true;
        y += 38f;
        for (int i = 0; i < 9; i++) {
            for (int j = 0; j < 8; j++) {
                recipeUnlockInfoText[i, j] =
                    wnd.AddText2(x + 100 * j, y, tab, "999/999", 15, $"text-recipe-unlock-info{i}");
                recipeUnlockInfoText[i, j].supportRichText = true;
            }
            y += 38f;
        }
        recipeUnlockInfoText[0, 0].text = "";
        for (int i = 1; i <= 7; i++) {
            recipeUnlockInfoText[i, 0].text = LDB.items.Select(Matrixes[i - 1]).name;
        }
        recipeUnlockInfoText[8, 0].text = "总计";
        for (int j = 1; j <= 6; j++) {
            recipeUnlockInfoText[0, j].text = RecipeTypeNames[j - 1];
        }
        recipeUnlockInfoText[0, 7].text = "总计";
    }

    public static void UpdateUI() {
        int[,] maxMemoryCountArr = new int[9, 8];
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
                recipes = recipes.Where(r => r.IsMaxMemory).ToList();
                maxMemoryCountArr[i, j] = recipes.Count;
                maxMemoryCountArr[8, j] += recipes.Count;
                maxMemoryCountArr[i, 7] += recipes.Count;
                maxMemoryCountArr[8, 7] += recipes.Count;
            }
        }
        for (int i = 1; i <= 8; i++) {
            for (int j = 1; j <= 7; j++) {
                recipeUnlockInfoText[i, j].text =
                    $"{maxMemoryCountArr[i, j].ToString().WithColor(Orange)}/{unlockCountArr[i, j].ToString().WithColor(Blue)}/{totalCountArr[i, j]}";
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
