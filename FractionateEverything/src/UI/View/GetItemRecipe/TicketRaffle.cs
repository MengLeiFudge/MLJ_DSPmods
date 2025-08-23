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
    private static Text ticketCountText1;
    private static ConfigEntry<bool> EnableAutoRaffleEntry1;
    /// <summary>
    /// 下一抽是第几抽。
    /// </summary>
    private static int RecipeRaffleCount = 1;
    private static double RecipeRaffleRate => 0.006 + Math.Max(0, RecipeRaffleCount - 73) * 0.06;

    private static ConfigEntry<int> TicketTypeEntry2;
    private static int TicketType2;
    private static int SelectedTicketId2 => TicketIds[TicketTypeEntry2.Value];
    private static int SelectedTicketMatrixId2 => LDB.items.Select(SelectedTicketId2).maincraft.Items[0];
    private static Text ticketCountText2;
    private static ConfigEntry<bool> EnableAutoRaffleEntry2;

    public static void AddTranslations() {
        Register("奖券抽奖", "Ticket Raffle");

        Register("配方奖池", "Recipe pool");
        Register("配方奖池说明",
            "Except for Dark Fog Tickets, other lottery tickets can draw all recipes up to the level of the lottery ticket used.\n"
            + "Only Dark Fog Tickets, can draw Dark Fog recipes; non-Dark Fog Tickets cannot.\n\n"
            + "Probability announcement:\n"
            + "Fractionate Recipe Core: <=0.20% (the higher the value of the lottery ticket, the higher the probability)\n"
            + "Fractionate recipe: ≈0.6% (guaranteed to appear within 90 draws)\n"
            + "Miscellaneous items: ≈60% (unlocked items only)\n"
            + "Sand: ≈40%",
            "除黑雾奖券外，其他奖券可以抽取不超过所用奖券层级的所有配方。\n"
            + "只有黑雾奖券可以抽取黑雾配方，非黑雾奖券无法抽取。\n\n"
            + "概率公示：\n"
            + "分馏配方通用核心：<=0.20%（奖券价值越高则概率越高）\n"
            + "分馏配方：≈0.6%（至多90抽必出）\n"
            + "杂项物品：≈60%（仅限已解锁的物品）\n"
            + "沙土：≈40%");

        Register("当前奖券", "Current ticket");
        Register("奖券数目", "Ticket count");
        Register("：", ": ");

        Register("单抽", "Single draw");
        Register("十连", "Ten draws");
        Register("百连", "Hundred draws");
        Register("自动百连", "Auto hundred draws");

        Register("建筑奖池", "Building pool");
        Register("建筑奖池说明",
            "The type of lottery ticket does not affect the reward content or probability.\n"
            + "Furthermore, the prize pool may include buildings that have not yet been unlocked.\n\n"
            + "Probability announcement:\n"
            + "Fractionator Increase Chip: <=0.12% (the higher the value of the lottery ticket, the higher the probability)\n"
            + "Frac Building Proto: ≈25% (non-directional only)\n"
            + "Fractionator: ≈5% (the higher the value of the fractionator, the lower the probability)\n"
            + "Miscellaneous buildings: ≈40% (including unlocked buildings)\n"
            + "Sand: ≈40%",
            "奖券类型不影响奖励内容与概率。\n"
            + "并且，该奖池有可能抽出尚未解锁的建筑。\n\n"
            + "概率公示：\n"
            + "分馏塔增幅芯片：<=0.12%（奖券价值越高则概率越高）\n"
            + "分馏塔原胚：≈25%（仅限非定向原胚）\n"
            + "分馏塔：≈5%（价值越高的分馏塔概率越低）\n"
            + "其他建筑：≈40%（包括未解锁的建筑）\n"
            + "沙土：≈40%");

        Register("时机未到，再探索一会当前星球吧！", "The time is not right yet, so let's explore the current planet a little more!");
        Register("该奖池已经没有配方可以抽取了！", "There are no more recipes left to draw from this prize pool!");
        Register("未解锁物品抽不到配方",
            "There are still {0} items that have not been unlocked, so you cannot draw the corresponding recipe at this time!\n\nThe unlocked items are:\n",
            "还有{0}个物品尚未解锁，现在抽取不到对应配方！\n\n未解锁的物品为：\n");
        Register("抽奖结果", "Raffle results");
        Register("获得了以下物品", "Obtained the following items");
        Register("已解锁", "unlocked");
        Register("已转为同名回响提示",
            "has been converted to a homonym echo (currently holding {0} homonym echoes)",
            "已转为同名回响（当前持有 {0} 同名回响）");
        Register("选择提取方式", "Select extraction method");
        Register("数据中心", "Data centre");
        Register("数据中心说明",
            "Store all items in data form in the fractionation data centre",
            "将全部物品以数据形式存储在分馏数据中心");
        Register("部分提取", "Extract part");
        Register("部分提取说明",
            "Extract valuable items to your backpack and store other items in the fractionation data centre",
            "将珍贵物品提取到背包，其他物品存储在分馏数据中心");
        Register("全部提取", "Extract all");
        Register("全部提取说明",
            "Extract all items in physical form to the backpack",
            "将全部物品以实体形式提取到背包");
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
        var txt = wnd.AddText2(x, y, tab, "配方奖池");
        wnd.AddTipsButton2(x + txt.preferredWidth + 5, y, tab, "配方奖池", "配方奖池说明");
        y += 36f;
        wnd.AddComboBox(x, y, tab, "当前奖券")
            .WithItems(TicketTypeNames).WithSize(200, 0).WithConfigEntry(TicketTypeEntry1);
        ticketCountText1 = wnd.AddText2(GetPosition(2, 3).Item1, y, tab, "动态刷新");
        y += 36f;
        wnd.AddButton(0, 4, y, tab, "单抽",
            onClick: () => RaffleRecipe(1));
        wnd.AddButton(1, 4, y, tab, "十连",
            onClick: () => RaffleRecipe(10));
        wnd.AddButton(2, 4, y, tab, "百连",
            onClick: () => RaffleRecipe(100, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffleEntry1, "自动百连");
        y += 36f;
        y += 36f;
        y += 36f;
        txt = wnd.AddText2(x, y, tab, "建筑奖池");
        wnd.AddTipsButton2(x + txt.preferredWidth + 5, y, tab, "建筑奖池", "建筑奖池说明");
        y += 36f;
        wnd.AddComboBox(x, y, tab, "当前奖券")
            .WithItems(TicketTypeNames).WithSize(200, 0).WithConfigEntry(TicketTypeEntry2);
        ticketCountText2 = wnd.AddText2(GetPosition(2, 3).Item1, y, tab, "动态刷新");
        y += 36f;
        wnd.AddButton(0, 4, y, tab, "单抽",
            onClick: () => RaffleBuilding(1));
        wnd.AddButton(1, 4, y, tab, "十连",
            onClick: () => RaffleBuilding(10));
        wnd.AddButton(2, 4, y, tab, "百连",
            onClick: () => RaffleBuilding(100, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffleEntry2, "自动百连");
        y += 36f;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            EnableAutoRaffleEntry1.Value = false;
            EnableAutoRaffleEntry2.Value = false;
            return;
        }
        AutoRaffle();
        ticketCountText1.text = $"{"奖券数目".Translate()}{"：".Translate()}{GetItemTotalCount(SelectedTicketId1)}";
        ticketCountText2.text = $"{"奖券数目".Translate()}{"：".Translate()}{GetItemTotalCount(SelectedTicketId2)}";
    }

    /// <summary>
    /// 配方奖池抽奖。
    /// </summary>
    /// <param name="raffleCount">抽奖次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽奖结果</param>
    /// <param name="showMessage">是否弹窗询问、显示结果</param>
    private static void RaffleRecipe(int raffleCount, int oneLineMaxCount = 1, bool showMessage = true) {
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
                UIMessageBox.Show("提示".Translate(),
                    "时机未到，再探索一会当前星球吧！".Translate(),
                    "确定".Translate(), UIMessageBox.WARNING,
                    null);
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
        //todo: 优化配方出现情况，当前层次概率至少翻倍（也许现在这样也行？）
        List<BaseRecipe> recipes = GetRecipesUnderMatrix(SelectedTicketMatrixId1).SelectMany(list => list).ToList();
        recipes.RemoveAll(recipe => recipe.IsMaxMemory);
        if (SelectedTicketId1 < IFE宇宙奖券 && recipes.Count == 0) {
            if (showMessage) {
                UIMessageBox.Show("提示".Translate(),
                    "该奖池已经没有配方可以抽取了！".Translate(),
                    "确定".Translate(), UIMessageBox.WARNING,
                    null);
            }
            return;
        }
        int oneLineCount = 0;
        if (SelectedTicketId1 < IFE宇宙奖券 && recipes.All(recipe => !GameMain.history.ItemUnlocked(recipe.InputID))) {
            if (showMessage) {
                StringBuilder tip = new(string.Format("未解锁物品抽不到配方".Translate(), recipes.Count));
                foreach (BaseRecipe recipe in recipes) {
                    if (oneLineCount >= oneLineMaxCount) {
                        tip.Append("\n");
                        oneLineCount = 0;
                    } else if (oneLineCount > 0) {
                        tip.Append("          ");
                    }
                    tip.Append(LDB.items.Select(recipe.InputID).name);
                    oneLineCount++;
                }
                UIMessageBox.Show("提示".Translate(),
                    tip.ToString(),
                    "确定".Translate(), UIMessageBox.WARNING,
                    null);
            }
            return;
        }
        if (!TakeItem(SelectedTicketId1, raffleCount, out _, showMessage)) {
            return;
        }
        Dictionary<int, int> specialItemDic = [];
        Dictionary<int, int> commonItemDic = [];
        StringBuilder sb = new($"{"获得了以下物品".Translate()}{"：".Translate()}\n");
        StringBuilder sb2 = new();
        oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            //分馏配方通用核心（动态概率）
            //todo: 确认概率计算方式与物品价值
            currRate += itemValue[SelectedTicketId1] / itemValue[IFE分馏配方通用核心] / 25;
            if (randDouble < currRate) {
                if (specialItemDic.ContainsKey(IFE分馏配方通用核心)) {
                    specialItemDic[IFE分馏配方通用核心]++;
                } else {
                    specialItemDic[IFE分馏配方通用核心] = 1;
                }
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{LDB.items.Select(IFE分馏配方通用核心).name} x 1".WithValueColor(IFE分馏配方通用核心));
                oneLineCount++;
                RecipeRaffleCount = 1;
                continue;
            }
            //配方（0.6%，74抽开始后每抽增加6%）
            if (recipes.Count > 0) {
                currRate += RecipeRaffleRate;
                if (randDouble < currRate) {
                    //按照当前配方奖池随机抽取
                    BaseRecipe recipe = recipes[GetRandInt(0, recipes.Count)];
                    recipe.RewardThis();
                    if (recipe.IsMaxMemory) {
                        recipes.Remove(recipe);
                    }
                    if (recipe.Memory == 0) {
                        sb2.AppendLine($"{recipe.TypeName} {"已解锁".Translate()}".WithColor(Orange));
                    } else {
                        string tip = string.Format("已转为同名回响提示".Translate(), recipe.Memory);
                        sb2.AppendLine($"{recipe.TypeName} {tip}".WithColor(Orange));
                    }
                    if (oneLineCount >= oneLineMaxCount) {
                        sb.Append("\n");
                        oneLineCount = 0;
                    } else if (oneLineCount > 0) {
                        sb.Append("          ");
                    }
                    sb.Append($"{recipe.TypeName}".WithColor(Gold));
                    oneLineCount++;
                    RecipeRaffleCount = 1;
                    continue;
                }
                RecipeRaffleCount++;
            }
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
                    if (oneLineCount >= oneLineMaxCount) {
                        sb.Append("\n");
                        oneLineCount = 0;
                    } else if (oneLineCount > 0) {
                        sb.Append("          ");
                    }
                    sb.Append($"{LDB.items.Select(itemId).name} x {count}".WithValueColor(itemId));
                    oneLineCount++;
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
            if (oneLineCount >= oneLineMaxCount) {
                sb.Append("\n");
                oneLineCount = 0;
            } else if (oneLineCount > 0) {
                sb.Append("          ");
            }
            sb.Append($"{LDB.items.Select(I沙土).name} x {sandCount}".WithValueColor(I沙土));
            oneLineCount++;
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果".Translate(),
                sb.ToString().TrimEnd('\n')
                + "\n\n"
                + sb2.ToString().TrimEnd('\n')
                + $"\n\n{"选择提取方式".Translate()}{"：".Translate()}\n"
                + $"{"数据中心".Translate()}{"：".Translate()}{"数据中心说明".Translate()}\n"
                + $"{"部分提取".Translate()}{"：".Translate()}{"部分提取说明".Translate()}\n"
                + $"{"全部提取".Translate()}{"：".Translate()}{"全部提取说明".Translate()}",
                "数据中心".Translate(), "部分提取".Translate(), "全部提取".Translate(), UIMessageBox.INFO,
                () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                },
                () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                },
                () => {
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
    /// 建筑奖池抽奖。
    /// </summary>
    /// <param name="raffleCount">抽奖次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽奖结果</param>
    /// <param name="showMessage">是否弹窗询问、显示结果</param>
    private static void RaffleBuilding(int raffleCount, int oneLineMaxCount = 1, bool showMessage = true) {
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
                UIMessageBox.Show("提示".Translate(),
                    "时机未到，再探索一会当前星球吧！".Translate(),
                    "确定".Translate(), UIMessageBox.WARNING,
                    null);
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
        StringBuilder sb = new($"{"获得了以下物品".Translate()}{"：".Translate()}\n");
        int oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            //分馏塔增幅芯片（动态概率）
            //todo: 确认概率计算方式与物品价值
            currRate += itemValue[SelectedTicketId2] / itemValue[IFE分馏塔增幅芯片] / 25;
            if (randDouble < currRate) {
                if (specialItemDic.ContainsKey(IFE分馏塔增幅芯片)) {
                    specialItemDic[IFE分馏塔增幅芯片]++;
                } else {
                    specialItemDic[IFE分馏塔增幅芯片] = 1;
                }
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{LDB.items.Select(IFE分馏塔增幅芯片).name} x 1".WithValueColor(IFE分馏塔增幅芯片));
                oneLineCount++;
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
                    if (oneLineCount >= oneLineMaxCount) {
                        sb.Append("\n");
                        oneLineCount = 0;
                    } else if (oneLineCount > 0) {
                        sb.Append("          ");
                    }
                    sb.Append($"{LDB.items.Select(itemId).name} x {count}".WithValueColor(itemId));
                    oneLineCount++;
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
            if (oneLineCount >= oneLineMaxCount) {
                sb.Append("\n");
                oneLineCount = 0;
            } else if (oneLineCount > 0) {
                sb.Append("          ");
            }
            sb.Append($"{LDB.items.Select(I沙土).name} x {sandCount}".WithValueColor(I沙土));
            oneLineCount++;
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果".Translate(),
                sb.ToString().TrimEnd('\n')
                + $"\n\n{"选择提取方式".Translate()}{"：".Translate()}\n"
                + $"{"数据中心".Translate()}{"：".Translate()}{"数据中心说明".Translate()}\n"
                + $"{"部分提取".Translate()}{"：".Translate()}{"部分提取说明".Translate()}\n"
                + $"{"全部提取".Translate()}{"：".Translate()}{"全部提取说明".Translate()}",
                "数据中心".Translate(), "部分提取".Translate(), "全部提取".Translate(), UIMessageBox.INFO,
                () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                },
                () => {
                    foreach (var p in specialItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToPackage(p.Key, p.Value);
                    }
                    foreach (var p in commonItemDic.OrderByDescending(kvp => itemValue[kvp.Key])) {
                        AddItemToModData(p.Key, p.Value);
                    }
                },
                () => {
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

    private static long lastAutoRaffleTick = 0;

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
        if (GameMain.gameTick % 10 != 0 || lastAutoRaffleTick == GameMain.gameTick) {
            return;
        }
        lastAutoRaffleTick = GameMain.gameTick;
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
