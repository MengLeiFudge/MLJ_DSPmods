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

public static class FracStatistic {
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
        var tab = wnd.AddTab(trans, "分馏统计");
        float x = 0f;
        float y = 10f;
    }

    public static void UpdateUI() { }

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
