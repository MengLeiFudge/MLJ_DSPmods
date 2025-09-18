using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
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
    private static readonly Text[] txtTicketCount = new Text[TicketIds.Length];

    private static ConfigEntry<int> TicketTypeEntry1;
    private static int TicketType1;
    private static int SelectedTicketId1 => TicketIds[TicketTypeEntry1.Value];
    private static int SelectedTicketMatrixId1 => LDB.items.Select(SelectedTicketId1).maincraft.Items[0];
    private static Text txtCoreCount;
    private static ConfigEntry<bool> EnableAutoRaffleEntry1;
    private static UIButton btnMaxRaffle1;
    private static int MaxRaffleCount1 => (int)Math.Min(100, GetItemTotalCount(SelectedTicketId1));
    /// <summary>
    /// 下一抽是第几抽。
    /// </summary>
    private static readonly int[] RecipeRaffleCounts = new int[7];
    private static readonly float[] RecipeRaffleMaxCounts = [32.768f, 40.96f, 51.2f, 64, 80, 100, 100];
    /// <summary>
    /// 计算某次抽奖的配方获取概率。
    /// 当前抽奖次数未超过RecipeRaffleMaxCount*0.8时，概率恒定为对应基础概率；
    /// 超过RecipeRaffleMaxCount*0.8时，每次抽奖都会增加概率，直至达到RecipeRaffleMaxCount次时，概率为100%。
    /// </summary>
    private static double RecipeRaffleRate {
        get {
            float baseRate = 0.6f / RecipeRaffleMaxCounts[TicketTypeEntry1.Value];
            float countP20 = RecipeRaffleMaxCounts[TicketTypeEntry1.Value] / 5.0f;
            float countP80 = RecipeRaffleMaxCounts[TicketTypeEntry1.Value] - countP20;
            float plusRate = (1.0f - baseRate) / countP20;
            return baseRate + Math.Max(0, RecipeRaffleCounts[TicketTypeEntry1.Value] - countP80) * plusRate;
        }
    }
    //矩阵7种（竖），但是由于有奖券选择，所以相当于指定矩阵；配方6种（横）+总计
    private static Text[,] recipeUnlockInfoText = new Text[2, 7];
    private static int[] Matrixes = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I黑雾矩阵];
    private static int SelectedMatrixId1 => Matrixes[TicketTypeEntry1.Value];

    private static ConfigEntry<int> TicketTypeEntry2;
    private static int TicketType2;
    private static int SelectedTicketId2 => TicketIds[TicketTypeEntry2.Value];
    private static int SelectedTicketMatrixId2 => LDB.items.Select(SelectedTicketId2).maincraft.Items[0];
    private static Text txtChipCount;
    private static ConfigEntry<bool> EnableAutoRaffleEntry2;
    private static UIButton btnMaxRaffle2;
    private static int MaxRaffleCount2 => (int)Math.Min(100, GetItemTotalCount(SelectedTicketId2));

    public static void AddTranslations() {
        Register("奖券抽奖", "Ticket Raffle");

        Register("配方奖池", "Recipe pool");
        Register("配方奖池说明",
            "Except for Dark Fog Tickets, other lottery tickets can draw all recipes up to the level of the lottery ticket used.\n"
            + "The Quantum Copy recipes can only be drawn after all the other recipes are full of echoes.\n"
            + "Only Dark Fog Tickets, can draw Dark Fog recipes; non-Dark Fog Tickets cannot.\n\n"
            + "Probability announcement:\n"
            + "Fractionate Recipe Core: 0.0020%-0.20% (the higher the value of the lottery ticket, the higher the probability)\n"
            + "Fractionate recipe: 1.83%-0.60% (guaranteed to appear within 33-100 draws)\n"
            + "Miscellaneous items: ≈60% (unlocked items only, excludes tickets and not-dark-fog-matrix)\n"
            + "Sand: ≈40%",
            "除黑雾奖券外，其他奖券可以抽取不超过所用奖券层级的所有配方。\n"
            + "其他配方全部满回响后，才能抽取到量子复制配方。\n"
            + "只有黑雾奖券可以抽取黑雾配方，非黑雾奖券无法抽取。\n\n"
            + "概率公示：\n"
            + "分馏配方通用核心：0.0020%-0.20%（奖券价值越高则概率越高）\n"
            + "分馏配方：1.83%-0.60%（至多33-100抽必出）\n"
            + "杂项物品：≈60%（仅限已解锁的物品，不包含奖券和非黑雾矩阵）\n"
            + "沙土：≈40%");

        Register("当前奖券", "Current ticket");
        Register("奖券数目", "Ticket count");
        Register("：", ": ");

        Register("抽奖", "Draw");
        Register("自动百连", "Auto hundred draws");

        Register("建筑奖池", "Building pool");
        Register("建筑奖池说明",
            "The type of lottery ticket does not affect the reward content or probability.\n\n"
            + "Probability announcement:\n"
            + "Fractionator Increase Chip: 0.0012%-0.12% (the higher the value of the lottery ticket, the higher the probability)\n"
            + "Planetary Interaction Station: 0.0462%-4.62% (the higher the value of the lottery ticket, the higher the probability)\n"
            + "Interstellar Interaction Station: 0.0176%-1.76% (the higher the value of the lottery ticket, the higher the probability)\n"
            + "Frac Building Proto: ≈30% (non-directional only)\n"
            + "Miscellaneous buildings: ≈42% (unlocked buildings only)\n"
            + "Sand: ≈28%",
            "奖券类型不影响奖励内容与概率。\n\n"
            + "概率公示：\n"
            + "分馏塔增幅芯片：0.0012%-0.12%（奖券价值越高则概率越高）\n"
            + "行星内物流交互站：0.0462%-4.62%（奖券价值越高则概率越高）\n"
            + "星际物流交互站：0.0176%-1.76%（奖券价值越高则概率越高）\n"
            + "分馏塔原胚：≈30%（仅限非定向原胚）\n"
            + "其他建筑：≈42%（仅限已解锁的建筑）\n"
            + "沙土：≈28%");

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
        float y = 18f + 7f;
        for (int i = 0; i < TicketIds.Length; i++) {
            var posX = GetPosition(i, TicketIds.Length).Item1;
            wnd.AddImageButton(posX, y, tab, TicketIds[i]);
            txtTicketCount[i] = wnd.AddText2(posX + 40 + 5, y, tab, "动态刷新");
        }
        y += 36f;
        y += 36f + 7f;
        var txt = wnd.AddText2(x, y, tab, "配方奖池");
        wnd.AddTipsButton2(x + txt.preferredWidth + 5, y, tab, "配方奖池", "配方奖池说明");
        wnd.AddComboBox(GetPosition(1, 4).Item1, y, tab, "当前奖券")
            .WithItems(TicketTypeNames).WithSize(200, 0).WithConfigEntry(TicketTypeEntry1);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, IFE分馏配方通用核心);
        txtCoreCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        wnd.AddButton(0, 4, y, tab, $"{"抽奖".Translate()} x 1",
            onClick: () => RaffleRecipe(1));
        wnd.AddButton(1, 4, y, tab, $"{"抽奖".Translate()} x 10",
            onClick: () => RaffleRecipe(10));
        btnMaxRaffle1 = wnd.AddButton(2, 4, y, tab, "动态刷新",
            onClick: () => RaffleRecipe(-1, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffleEntry1, "自动百连");
        y += 36f;
        wnd.AddText2(x, y, tab, "配方解锁情况").supportRichText = true;
        y += 36f;
        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 7; j++) {
                (float, float) position = GetPosition(j, 7);
                recipeUnlockInfoText[i, j] = wnd.AddText2(position.Item1, y, tab, "动态刷新");
                recipeUnlockInfoText[i, j].supportRichText = true;
            }
            y += 36f;
        }
        for (int j = 0; j <= 5; j++) {
            recipeUnlockInfoText[0, j].text = RecipeTypeShortNames[j];
        }
        recipeUnlockInfoText[0, 6].text = "总计".Translate();
        y += 36f + 7f;
        txt = wnd.AddText2(x, y, tab, "建筑奖池");
        wnd.AddTipsButton2(x + txt.preferredWidth + 5, y, tab, "建筑奖池", "建筑奖池说明");
        wnd.AddComboBox(GetPosition(1, 4).Item1, y, tab, "当前奖券")
            .WithItems(TicketTypeNames).WithSize(200, 0).WithConfigEntry(TicketTypeEntry2);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, IFE分馏塔增幅芯片);
        txtChipCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        wnd.AddButton(0, 4, y, tab, $"{"抽奖".Translate()} x 1",
            onClick: () => RaffleBuilding(1));
        wnd.AddButton(1, 4, y, tab, $"{"抽奖".Translate()} x 10",
            onClick: () => RaffleBuilding(10));
        btnMaxRaffle2 = wnd.AddButton(2, 4, y, tab, "动态刷新",
            onClick: () => RaffleBuilding(-1, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffleEntry2, "自动百连");
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        for (int i = 0; i < TicketIds.Length; i++) {
            txtTicketCount[i].text = $"x {GetItemTotalCount(TicketIds[i])}";
        }
        txtCoreCount.text = $"x {GetItemTotalCount(IFE分馏配方通用核心)}";
        txtChipCount.text = $"x {GetItemTotalCount(IFE分馏塔增幅芯片)}";
        btnMaxRaffle1.SetText($"{"抽奖".Translate()} x {MaxRaffleCount1}");
        btnMaxRaffle2.SetText($"{"抽奖".Translate()} x {MaxRaffleCount2}");
        int[,] fullUpgradeCountArr = new int[2, 7];
        int[,] maxEchoCountArr = new int[2, 7];
        int[,] unlockCountArr = new int[2, 7];
        int[,] totalCountArr = new int[2, 7];
        for (int j = 0; j <= 5; j++) {
            int matrixID = SelectedMatrixId1;
            ERecipe type = (ERecipe)(j + 1);
            List<BaseRecipe> recipes = GetRecipesByType(type)
                .Where(r => itemToMatrix[r.InputID] == matrixID).ToList();
            totalCountArr[1, j] = recipes.Count;
            totalCountArr[1, 6] += recipes.Count;
            recipes = recipes.Where(r => r.Unlocked).ToList();
            unlockCountArr[1, j] = recipes.Count;
            unlockCountArr[1, 6] += recipes.Count;
            recipes = recipes.Where(r => r.IsMaxEcho).ToList();
            maxEchoCountArr[1, j] = recipes.Count;
            maxEchoCountArr[1, 6] += recipes.Count;
            recipes = recipes.Where(r => r.FullUpgrade).ToList();
            fullUpgradeCountArr[1, j] = recipes.Count;
            fullUpgradeCountArr[1, 6] += recipes.Count;
        }
        for (int j = 0; j <= 6; j++) {
            recipeUnlockInfoText[1, j].text =
                $"{fullUpgradeCountArr[1, j].ToString().WithColor(Orange)}"
                + $"/{maxEchoCountArr[1, j].ToString().WithColor(Red)}"
                + $"/{unlockCountArr[1, j].ToString().WithColor(Blue)}"
                + $"/{totalCountArr[1, j]}";
        }
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
        if (raffleCount == -1) {
            raffleCount = MaxRaffleCount1;
        }
        //构建杂项物品奖励列表
        //主体为已解锁的非建筑物品，可抽到精华，不可抽到原胚、核心等
        List<int> baseItems = [];
        foreach (ItemProto item in LDB.items.dataArray) {
            if ((item.ID >= IFE电磁奖券 && item.ID <= IFE黑雾奖券)
                || (item.ID >= IFE分馏塔原胚普通 && item.ID <= IFE分馏塔原胚定向)
                || (item.ID >= IFE复制精华 && item.ID <= IFE转化精华)
                || (item.ID >= IFE分馏配方通用核心 && item.ID <= IFE分馏塔增幅芯片)
                || item.ID == I沙土
                || item.BuildMode != 0
                || item.Type == EItemType.Matrix
                || itemValue[item.ID] >= maxValue) {
                continue;
            }
            if (GameMain.history.ItemUnlocked(item.ID)) {
                //当前持有的物品组数越少，获得该物品的概率越大，0组概率*500，>=10组概率*10
                float stacks = Math.Min(10, (float)GetItemTotalCount(item.ID) / item.StackSize);
                int addCounts = 500 - (int)(stacks * 49);
                for (int i = 0; i < addCounts; i++) {
                    baseItems.Add(item.ID);
                }
            }
        }
        //初步构建杂项物品奖励列表
        if (baseItems.Count == 0) {
            if (showMessage) {
                UIMessageBox.Show("提示".Translate(),
                    "时机未到，再探索一会当前星球吧！".Translate(),
                    "确定".Translate(), UIMessageBox.WARNING,
                    null);
            }
            return;
        }
        List<int> items = baseItems.ToList();
        while (items.Count < 10000) {
            items.InsertRange(items.Count, baseItems);
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
        List<BaseRecipe> recipes = GetRecipesByMatrix(SelectedTicketMatrixId1);
        recipes.RemoveAll(recipe => recipe.IsMaxEcho);
        // if (SelectedTicketId1 < IFE宇宙奖券 && recipes.Count == 0) {
        //     if (showMessage) {
        //         UIMessageBox.Show("提示".Translate(),
        //             "该奖池已经没有配方可以抽取了！".Translate(),
        //             "确定".Translate(), UIMessageBox.WARNING,
        //             null);
        //     }
        //     return;
        // }
        int oneLineCount = 0;
        if (SelectedTicketId1 < IFE宇宙奖券
            && recipes.All(recipe => !GameMain.history.ItemUnlocked(recipe.InputID))
            && recipes.Count > 0) {
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
        if (!TakeItemWithTip(SelectedTicketId1, raffleCount, out _, showMessage)) {
            return;
        }
        Dictionary<int, int> specialItemDic = [];
        Dictionary<int, int> commonItemDic = [];
        StringBuilder sb = new($"{"获得了以下物品".Translate()}{"：".Translate()}\n");
        StringBuilder sb2 = new();
        oneLineCount = 0;
        List<BaseRecipe> recipesOri = [..recipes];
        ReCreateRecipeList:
        recipesOri.RemoveAll(recipe => recipe.IsMaxEcho);
        recipes = recipesOri.Any(recipe => recipe.RecipeType != ERecipe.QuantumCopy)
            ? recipesOri.Where(recipe => recipe.RecipeType != ERecipe.QuantumCopy).ToList()
            : [..recipesOri];
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
                RecipeRaffleCounts[TicketTypeEntry1.Value]++;
                continue;
            }
            //配方（0.6%，74抽开始后每抽增加6%）
            if (recipes.Count > 0) {
                currRate += RecipeRaffleRate;
                if (randDouble < currRate) {
                    //按照当前配方奖池随机抽取
                    BaseRecipe recipe = recipes[GetRandInt(0, recipes.Count)];
                    recipe.RewardThis();
                    if (recipe.Echo == 0) {
                        sb2.AppendLine($"{recipe.TypeName} {"已解锁".Translate()}".WithColor(Orange));
                    } else {
                        string tip = string.Format("已转为同名回响提示".Translate(), recipe.Echo);
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
                    RecipeRaffleCounts[TicketTypeEntry1.Value] = 1;
                    if (recipe.IsMaxEcho) {
                        goto ReCreateRecipeList;
                    }
                    continue;
                }
                RecipeRaffleCounts[TicketTypeEntry1.Value]++;
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
        if (raffleCount == -1) {
            raffleCount = MaxRaffleCount2;
        }
        //构建杂项物品奖励列表
        //主体为已解锁的建筑物品，可抽到原胚，不可抽到精华、核心等
        List<int> baseItems = [];
        foreach (ItemProto item in LDB.items.dataArray) {
            //排除原胚、分馏塔、交互站，后面再加原胚和交互站
            if ((item.ID >= IFE交互塔 && item.ID <= IFE星际物流交互站)
                || item.BuildMode == 0
                || itemValue[item.ID] >= maxValue) {
                continue;
            }
            if (GameMain.history.ItemUnlocked(item.ID)) {
                //当前持有的物品组数越少，获得该物品的概率越大，0组概率*500，>=10组概率*10
                float stacks = Math.Min(10, (float)GetItemTotalCount(item.ID) / item.StackSize);
                int addCounts = 500 - (int)(stacks * 49);
                for (int i = 0; i < addCounts; i++) {
                    baseItems.Add(item.ID);
                }
            }
        }
        //初步构建杂项物品奖励列表
        if (baseItems.Count == 0) {
            if (showMessage) {
                UIMessageBox.Show("提示".Translate(),
                    "时机未到，再探索一会当前星球吧！".Translate(),
                    "确定".Translate(), UIMessageBox.WARNING,
                    null);
            }
            return;
        }
        List<int> items = baseItems.ToList();
        while (items.Count < 10000) {
            items.InsertRange(items.Count, baseItems);
        }
        //分馏塔原胚概率增加至总比例30%以上
        int protoCount = 0;
        int[] protoAddCount = [50, 35, 20, 10, 5, 1];
        while ((float)protoCount / items.Count < 0.3f / 0.6f) {
            for (int itemID = IFE分馏塔原胚普通; itemID <= IFE分馏塔原胚定向; itemID++) {
                for (int i = 0; i < protoAddCount[itemID - IFE分馏塔原胚普通]; i++) {
                    items.Add(itemID);
                    protoCount++;
                }
            }
        }
        //排序一下
        items.Sort();
        if (!TakeItemWithTip(SelectedTicketId2, raffleCount, out _, showMessage)) {
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
            //行星内物流交互站（动态概率）
            currRate += itemValue[SelectedTicketId2] / itemValue[IFE行星内物流交互站] / 25;
            if (randDouble < currRate) {
                if (specialItemDic.ContainsKey(IFE行星内物流交互站)) {
                    specialItemDic[IFE行星内物流交互站]++;
                } else {
                    specialItemDic[IFE行星内物流交互站] = 1;
                }
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{LDB.items.Select(IFE行星内物流交互站).name} x 1".WithValueColor(IFE行星内物流交互站));
                oneLineCount++;
                continue;
            }
            //星际物流交互站（动态概率）
            currRate += itemValue[SelectedTicketId2] / itemValue[IFE星际物流交互站] / 25;
            if (randDouble < currRate) {
                if (specialItemDic.ContainsKey(IFE星际物流交互站)) {
                    specialItemDic[IFE星际物流交互站]++;
                } else {
                    specialItemDic[IFE星际物流交互站] = 1;
                }
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{LDB.items.Select(IFE星际物流交互站).name} x 1".WithValueColor(IFE星际物流交互站));
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
    /// 每隔一段时间（至少6tick）自动抽取一次百连。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameMain_FixedUpdate_Postfix(GameMain __instance) {
        if (!__instance._running || __instance._paused) {
            return;
        }
        //如果奖券类型切换，重置自动抽奖标记
        if (TicketType1 != TicketTypeEntry1.Value) {
            TicketType1 = TicketTypeEntry1.Value;
            EnableAutoRaffleEntry1.Value = false;
        }
        if (TicketType2 != TicketTypeEntry2.Value) {
            TicketType2 = TicketTypeEntry2.Value;
            EnableAutoRaffleEntry2.Value = false;
        }
        //todo: vip可以提速
        if (__instance.timei - lastAutoRaffleTick < 6) {
            return;
        }
        lastAutoRaffleTick = __instance.timei;
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
        if (version >= 2) {
            for (int i = 0; i < RecipeRaffleCounts.Length; i++) {
                RecipeRaffleCounts[i] = r.ReadInt32();
            }
        } else {
            RecipeRaffleCounts[RecipeRaffleCounts.Length - 1] = r.ReadInt32();
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        for (int i = 0; i < RecipeRaffleCounts.Length; i++) {
            w.Write(RecipeRaffleCounts[i]);
        }
    }

    public static void IntoOtherSave() {
        for (int i = 0; i < RecipeRaffleCounts.Length; i++) {
            RecipeRaffleCounts[i] = 0;
        }
    }

    #endregion
}
