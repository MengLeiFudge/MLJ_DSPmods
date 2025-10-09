using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public class ExchangeInfo {
    public ItemProto item = null;
    public int itemCount = 0;
    public BaseRecipe recipe = null;
    public ItemProto matrix = null;
    public float matrixCount = 0;
    public int matrixDiscountedCount => (int)Math.Ceiling(matrixCount * VipFeatures.ExchangeDiscount);
    public bool exchanged = false;
    public bool IsValid => (item != null && itemCount > 0 && matrix != null && matrixCount >= 0)
                           || (recipe != null && !recipe.IsMaxEcho && matrix != null && matrixCount >= 0);

    /// <summary>
    /// 一个空的兑换信息。
    /// </summary>
    public ExchangeInfo() { }

    /// <summary>
    /// 一个物品兑换信息。
    /// </summary>
    public ExchangeInfo(ItemProto item, int itemCount, ItemProto matrix, float matrixCount) {
        this.item = item;
        this.itemCount = itemCount;
        this.matrix = matrix;
        this.matrixCount = matrixCount;
    }

    /// <summary>
    /// 一个配方兑换信息。
    /// </summary>
    public ExchangeInfo(BaseRecipe recipe, ItemProto matrix, float matrixCount) {
        this.recipe = recipe;
        this.matrix = matrix;
        this.matrixCount = matrixCount;
    }

    public ExchangeInfo DeepCopy() {
        return new() {
            item = item,// ItemProto 通常是不可变的，可以共享引用
            itemCount = itemCount,
            recipe = recipe,// BaseRecipe 如果是不可变的，可以共享引用
            matrix = matrix,// ItemProto 通常是不可变的，可以共享引用
            matrixCount = matrixCount,
            exchanged = exchanged
        };
    }

    #region IModCanSave

    public void Import(BinaryReader r) {
        int version = r.ReadInt32();
        item = LDB.items.Select(r.ReadInt32());
        itemCount = r.ReadInt32();
        recipe = GetRecipe<BaseRecipe>((ERecipe)r.ReadInt32(), r.ReadInt32());
        matrix = LDB.items.Select(r.ReadInt32());
        matrixCount = version >= 2 ? r.ReadSingle() : r.ReadInt32();
        exchanged = r.ReadBoolean();
    }

    public void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(item != null ? item.ID : 0);
        w.Write(itemCount);
        w.Write(recipe != null ? (int)recipe.RecipeType : 0);
        w.Write(recipe != null ? recipe.InputID : 0);
        w.Write(matrix != null ? matrix.ID : 0);
        w.Write(matrixCount);
        w.Write(exchanged);
    }

    #endregion
}

public static class LimitedTimeStore {
    private static RectTransform window;
    private static RectTransform tab;

