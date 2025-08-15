using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketRaffle {
    public static RectTransform _windowTrans;
    public static RectTransform tabRecipeRaffle;
    public static RectTransform tabBuildingRaffle;

    public static ConfigEntry<int> TicketTypeEntry;
    public static ConfigEntry<bool> EnableAutoRaffleEntry;
    public static bool[] EnableAutoRaffle = [false, false, false, false, false, false, false];
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
    public static Text ticketCountText1;

    public static Text ticketCountText2;
    public static bool[] ignoreRecipeCount = new bool[TicketIds.Length];

    /// <summary>
    /// 下一抽是第几抽。
    /// </summary>
    public static int RecipeRaffleCount = 1;
    public static double RecipeRaffleRate => 0.006 + Math.Max(0, RecipeRaffleCount - 73) * 0.06;


    private static DateTime nextFreshTime;
    private static Text textLeftTime;
    private static Text[] textItemInfo = new Text[3];

    public static void LoadConfig(ConfigFile configFile) {
        TicketTypeEntry = configFile.Bind("LimitedTimeStore", "Ticket Type", 0, "想要使用的奖券类型。");
        if (TicketTypeEntry.Value < 0 || TicketTypeEntry.Value >= TicketIds.Length) {
            TicketTypeEntry.Value = 0;
        }
        EnableAutoRaffleEntry = configFile.Bind("LimitedTimeStore", "Enable Auto Raffle", false, "是否自动抽取。");
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        {
            var tab = wnd.AddTab(trans, "奖券抽奖");
            tabRecipeRaffle = tab;
            x = 0f;
            y = 10f;
            wnd.AddComboBox(x, y, tab, "奖券选择").WithItems(TicketTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(TicketTypeEntry);
            wnd.AddTipsButton2(x + 240, y + 6f, tab, "配方卡池说明",
                "选择某种奖券后，只能抽取对应层级的配方。"
                + "宇宙奖券比其他奖券效果更强，不仅可以抽取所有配方，还能以双倍概率获取配方和分馏配方通用核心。\n"
                + "概率公示：\n"
                + "分馏配方通用核心：0.05%\n"
                + "分馏配方：0.6%（至多90抽必出）\n"
                + "杂项物品：59.61%\n"
                + "沙土：39.74%");
            ticketCountText1 = wnd.AddText2(x + 300, y, tab, "奖券数目", 15, "text-ticket-count-1");
            wnd.AddCheckBox(x + 500, y, tab, EnableAutoRaffleEntry, "自动抽取");
            y += 38f;
            wnd.AddButton(x, y, 200, tab, "配方单抽", 16, "button-raffle-recipe-1",
                () => RaffleRecipe(1));
            wnd.AddButton(x + 220, y, 200, tab, "配方十连", 16, "button-raffle-recipe-10",
                () => RaffleRecipe(10));
            wnd.AddButton(x + 440, y, 200, tab, "配方百连", 16, "button-raffle-recipe-100",
                () => RaffleRecipe(100, 5));
            y += 38f;
            wnd.AddComboBox(x, y, tab, "奖券选择").WithItems(TicketTypeNames).WithSize(150f, 0f)
                .WithConfigEntry(TicketTypeEntry);
            wnd.AddTipsButton2(x + 240, y + 6f, tab, "建筑卡池说明",
                "无论选择哪种奖券，都不影响可以获取的建筑类型。"
                + "宇宙奖券比其他奖券效果更强，可以以双倍概率获取分馏塔增幅芯片。\n"
                + "概率公示：\n"
                + "分馏塔增幅芯片：0.3%\n"
                + "分馏塔原胚：25%\n"
                + "分馏塔：5%\n"
                + "其他建筑：39.82%\n"
                + "沙土：39.88%");
            ticketCountText2 = wnd.AddText2(x + 300, y, tab, "奖券数目", 15, "text-ticket-count-2");
            wnd.AddCheckBox(x + 500, y, tab, EnableAutoRaffleEntry, "自动抽取");
            y += 38f;
            wnd.AddButton(x, y, 200, tab, "建筑单抽", 16, "button-raffle-building-1",
                () => RaffleBuilding(1));
            wnd.AddButton(x + 220, y, 200, tab, "建筑十连", 16, "button-raffle-building-10",
                () => RaffleBuilding(10));
            wnd.AddButton(x + 440, y, 200, tab, "建筑百连", 16, "button-raffle-building-100",
                () => RaffleBuilding(100, 5));
        }
        {
            var tab = wnd.AddTab(trans, "自选抽奖");
            x = 0f;
            y = 10f;
        }
        {
            var tab = wnd.AddTab(trans, "限时商店");
            nextFreshTime = DateTime.Now.Date.AddHours(DateTime.Now.Hour)
                .AddMinutes(DateTime.Now.Minute / 10 * 10 + 10);
            x = 0f;
            y = 10f;
            textLeftTime = wnd.AddText2(x, y, tab, "剩余刷新时间：xx s", 15, "textLeftTime");
            y += 36f;
            textItemInfo[0] = wnd.AddText2(x, y, tab, "物品0信息", 15, "textLeftTime0");
            wnd.AddButton(x + 350, y, 400, tab, "兑换", 16, "btn-buy-time1",
                () => ExchangeItem(0));
            y += 36f;
            textItemInfo[1] = wnd.AddText2(x, y, tab, "物品1信息", 15, "textLeftTime1");
            wnd.AddButton(x + 350, y, 400, tab, "兑换", 16, "btn-buy-time2",
                () => ExchangeItem(1));
            y += 36f;
            textItemInfo[2] = wnd.AddText2(x, y, tab, "物品2信息", 15, "textLeftTime2");
            wnd.AddButton(x + 350, y, 400, tab, "兑换", 16, "btn-buy-time3",
                () => ExchangeItem(2));
            y += 36f;
        }
    }

    public static void UpdateUI() {
        if (tabRecipeRaffle.gameObject.activeSelf) {
            AutoRaffle(true);
        }
        // else if (tabBuildingRaffle.gameObject.activeSelf) {
        //     AutoRaffle(false);
        // }
        ticketCountText1.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId)}";
        // EnableAutoRaffleEntry.Value = EnableAutoRaffle[TicketTypeEntry.Value];

        ticketCountText2.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId)}";


        if (DateTime.Now >= nextFreshTime) {
            nextFreshTime = nextFreshTime.AddMinutes(10);
            //更新三份限时购买物品的信息
            // textItemInfo[0].text = GetTimeLimitedItemInfo(0);
            // textItemInfo[1].text = GetTimeLimitedItemInfo(1);
            // textItemInfo[2].text = GetTimeLimitedItemInfo(2);
        }
        TimeSpan ts = nextFreshTime - DateTime.Now;
        textLeftTime.text = $"还有 {(int)ts.TotalMinutes} min {ts.Seconds} s 刷新";
    }

    /// <summary>
    /// 配方卡池抽奖。
    /// </summary>
    /// <param name="raffleCount">抽奖次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽奖结果</param>
    /// <param name="showMessage">是否弹窗询问、显示结果</param>
    public static void RaffleRecipe(int raffleCount, int oneLineMaxCount = 1, bool showMessage = true) {
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
            if (showMessage) {
                UIMessageBox.Show("提示", "时机未到，再探索一会当前星球吧！", "确认", UIMessageBox.WARNING);
            }
            return;
        }
        List<int> items = itemHashSet.ToList();
        while (items.Count < 10000) {
            items.InsertRange(items.Count, itemHashSet);
        }
        //精华概率增加至总比例10%以上
        int essenceCount = 0;
        while ((float)essenceCount / items.Count < 0.1f / 0.6f) {
            for (int itemID = IFE复制精华; itemID <= IFE转化精华; itemID++) {
                items.Add(itemID);
                essenceCount++;
            }
        }
        //排序一下
        items.Sort();
        //构建可抽到的分馏配方列表
        List<BaseRecipe> recipes = [..GetRecipesByMatrix(SelectedTicketMatrixId)];
        recipes.RemoveAll(recipe => recipe.IsMaxMemory);
        if (showMessage && recipes.Count == 0 && !ignoreRecipeCount[TicketTypeEntry.Value]) {
            UIMessageBox.Show("提示", "该卡池已经没有配方可以抽取了！\n确定继续抽取吗？", "确定", "取消", UIMessageBox.WARNING,
                () => {
                    ignoreRecipeCount[TicketTypeEntry.Value] = true;
                    RaffleRecipe(raffleCount, oneLineMaxCount);
                }, null);
            return;
        }
        List<BaseRecipe> recipesTemp = [..recipes];
        int removedCount = recipes.RemoveAll(recipe => !GameMain.history.ItemUnlocked(recipe.InputID));
        int oneLineCount = 0;
        if (showMessage && recipes.Count == 0) {
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
        if (!TakeItem(SelectedTicketId, raffleCount, out _, showMessage)) {
            return;
        }
        Dictionary<int, int> specialItemDic = [];
        Dictionary<int, int> commonItemDic = [];
        StringBuilder sb = new StringBuilder("获得了以下物品：\n");
        StringBuilder sb2 = new StringBuilder();
        oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            //分馏配方通用核心（0.05%）
            currRate += 0.0005 * SelectedTicketRatioPlus;
            if (randDouble < currRate) {
                if (specialItemDic.ContainsKey(IFE分馏配方通用核心)) {
                    specialItemDic[IFE分馏配方通用核心]++;
                } else {
                    specialItemDic[IFE分馏配方通用核心] = 1;
                }
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
            if (recipes.Count > 0) {
                currRate += RecipeRaffleRate * SelectedTicketRatioPlus;
                if (randDouble < currRate) {
                    //按照当前配方奖池随机抽取
                    BaseRecipe recipe = recipes[GetRandInt(0, recipes.Count)];
                    sb.Append($"{recipe.TypeName}".WithColor(Gold));
                    recipe.RewardThis();
                    if (recipe.Memory == 0) {
                        sb2.AppendLine($"{recipe.TypeName} 已解锁".WithColor(Orange));
                    } else {
                        sb2.AppendLine($"{recipe.TypeName} 已转为同名回响（当前持有 {recipe.Memory} 同名回响）".WithColor(Orange));
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
                    if (commonItemDic.ContainsKey(itemId)) {
                        commonItemDic[itemId] += count;
                    } else {
                        commonItemDic[itemId] = count;
                    }
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
            if (commonItemDic.ContainsKey(I沙土)) {
                commonItemDic[I沙土] += sandCount;
            } else {
                commonItemDic[I沙土] = sandCount;
            }
            sb.Append($"{LDB.items.Select(I沙土).name} x {sandCount}".WithValueColor(I沙土));
            oneLineCount++;
            if (oneLineCount >= oneLineMaxCount) {
                sb.Append("\n");
                oneLineCount = 0;
            } else {
                sb.Append("          ");
            }
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果", sb.ToString().TrimEnd('\n')
                                      + "\n\n"
                                      + sb2
                                      + "\n\n选择提取方式：\n"
                                      + "数据中心：将全部物品以数据形式存储在分馏数据中心\n"
                                      + "部分提取：将分馏配方通用核心提取到背包，除此之外的物品存储在分馏数据中心\n"
                                      + "全部提取：将全部物品以实体形式提取到背包",
                "数据中心", "部分提取", "全部提取", UIMessageBox.INFO,
                () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                }, () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                }, () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                });
        } else {
            foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                AddItemToModData(p.Key, p.Value);
            }
            foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                AddItemToModData(p.Key, p.Value);
            }
        }
    }

    /// <summary>
    /// 建筑卡池抽奖。
    /// </summary>
    /// <param name="raffleCount">抽奖次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽奖结果</param>
    /// <param name="showMessage">是否弹窗询问、显示结果</param>
    public static void RaffleBuilding(int raffleCount, int oneLineMaxCount = 1, bool showMessage = true) {
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
            if (showMessage) {
                UIMessageBox.Show("提示", "时机未到，再探索一会当前星球吧！", "确认", UIMessageBox.WARNING);
            }
            return;
        }
        List<int> items = itemHashSet.ToList();
        while (items.Count < 10000) {
            items.InsertRange(items.Count, itemHashSet);
        }
        //分馏塔概率增加至总比例5%以上，原胚概率增加至总比例25%以上
        int buildingCount = 0;
        int protoCount = 0;
        int[] buildingAddCount = [10, 30, 5, 2, 10, 10, 10];
        int[] protoAddCount = [16, 8, 4, 2, 1];
        while (true) {
            while ((float)buildingCount / items.Count < 0.05f / 0.6f) {
                for (int itemID = IFE交互塔; itemID <= IFE转化塔; itemID++) {
                    for (int i = 0; i < buildingAddCount[itemID - IFE交互塔]; i++) {
                        items.Add(itemID);
                        buildingCount++;
                    }
                }
            }
            while ((float)protoCount / items.Count < 0.25f / 0.6f) {
                for (int itemID = IFE分馏塔原胚普通; itemID <= IFE分馏塔原胚传说; itemID++) {
                    for (int i = 0; i < protoAddCount[itemID - IFE分馏塔原胚普通]; i++) {
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
        if (!TakeItem(SelectedTicketId, raffleCount, out _, showMessage)) {
            return;
        }
        Dictionary<int, int> specialItemDic = [];
        Dictionary<int, int> commonItemDic = [];
        StringBuilder sb = new StringBuilder("获得了以下物品：\n");
        int oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            //分馏塔增幅芯片（0.3%）
            currRate += 0.003 * SelectedTicketRatioPlus;
            if (randDouble < currRate) {
                if (specialItemDic.ContainsKey(IFE分馏塔增幅芯片)) {
                    specialItemDic[IFE分馏塔增幅芯片]++;
                } else {
                    specialItemDic[IFE分馏塔增幅芯片] = 1;
                }
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
                    if (itemId >= IFE交互塔 && itemId <= IFE转化塔) {
                        if (specialItemDic.ContainsKey(itemId)) {
                            specialItemDic[itemId] += count;
                        } else {
                            specialItemDic[itemId] = count;
                        }
                    } else {
                        if (commonItemDic.ContainsKey(itemId)) {
                            commonItemDic[itemId] += count;
                        } else {
                            commonItemDic[itemId] = count;
                        }
                    }
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
            if (commonItemDic.ContainsKey(I沙土)) {
                commonItemDic[I沙土] += sandCount;
            } else {
                commonItemDic[I沙土] = sandCount;
            }
            sb.Append($"{LDB.items.Select(I沙土).name} x {sandCount}".WithValueColor(I沙土));
            oneLineCount++;
            if (oneLineCount >= oneLineMaxCount) {
                sb.Append("\n");
                oneLineCount = 0;
            } else {
                sb.Append("          ");
            }
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果", sb.ToString().TrimEnd('\n')
                                      + "\n\n选择提取方式：\n"
                                      + "数据中心：将全部物品以数据形式存储在分馏数据中心\n"
                                      + "部分提取：将分馏塔增幅芯片、分馏塔提取到背包，其他物品存储在分馏数据中心\n"
                                      + "全部提取：将全部物品以实体形式提取到背包",
                "数据中心", "部分提取", "全部提取", UIMessageBox.INFO,
                () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                }, () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                }, () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                });
        } else {
            foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                AddItemToModData(p.Key, p.Value);
            }
            foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                AddItemToModData(p.Key, p.Value);
            }
        }
    }

    private static long times = 0;

    /// <summary>
    /// 每0.1s左右自动抽取一次百连。
    /// </summary>
    public static void AutoRaffle(bool recipeRaffle) {
        times++;
        if (times % 10 != 0) {
            return;
        }
        times = 0;
        if (EnableAutoRaffleEntry.Value) {
            if (recipeRaffle) {
                RaffleRecipe(100, 5, false);
            } else {
                RaffleBuilding(100, 5, false);
            }
        }
    }

    public static void ExchangeItem(int index) { }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        RecipeRaffleCount = r.ReadInt32();
        // for (int i = 0; i < EnableAutoRaffle.Length; i++) {
        //     EnableAutoRaffle[i] = r.ReadBoolean();
        // }
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(RecipeRaffleCount);
        // for (int i = 0; i < EnableAutoRaffle.Length; i++) {
        //     w.Write(EnableAutoRaffle[i]);
        // }
    }

    public static void IntoOtherSave() {
        RecipeRaffleCount = 0;
        // for (int i = 0; i < EnableAutoRaffle.Length; i++) {
        //     EnableAutoRaffle[i] = false;
        // }
    }

    #endregion
}
