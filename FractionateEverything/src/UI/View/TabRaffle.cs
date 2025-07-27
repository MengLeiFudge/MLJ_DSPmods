using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.UI.View.TabRecipeAndBuilding;
using static FE.Utils.Utils;

namespace FE.UI.View;

public static class TabRaffle {
    public static RectTransform _windowTrans;

    #region 选择物品

    private static Text textCurrItem;
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick(bool showLocked) {
        //_windowTrans.anchoredPosition是窗口的中心点
        //Popup的位置是弹出窗口的左上角
        //所以要向右（x+）向上（y+）
        float x = _windowTrans.anchoredPosition.x + _windowTrans.rect.width / 2;
        float y = _windowTrans.anchoredPosition.y + _windowTrans.rect.height / 2;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            SelectedItem = item;
        }, false, item => {
            BaseRecipe recipe = GetRecipe<BaseRecipe>(SelectedRecipeType, item.ID);
            return recipe != null && (showLocked || recipe.IsUnlocked);
        });
    }

    #endregion

    public static ConfigEntry<int> TicketTypeEntry;
    public static int[] TicketIds = [
        IFE电磁奖券, IFE能量奖券, IFE结构奖券, IFE信息奖券, IFE引力奖券, IFE宇宙奖券, IFE黑雾奖券,
    ];
    public static string[] TicketTypeNames = [
        "电磁奖券".Translate(), "能量奖券".Translate(), "结构奖券".Translate(),
        "信息奖券".Translate(), "引力奖券".Translate(), "宇宙奖券".Translate(), "黑雾奖券".Translate()
    ];
    public static int SelectedTicketId => TicketIds[TicketTypeEntry.Value];
    public static int SelectedTicketMatrixId => LDB.items.Select(SelectedTicketId).maincraft.Items[0];
    public static float SelectedTicketRatioPlus => SelectedTicketId == IFE宇宙奖券 ? 2.0f : 1.0f;
    public static Text TicketCountText1;
    public static Text TicketCountText2;

    /// <summary>
    /// 下一抽是第几抽。
    /// </summary>
    public static int RecipeRaffleCount = 1;
    public static double RecipeRaffleRate => 0.006 + Math.Max(RecipeRaffleCount - 73, 0) * 0.06;

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
        // {
        //     var tab = wnd.AddTab(trans, "自选卡池");
        //     x = 0f;
        //     y = 10f;
        // }
        {
            var tab = wnd.AddTab(trans, "配方卡池");
            x = 0f;
            y = 10f;
            textCurrItem = wnd.AddText2(x, y + 5f, tab, "当前物品：", 15, "textCurrItem");
            btnSelectedItem = wnd.AddImageButton(x + textCurrItem.preferredWidth + 5f, y, tab,
                SelectedItem.ID, "button-change-item",
                () => { OnButtonChangeItemClick(false); }, () => { OnButtonChangeItemClick(true); },
                "切换说明", "左键在已解锁配方之间切换，右键在全部可用配方中切换");
            //todo: 修复按钮提示窗后移除该内容
            wnd.AddTipsButton2(x + textCurrItem.preferredWidth + 5f + 60, y + 11f, tab,
                "切换说明", "左键在已解锁配方之间切换，右键在全部可用配方中切换");
            wnd.AddComboBox(x + 250, y, tab, "配方类型").WithItems(RecipeTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(RecipeTypeEntry);
            y += 50f;
            wnd.AddComboBox(x, y, tab, "卡池选择").WithItems(RecipeTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(RecipeTypeEntry);
            wnd.AddComboBox(x + 250, y, tab, "奖券选择").WithItems(TicketTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(TicketTypeEntry);
            TicketCountText1 = wnd.AddText2(x + 500, y, tab, "奖券数目", 15, "text-ticket-count-1");
            y += 38f;
            wnd.AddButton(x, y, 200, tab, "单抽", 16, "button-raffle-recipe-1",
                () => RaffleRecipe(1));
            wnd.AddButton(x + 220, y, 200, tab, "十连", 16, "button-raffle-recipe-10",
                () => RaffleRecipe(10));
            wnd.AddButton(x + 440, y, 200, tab, "百连", 16, "button-raffle-recipe-100",
                () => RaffleRecipe(100, 5));
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
            wnd.AddButton(x + 220, y, 200, tab, "十连", 16, "button-raffle-building-10",
                () => RaffleBuilding(10));
            wnd.AddButton(x + 440, y, 200, tab, "百连", 16, "button-raffle-building-100",
                () => RaffleBuilding(100, 5));
        }
    }

    public static void UpdateUI() {
        btnSelectedItem.SetSprite(SelectedItem.iconSprite);
        TicketCountText1.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId)}";
        TicketCountText2.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId)}";
    }

    /// <summary>
    /// 配方卡池抽卡。
    /// </summary>
    /// <param name="ticketCount">抽卡次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽卡结果</param>
    public static void RaffleRecipe(int ticketCount, int oneLineMaxCount = 1) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        //构建杂项物品奖励列表
        //主体为已解锁的非建筑物品，可抽到精华，不可抽到原胚、核心等
        HashSet<int> itemHashSet = [];
        foreach (ItemProto item in LDB.items.dataArray) {
            if ((item.ID >= IFE分馏塔原胚普通 && item.ID <= IFE分馏塔原胚定向)
                //这里先排除掉精华，后面再加
                || (item.ID >= IFE复制精华 && item.ID <= IFE转化精华)
                || (item.ID >= IFE分馏配方通用核心 && item.ID <= IFE分馏塔增幅芯片)
                || item.ID == I沙土
                || item.BuildMode != 0
                || itemValue[item.ID] >= maxValue) {
                continue;
            }
            if (GameMain.history.ItemUnlocked(item.ID)) {
                itemHashSet.Add(item.ID);
            }
        }
        //初步构建杂项物品奖励列表
        if (itemHashSet.Count == 0) {
            UIMessageBox.Show("提示", "时机未到，再探索一会当前星球吧！", "确认", UIMessageBox.WARNING);
            return;
        }
        List<int> items = itemHashSet.ToList();
        while (items.Count < 1000) {
            items.InsertRange(items.Count, itemHashSet);
        }
        //精华概率增加至总比例10%以上
        int essenceCount = 0;
        while ((float)essenceCount / items.Count < 0.1f / 0.6f) {
            for (int itemID = IFE复制精华; itemID <= IFE转化精华; itemID++) {
                items.Add(itemID);
            }
        }
        //排序一下
        items.Sort();
        //构建可抽到的分馏配方列表
        List<BaseRecipe> recipes = [..GetRecipesByMatrix(SelectedTicketMatrixId)];
        recipes.RemoveAll(recipe => recipe.MemoryCount >= recipe.MaxMemoryCount);
        if (recipes.Count == 0) {
            UIMessageBox.Show("提示", "该卡池已经没有配方可以抽取了！", "确认", UIMessageBox.WARNING);
            return;
        }
        List<BaseRecipe> recipesTemp = [..recipes];
        int removedCount = recipes.RemoveAll(recipe => !GameMain.history.ItemUnlocked(recipe.InputID));
        int oneLineCount = 0;
        if (recipes.Count == 0) {
            StringBuilder tip = new StringBuilder($"还有{removedCount}个物品尚未解锁，现在抽取不到对应配方！\n\n"
                                                  + $"未解锁的物品为：\n");
            while (removedCount > 0) {
                tip.Append(LDB.items.Select(recipesTemp[removedCount - 1].InputID).name);
                oneLineCount++;
                if (oneLineCount >= oneLineMaxCount) {
                    tip.Append("\n");
                    oneLineCount = 0;
                } else {
                    tip.Append("          ");
                }
                removedCount--;
            }
            UIMessageBox.Show("提示", tip.ToString(), "确认", UIMessageBox.WARNING);
            return;
        }
        if (!TakeItem(SelectedTicketId, ticketCount)) {
            return;
        }
        StringBuilder sb = new StringBuilder("获得了以下物品：\n");
        StringBuilder sb2 = new StringBuilder();
        oneLineCount = 0;
        while (ticketCount > 0) {
            ticketCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            //分馏配方通用核心（0.05%）
            currRate += 0.0005 * SelectedTicketRatioPlus;
            if (randDouble < currRate) {
                AddItemToPackage(IFE分馏配方通用核心, 1);
                sb.Append($"{LDB.items.Select(IFE分馏配方通用核心).name} x 1".WithValueColor(IFE分馏配方通用核心));
                oneLineCount++;
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else {
                    sb.Append("          ");
                }
                RecipeRaffleCount = 1;
                continue;
            }
            //配方（0.6%，74抽开始后每抽增加6%）
            currRate += RecipeRaffleRate * SelectedTicketRatioPlus;
            if (randDouble < currRate) {
                //按照当前配方奖池随机抽取
                BaseRecipe recipe = recipes[GetRandInt(0, recipes.Count)];
                sb.Append($"{recipe.TypeName}".WithColor(Gold));
                if (!recipe.IsUnlocked) {
                    recipe.Level = 1;
                    recipe.Quality = 1;
                    sb2.AppendLine($"{recipe.TypeName} 已解锁".WithColor(Orange));
                } else {
                    recipe.MemoryCount++;
                    sb2.AppendLine($"{recipe.TypeName} 已转为对应回响，该配方回响数目：{recipe.MemoryCount}".WithColor(Orange));
                }
                oneLineCount++;
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else {
                    sb.Append("          ");
                }
                RecipeRaffleCount = 1;
                continue;
            }
            RecipeRaffleCount++;
            //剩余的概率中，60%各种非建筑的物品（不含分馏某些特殊物品）
            double ratioItem = (1 - currRate) * 0.6 / items.Count;
            bool getItem = false;
            foreach (int itemId in items) {
                currRate += ratioItem;
                if (randDouble < currRate) {
                    float ratio = itemValue[SelectedTicketId] / itemValue[itemId];
                    int count = ratio <= 49
                        ? (int)Math.Ceiling(ratio * 0.5f)
                        : (int)Math.Ceiling(Math.Sqrt(ratio) * 7 * 0.5f);
                    AddItemToModData(itemId, count);
                    sb.Append($"{LDB.items.Select(itemId).name} x {count}".WithValueColor(itemId));
                    oneLineCount++;
                    if (oneLineCount >= oneLineMaxCount) {
                        sb.Append("\n");
                        oneLineCount = 0;
                    } else {
                        sb.Append("          ");
                    }
                    getItem = true;
                    break;
                }
            }
            if (getItem) {
                continue;
            }
            //40%沙土
            int sandCount = (int)Math.Ceiling(itemValue[SelectedTicketId] / itemValue[I沙土] * 0.5f);
            AddItemToPackage(I沙土, sandCount);
            sb.Append($"{LDB.items.Select(I沙土).name} x {sandCount}".WithValueColor(I沙土));
            oneLineCount++;
            if (oneLineCount >= oneLineMaxCount) {
                sb.Append("\n");
                oneLineCount = 0;
            } else {
                sb.Append("          ");
            }
        }
        UIMessageBox.Show("抽卡结果", sb.ToString().TrimEnd('\n') + "\n\n" + sb2, "确认", UIMessageBox.INFO);
    }

    /// <summary>
    /// 建筑卡池抽卡。
    /// </summary>
    /// <param name="ticketCount">抽卡次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽卡结果</param>
    public static void RaffleBuilding(int ticketCount, int oneLineMaxCount = 1) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        //构建杂项物品奖励列表
        //主体为已解锁的建筑物品，可抽到原胚，不可抽到精华、核心等
        HashSet<int> itemHashSet = [];
        foreach (ItemProto item in LDB.items.dataArray) {
            //这里先排除掉分馏塔和原胚，后面再加
            if ((item.ID >= IFE交互塔 && item.ID <= IFE转化塔)
                || item.BuildMode == 0
                || itemValue[item.ID] >= maxValue) {
                continue;
            }
            if (GameMain.history.ItemUnlocked(item.ID)) {
                itemHashSet.Add(item.ID);
            }
        }
        //初步构建杂项物品奖励列表
        if (itemHashSet.Count == 0) {
            UIMessageBox.Show("提示", "时机未到，再探索一会当前星球吧！", "确认", UIMessageBox.WARNING);
            return;
        }
        List<int> items = itemHashSet.ToList();
        while (items.Count < 1000) {
            items.InsertRange(items.Count, itemHashSet);
        }
        //分馏塔概率增加至总比例5%以上，原胚概率增加至总比例25%以上
        int buildingCount = 0;
        int protoCount = 0;
        while (true) {
            while ((float)buildingCount / items.Count < 0.05f / 0.6f) {
                for (int itemID = IFE交互塔; itemID <= IFE转化塔; itemID++) {
                    items.Add(itemID);
                    buildingCount++;
                }
            }
            while ((float)protoCount / items.Count < 0.25f / 0.6f) {
                for (int itemID = IFE分馏塔原胚普通; itemID <= IFE分馏塔原胚传说; itemID++) {
                    for (int i = 0; i < Math.Pow(2, IFE分馏塔原胚传说 - itemID); itemID++) {
                        items.Add(itemID);
                        protoCount++;
                    }
                }
            }
            if ((float)buildingCount / items.Count >= 0.05f / 0.6f
                && (float)protoCount / items.Count >= 0.25f / 0.6f) {
                break;
            }
        }
        //排序一下
        items.Sort();
        if (!TakeItem(SelectedTicketId, ticketCount)) {
            return;
        }
        StringBuilder sb = new StringBuilder("获得了以下物品：\n");
        int oneLineCount = 0;
        while (ticketCount > 0) {
            ticketCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            //分馏塔增幅芯片（0.3%）
            currRate += 0.003 * SelectedTicketRatioPlus;
            if (randDouble < currRate) {
                AddItemToPackage(IFE分馏塔增幅芯片, 1);
                sb.Append($"{LDB.items.Select(IFE分馏塔增幅芯片).name} x 1".WithValueColor(IFE分馏塔增幅芯片));
                oneLineCount++;
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else {
                    sb.Append("          ");
                }
                continue;
            }
            //剩余的概率中，60%各种建筑（含有分馏某些特殊物品）
            double ratioItem = (1 - currRate) * 0.6 / items.Count;
            bool getItem = false;
            foreach (int itemId in items) {
                currRate += ratioItem;
                if (randDouble < currRate) {
                    float ratio = itemValue[SelectedTicketId] / itemValue[itemId];
                    int count = ratio <= 49
                        ? (int)Math.Ceiling(ratio * 0.5f)
                        : (int)Math.Ceiling(Math.Sqrt(ratio) * 7 * 0.5f);
                    AddItemToModData(itemId, count);
                    sb.Append($"{LDB.items.Select(itemId).name} x {count}");
                    oneLineCount++;
                    if (oneLineCount >= oneLineMaxCount) {
                        sb.Append("\n");
                        oneLineCount = 0;
                    } else {
                        sb.Append("          ");
                    }
                    getItem = true;
                    break;
                }
            }
            if (getItem) {
                continue;
            }
            //40%沙土
            int sandCount = (int)Math.Ceiling(itemValue[SelectedTicketId] / itemValue[I沙土] * 0.5f);
            AddItemToPackage(I沙土, sandCount);
            sb.Append($"{LDB.items.Select(I沙土).name} x {sandCount}");
            oneLineCount++;
            if (oneLineCount >= oneLineMaxCount) {
                sb.Append("\n");
                oneLineCount = 0;
            } else {
                sb.Append("          ");
            }
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