    private static int[] Matrixes = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵];
    private static Text[] txtMatrixCount = new Text[Matrixes.Length];

    /// <summary>
    /// 基础刷新间隔，10分钟，也就是10*60*60=36000tick；实际刷新间隔需要考虑VIP
    /// </summary>
    private static readonly long baseFreshTs = 36000;
    private static long nextFreshTick = baseFreshTs;
    private static Text txtLeftTime;
    /// <summary>
    /// 交换信息的数目，受VIP影响
    /// </summary>
    private static int exchangeInfoCount = 12;
    private static readonly int exchangeInfoMaxCount = 12;
    private static ExchangeInfo[] exchangeInfos = new ExchangeInfo[exchangeInfoMaxCount];
    private static MyImageButton[] exchangeImages1 = new MyImageButton[exchangeInfoMaxCount];
    private static MyImageButton[] exchangeImages2 = new MyImageButton[exchangeInfoMaxCount];
    private static Text[] txtExchangeInfos1 = new Text[exchangeInfoMaxCount];
    private static Text[] txtExchangeInfos2 = new Text[exchangeInfoMaxCount];
    private static MyImageButton[] exchangeImages3 = new MyImageButton[exchangeInfoMaxCount];
    private static Text[] txtExchangeInfos3 = new Text[exchangeInfoMaxCount];
    private static UIButton[] btnExchangeInfos = new UIButton[exchangeInfoMaxCount];
    private static readonly int[][] itemIdOriArr = [
        [IFE电磁奖券, IFE能量奖券, IFE结构奖券, IFE信息奖券, IFE引力奖券, IFE宇宙奖券, IFE黑雾奖券],
        [IFE分馏塔原胚I型, IFE分馏塔原胚II型, IFE分馏塔原胚III型, IFE分馏塔原胚IV型, IFE分馏塔原胚V型, IFE分馏塔定向原胚],
        [IFE分馏配方通用核心, IFE分馏塔增幅芯片],
        [IFE交互塔, IFE行星内物流交互站, IFE星际物流交互站],
        [IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔],
        //[IFE行星交互塔, IFE行星矿物复制塔, IFE行星点数聚集塔, IFE行星量子复制塔, IFE行星点金塔, IFE行星分解塔, IFE行星转化塔],
        [IFE复制精华, IFE点金精华, IFE分解精华, IFE转化精华],
        //[I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵],
        [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素],
    ];
    private static readonly int[] itemIdArr = itemIdOriArr.SelectMany(arr => arr).ToArray();

    public static void AddTranslations() {
        Register("限时商店", "Limited Time Store");

        Register("刷新剩余时间",
            "Refresh in {0} min {1} s",
            "还有 {0} min {1} s 刷新");
        Register("刷新", "Fresh");
        Register("刷新商店吗？", "to fresh store?");
        Register("兑换全部", "Exchange all");
        Register("要兑换全部配方/物品", "Would you like to exchange all recipes/items");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        //todo: 增加选项，可以选择物品去向
        window = trans;
        tab = wnd.AddTab(trans, "限时商店");
        float x = 0f;
        float y = 18f + 7f;
        for (int i = 0; i < Matrixes.Length; i++) {
            var posX = GetPosition(i, Matrixes.Length).Item1;
            wnd.AddImageButton(posX, y, tab, Matrixes[i]);
            txtMatrixCount[i] = wnd.AddText2(posX + 40 + 5, y, tab, "动态刷新");
        }
        y += 36f + 7f;
        txtLeftTime = wnd.AddText2(x, y, tab, "动态刷新", 15, "textLeftTime");
        wnd.AddButton(1, 3, y, tab, "刷新",
            onClick: () => ModifyExchangeItemInfo(true));
        wnd.AddButton(2, 3, y, tab, "兑换全部",
            onClick: () => ExchangeAll());
        y += 36f + 7f;
        for (int i = 0; i < exchangeInfoMaxCount; i++) {
            int j = i;
            //exchangeInfos在Import、IntoOtherSave时创建
            exchangeImages1[j] = wnd.AddImageButton(x, y, tab);
            exchangeImages2[j] = wnd.AddImageButton(x + 36 + 7, y, tab);
            txtExchangeInfos1[j] = wnd.AddText2(x + 40 + 5, y, tab, "动态刷新");
            txtExchangeInfos2[j] = wnd.AddText2(x + 125, y, tab, "<=");
            exchangeImages3[j] = wnd.AddImageButton(GetPosition(1, 4).Item1, y, tab);
            txtExchangeInfos3[j] = wnd.AddText2(GetPosition(1, 4).Item1 + 40 + 5, y, tab, "动态刷新");
            btnExchangeInfos[j] = wnd.AddButton(2, 3, y, tab, "兑换", 16, $"btn-exchange{j}",
                () => Exchange(j));
            y += 36f + 7f;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        for (int i = 0; i < Matrixes.Length; i++) {
            txtMatrixCount[i].text = $"x {GetItemTotalCount(Matrixes[i])}";
        }
        long ts = nextFreshTick - GameMain.gameTick;
        int minute = (int)(ts / 3600);
        ts %= 3600;
        int second = (int)(ts / 60);
        txtLeftTime.text = string.Format("刷新剩余时间".Translate(), minute, second);
        FreshExchangeItemInfo();
    }

    /// <summary>
    /// 后台刷新商店。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameData_GameTick_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (!GameMain.history.TechUnlocked(TFE分馏数据中心)) {
            return;
        }
        if (GameMain.gameTick >= nextFreshTick) {
            ModifyExchangeItemInfo();
        }
    }

    /// <summary>
    /// 更换所有限时物品/配方的信息
    /// </summary>
    private static void ModifyExchangeItemInfo(bool manual = false) {
        long gameTick = GameMain.gameTick;
        //获取当前解锁的最高级矩阵
        int matrixID;
        if (GameMain.history.ItemUnlocked(I宇宙矩阵)) {
            matrixID = I宇宙矩阵;
        } else if (GameMain.history.ItemUnlocked(I引力矩阵)) {
            matrixID = I引力矩阵;
        } else if (GameMain.history.ItemUnlocked(I信息矩阵)) {
            matrixID = I信息矩阵;
        } else if (GameMain.history.ItemUnlocked(I结构矩阵)) {
            matrixID = I结构矩阵;
        } else if (GameMain.history.ItemUnlocked(I能量矩阵)) {
            matrixID = I能量矩阵;
        } else {
            matrixID = I电磁矩阵;
        }
        ItemProto matrix = LDB.items.Select(matrixID);
        //获取兑换矩阵对应层次配方需要的矩阵数目
        float matrixRecipeCost = TicketRaffle.RecipeValues[matrixID - I电磁矩阵];
        int matrixRecipeCostInt = (int)Math.Ceiling(matrixRecipeCost);
        if (manual) {
            //todo: 添加vip影响
            UIMessageBox.Show("提示".Translate(),
                $"{"要花费".Translate()} {matrix.name} x {matrixRecipeCostInt} {"刷新商店吗？".Translate()}",
                "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                () => {
                    if (!TakeItemWithTip(matrixID, matrixRecipeCostInt, out _)) {
                        return;
                    }
                    VipFeatures.AddExp(itemValue[matrixID] * matrixRecipeCostInt);
                    nextFreshTick = gameTick - baseFreshTs + 1;
                    ModifyExchangeItemInfo();
                },
                null);
            return;
        }
        UIItemup.Up(IFE万物分馏商店刷新提示, 1);
        if (gameTick >= nextFreshTick) {
            long tickDiff = gameTick - nextFreshTick;
            long skipCycles = tickDiff / baseFreshTs + 1;
            //todo: 添加vip影响
            nextFreshTick += skipCycles * baseFreshTs;
        }
        //计算矩阵总价值
        float matrixTotalValue = itemValue[matrixID] * matrixRecipeCost;

        //1.构建可兑换的物品列表
        List<ExchangeInfo> itemExchangeList = [];
        //计算所有物品的概率总和（假设至少有10000个物品，因为电磁矩阵的概率是建筑增幅芯片的1150倍）
        int[] itemIdArr0 = itemIdArr.Where(itemId => GameMain.history.ItemUnlocked(itemId)).ToArray();
        float[] ratioArr =
            itemIdArr0.Select(itemId => (float)(1.0 / Math.Log(itemValue[itemId] + 10, Math.E))).ToArray();
        float ratioSum = ratioArr.Sum();
        for (int i = 0; i < itemIdArr0.Length; i++) {
            int itemId = itemIdArr0[i];
            ItemProto item = LDB.items.Select(itemId);
            //物品至少1，至多1(核心/芯片)/半组(建筑)/10组(材料)
            int itemCount;
            if (itemId == IFE分馏配方通用核心 || itemId == IFE分馏塔增幅芯片) {
                itemCount = 1;
            } else if (item.BuildMode == 0) {
                itemCount = item.StackSize * 10;
            } else {
                itemCount = item.StackSize / 2;
            }
            //如果超过matrixRecipeCost个矩阵，则根据matrixRecipeCost个矩阵的价值倒推物品数目以减少物品数目
            float matrixCount = itemCount * itemValue[item.ID] / itemValue[matrix.ID];
            if (matrixCount > matrixRecipeCost) {
                itemCount = (int)(matrixTotalValue / itemValue[item.ID]);
                if (itemCount == 0) {
                    itemCount++;
                }
                matrixCount = itemCount * itemValue[item.ID] / itemValue[matrix.ID];
            }
            //生成兑换信息
            ExchangeInfo info = new(item, itemCount, matrix, matrixCount);
            int repeatCount = (int)Math.Ceiling(ratioArr[i] / ratioSum * 10000);
            for (int j = 0; j < repeatCount; j++) {
                itemExchangeList.Add(info);
            }
        }
        //2.构建可兑换的配方列表
        List<ExchangeInfo> recipeExchangeList = [];
        List<BaseRecipe> recipes = GetRecipesUnderMatrix(matrix.ID).SelectMany(list => list)
            .Where(recipe => !recipe.IsMaxEcho
                             && GameMain.history.ItemUnlocked(recipe.InputID)
                             && GameMain.history.ItemUnlocked(recipe.MatrixID))
            .ToList();
        if (recipes.Any(recipe => recipe.RecipeType != ERecipe.QuantumCopy)) {
            recipes.RemoveAll(recipe => recipe.RecipeType == ERecipe.QuantumCopy);
        }
        foreach (BaseRecipe recipe in recipes) {
            ItemProto recipeMatrix = LDB.items.Select(recipe.MatrixID);
            float matrixCount = TicketRaffle.RecipeValues[recipeMatrix.ID - I电磁矩阵];
            recipeExchangeList.Add(new(recipe, recipeMatrix, matrixCount));
        }

        //从兑换列表中挑选exchangeInfoMaxCount个，组成新的兑换列表
        Dictionary<BaseRecipe, int> recipeExchangeCounts = [];
        for (int i = 0; i < exchangeInfoMaxCount; i++) {
            if (recipeExchangeList.Count > 0 && GetRandDouble() < 0.5) {
                //兑换信息为配方（深拷贝以避免兑换信息之间互相影响）
                ExchangeInfo info = recipeExchangeList[GetRandInt(0, recipeExchangeList.Count)];
                BaseRecipe recipe = info.recipe;
                if (recipeExchangeCounts.ContainsKey(recipe)) {
                    recipeExchangeCounts[recipe]++;
                    int maxExchangeCount = recipe.Locked ? 1 + recipe.MaxEcho : recipe.MaxEcho - recipe.Echo;
                    if (recipeExchangeCounts[recipe] >= maxExchangeCount) {
                        recipeExchangeList.Remove(info);
                    }
                } else {
                    recipeExchangeCounts[recipe] = 1;
                }
                exchangeInfos[i] = info.DeepCopy();
            } else {
                //兑换信息为物品（深拷贝以避免兑换信息之间互相影响）
                exchangeInfos[i] = itemExchangeList[GetRandInt(0, itemExchangeList.Count)].DeepCopy();
            }
            //前vipFreeCount个交换信息改为免费
            if (i < VipFeatures.FreeExchangeCount) {
                exchangeInfos[i].matrixCount = 0;
            }
        }
        //自动兑换所有价值为0的物品/配方
        for (int i = 0; i < exchangeInfos.Length; i++) {
            if (exchangeInfos[i].matrixCount == 0) {
                Exchange(i, false);
            }
        }
    }

    /// <summary>
    /// 刷新显示物品/配方的信息
    /// </summary>
    private static void FreshExchangeItemInfo() {
        for (int i = 0; i < exchangeInfoMaxCount; i++) {
            ExchangeInfo info = exchangeInfos[i];
            if (info.item != null) {
                exchangeImages1[i].gameObject.SetActive(true);
                exchangeImages1[i].ItemId = info.item.ID;
                exchangeImages2[i].gameObject.SetActive(false);
                txtExchangeInfos1[i].text = $"x {info.itemCount}";
                txtExchangeInfos2[i].text = "<=";
                exchangeImages3[i].gameObject.SetActive(true);
                exchangeImages3[i].ItemId = info.matrix.ID;
                txtExchangeInfos3[i].gameObject.SetActive(true);
                txtExchangeInfos3[i].text = $"x {info.matrixDiscountedCount}";
                btnExchangeInfos[i].gameObject.SetActive(true);
                if (!info.IsValid) {
                    btnExchangeInfos[i].enabled = false;
                    btnExchangeInfos[i].SetText("无法兑换".Translate());
                } else if (info.exchanged) {
                    btnExchangeInfos[i].enabled = false;
                    btnExchangeInfos[i].SetText("已兑换".Translate());
                } else {
                    btnExchangeInfos[i].enabled = true;
                    btnExchangeInfos[i].SetText("兑换".Translate());
                }
            } else if (info.recipe != null) {
                exchangeImages1[i].gameObject.SetActive(true);
                exchangeImages1[i].ItemId = info.recipe.RecipeType.GetSpriteItemId();
                exchangeImages2[i].gameObject.SetActive(true);
                exchangeImages2[i].ItemId = info.recipe.InputID;
                txtExchangeInfos1[i].text = "";
                txtExchangeInfos2[i].text = "<=";
                exchangeImages3[i].gameObject.SetActive(true);
                exchangeImages3[i].ItemId = info.matrix.ID;
                txtExchangeInfos3[i].gameObject.SetActive(true);
                txtExchangeInfos3[i].text = $"x {info.matrixDiscountedCount}";
                btnExchangeInfos[i].gameObject.SetActive(true);
                if (!info.IsValid) {
                    btnExchangeInfos[i].enabled = false;
                    btnExchangeInfos[i].SetText("无法兑换".Translate());
                } else if (info.exchanged) {
                    btnExchangeInfos[i].enabled = false;
                    btnExchangeInfos[i].SetText("已兑换".Translate());
                } else {
                    btnExchangeInfos[i].enabled = true;
                    btnExchangeInfos[i].SetText("兑换".Translate());
                }
            } else {
                exchangeImages1[i].gameObject.SetActive(false);
                exchangeImages2[i].gameObject.SetActive(false);
                txtExchangeInfos1[i].text = "";
                txtExchangeInfos2[i].text = "";
                exchangeImages3[i].gameObject.SetActive(false);
                txtExchangeInfos3[i].text = "";
                btnExchangeInfos[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 购买限时物品/配方
    /// </summary>
    private static void Exchange(int index, bool showMessage = true) {
        ExchangeInfo info = exchangeInfos[index];
        if (!info.IsValid || info.exchanged) {
            return;
        }
        if (info.item != null) {
            if (info.matrixDiscountedCount == 0 || !showMessage) {
                if (!TakeItemWithTip(info.matrix.ID, info.matrixDiscountedCount, out _, showMessage)) {
                    return;
                }
                VipFeatures.AddExp(itemValue[info.matrix.ID] * info.matrixDiscountedCount);
                AddItemToModData(info.item.ID, info.itemCount);
                info.exchanged = true;
            } else {
                UIMessageBox.Show("提示".Translate(),
                    $"{"要花费".Translate()} {info.matrix.name} x {info.matrixDiscountedCount} "
                    + $"{"来兑换".Translate()} {info.item.name} x {info.itemCount} {"吗？".Translate()}",
                    "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                    () => {
                        if (!TakeItemWithTip(info.matrix.ID, info.matrixDiscountedCount, out _, showMessage)) {
                            return;
                        }
                        VipFeatures.AddExp(itemValue[info.matrix.ID] * info.matrixDiscountedCount);
                        AddItemToModData(info.item.ID, info.itemCount);
                        info.exchanged = true;
                    },
                    null);
            }
        } else {
            if (info.matrixDiscountedCount == 0 || !showMessage) {
                if (!TakeItemWithTip(info.matrix.ID, info.matrixDiscountedCount, out _, showMessage)) {
                    return;
                }
                VipFeatures.AddExp(itemValue[info.matrix.ID] * info.matrixDiscountedCount);
                info.recipe.RewardThis();
                info.exchanged = true;
            } else {
                UIMessageBox.Show("提示".Translate(),
                    $"{"要花费".Translate()} {info.matrix.name} x {info.matrixDiscountedCount} "
                    + $"{"来兑换".Translate()} {info.recipe.TypeName} {"吗？".Translate()}",
                    "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                    () => {
                        if (!TakeItemWithTip(info.matrix.ID, info.matrixDiscountedCount, out _, showMessage)) {
                            return;
                        }
                        VipFeatures.AddExp(itemValue[info.matrix.ID] * info.matrixDiscountedCount);
                        info.recipe.RewardThis();
                        info.exchanged = true;
                    },
                    null);
            }
        }
    }

    private static void ExchangeAll() {
        UIMessageBox.Show("提示".Translate(),
            $"{"要兑换全部配方/物品".Translate()}{"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                for (int i = 0; i < exchangeInfoCount; i++) {
                    Exchange(i, false);
                }
            },
            null);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        nextFreshTick = version >= 2 ? r.ReadInt64() : baseFreshTs;
        if (version >= 3) {
            int count = r.ReadInt32();
            int i = 0;
            while (count > 0 && i < exchangeInfoMaxCount) {
                ExchangeInfo info = new();
                info.Import(r);
                exchangeInfos[i] = info;
                count--;
                i++;
            }
            while (i < exchangeInfoMaxCount) {
                exchangeInfos[i] = new();
                i++;
            }
            while (count > 0) {
                new ExchangeInfo().Import(r);
                count--;
            }
        } else {
            for (int i = 0; i < exchangeInfoMaxCount; i++) {
                exchangeInfos[i] = new();
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(3);
        w.Write(nextFreshTick);
        w.Write(exchangeInfos.Length);
        foreach (ExchangeInfo info in exchangeInfos) {
            info.Export(w);
        }
    }

    public static void IntoOtherSave() {
        nextFreshTick = 0;
        for (int i = 0; i < exchangeInfoMaxCount; i++) {
            exchangeInfos[i] = new();
        }
    }

    #endregion
}
