using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using static FE.UI.View.TabRecipeAndBuilding;
using Random = System.Random;
using static FE.Utils.ProtoID;

namespace FE.UI.View;

public static class TabRaffle {
    public static RectTransform _windowTrans;

    public static Random random = new();

    /// <summary>
    /// 下一抽是第几抽。
    /// </summary>
    public static int RaffleCount = 1;

    public static double RecipeRate => 0.006 + Math.Max(RaffleCount - 73, 0) * 0.06;
    public static double[] FracProtoArr = [0.05, 0.02, 0.01, 0.005, 0.002, 0.001];
    public static int[] FracProtoID = [IFE分馏原胚普通, IFE分馏原胚精良, IFE分馏原胚稀有, IFE分馏原胚史诗, IFE分馏原胚传说, IFE分馏原胚定向];
    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        float x;
        float y;
        wnd.AddTabGroup(trans, "抽卡", "tab-group-fe2");
        {
            var tab = wnd.AddTab(trans, "抽卡");
            x = 0f;
            y = 10f;
            wnd.AddComboBox(x, y, tab, "卡池选择").WithItems(RecipeTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(RecipeTypeEntry);
            y += 38f;
            wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-raffle-1",
                () => Raffle(RecipeTypes[RecipeTypeEntry.Value], 1));
            wnd.AddButton(x + 300, y, 200, tab, "十连", 16, "button-raffle-10",
                () => Raffle(RecipeTypes[RecipeTypeEntry.Value], 10));
        }
    }

    public static void UpdateUI() { }

    /// <summary>
    /// 抽卡。
    /// </summary>
    /// <param name="recipeType">配方奖池</param>
    /// <param name="count">抽卡次数</param>
    public static void Raffle(ERecipe recipeType, int count) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        List<BaseRecipe> recipeArr = RecipeManager.GetRecipes(recipeType);
        StringBuilder sb = new StringBuilder("获得了以下物品：");
        while (count > 0) {
            count--;
            double currRate = RecipeRate;
            double randDouble = random.NextDouble();
            //配方
            if (randDouble < currRate) {
                int idx = random.Next(0, recipeArr.Count);
                BaseRecipe recipe = recipeArr[idx];
                if (!recipe.IsUnlocked) {
                    recipe.Level = 1;
                    recipe.Quality = 1;
                    sb.AppendLine($"\n[配方] {recipeType.GetName()}-{LDB.items.Select(recipe.InputID).name.Trim()}");
                } else {
                    recipe.MemoryCount++;
                    sb.AppendLine($"\n[回响] {recipeType.GetName()}-{LDB.items.Select(recipe.InputID).name.Trim()}");
                }
                RaffleCount = 1;
                continue;
            }
            RaffleCount++;
            //分馏原胚
            bool getFracProto = false;
            for (int i = 0; i < FracProtoArr.Length; i++) {
                currRate += FracProtoArr[i];
                if (randDouble < currRate) {
                    GameMain.mainPlayer.TryAddItemToPackage(FracProtoID[i], 1, 0, true);
                    sb.Append($"\n[原胚] {LDB.items.Select(FracProtoID[i]).name} x 1");
                    getFracProto = true;
                    break;
                }
            }
            if (getFracProto) {
                continue;
            }
            //狗粮
            GameMain.mainPlayer.TryAddItemToPackage(I沙土, 1000, 0, true);
            sb.Append("\n[沙土] 沙土 x 1000");
        }
        UIMessageBox.Show("抽卡结果", sb.ToString(), "确认", UIMessageBox.INFO);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        RaffleCount = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(RaffleCount);
    }

    public static void IntoOtherSave() {
        RaffleCount = 0;
    }

    #endregion
}
