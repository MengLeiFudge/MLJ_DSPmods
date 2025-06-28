using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
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

    /// <summary>
    /// 抽到次优先配方的次数。
    /// </summary>
    public static int NotMostPreferredCount = 0;
    public static Text[] PreferredTexts = new Text[4];
    /// <summary>
    /// 优先配方类型，[0]为主优先配方（30%），其余为次优先配方（10%）
    /// </summary>
    public static int[] PreferredItems = new int[4];
    /// <summary>
    /// 优先配方ID，[0]为主优先配方（30%），其余为次优先配方（10%）
    /// </summary>
    public static ERecipe[] PreferredRecipeTypes = new ERecipe[4];

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
            for (int i = 0; i < PreferredItems.Length; i++) {
                PreferredTexts[i] = wnd.AddText2(x, y, tab, $"优先配方{i + 1}", 15, "text-preferred-recipe");
                int j = i;
                wnd.AddButton(x + 400, y, tab, "设置优先配方", 16, "button-set-preferred-recipe",
                    () => SetPreferredRecipe(j));
                y += 38f;
            }
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

    public static void UpdateUI() {
        for (int i = 0; i < PreferredItems.Length; i++) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(PreferredRecipeTypes[i], PreferredItems[i]);
            if (recipe == null) {
                PreferredTexts[i].text = "未设置优先配方".WithColor(Red);
            } else {
                PreferredTexts[i].text = $"{(i == 0 ? "30%" : "10%")}优先配方：{recipe.TypeNameWC}";
            }
        }
    }

    public static void SetPreferredRecipe(int index) {
        for (int i = 0; i < PreferredItems.Length; i++) {
            if (PreferredRecipeTypes[i] == SelectedRecipeType && PreferredItems[i] == SelectedItemId) {
                UIMessageBox.Show("提示", "该配方已经是优先配方！", "确认", UIMessageBox.WARNING);
                return;
            }
        }
        PreferredRecipeTypes[index] = SelectedRecipeType;
        PreferredItems[index] = SelectedItemId;
        UIMessageBox.Show("提示", "已设定优先配方！", "确认", UIMessageBox.INFO);
    }

    /// <summary>
    /// 抽卡。
    /// </summary>
    /// <param name="recipeType">配方奖池</param>
    /// <param name="count">抽卡次数</param>
    public static void Raffle(ERecipe recipeType, int count) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        for (int i = 0; i < PreferredItems.Length; i++) {
            if (RecipeManager.GetRecipe<BaseRecipe>(PreferredRecipeTypes[i], PreferredItems[i]) == null) {
                UIMessageBox.Show("提示", "你还未设置喜好配方！", "确认", UIMessageBox.WARNING);
                return;
            }
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
                sb.AppendLine($"{LDB.items.Select(IFE分馏配方核心).name.WithColor(Gold)} x 1");
                RecipeRaffleCount = 1;
                continue;
            }
            //配方（0.6%，74抽开始后每抽增加6%）
            currRate += RecipeRaffleRate;
            if (randDouble < currRate) {
                //先判断是不是优先配方
                double preferred = NotMostPreferredCount >= 2 ? 0 : random.NextDouble();
                double currPreferred = 0;
                bool getPreferred = false;
                BaseRecipe recipe = null;
                for (int i = 0; i < PreferredItems.Length; i++) {
                    currPreferred += i == 0 ? 0.3 : 0.1;
                    if (preferred < currPreferred) {
                        recipe = RecipeManager.GetRecipe<BaseRecipe>(PreferredRecipeTypes[i], PreferredItems[i]);
                        getPreferred = true;
                        //如果是次有限配方，累计次数
                        if (i == 0) {
                            NotMostPreferredCount = 0;
                        } else {
                            NotMostPreferredCount++;
                        }
                        break;
                    }
                }
                //不是优先配方，则按照当前选择的奖池随机抽取
                if (!getPreferred) {
                    recipe = recipeArr[random.Next(0, recipeArr.Count)];
                }
                if (!recipe.IsUnlocked) {
                    recipe.Level = 1;
                    recipe.Quality = 1;
                    sb.AppendLine($"{recipe.TypeName.WithColor(Red)} => 已解锁");
                } else if (recipe.MemoryCount < recipe.MaxMemoryCount) {
                    recipe.MemoryCount++;
                    sb.AppendLine($"{recipe.TypeName.WithColor(Red)} => 已转为回响（当前拥有{recipe.MemoryCount}）");
                } else {
                    GameMain.mainPlayer.TryAddItemToPackage(IFE残破核心, 1, 0, true);
                    sb.AppendLine($"{recipe.TypeName.WithColor(Red)} => 已转为残破核心");
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
                    Color color = i < 2 ? Green : (i < 4 ? Blue : Purple);
                    sb.AppendLine($"{LDB.items.Select(FracProtoID[i]).name.WithColor(color)} x 1");
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
                sb.AppendLine($"{LDB.items.Select(IFE建筑增幅芯片).name.WithColor(Gold)} x 1");
                continue;
            }
            //建筑（3% * 1 + 0.5% * 6）
            bool getBuilding = false;
            for (int i = 0; i < BuildingIds.Length; i++) {
                currRate += i == buildingType ? 0.03 : 0.005;
                if (randDouble < currRate) {
                    GameMain.mainPlayer.TryAddItemToPackage(BuildingIds[i], 1, 0, true);
                    sb.AppendLine($"{LDB.items.Select(BuildingIds[i]).name.WithColor(Purple)} x 1");
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
                    Color color = i < 2 ? Green : (i < 4 ? Blue : Purple);
                    sb.AppendLine($"{LDB.items.Select(FracProtoID[i]).name.WithColor(color)} x 1");
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
        if (version >= 2) {
            for (int i = 0; i < PreferredItems.Length; i++) {
                PreferredItems[i] = r.ReadInt32();
                PreferredRecipeTypes[i] = (ERecipe)r.ReadInt32();
            }
            NotMostPreferredCount = r.ReadInt32();
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(RecipeRaffleCount);
        for (int i = 0; i < PreferredItems.Length; i++) {
            w.Write(PreferredItems[i]);
            w.Write((int)PreferredRecipeTypes[i]);
        }
        w.Write(NotMostPreferredCount);
    }

    public static void IntoOtherSave() {
        RecipeRaffleCount = 0;
        for (int i = 0; i < PreferredItems.Length; i++) {
            PreferredItems[i] = 0;
            PreferredRecipeTypes[i] = ERecipe.Unknown;
        }
        NotMostPreferredCount = 0;
    }

    #endregion
}
