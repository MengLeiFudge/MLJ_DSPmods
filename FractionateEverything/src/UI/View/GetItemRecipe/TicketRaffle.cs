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
    private static RectTransform window;
    private static RectTransform tab;

    private static readonly int[] TicketIds = [
        IFE电磁奖券, IFE能量奖券, IFE结构奖券, IFE信息奖券, IFE引力奖券, IFE宇宙奖券, IFE黑雾奖券,
    ];
    private static readonly string[] TicketTypeNames = [
        "电磁奖券".Translate(), "能量奖券".Translate(), "结构奖券".Translate(),
        "信息奖券".Translate(), "引力奖券".Translate(), "宇宙奖券".Translate(), "黑雾奖券".Translate()
    ];

    private static ConfigEntry<int> TicketTypeEntry1;
    private static int TicketType1;
    private static int SelectedTicketId1 => TicketIds[TicketTypeEntry1.Value];
    private static int SelectedTicketMatrixId1 => LDB.items.Select(SelectedTicketId1).maincraft.Items[0];
    private static float SelectedTicketRatioPlus1 => SelectedTicketId1 == IFE宇宙奖券 ? 2.0f : 1.0f;
    private static Text ticketCountText1;
    private static ConfigEntry<bool> EnableAutoRaffleEntry1;
    private static readonly bool[] ignoreRecipeCount = new bool[TicketIds.Length];
    /// <summary>
    /// 下一抽是第几抽。
    /// </summary>
    private static int RecipeRaffleCount = 1;
    private static double RecipeRaffleRate => 0.006 + Math.Max(0, RecipeRaffleCount - 73) * 0.06;

    private static ConfigEntry<int> TicketTypeEntry2;
    private static int TicketType2;
    private static int SelectedTicketId2 => TicketIds[TicketTypeEntry2.Value];
    private static int SelectedTicketMatrixId2 => LDB.items.Select(SelectedTicketId2).maincraft.Items[0];
    private static float SelectedTicketRatioPlus2 => SelectedTicketId2 == IFE宇宙奖券 ? 2.0f : 1.0f;
    private static Text ticketCountText2;
    private static ConfigEntry<bool> EnableAutoRaffleEntry2;

    public static void AddTranslations() {
        Register("奖券抽奖", "Ticket Raffle");

        Register("当前奖券", "Current ticket");
    }

    public static void LoadConfig(ConfigFile configFile) {
        TicketTypeEntry1 = configFile.Bind("Ticket Raffle", "Ticket Type 1", 0, "配方抽奖奖券类型。");
        if (TicketTypeEntry1.Value < 0 || TicketTypeEntry1.Value >= TicketIds.Length) {
            TicketTypeEntry1.Value = 0;
        }
        TicketType1 = TicketTypeEntry1.Value;
        EnableAutoRaffleEntry1 = configFile.Bind("Ticket Raffle", "Enable Auto Raffle 1", false, "配方抽奖是否自动百连。");
        TicketTypeEntry2 = configFile.Bind("Ticket Raffle", "Ticket Type 2", 0, "配方抽奖奖券类型。");
        if (TicketTypeEntry2.Value < 0 || TicketTypeEntry2.Value >= TicketIds.Length) {
            TicketTypeEntry2.Value = 0;
        }
        TicketType2 = TicketTypeEntry2.Value;
        EnableAutoRaffleEntry2 = configFile.Bind("Ticket Raffle", "Enable Auto Raffle 2", false, "配方抽奖是否自动百连。");
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "奖券抽奖");
        float x = 0f;
        float y = 18f;
        var cbx = wnd.AddComboBox(x, y, tab, "当前奖券")
            .WithItems(TicketTypeNames).WithSize(200, 0).WithConfigEntry(TicketTypeEntry1);
        wnd.AddTipsButton2(x + cbx.Width + 5, y, tab, "配方卡池说明",
            "选择某种奖券后，只能抽取对应层级的配方。"
            + "宇宙奖券比其他奖券效果更强，不仅可以抽取所有配方，还能以双倍概率获取配方和分馏配方通用核心。\n"
            + "概率公示：\n"
            + "分馏配方通用核心：0.05%\n"
            + "分馏配方：0.6%（至多90抽必出）\n"
            + "杂项物品：59.61%\n"
            + "沙土：39.74%");
        ticketCountText1 = wnd.AddText2(x + 350, y, tab, "奖券数目", 15, "text-ticket-count-1");
        wnd.AddCheckBox(x + 500, y, tab, EnableAutoRaffleEntry1, "自动百连");
        y += 36f;
        wnd.AddButton(0, 3, y, tab, "配方单抽", 16, "button-raffle-recipe-1",
            () => RaffleRecipe(1));
        wnd.AddButton(1, 3, y, tab, "配方十连", 16, "button-raffle-recipe-10",
            () => RaffleRecipe(10));
        wnd.AddButton(2, 3, y, tab, "配方百连", 16, "button-raffle-recipe-100",
            () => RaffleRecipe(100, 5));
        y += 36f;
        y += 36f;
        cbx = wnd.AddComboBox(x, y, tab, "当前奖券")
            .WithItems(TicketTypeNames).WithSize(200, 0).WithConfigEntry(TicketTypeEntry2);
        wnd.AddTipsButton2(x + cbx.Width + 5, y, tab, "建筑卡池说明",
            "无论选择哪种奖券，都不影响可以获取的建筑类型。"
            + "宇宙奖券比其他奖券效果更强，可以以双倍概率获取分馏塔增幅芯片。\n"
            + "概率公示：\n"
            + "分馏塔增幅芯片：0.3%\n"
            + "分馏塔原胚：25%\n"
            + "分馏塔：5%\n"
            + "其他建筑：39.82%\n"
            + "沙土：39.88%");
        ticketCountText2 = wnd.AddText2(x + 350, y, tab, "奖券数目", 15, "text-ticket-count-2");
        wnd.AddCheckBox(x + 500, y, tab, EnableAutoRaffleEntry2, "自动百连");
        y += 36f;
        wnd.AddButton(0, 3, y, tab, "建筑单抽", 16, "button-raffle-building-1",
            () => RaffleBuilding(1));
        wnd.AddButton(1, 3, y, tab, "建筑十连", 16, "button-raffle-building-10",
            () => RaffleBuilding(10));
        wnd.AddButton(2, 3, y, tab, "建筑百连", 16, "button-raffle-building-100",
            () => RaffleBuilding(100, 5));
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            EnableAutoRaffleEntry1.Value = false;
            EnableAutoRaffleEntry2.Value = false;
            return;
        }
        AutoRaffle();
        ticketCountText1.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId1)}";
        ticketCountText2.text = $"奖券数目：{GetItemTotalCount(SelectedTicketId2)}";
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
                UIMessageBox.Show("提示".Translate(), "时机未到，再探索一会当前星球吧！", "确定".Translate(), UIMessageBox.WARNING);
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
        List<BaseRecipe> recipes = [..GetRecipesUnderMatrix(SelectedTicketMatrixId1)];
        recipes.RemoveAll(recipe => recipe.IsMaxMemory);
        if (showMessage && recipes.Count == 0 && !ignoreRecipeCount[TicketTypeEntry1.Value]) {
            UIMessageBox.Show("提示".Translate(), $"该卡池已经没有配方可以抽取了！\n确定继续抽取{"吗？".Translate()}", "确定".Translate(),
                "取消".Translate(), UIMessageBox.WARNING,
                () => {
                    ignoreRecipeCount[TicketTypeEntry1.Value] = true;
                    RaffleRecipe(raffleCount, oneLineMaxCount);
                }, null);
            return;
        }
        List<BaseRecipe> recipesTemp = [..recipes];
        int removedCount = recipes.RemoveAll(recipe => !GameMain.history.ItemUnlocked(recipe.InputID));
        int oneLineCount = 0;
        if (showMessage && recipes.Count == 0 && removedCount > 0) {
            //todo：调整这里
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
            UIMessageBox.Show("提示".Translate(), tip.ToString(), "确定".Translate(), UIMessageBox.WARNING);
            return;
        }
        if (!TakeItem(SelectedTicketId1, raffleCount, out _, showMessage)) {
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
            currRate += 0.0005 * SelectedTicketRatioPlus1;
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
                currRate += RecipeRaffleRate * SelectedTicketRatioPlus1;
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
                    float ratio = itemValue[SelectedTicketId1] / itemValue[itemId];
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
            int sandCount = (int)Math.Ceiling(itemValue[SelectedTicketId1] / itemValue[I沙土] * 0.5f);
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
                UIMessageBox.Show("提示".Translate(), "时机未到，再探索一会当前星球吧！", "确定".Translate(), UIMessageBox.WARNING);
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
        if (!TakeItem(SelectedTicketId2, raffleCount, out _, showMessage)) {
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
            currRate += 0.003 * SelectedTicketRatioPlus2;
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
                    float ratio = itemValue[SelectedTicketId2] / itemValue[itemId];
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
            int sandCount = (int)Math.Ceiling(itemValue[SelectedTicketId2] / itemValue[I沙土] * 0.5f);
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

    private static long lastAtuoRaffleTick = 0;

    /// <summary>
    /// 每0.1s左右自动抽取一次百连。
    /// </summary>
    private static void AutoRaffle() {
        if (TicketType1 != TicketTypeEntry1.Value) {
            TicketType1 = TicketTypeEntry1.Value;
            EnableAutoRaffleEntry1.Value = false;
        }
        if (TicketType2 != TicketTypeEntry2.Value) {
            TicketType2 = TicketTypeEntry2.Value;
            EnableAutoRaffleEntry2.Value = false;
        }
        //todo: vip可以提速
        if (GameMain.gameTick % 10 != 0 || lastAtuoRaffleTick == GameMain.gameTick) {
            return;
        }
        //todo：为什么会扣除200？
        if (EnableAutoRaffleEntry1.Value) {
            RaffleRecipe(100, 5, false);
        }
        if (EnableAutoRaffleEntry2.Value) {
            RaffleBuilding(100, 5, false);
        }
    }

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
