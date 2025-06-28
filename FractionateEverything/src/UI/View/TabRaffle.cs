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

    public static ConfigEntry<int> TicketTypeEntry;
    public static int[] TicketIds = [
        IFE电磁奖券, IFE能量奖券, IFE结构奖券, IFE信息奖券, IFE引力奖券, IFE宇宙奖券, IFE黑雾奖券,
    ];
    public static string[] TicketTypeNames = [
        "电磁奖券".Translate(), "能量奖券".Translate(), "结构奖券".Translate(),
        "信息奖券".Translate(), "引力奖券".Translate(), "宇宙奖券".Translate(), "黑雾奖券".Translate()
    ];
    public static float[] TicketRatioPlus = [
        1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.6f, 1.0f,
    ];
    public static int SelectedTicketId => TicketIds[TicketTypeEntry.Value];
    public static float SelectedTicketRatioPlus => TicketRatioPlus[TicketTypeEntry.Value];
    public static Text TicketCountText1;
    public static Text TicketCountText2;

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

    public static void LoadConfig(ConfigFile configFile) {
        TicketTypeEntry = configFile.Bind("TabRaffle", "Ticket Type", 0, "想要使用的奖券类型。");
        if (TicketTypeEntry.Value < 0 || TicketTypeEntry.Value >= TicketIds.Length) {
            TicketTypeEntry.Value = 0;
        }
    }

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
            wnd.AddComboBox(x + 250, y, tab, "奖券选择").WithItems(TicketTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(TicketTypeEntry);
            TicketCountText1 = wnd.AddText2(x + 500, y, tab, "奖券数目", 15, "text-ticket-count-1");
            y += 38f;
            for (int i = 0; i < PreferredItems.Length; i++) {
                PreferredTexts[i] = wnd.AddText2(x, y, tab, $"优先配方{i + 1}", 15, "text-preferred-recipe");
                int j = i;
                wnd.AddButton(x + 350, y, tab, "设置优先配方", 16, "button-set-preferred-recipe",
                    () => SetPreferredRecipe(j));
                y += 38f;
            }
            wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-raffle-recipe-1",
                () => RaffleRecipe(1));
            wnd.AddButton(x + 300, y, 200, tab, "十连", 16, "button-raffle-recipe-10",
                () => RaffleRecipe(10));
        }
        {
            var tab = wnd.AddTab(trans, "建筑卡池");
            x = 0f;
            y = 10f;
            wnd.AddComboBox(x, y, tab, "卡池选择").WithItems(BuildingTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(BuildingTypeEntry);
            wnd.AddComboBox(x + 250, y, tab, "奖券选择").WithItems(TicketTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(TicketTypeEntry);
            TicketCountText2 = wnd.AddText2(x + 500, y, tab, "奖券数目", 15, "text-ticket-count-2");
            y += 38f;
            wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-raffle-building-1",
                () => RaffleBuilding(1));
            wnd.AddButton(x + 300, y, 200, tab, "十连", 16, "button-raffle-building-10",
                () => RaffleBuilding(10));
        }
    }

    public static void UpdateUI() {
        TicketCountText1.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId)}";
        TicketCountText2.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId)}";
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
        ERecipe oldType = PreferredRecipeTypes[index];
        int oldItemId = PreferredItems[index];
        if (oldType != SelectedRecipeType || oldItemId != SelectedItemId) {
            //有可能交换位置，也有可能是替换
            int changeIndex = -1;
            for (int i = 0; i < PreferredItems.Length; i++) {
                if (PreferredRecipeTypes[i] == SelectedRecipeType && PreferredItems[i] == SelectedItemId) {
                    changeIndex = i;
                }
            }
            if (changeIndex != -1) {
                PreferredRecipeTypes[changeIndex] = PreferredRecipeTypes[index];
                PreferredItems[changeIndex] = PreferredItems[index];
            }
            PreferredRecipeTypes[index] = SelectedRecipeType;
            PreferredItems[index] = SelectedItemId;
        }
        UIMessageBox.Show("提示", "已设定优先配方！", "确认", UIMessageBox.INFO);
    }

    /// <summary>
    /// 抽卡。
    /// </summary>
    /// <param name="count">抽卡次数</param>
    public static void RaffleRecipe(int count) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        for (int i = 0; i < PreferredItems.Length; i++) {
            if (RecipeManager.GetRecipe<BaseRecipe>(PreferredRecipeTypes[i], PreferredItems[i]) == null) {
                UIMessageBox.Show("提示", "你还未设置喜好配方！", "确认", UIMessageBox.WARNING);
                return;
            }
        }
        if (!TakeItem(SelectedTicketId, count)) {
            return;
        }
        List<BaseRecipe> recipeArr = RecipeManager.GetRecipes(RecipeTypes[RecipeTypeEntry.Value]);
        StringBuilder sb = new StringBuilder("获得了以下物品：\n");
        while (count > 0) {
            count--;
            double currRate = 0;
            double randDouble = random.NextDouble();
            //分馏配方核心（0.05%）
            currRate += 0.0005 * SelectedTicketRatioPlus;
            if (randDouble < currRate) {
                AddItem(IFE分馏配方核心, 1);
                sb.AppendLine($"{LDB.items.Select(IFE分馏配方核心).name.WithColor(Gold)} x 1");
                RecipeRaffleCount = 1;
                continue;
            }
            //配方（0.6%，74抽开始后每抽增加6%）
            currRate += RecipeRaffleRate * SelectedTicketRatioPlus;
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
                    AddItem(IFE残破核心, 1);
                    sb.AppendLine($"{recipe.TypeName.WithColor(Red)} => 已转为残破核心");
                }
                RecipeRaffleCount = 1;
                continue;
            }
            RecipeRaffleCount++;
            //分馏原胚（8.8%）
            bool getFracProto = false;
            for (int i = 0; i < FracProtoRateArr.Length; i++) {
                currRate += FracProtoRateArr[i] * SelectedTicketRatioPlus;
                if (randDouble < currRate) {
                    AddItem(FracProtoID[i], 1);
                    Color color = i < 2 ? Green : (i < 4 ? Blue : Purple);
                    sb.AppendLine($"{LDB.items.Select(FracProtoID[i]).name.WithColor(color)} x 1");
                    getFracProto = true;
                    break;
                }
            }
            if (getFracProto) {
                continue;
            }
            //剩余的概率中，50%黑雾掉落
            int enemyDropCount = GameMain.history.enemyDropItemUnlocked.Count;
            if (enemyDropCount > 0) {
                double ratioDarkFog = (1 - currRate) * 0.5 / enemyDropCount;
                bool getDarkFog = false;
                foreach (int itemId in GameMain.history.enemyDropItemUnlocked) {
                    currRate += ratioDarkFog;
                    if (randDouble < currRate) {
                        AddItem(itemId, 200);
                        sb.AppendLine($"{LDB.items.Select(itemId).name} x 200");
                        getDarkFog = true;
                        break;
                    }
                }
                if (getDarkFog) {
                    continue;
                }
            }
            //50%沙土
            AddItem(I沙土, 1000);
            sb.AppendLine($"{LDB.items.Select(I沙土).name} x 1000");
        }
        UIMessageBox.Show("抽卡结果", sb.ToString().TrimEnd('\n'), "确认", UIMessageBox.INFO);
    }

    /// <summary>
    /// 抽卡。
    /// </summary>
    /// <param name="count">抽卡次数</param>
    public static void RaffleBuilding(int count) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (!TakeItem(SelectedTicketId, count)) {
            return;
        }
        StringBuilder sb = new StringBuilder("获得了以下物品：\n");
        while (count > 0) {
            count--;
            double currRate = 0;
            double randDouble = random.NextDouble();
            //建筑增幅芯片（0.3%）
            currRate += 0.003 * SelectedTicketRatioPlus;
            if (randDouble < currRate) {
                AddItem(IFE建筑增幅芯片, 1);
                sb.AppendLine($"{LDB.items.Select(IFE建筑增幅芯片).name.WithColor(Gold)} x 1");
                continue;
            }
            //建筑（3% * 1 + 0.5% * 6）
            bool getBuilding = false;
            for (int i = 0; i < BuildingIds.Length; i++) {
                currRate += (i == BuildingTypeEntry.Value ? 0.03 : 0.005) * SelectedTicketRatioPlus;
                if (randDouble < currRate) {
                    AddItem(BuildingIds[i], 1);
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
                currRate += FracProtoRateArr[i] * 3 * SelectedTicketRatioPlus;
                if (randDouble < currRate) {
                    AddItem(FracProtoID[i], 1);
                    Color color = i < 2 ? Green : (i < 4 ? Blue : Purple);
                    sb.AppendLine($"{LDB.items.Select(FracProtoID[i]).name.WithColor(color)} x 1");
                    getFracProto = true;
                    break;
                }
            }
            if (getFracProto) {
                continue;
            }
            //剩余的概率中，50%黑雾掉落
            int enemyDropCount = GameMain.history.enemyDropItemUnlocked.Count;
            if (enemyDropCount > 0) {
                double ratioDarkFog = (1 - currRate) * 0.5 / enemyDropCount;
                bool getDarkFog = false;
                foreach (int itemId in GameMain.history.enemyDropItemUnlocked) {
                    currRate += ratioDarkFog;
                    if (randDouble < currRate) {
                        AddItem(itemId, 200);
                        sb.AppendLine($"{LDB.items.Select(itemId).name} x 200");
                        getDarkFog = true;
                        break;
                    }
                }
                if (getDarkFog) {
                    continue;
                }
            }
            //50%沙土
            AddItem(I沙土, 1000);
            sb.AppendLine($"{LDB.items.Select(I沙土).name} x 1000");
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
