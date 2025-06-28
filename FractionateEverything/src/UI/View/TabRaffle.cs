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
using static FE.Utils.Utils;
using Random = System.Random;

namespace FE.UI.View;

public static class TabRaffle {
    public static RectTransform _windowTrans;

    public static Random random = new();

    /// <summary>
    /// 下一抽是第几抽。
    /// </summary>
    public static int RecipeRaffleCount = 1;
    public static double RecipeRaffleRate => 0.006 + Math.Max(RecipeRaffleCount - 73, 0) * 0.06;

    public static double[] FracProtoRateArr = [0.05, 0.02, 0.01, 0.005, 0.002, 0.001];
    public static int[] FracProtoID = [IFE分馏原胚普通, IFE分馏原胚精良, IFE分馏原胚稀有, IFE分馏原胚史诗, IFE分馏原胚传说, IFE分馏原胚定向];
    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        wnd.AddTabGroup(trans, "抽卡", "tab-group-fe2");
        {
            var tab = wnd.AddTab(trans, "配方卡池");
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
        {
            var tab = wnd.AddTab(trans, "建筑卡池");
            x = 0f;
            y = 10f;
            wnd.AddComboBox(x, y, tab, "卡池选择").WithItems(BuildingTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(BuildingTypeEntry);
            y += 38f;
            wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-raffle-1",
                () => Raffle(BuildingTypeEntry.Value, 1));
            wnd.AddButton(x + 300, y, 200, tab, "十连", 16, "button-raffle-10",
                () => Raffle(BuildingTypeEntry.Value, 10));
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
        StringBuilder sb = new StringBuilder("获得了以下物品：\n");
        while (count > 0) {
            count--;
            double currRate = 0;
            double randDouble = random.NextDouble();
            //分馏配方核心（0.05%）
            currRate += 0.0005;
            if (randDouble < currRate) {
                GameMain.mainPlayer.TryAddItemToPackage(IFE分馏配方核心, 1, 0, true);
                sb.AppendLine($"{LDB.items.Select(IFE分馏配方核心).name} x 1".WithColor(Red));
                RecipeRaffleCount = 1;
                continue;
            }
            //配方（0.6%，74抽开始后每抽增加6%）
            currRate += RecipeRaffleRate;
            if (randDouble < currRate) {
                int idx = random.Next(0, recipeArr.Count);
                BaseRecipe recipe = recipeArr[idx];
                string name = LDB.items.Select(recipe.InputID).name;
                if (!recipe.IsUnlocked) {
                    recipe.Level = 1;
                    recipe.Quality = 1;
                    sb.AppendLine($"{recipe.TypeName} => 已解锁".WithColor(QualityGold));
                } else {
                    recipe.MemoryCount++;
                    sb.AppendLine($"{recipe.TypeName} => 已转为回响（当前拥有{recipe.MemoryCount}）".WithColor(QualityGold));
                }
                RecipeRaffleCount = 1;
                continue;
            }
            RecipeRaffleCount++;
            //分馏原胚（8.8%）
            bool getFracProto = false;
            for (int i = 0; i < FracProtoRateArr.Length; i++) {
                currRate += FracProtoRateArr[i];
                if (randDouble < currRate) {
                    GameMain.mainPlayer.TryAddItemToPackage(FracProtoID[i], 1, 0, true);
                    sb.AppendLine($"{LDB.items.Select(FracProtoID[i]).name} x 1".WithColor(Blue));
                    getFracProto = true;
                    break;
                }
            }
            if (getFracProto) {
                continue;
            }
            //沙土
            GameMain.mainPlayer.TryAddItemToPackage(I沙土, 1000, 0, true);
            sb.AppendLine("沙土 x 1000");
        }
        UIMessageBox.Show("抽卡结果", sb.ToString().TrimEnd('\n'), "确认", UIMessageBox.INFO);
    }

    /// <summary>
    /// 抽卡。
    /// </summary>
    /// <param name="buildingType">建筑奖池</param>
    /// <param name="count">抽卡次数</param>
    public static void Raffle(int buildingType, int count) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        StringBuilder sb = new StringBuilder("获得了以下物品：");
        while (count > 0) {
            count--;
            double currRate = 0;
            double randDouble = random.NextDouble();
            //建筑增幅芯片（0.3%）
            currRate += 0.003;
            if (randDouble < currRate) {
                GameMain.mainPlayer.TryAddItemToPackage(IFE建筑增幅芯片, 1, 0, true);
                sb.AppendLine($"{LDB.items.Select(IFE建筑增幅芯片).name} x 1".WithColor(Red));
                continue;
            }
            //建筑（3% * 1 + 0.5% * 6）
            bool getBuilding = false;
            for (int i = 0; i < BuildingIds.Length; i++) {
                currRate += i == buildingType ? 0.03 : 0.005;
                if (randDouble < currRate) {
                    GameMain.mainPlayer.TryAddItemToPackage(BuildingIds[i], 1, 0, true);
                    sb.AppendLine($"{LDB.items.Select(BuildingIds[i]).name} x 1".WithColor(Blue));
                    getBuilding = true;
                    break;
                }
            }
            if (getBuilding) {
                continue;
            }
            //分馏原胚（26.4%）
            bool getFracProto = false;
            for (int i = 0; i < FracProtoRateArr.Length; i++) {
                currRate += FracProtoRateArr[i] * 3;
                if (randDouble < currRate) {
                    GameMain.mainPlayer.TryAddItemToPackage(FracProtoID[i], 1, 0, true);
                    sb.AppendLine($"{LDB.items.Select(FracProtoID[i]).name} x 1".WithColor(Blue));
                    getFracProto = true;
                    break;
                }
            }
            if (getFracProto) {
                continue;
            }
            //沙土
            GameMain.mainPlayer.TryAddItemToPackage(I沙土, 1000, 0, true);
            sb.AppendLine("沙土 x 1000");
        }
        UIMessageBox.Show("抽卡结果", sb.ToString().TrimEnd('\n'), "确认", UIMessageBox.INFO);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        RecipeRaffleCount = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(RecipeRaffleCount);
    }

    public static void IntoOtherSave() {
        RecipeRaffleCount = 0;
    }

    #endregion
}
