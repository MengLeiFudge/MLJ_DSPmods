using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View.Setting;
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
    private static readonly int[] MatrixIds = [
        I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵, I黑雾矩阵,
    ];
    private static readonly string[] TicketNames = [
        "电磁奖券".Translate(), "能量奖券".Translate(), "结构奖券".Translate(),
        "信息奖券".Translate(), "引力奖券".Translate(), "宇宙奖券".Translate(), "黑雾奖券".Translate()
    ];
    private static readonly Text[] txtTicketCount = new Text[TicketIds.Length];

    private static ConfigEntry<int> TicketIdx1Entry;
    private static int TicketIdx1 => TicketIdx1Entry.Value;
    private static int SelectedTicketId1 => TicketIds[TicketIdx1];
    private static int SelectedMatrixId1 => MatrixIds[TicketIdx1];
    private static Text txtCoreCount;
    private static (float[] rates, int[] counts) pool1;
    private static List<BaseRecipe> recipes;

    private static UIButton btnMaxRaffle1;
    private static int MaxRaffleCount1 => (int)Math.Min(100, GetItemTotalCount(SelectedTicketId1));
    private static ConfigEntry<bool> EnableAutoRaffle1Entry;
    private static readonly float[] RecipeRaffleMaxCounts = [33.614f, 48.02f, 68.6f, 98, 140, 200, 100];
    private static float[] _recipeValues;
    public static float[] RecipeValues {
        get {
            if (_recipeValues == null && itemValue[IFE电磁奖券] > 0) {
                _recipeValues = new float[7];
                for (int i = 0; i < 7; i++) {
                    _recipeValues[i] = (float)Math.Sqrt(itemValue[TicketIds[i]] * RecipeRaffleMaxCounts[i]);
                }
            }
            return _recipeValues;
        }
    }
    private static float RecipeValue => RecipeValues == null ? float.MaxValue : RecipeValues[TicketIdx1];
    //矩阵7种（竖），但是由于有奖券选择，所以相当于指定矩阵；配方6种（横）+总计
    private static Text[,] recipeUnlockInfoText = new Text[2, 7];

    private static ConfigEntry<int> TicketIdx2Entry;
    private static int TicketIdx2 => TicketIdx2Entry.Value;
    private static int SelectedTicketId2 => TicketIds[TicketIdx2];
    private static int SelectedMatrixId2 => MatrixIds[TicketIdx2];
    private static Text txtChipCount;
    private static (float[] rates, int[] counts) pool2;

    private static UIButton btnMaxRaffle2;
    private static int MaxRaffleCount2 => (int)Math.Min(100, GetItemTotalCount(SelectedTicketId2));
    private static ConfigEntry<bool> EnableAutoRaffle2Entry;
    private static Text[] txtFracProtoCounts = new Text[6];

    private static ConfigEntry<int> TicketIdx3Entry;
    private static int TicketIdx3 => TicketIdx3Entry.Value;
    private static int SelectedTicketId3 => TicketIds[TicketIdx3];
    private static int SelectedMatrixId3 => MatrixIds[TicketIdx3];
    private static List<ItemProto> commonItems3 = [];
    private static (float[] rates, int[] counts) pool3;

    private static UIButton btnMaxRaffle3;
    private static int MaxRaffleCount3 => (int)Math.Min(100, GetItemTotalCount(SelectedTicketId3));
    private static ConfigEntry<bool> EnableAutoRaffle3Entry;

    private static ConfigEntry<int> TicketIdx4Entry;
    private static int TicketIdx4 => TicketIdx4Entry.Value;
    private static int SelectedTicketId4 => TicketIds[TicketIdx4];
    private static int SelectedMatrixId4 => MatrixIds[TicketIdx4];
    private static List<ItemProto> commonItems4 = [];
    private static (float[] rates, int[] counts) pool4;

    private static UIButton btnMaxRaffle4;
    private static int MaxRaffleCount4 => (int)Math.Min(100, GetItemTotalCount(SelectedTicketId4));
    private static ConfigEntry<bool> EnableAutoRaffle4Entry;

    public static void AddTranslations() {
        Register("奖券抽奖", "Ticket Raffle");

        Register("配方奖池", "Recipe pool");
        Register("配方奖池说明",
            "Various fractionate recipes and Fractionate Recipe Core can be drawn.\n"
            + "Each type of lottery ticket can only yield recipes for items of the same technological tier.\n"
            + "The Quantum Replication recipes can only be drawn after all the other recipes are full of echoes.",
            "可以抽取各种分馏配方，以及分馏配方通用核心。\n"
            + "每种奖券只能抽到相同科技层次物品的相关配方。\n"
            + "其他配方全部满回响后，才能抽取到量子复制配方。");

        Register("当前奖券", "Current ticket");
        Register("奖券数目", "Ticket count");
        Register("：", ": ");

        Register("抽奖", "Draw");
        Register("自动百连", "Auto hundred draws");

        Register("原胚奖池", "Fractionator Proto pool");
        Register("原胚奖池说明",
            "Various fractionator prototypes and Fractionator Increase Chip can be drawn.",
            "可以抽取各种分馏塔原胚，以及分馏塔增幅芯片。");

        Register("材料奖池", "Material pool");
        Register("材料奖池说明",
            "Various materials can be drawn.\n"
            + "Only materials that have been unlocked can be drawn.\n"
            + "Unable to draw Matrix cards (except Dark Fog Matrix) or lottery tickets.",
            "可以抽取各种材料。\n"
            + "只能抽到已解锁的材料。\n"
            + "无法抽到矩阵（黑雾矩阵除外）、奖券。");

        Register("建筑奖池", "Building pool");
        Register("建筑奖池说明",
            "Various buildings can be drawn.\n"
            + "Only buildings that have been unlocked can be drawn.\n"
            + "Unable to draw the newly added fractionator or logistic interaction station.",
            "可以抽取各种建筑。\n"
            + "只能抽到已解锁的建筑。\n"
            + "无法抽到新增的分馏塔、物流交互站。");

        Register("抽奖结果", "Raffle results");
        Register("获得了以下物品", "Obtained the following items");
        Register("谢谢惠顾喵", "Thank you meow");
        Register("已解锁", "unlocked");
        Register("已转为同名回响提示",
            "has been converted to a homonym echo (currently holding {0} homonym echoes)",
            "已转为同名回响（当前持有 {0} 同名回响）");
        Register("所有奖励已存储至分馏数据中心。", "All rewards have been stored in the fractionation data centre.");
    }

    public static void LoadConfig(ConfigFile configFile) {
        TicketIdx1Entry = configFile.Bind("Ticket Raffle", "Ticket Idx 1", 0, "配方抽奖奖券索引。");
        if (TicketIdx1Entry.Value < 0 || TicketIdx1Entry.Value >= TicketIds.Length) {
            TicketIdx1Entry.Value = 0;
        }
        EnableAutoRaffle1Entry = configFile.Bind("Ticket Raffle", "Enable Auto Raffle 1", false, "配方抽奖是否自动百连。");
        TicketIdx1Entry.SettingChanged += (_, _) => EnableAutoRaffle1Entry.Value = false;
        TicketIdx1Entry.SettingChanged += (_, _) => FreshPool(1);

        TicketIdx2Entry = configFile.Bind("Ticket Raffle", "Ticket Idx 2", 0, "原胚抽奖奖券索引。");
        if (TicketIdx2Entry.Value < 0 || TicketIdx2Entry.Value >= TicketIds.Length) {
            TicketIdx2Entry.Value = 0;
        }
        EnableAutoRaffle2Entry = configFile.Bind("Ticket Raffle", "Enable Auto Raffle 2", false, "原胚抽奖是否自动百连。");
        TicketIdx2Entry.SettingChanged += (_, _) => EnableAutoRaffle2Entry.Value = false;
        TicketIdx2Entry.SettingChanged += (_, _) => FreshPool(2);

        TicketIdx3Entry = configFile.Bind("Ticket Raffle", "Ticket Idx 3", 0, "材料抽奖奖券索引。");
        if (TicketIdx3Entry.Value < 0 || TicketIdx3Entry.Value >= TicketIds.Length) {
            TicketIdx3Entry.Value = 0;
        }
        EnableAutoRaffle3Entry = configFile.Bind("Ticket Raffle", "Enable Auto Raffle 3", false, "材料抽奖是否自动百连。");
        TicketIdx3Entry.SettingChanged += (_, _) => EnableAutoRaffle3Entry.Value = false;
        TicketIdx3Entry.SettingChanged += (_, _) => FreshPool(3);

        TicketIdx4Entry = configFile.Bind("Ticket Raffle", "Ticket Idx 4", 0, "建筑抽奖奖券索引。");
        if (TicketIdx4Entry.Value < 0 || TicketIdx4Entry.Value >= TicketIds.Length) {
            TicketIdx4Entry.Value = 0;
        }
        EnableAutoRaffle4Entry = configFile.Bind("Ticket Raffle", "Enable Auto Raffle 4", false, "建筑抽奖是否自动百连。");
        TicketIdx4Entry.SettingChanged += (_, _) => EnableAutoRaffle4Entry.Value = false;
        TicketIdx4Entry.SettingChanged += (_, _) => FreshPool(4);
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
        y += 36f + 7f;

        y += 20f + 7f;
        var txt = wnd.AddText2(x, y, tab, "配方奖池");
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth, y, tab, "配方奖池", "配方奖池说明");
        txt = wnd.AddText2(GetPosition(1, 4).Item1, y, tab, "当前奖券");
        wnd.AddComboBox(GetPosition(1, 4).Item1 + 5 + txt.preferredWidth, y, tab)
            .WithItems(TicketNames).WithSize(200, 0).WithConfigEntry(TicketIdx1Entry);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, IFE分馏配方通用核心);
        txtCoreCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        wnd.AddButton(0, 4, y, tab, $"{"抽奖".Translate()} x 1",
            onClick: () => RaffleRecipe(1));
        wnd.AddButton(1, 4, y, tab, $"{"抽奖".Translate()} x 10",
            onClick: () => RaffleRecipe(10));
        btnMaxRaffle1 = wnd.AddButton(2, 4, y, tab, "动态刷新",
            onClick: () => RaffleRecipe(-1, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffle1Entry, "自动百连");
        y += 36f;
        wnd.AddText2(x, y, tab, "配方解锁情况").supportRichText = true;
        for (int i = 0; i < 2; i++) {
            y += 36f;
            for (int j = 0; j < 7; j++) {
                (float, float) position = GetPosition(j, 7);
                recipeUnlockInfoText[i, j] = wnd.AddText2(position.Item1, y, tab, "动态刷新");
                recipeUnlockInfoText[i, j].supportRichText = true;
            }
        }
        for (int j = 0; j <= 5; j++) {
            recipeUnlockInfoText[0, j].text = RecipeTypeShortNames[j];
        }
        recipeUnlockInfoText[0, 6].text = "总计".Translate();
        y += 36f;

        y += 20f + 7f;
        txt = wnd.AddText2(x, y, tab, "原胚奖池");
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth, y, tab, "原胚奖池", "原胚奖池说明");
        txt = wnd.AddText2(GetPosition(1, 4).Item1, y, tab, "当前奖券");
        wnd.AddComboBox(GetPosition(1, 4).Item1 + 5 + txt.preferredWidth, y, tab)
            .WithItems(TicketNames).WithSize(200, 0).WithConfigEntry(TicketIdx2Entry);
        wnd.AddImageButton(GetPosition(3, 4).Item1, y, tab, IFE分馏塔增幅芯片);
        txtChipCount = wnd.AddText2(GetPosition(3, 4).Item1 + 40 + 5, y, tab, "动态刷新");
        y += 36f + 7f;
        wnd.AddButton(0, 4, y, tab, $"{"抽奖".Translate()} x 1",
            onClick: () => RaffleFracProto(1));
        wnd.AddButton(1, 4, y, tab, $"{"抽奖".Translate()} x 10",
            onClick: () => RaffleFracProto(10));
        btnMaxRaffle2 = wnd.AddButton(2, 4, y, tab, "动态刷新",
            onClick: () => RaffleFracProto(-1, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffle2Entry, "自动百连");
        y += 36f + 7f;
        for (int i = 0; i < 6; i++) {
            wnd.AddImageButton(GetPosition(i, 6).Item1, y, tab, IFE分馏塔原胚I型 + i);
            txtFracProtoCounts[i] = wnd.AddText2(GetPosition(i, 6).Item1 + 40 + 5, y, tab, "动态刷新");
        }
        y += 36f + 7f;

        y += 20f;
        txt = wnd.AddText2(x, y, tab, "材料奖池");
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth, y, tab, "材料奖池", "材料奖池说明");
        txt = wnd.AddText2(GetPosition(1, 4).Item1, y, tab, "当前奖券");
        wnd.AddComboBox(GetPosition(1, 4).Item1 + 5 + txt.preferredWidth, y, tab)
            .WithItems(TicketNames).WithSize(200, 0).WithConfigEntry(TicketIdx3Entry);
        y += 36f;
        wnd.AddButton(0, 4, y, tab, $"{"抽奖".Translate()} x 1",
            onClick: () => RaffleMaterial(1));
        wnd.AddButton(1, 4, y, tab, $"{"抽奖".Translate()} x 10",
            onClick: () => RaffleMaterial(10));
        btnMaxRaffle3 = wnd.AddButton(2, 4, y, tab, "动态刷新",
            onClick: () => RaffleMaterial(-1, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffle3Entry, "自动百连");
        y += 36f;

        y += 20f;
        txt = wnd.AddText2(x, y, tab, "建筑奖池");
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth, y, tab, "建筑奖池", "建筑奖池说明");
        txt = wnd.AddText2(GetPosition(1, 4).Item1, y, tab, "当前奖券");
        wnd.AddComboBox(GetPosition(1, 4).Item1 + 5 + txt.preferredWidth, y, tab)
            .WithItems(TicketNames).WithSize(200, 0).WithConfigEntry(TicketIdx4Entry);
        y += 36f;
        wnd.AddButton(0, 4, y, tab, $"{"抽奖".Translate()} x 1",
            onClick: () => RaffleBuilding(1));
        wnd.AddButton(1, 4, y, tab, $"{"抽奖".Translate()} x 10",
            onClick: () => RaffleBuilding(10));
        btnMaxRaffle4 = wnd.AddButton(2, 4, y, tab, "动态刷新",
            onClick: () => RaffleBuilding(-1, 5));
        wnd.AddCheckBox(GetPosition(3, 4).Item1, y, tab, EnableAutoRaffle4Entry, "自动百连");
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
        for (int i = 0; i < 6; i++) {
            txtFracProtoCounts[i].text = $"x {GetItemTotalCount(IFE分馏塔原胚I型 + i)}";
        }
        btnMaxRaffle1.SetText($"{"抽奖".Translate()} x {MaxRaffleCount1}");
        btnMaxRaffle2.SetText($"{"抽奖".Translate()} x {MaxRaffleCount2}");
        btnMaxRaffle3.SetText($"{"抽奖".Translate()} x {MaxRaffleCount3}");
        btnMaxRaffle4.SetText($"{"抽奖".Translate()} x {MaxRaffleCount4}");

        int[,] fullUpgradeCountArr = new int[2, 7];
        int[,] maxEchoCountArr = new int[2, 7];
        int[,] unlockCountArr = new int[2, 7];
        int[,] totalCountArr = new int[2, 7];
        for (int j = 0; j <= 5; j++) {
            int matrixID = SelectedMatrixId1;
            ERecipe type = (ERecipe)(j + 1);
            List<BaseRecipe> recipes = GetRecipesByType(type)
                .Where(r => r.MatrixID == matrixID).ToList();
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
                $"{fullUpgradeCountArr[1, j].ToString().WithColor(7)}"
                + $"/{maxEchoCountArr[1, j].ToString().WithColor(5)}"
                + $"/{unlockCountArr[1, j].ToString().WithColor(3)}"
                + $"/{totalCountArr[1, j].ToString().WithColor(1)}";
        }
    }

    #region 抽奖逻辑简述与奖池生成

    /*
    【抽奖逻辑简述】
    最终奖池中，每种奖项只需要 概率、数目 这两个数值
    应该满足：∑概率<=1 且 任意数目>=1 且 ∑(奖项概率*奖项中奖品的数目*1个奖品的价值)=奖券价值
    pc 表示 奖项概率*奖项中奖品的数目，其值 = 这个奖项分到的价值 / 1个奖品的价值
    1.对于已预定价值比例的物品，直接根据奖券价值计算出其pc
    注意，预定的价值比例之和可能超过100%。如果超过，归一化价值比例后直接返回；如果未超过，继续执行234步。
    2.对于未预定价值比例的物品，它们分配奖券剩余价值，但并非均分
    具体分配方式为：权重 = Math.Sqrt(1个奖品的价值) * 缺失数目增幅
    缺失数目增幅 指的是 这个物品越少，权重就越高。物品数目为0时增幅为10，达到5组后增幅为1
    分配权重计算完毕后，再计算出每个物品的pc
    3.假设每个物品的c都为1，显然每个物品的p就等于这个物品的pc
    4.此时∑p可能有两种情况：>1，或者<=1。
    如果>1，将p最高的一项或多项的p减半，c翻倍，直至∑p<=1
    如果<=1，不做任何额外处理，抽奖时缺失的部分使用谢谢惠顾或沙土填充
    */

    /// <summary>
    /// 根据参数生成奖池。
    /// </summary>
    /// <param name="ticketId">奖券id</param>
    /// <param name="specialItems">已预订奖券价值的物品id（配方使用0替代）</param>
    /// <param name="specialRates">已预订奖券价值的物品占据奖券价值的比例</param>
    /// <param name="commonItems">未预订奖券价值，但是在奖池中的物品</param>
    /// <param name="recipeValue">配方价值</param>
    /// <returns>返回一个元组(概率[12000], 数目[12000])，索引表示物品id（0表示配方）</returns>
    private static (float[], int[]) GeneratePool(int ticketId, int[] specialItems, float[] specialRates,
        List<ItemProto> commonItems, float recipeValue = float.MaxValue) {
        float ticketValue = itemValue[ticketId] * VipFeatures.TicketValueMulti;
        float leftValue = ticketValue;
        //1.计算已预订奖券价值物品的pc
        //每个物品的pc（如果c为1，则pc等于p）
        float[] pc = new float[12000];
        //每个物品的数目
        int[] counts = new int[12000];
        //如果specialRates过大，归一化specialRates，然后直接返回
        float specialRatesSum = specialRates.Sum();
        bool specialRatesOverRange = specialRatesSum > 1;
        if (specialRatesOverRange) {
            for (int i = 0; i < specialItems.Length; i++) {
                specialRates[i] /= specialRatesSum;
            }
        }
        for (int i = 0; i < specialItems.Length; i++) {
            int id = specialItems[i];
            if (itemValue[id] < maxValue) {
                pc[id] = ticketValue * specialRates[i] / itemValue[id];
            } else {
                pc[id] = ticketValue * specialRates[i] / recipeValue;
            }
            leftValue -= pc[id];
            counts[id] = 1;
        }
        if (specialRatesOverRange) {
            return (pc, counts);
        }
        //2.计算未预订奖券价值物品的pc
        //每个常规物品分配奖券剩余价值的权重
        float[] commonItemDistributedWeights = new float[12000];
        foreach (ItemProto item in commonItems) {
            float stacks = Math.Min(5, (float)GetItemTotalCount(item.ID) / item.StackSize);
            commonItemDistributedWeights[item.ID] = (float)(Math.Sqrt(itemValue[item.ID]) * (10 - stacks * 9 / 5));
        }
        float weightsSum = commonItemDistributedWeights.Sum();
        foreach (ItemProto item in commonItems) {
            pc[item.ID] = leftValue * commonItemDistributedWeights[item.ID] / weightsSum / itemValue[item.ID];
            counts[item.ID] = 1;
        }
        //3.假设每个物品的c都为1，显然每个物品的p就等于这个物品的pc
        //4.如果∑p>1，选出p最高的一项或多项，将它们的p减半，c翻倍，直至∑p<=1
        while (pc.Sum() > 1) {
            float maxPercent = pc.Max();
            for (int i = 0; i < pc.Length; i++) {
                if (Math.Abs(pc[i] - maxPercent) < 1e-4) {
                    pc[i] /= 2;
                    counts[i] *= 2;
                }
            }
        }
        return (pc, counts);
    }

    /// <summary>
    /// 刷新现有奖池的奖励内容。
    /// </summary>
    public static void FreshPool(int poolId) {
        if (poolId == 1) {
            recipes = GetRecipesByMatrix(SelectedMatrixId1);
            recipes.RemoveAll(recipe => recipe.IsMaxEcho);
            int[] specialItems = [IFE分馏配方通用核心, 0];
            float[] specialRates = new float[2];
            //非常珍贵的物品，价值占比会随VIP提升，但是提升效果开根号
            specialRates[0] = 0.1f / (float)Math.Sqrt(VipFeatures.TicketValueMulti);
            //配方的最终比例永远为 1/RecipeRaffleMaxCounts[TicketIdx1]
            specialRates[1] = recipes.Count == 0
                ? 0
                : (1.0f / RecipeRaffleMaxCounts[TicketIdx1])
                  * RecipeValue
                  / itemValue[SelectedTicketId1]
                  / VipFeatures.TicketValueMulti;
            List<ItemProto> commonItems = LDB.items.dataArray.Where(item =>
                item.ID >= IFE复制精华 && item.ID <= IFE转化精华
            ).ToList();
            pool1 = GeneratePool(SelectedTicketId1, specialItems, specialRates, commonItems, RecipeValue);
        } else if (poolId == 2) {
            int[] specialItems = [
                IFE分馏塔增幅芯片,
                IFE分馏塔原胚I型,
                IFE分馏塔原胚II型,
                IFE分馏塔原胚III型,
                IFE分馏塔原胚IV型,
                IFE分馏塔原胚V型,
                IFE分馏塔定向原胚,
            ];
            float[] specialRates = new float[7];
            //非常珍贵的物品，价值占比会随VIP提升，但是提升效果开根号
            specialRates[0] = 0.1f / (float)Math.Sqrt(VipFeatures.TicketValueMulti);
            float specialRates16Sum = 1 - specialRates[0];
            specialRates[1] = specialRates16Sum * 50 / 121;
            specialRates[2] = specialRates16Sum * 35 / 121;
            specialRates[3] = specialRates16Sum * 20 / 121;
            specialRates[4] = specialRates16Sum * 10 / 121;
            specialRates[5] = specialRates16Sum * 5 / 121;
            specialRates[6] = specialRates16Sum * 1 / 121;
            pool2 = GeneratePool(SelectedTicketId2, specialItems, specialRates, []);
        } else if (poolId == 3) {
            if (commonItems3.Count == 0) {
                commonItems3 = LDB.items.dataArray.Where(item =>
                    itemValue[item.ID] < maxValue
                    && item.BuildMode == 0
                    && item.Type != EItemType.Matrix
                    && (item.ID < IFE电磁奖券 || item.ID > IFE黑雾奖券)
                    && (item.ID < IFE分馏塔原胚I型 || item.ID > IFE分馏塔增幅芯片)
                    && item.ID != I沙土
                    && GameMain.history.ItemUnlocked(item.ID)
                ).ToList();
            }
            pool3 = GeneratePool(SelectedTicketId3, [], [], commonItems3);
        } else if (poolId == 4) {
            if (commonItems4.Count == 0) {
                commonItems4 = LDB.items.dataArray.Where(item =>
                    itemValue[item.ID] < maxValue
                    && item.BuildMode != 0
                    && (item.ID < IFE交互塔 || item.ID > IFE星际物流交互站)
                    && item.ID != I沙土
                    && GameMain.history.ItemUnlocked(item.ID)
                ).ToList();
            }
            pool4 = GeneratePool(SelectedTicketId4, [], [], commonItems4);
        }
    }

    #endregion

    #region 抽奖

    /// <summary>
    /// 配方抽奖。
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
        if (!TakeItemWithTip(SelectedTicketId1, raffleCount, out _, showMessage)) {
            return;
        }
        float ticketValue = itemValue[SelectedTicketId1];
        VipFeatures.AddExp(ticketValue * raffleCount);
        //构建奖池
        (float[] rates, int[] counts) = pool1;
        //开抽！
        Dictionary<int, int> rewardDic = [];
        StringBuilder sb = new($"{"获得了以下物品".Translate()}{"：".Translate()}\n");
        StringBuilder sb2 = new();
        int oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            bool nothing = true;
            for (int i = 0; i < 12000; i++) {
                currRate += rates[i];
                if (randDouble >= currRate) {
                    continue;
                }
                nothing = false;
                if (i == 0) {
                    //优先抽取非量子复制配方
                    List<BaseRecipe> recipesOptimize = [..recipes];
                    if (recipesOptimize.Any(recipe => recipe.RecipeType != ERecipe.QuantumCopy)) {
                        recipesOptimize.RemoveAll(recipe => recipe.RecipeType == ERecipe.QuantumCopy);
                    }
                    //按照当前配方奖池随机抽取
                    BaseRecipe recipe = recipesOptimize[GetRandInt(0, recipesOptimize.Count)];
                    recipe.RewardThis(true);
                    if (recipe.Echo == 0) {
                        sb2.AppendLine($"{recipe.TypeName} {"已解锁".Translate()}".WithColor(RecipeValue));
                    } else {
                        string tip = string.Format("已转为同名回响提示".Translate(), recipe.Echo);
                        sb2.AppendLine($"{recipe.TypeName} {tip}".WithColor(RecipeValue));
                    }
                    if (oneLineCount >= oneLineMaxCount) {
                        sb.Append("\n");
                        oneLineCount = 0;
                    } else if (oneLineCount > 0) {
                        sb.Append("          ");
                    }
                    sb.Append($"{recipe.TypeName}".WithColor(RecipeValue));
                    //更新可抽取的配方状态
                    recipes.RemoveAll(recipe => recipe.IsMaxEcho);
                    if (recipes.Count == 0) {
                        FreshPool(1);
                        (rates, counts) = pool1;
                    }
                    oneLineCount++;
                } else {
                    int count = counts[i];
                    if (rewardDic.ContainsKey(i)) {
                        rewardDic[i] += count;
                    } else {
                        rewardDic[i] = count;
                    }
                    if (oneLineCount >= oneLineMaxCount) {
                        sb.Append("\n");
                        oneLineCount = 0;
                    } else if (oneLineCount > 0) {
                        sb.Append("          ");
                    }
                    sb.Append($"{LDB.items.Select(i).name} x {count}".WithColor(itemValue[i]));
                    oneLineCount++;
                }
                break;
            }
            if (nothing) {
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{"谢谢惠顾喵".Translate()} x 1".WithColor(Gray));
                oneLineCount++;
            }
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果".Translate(),
                sb.ToString().TrimEnd('\n')
                + "\n\n"
                + sb2.ToString().TrimEnd('\n')
                + $"\n\n{"所有奖励已存储至分馏数据中心。".Translate()}",
                "确定".Translate(), UIMessageBox.INFO,
                () => {
                    foreach (var p in rewardDic) {
                        AddItemToModData(p.Key, p.Value, 0, true);
                    }
                });
        } else {
            foreach (var p in rewardDic) {
                AddItemToModData(p.Key, p.Value, 0, true);
            }
        }
    }

    /// <summary>
    /// 原胚抽奖。
    /// </summary>
    /// <param name="raffleCount">抽奖次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽奖结果</param>
    /// <param name="showMessage">是否弹窗询问、显示结果</param>
    private static void RaffleFracProto(int raffleCount, int oneLineMaxCount = 1, bool showMessage = true) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (raffleCount == -1) {
            raffleCount = MaxRaffleCount2;
        }
        if (!TakeItemWithTip(SelectedTicketId2, raffleCount, out _, showMessage)) {
            return;
        }
        VipFeatures.AddExp(itemValue[SelectedTicketId2] * raffleCount);
        //构建奖池
        (float[] rates, int[] counts) = pool2;
        //开抽！
        Dictionary<int, int> rewardDic = [];
        StringBuilder sb = new($"{"获得了以下物品".Translate()}{"：".Translate()}\n");
        int oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            bool nothing = true;
            for (int i = 0; i < 12000; i++) {
                currRate += rates[i];
                if (randDouble >= currRate) {
                    continue;
                }
                nothing = false;
                int count = counts[i];
                if (rewardDic.ContainsKey(i)) {
                    rewardDic[i] += count;
                } else {
                    rewardDic[i] = count;
                }
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{LDB.items.Select(i).name} x {count}".WithColor(itemValue[i]));
                oneLineCount++;
                break;
            }
            if (nothing) {
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{"谢谢惠顾喵".Translate()} x 1".WithColor(Gray));
                oneLineCount++;
            }
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果".Translate(),
                sb.ToString().TrimEnd('\n')
                + $"\n\n{"所有奖励已存储至分馏数据中心。".Translate()}",
                "确定".Translate(), UIMessageBox.INFO,
                () => {
                    foreach (var p in rewardDic) {
                        AddItemToModData(p.Key, p.Value, 0, true);
                    }
                });
        } else {
            foreach (var p in rewardDic) {
                AddItemToModData(p.Key, p.Value, 0, true);
            }
        }
    }

    /// <summary>
    /// 材料抽奖。
    /// </summary>
    /// <param name="raffleCount">抽奖次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽奖结果</param>
    /// <param name="showMessage">是否弹窗询问、显示结果</param>
    private static void RaffleMaterial(int raffleCount, int oneLineMaxCount = 1, bool showMessage = true) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (raffleCount == -1) {
            raffleCount = MaxRaffleCount3;
        }
        if (!TakeItemWithTip(SelectedTicketId3, raffleCount, out _, showMessage)) {
            return;
        }
        VipFeatures.AddExp(itemValue[SelectedTicketId3] * raffleCount);
        //构建奖池
        List<ItemProto> commonItems = LDB.items.dataArray.Where(item =>
            itemValue[item.ID] < maxValue
            && item.BuildMode == 0
            && item.Type != EItemType.Matrix
            && (item.ID < IFE电磁奖券 || item.ID > IFE黑雾奖券)
            && (item.ID < IFE分馏塔原胚I型 || item.ID > IFE分馏塔增幅芯片)
            && item.ID != I沙土
            && GameMain.history.ItemUnlocked(item.ID)
        ).ToList();
        if (commonItems.Count != commonItems3.Count) {
            commonItems3 = commonItems;
            FreshPool(3);
        }
        (float[] rates, int[] counts) = pool3;
        //开抽！
        Dictionary<int, int> rewardDic = [];
        StringBuilder sb = new($"{"获得了以下物品".Translate()}{"：".Translate()}\n");
        int oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            bool nothing = true;
            for (int i = 0; i < 12000; i++) {
                currRate += rates[i];
                if (randDouble >= currRate) {
                    continue;
                }
                nothing = false;
                int count = counts[i];
                if (rewardDic.ContainsKey(i)) {
                    rewardDic[i] += count;
                } else {
                    rewardDic[i] = count;
                }
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{LDB.items.Select(i).name} x {count}".WithColor(itemValue[i]));
                oneLineCount++;
                break;
            }
            if (nothing) {
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{"谢谢惠顾喵".Translate()} x 1".WithColor(Gray));
                oneLineCount++;
            }
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果".Translate(),
                sb.ToString().TrimEnd('\n')
                + $"\n\n{"所有奖励已存储至分馏数据中心。".Translate()}",
                "确定".Translate(), UIMessageBox.INFO,
                () => {
                    foreach (var p in rewardDic) {
                        AddItemToModData(p.Key, p.Value, 0, true);
                    }
                });
        } else {
            foreach (var p in rewardDic) {
                AddItemToModData(p.Key, p.Value, 0, true);
            }
        }
    }

    /// <summary>
    /// 建筑抽奖。
    /// </summary>
    /// <param name="raffleCount">抽奖次数</param>
    /// <param name="oneLineMaxCount">一行显示多少个抽奖结果</param>
    /// <param name="showMessage">是否弹窗询问、显示结果</param>
    private static void RaffleBuilding(int raffleCount, int oneLineMaxCount = 1, bool showMessage = true) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (raffleCount == -1) {
            raffleCount = MaxRaffleCount4;
        }
        if (!TakeItemWithTip(SelectedTicketId4, raffleCount, out _, showMessage)) {
            return;
        }
        VipFeatures.AddExp(itemValue[SelectedTicketId4] * raffleCount);
        //构建奖池
        List<ItemProto> commonItems = LDB.items.dataArray.Where(item =>
            itemValue[item.ID] < maxValue
            && item.BuildMode != 0
            && (item.ID < IFE交互塔 || item.ID > IFE星际物流交互站)
            && item.ID != I沙土
            && GameMain.history.ItemUnlocked(item.ID)
        ).ToList();
        if (commonItems.Count != commonItems4.Count) {
            commonItems4 = commonItems;
            FreshPool(4);
        }
        (float[] rates, int[] counts) = pool4;
        //开抽！
        Dictionary<int, int> rewardDic = [];
        StringBuilder sb = new($"{"获得了以下物品".Translate()}{"：".Translate()}\n");
        int oneLineCount = 0;
        while (raffleCount > 0) {
            raffleCount--;
            double currRate = 0;
            double randDouble = GetRandDouble();
            bool nothing = true;
            for (int i = 0; i < 12000; i++) {
                currRate += rates[i];
                if (randDouble >= currRate) {
                    continue;
                }
                nothing = false;
                int count = counts[i];
                if (rewardDic.ContainsKey(i)) {
                    rewardDic[i] += count;
                } else {
                    rewardDic[i] = count;
                }
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{LDB.items.Select(i).name} x {count}".WithColor(itemValue[i]));
                oneLineCount++;
                break;
            }
            if (nothing) {
                if (oneLineCount >= oneLineMaxCount) {
                    sb.Append("\n");
                    oneLineCount = 0;
                } else if (oneLineCount > 0) {
                    sb.Append("          ");
                }
                sb.Append($"{"谢谢惠顾喵".Translate()} x 1".WithColor(Gray));
                oneLineCount++;
            }
        }
        if (showMessage) {
            UIMessageBox.Show("抽奖结果".Translate(),
                sb.ToString().TrimEnd('\n')
                + $"\n\n{"所有奖励已存储至分馏数据中心。".Translate()}",
                "确定".Translate(), UIMessageBox.INFO,
                () => {
                    foreach (var p in rewardDic) {
                        AddItemToModData(p.Key, p.Value, 0, true);
                    }
                });
        } else {
            foreach (var p in rewardDic) {
                AddItemToModData(p.Key, p.Value, 0, true);
            }
        }
    }

    #endregion

    #region 后台抽奖

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
        //todo: 抽奖逻辑执行效率进一步优化，减少时间；开放自动抽取次数
        if (__instance.timei - lastAutoRaffleTick < 6) {
            return;
        }
        lastAutoRaffleTick = __instance.timei;
        //如果有多个后台抽奖勾选，并且选定了同一个奖券，只有在奖券数目大于100*选择此奖券的奖池数时，才对这些池子抽奖
        int[] usedTickets = new int[7];
        bool[] autoRaffles = new bool[4];
        if (EnableAutoRaffle1Entry.Value) {
            usedTickets[TicketIdx1]++;
            autoRaffles[0] = true;
        }
        if (EnableAutoRaffle2Entry.Value) {
            usedTickets[TicketIdx2]++;
            autoRaffles[1] = true;
        }
        if (EnableAutoRaffle3Entry.Value) {
            usedTickets[TicketIdx3]++;
            autoRaffles[2] = true;
        }
        if (EnableAutoRaffle4Entry.Value) {
            usedTickets[TicketIdx4]++;
            autoRaffles[3] = true;
        }
        long[] currTicketCounts = new long[7];
        for (int i = 0; i < 7; i++) {
            if (usedTickets[i] > 0) {
                currTicketCounts[i] = GetItemTotalCount(IFE电磁奖券 + i);
            }
        }
        autoRaffles[0] &= currTicketCounts[TicketIdx1] >= 100 * usedTickets[TicketIdx1];
        autoRaffles[1] &= currTicketCounts[TicketIdx2] >= 100 * usedTickets[TicketIdx2];
        autoRaffles[2] &= currTicketCounts[TicketIdx3] >= 100 * usedTickets[TicketIdx3];
        autoRaffles[3] &= currTicketCounts[TicketIdx4] >= 100 * usedTickets[TicketIdx4];
        if (autoRaffles[0]) {
            RaffleRecipe(100, 5, false);
        }
        if (autoRaffles[1]) {
            RaffleFracProto(100, 5, false);
        }
        if (autoRaffles[2]) {
            RaffleMaterial(100, 5, false);
        }
        if (autoRaffles[3]) {
            RaffleBuilding(100, 5, false);
        }
    }

    #endregion

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        if (version == 1) {
            r.ReadInt32();
        } else if (version == 2 || version == 3) {
            for (int i = 0; i < 7; i++) {
                r.ReadInt32();
            }
        }
        if (version >= 3) {
            TicketIdx1Entry.Value = r.ReadInt32();
            if (TicketIdx1Entry.Value < 0 || TicketIdx1Entry.Value >= TicketIds.Length) {
                TicketIdx1Entry.Value = 0;
            }
            EnableAutoRaffle1Entry.Value = r.ReadBoolean();
            TicketIdx2Entry.Value = r.ReadInt32();
            if (TicketIdx2Entry.Value < 0 || TicketIdx2Entry.Value >= TicketIds.Length) {
                TicketIdx2Entry.Value = 0;
            }
            EnableAutoRaffle2Entry.Value = r.ReadBoolean();
            TicketIdx3Entry.Value = r.ReadInt32();
            if (TicketIdx3Entry.Value < 0 || TicketIdx3Entry.Value >= TicketIds.Length) {
                TicketIdx3Entry.Value = 0;
            }
            EnableAutoRaffle3Entry.Value = r.ReadBoolean();
            TicketIdx4Entry.Value = r.ReadInt32();
            if (TicketIdx4Entry.Value < 0 || TicketIdx4Entry.Value >= TicketIds.Length) {
                TicketIdx4Entry.Value = 0;
            }
            EnableAutoRaffle4Entry.Value = r.ReadBoolean();
        }
        for (int i = 0; i < 4; i++) {
            FreshPool(i);
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(4);
        w.Write(TicketIdx1Entry.Value);
        w.Write(EnableAutoRaffle1Entry.Value);
        w.Write(TicketIdx2Entry.Value);
        w.Write(EnableAutoRaffle2Entry.Value);
        w.Write(TicketIdx3Entry.Value);
        w.Write(EnableAutoRaffle3Entry.Value);
        w.Write(TicketIdx4Entry.Value);
        w.Write(EnableAutoRaffle4Entry.Value);
    }

    public static void IntoOtherSave() {
        lastAutoRaffleTick = 0;
        TicketIdx1Entry.Value = 0;
        EnableAutoRaffle1Entry.Value = false;
        TicketIdx2Entry.Value = 0;
        EnableAutoRaffle2Entry.Value = false;
        TicketIdx3Entry.Value = 0;
        EnableAutoRaffle3Entry.Value = false;
        TicketIdx4Entry.Value = 0;
        EnableAutoRaffle4Entry.Value = false;
        commonItems3 = [];
        commonItems4 = [];
        for (int i = 0; i < 4; i++) {
            FreshPool(i);
        }
    }

    #endregion
}
