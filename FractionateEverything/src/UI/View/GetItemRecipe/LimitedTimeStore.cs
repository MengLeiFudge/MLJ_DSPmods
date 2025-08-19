using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
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
    public int matrixCount = 0;
    public bool exchanged = false;
    public bool IsValid => (item != null && itemCount > 0 && matrix != null && matrixCount >= 0)
                           || (recipe != null && !recipe.IsMaxMemory && matrix != null && matrixCount >= 0);

    /// <summary>
    /// 一个空的兑换信息。
    /// </summary>
    public ExchangeInfo() { }

    /// <summary>
    /// 一个物品兑换信息。
    /// </summary>
    public ExchangeInfo(ItemProto item, int itemCount, ItemProto matrix, int matrixCount) {
        this.item = item;
        this.itemCount = itemCount;
        this.matrix = matrix;
        this.matrixCount = matrixCount;
    }

    /// <summary>
    /// 一个配方兑换信息。
    /// </summary>
    public ExchangeInfo(BaseRecipe recipe, ItemProto matrix, int matrixCount) {
        this.recipe = recipe;
        this.matrix = matrix;
        this.matrixCount = matrixCount;
    }

    public override string ToString() {
        if (!IsValid) {
            return "异常兑换信息".Translate();
        }
        return item != null
            ? $"{item.name} x {itemCount} <= {matrix.name} x {matrixCount}"
            : $"{recipe.RecipeType.GetName()} <= {matrix.name} x {matrixCount}";
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
        matrixCount = r.ReadInt32();
        exchanged = r.ReadBoolean();
    }

    public void Export(BinaryWriter w) {
        w.Write(1);
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

    /// <summary>
    /// 基础刷新间隔，10分钟，也就是10*60*60=36000tick；实际刷新间隔需要考虑VIP
    /// </summary>
    private static readonly long baseFreshTs = 36000;
    private static long nextFreshTick = baseFreshTs;
    private static Text textLeftTime;
    /// <summary>
    /// 交换信息的数目，受VIP影响
    /// </summary>
    private static int exchangeInfoCount = 20;
    private static int exchangeInfoMaxCount = 20;
    private static List<ExchangeInfo> exchangeInfos = [];
    private static List<Text> textExchangeInfos = [];
    private static List<UIButton> btnExchangeInfos = [];
    /// <summary>
    /// 兑换不同矩阵层次的配方所需的矩阵数目
    /// </summary>
    private static readonly int[] matrixRecipeCosts = [100, 300, 500, 700, 900, 900];

    private static readonly int[][] itemIdOriArr = [
        [IFE电磁奖券, IFE能量奖券, IFE结构奖券, IFE信息奖券, IFE引力奖券, IFE宇宙奖券, IFE黑雾奖券],
        [IFE分馏塔原胚普通, IFE分馏塔原胚精良, IFE分馏塔原胚稀有, IFE分馏塔原胚史诗, IFE分馏塔原胚传说, IFE分馏塔原胚定向],
        [IFE分馏配方通用核心, IFE分馏塔增幅芯片],
        [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔],
        //[IFE行星交互塔, IFE行星矿物复制塔, IFE行星点数聚集塔, IFE行星量子复制塔, IFE行星点金塔, IFE行星分解塔, IFE行星转化塔],
        [IFE复制精华, IFE点金精华, IFE分解精华, IFE转化精华],
        [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵],
        [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素],
    ];
    private static readonly int[] itemIdArr = itemIdOriArr.SelectMany(arr => arr).ToArray();

    public static void AddTranslations() {
        Register("限时商店", "Limited Time Store");

        Register("刷新", "Fresh");

        Register("刷新商店吗？", "to fresh store?");
        Register("异常兑换信息", "Invalid exchange info");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "限时商店");
        float x = 0f;
        float y = 18f;
        textLeftTime = wnd.AddText2(x, y, tab, "动态刷新", 15, "textLeftTime");
        //todo: 刷新前需要使用一定数目物品
        wnd.AddButton(2, 3, y, tab, "刷新", 16, "btn-modify",
            () => ModifyExchangeItemInfo(true));
        y += 36f;
        for (int i = 0; i < exchangeInfoMaxCount; i++) {
            int j = i;
            textExchangeInfos.Add(wnd.AddText2(x, y, tab, "动态刷新", 15, $"textLeftTime{j}"));
            btnExchangeInfos.Add(wnd.AddButton(2, 3, y, tab, "兑换", 16, $"btn-exchange{j}",
                () => Exchange(j)));
            y += 36f;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        long gameTick = GameMain.gameTick;
        if (gameTick >= nextFreshTick) {
            ModifyExchangeItemInfo();
        }
        long ts = nextFreshTick - gameTick;
        int minute = (int)(ts / 3600);
        ts %= 3600;
        int second = (int)(ts / 60);
        textLeftTime.text = $"还有 {minute} min {second} s 刷新";
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
        //获取矩阵对应的数目
        int matrixRecipeCost = matrixRecipeCosts[matrixID - I电磁矩阵];
        if (manual) {
            //todo: 添加vip影响
            UIMessageBox.Show("提示".Translate(),
                $"{"要花费".Translate()} {matrix.name} x {matrixRecipeCost / 10} {"刷新商店吗？".Translate()}",
                "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                () => {
                    if (!TakeItem(matrixID, matrixRecipeCost / 10, out _)) {
                        return;
                    }
                    nextFreshTick = gameTick - baseFreshTs + 1;
                    ModifyExchangeItemInfo();
                },
                null);
            return;
        }
        if (gameTick >= nextFreshTick) {
            long tickDiff = gameTick - nextFreshTick;
            long skipCycles = tickDiff / baseFreshTs + 1;
            //todo: 添加vip影响
            nextFreshTick += skipCycles * baseFreshTs;
        }
        //构建奖池
        //配方科技只能小于等于当前矩阵科技，出现未解锁配方的概率高达50%；不包含黑雾配方
        //由于配方的最大价值为90抽（90奖券，900矩阵），所以兑换所需矩阵为：100,300,500,700,900,900*vip折扣
        //根据矩阵类型、数目（此数目不考虑vip），可以得到矩阵总价值；
        //物品只包含分馏添加的所有物品，价值决定概率；但是高价值物品概率需要适当增加（需要一个价值=>概率的转化函数）
        //物品数目=ceiling(矩阵总价值/物品价值)*vip折扣
        //兑换所需矩阵类型=物品科技层次，矩阵数目=ceiling(物品数目*物品价值/矩阵价值*vip折扣)
        //配方概率=50%，物品概率=50%

        //计算矩阵总价值
        float matrixTotalValue = itemValue[matrixID] * matrixRecipeCost;
        //vip折扣，未实装
        float vipDiscount = 1.0f;

        //可能出现的随机兑换信息有：（VIP影响矩阵数目，起步九折，最低五折，且第一个免费）
        //Mod独有物品（>=50%，高价值物品出现概率会稍低但是不会过于低）
        //未解锁的配方（<=20%）
        //已解锁但未满回响的配方（<=30%）
        //todo: 同一时间，配方只能至多出现 5-当前回响 次，且满回响时禁止兑换

        //构建物品基础列表
        List<ExchangeInfo> itemExchangeList = [];
        //计算所有物品的概率总和（假设至少有10000个物品，因为电磁矩阵的概率是建筑增幅芯片的1150倍）
        float ratioSum = itemIdArr.Select(itemId => itemRatio[itemId]).Sum();
        foreach (int itemId in itemIdArr) {
            ItemProto item = LDB.items.Select(itemId);
            int itemCount = (int)Math.Ceiling(matrixTotalValue / itemValue[item.ID]);
            //考虑到某些物品属于电磁矩阵/黑雾矩阵，这里全部使用当前矩阵
            int matrixCount = (int)Math.Ceiling(itemCount * itemValue[item.ID] / itemValue[matrix.ID] * vipDiscount);
            ExchangeInfo info = new(item, itemCount, matrix, matrixCount);
            int repeatCount = (int)Math.Ceiling(itemRatio[itemId] / ratioSum * 10000);
            for (int i = 0; i < repeatCount; i++) {
                itemExchangeList.Add(info);
            }
        }
        //获取当前可能的所有配方（物品未解锁也可能出现）
        List<BaseRecipe> recipes = GetRecipesUnderMatrix(matrix.ID).SelectMany(list => list).ToList();
        //构建未解锁配方列表
        List<ExchangeInfo> recipeLockedExchangeList = [];
        foreach (BaseRecipe recipe in recipes.Where(recipe => recipe.Locked).ToList()) {
            ItemProto recipeMatrix = LDB.items.Select(itemToMatrix[recipe.InputID]);
            int matrixCount = (int)Math.Ceiling(matrixRecipeCosts[recipeMatrix.ID - I电磁矩阵] * vipDiscount);
            recipeLockedExchangeList.Add(new(recipe, recipeMatrix, matrixCount));
        }
        //构建已解锁但未满回响配方列表
        List<ExchangeInfo> recipeNotMaxMemoryExchangeList = [];
        foreach (BaseRecipe recipe in recipes.Where(recipe => recipe.Unlocked && !recipe.IsMaxMemory).ToList()) {
            ItemProto recipeMatrix = LDB.items.Select(itemToMatrix[recipe.InputID]);
            int matrixCount = (int)Math.Ceiling(matrixRecipeCosts[recipeMatrix.ID - I电磁矩阵] * vipDiscount);
            recipeNotMaxMemoryExchangeList.Add(new(recipe, recipeMatrix, matrixCount));
        }

        //构建可能出现的随机兑换信息列表
        List<ExchangeInfo> exchangeList = [];
        // 添加Mod独有物品（>=50%概率）
        // 物品列表已经按照概率权重重复添加，直接添加到兑换列表
        exchangeList.AddRange(itemExchangeList);
        // 添加未解锁的配方（<=20%概率）
        if (recipeLockedExchangeList.Count > 0) {
            // 计算需要添加的未解锁配方数量，使其占总数的20%
            int lockedRecipeCount = (int)Math.Ceiling(itemExchangeList.Count * 0.2f / 0.5f);
            for (int i = 0; i < lockedRecipeCount; i++) {
                exchangeList.Add(recipeLockedExchangeList[GetRandInt(0, recipeLockedExchangeList.Count)]);
            }
        }
        // 添加已解锁但未满回响的配方（<=30%概率）
        if (recipeNotMaxMemoryExchangeList.Count > 0) {
            // 计算需要添加的未解锁配方数量，使其占总数的20%
            int notMaxMemoryRecipeCount = (int)Math.Ceiling(itemExchangeList.Count * 0.2f / 0.5f);
            for (int i = 0; i < notMaxMemoryRecipeCount; i++) {
                exchangeList.Add(recipeNotMaxMemoryExchangeList[GetRandInt(0, recipeNotMaxMemoryExchangeList.Count)]);
            }
        }

        for (int i = 0; i < exchangeInfoMaxCount; i++) {
            //随机选择一个兑换信息（由于List的info是同一个对象，这里需要深拷贝）
            exchangeInfos[i] = exchangeList[GetRandInt(0, exchangeList.Count)].DeepCopy();
            //如果是前两个，改为免费
            if (i < 2) {
                exchangeInfos[i].matrixCount = 0;
            }
            //显示兑换信息
            textExchangeInfos[i].text = exchangeInfos[i].ToString();
        }
    }

    /// <summary>
    /// 购买限时物品/配方
    /// </summary>
    private static void Exchange(int index) {
        //todo: 物品可以考虑用不同的矩阵来兑换，价值怎么算？？？如何动态变化
        //todo: 物品兑换时，数目不能超过1组，且黑雾物品出现概率太高了，得重新设计概率曲线

        ExchangeInfo info = exchangeInfos[index];
        //todo: 如果出现这种情况，按钮显示已兑换，并且禁用
        if (!info.IsValid || info.exchanged) {
            return;
        }
        if (info.item != null) {
            UIMessageBox.Show("提示".Translate(),
                $"{"要花费".Translate()} {info.matrix.name} x {info.matrixCount} "
                + $"{"来兑换".Translate()} {info.item.name} x {info.itemCount} {"吗？".Translate()}",
                "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                () => {
                    if (!TakeItem(info.matrix.ID, info.matrixCount, out _)) {
                        return;
                    }
                    AddItemToPackage(info.item.ID, info.itemCount);
                    info.exchanged = true;
                },
                null);
        } else {
            UIMessageBox.Show("提示".Translate(),
                $"{"要花费".Translate()} {info.matrix.name} x {info.matrixCount} "
                + $"{"来兑换".Translate()} {info.recipe.TypeName} {"吗？".Translate()}",
                "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
                () => {
                    if (!TakeItem(info.matrix.ID, info.matrixCount, out _)) {
                        return;
                    }
                    info.recipe.RewardThis();
                    info.exchanged = true;
                },
                null);
        }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        nextFreshTick = version >= 2 ? r.ReadInt64() : baseFreshTs;
        exchangeInfos.Clear();
        if (version >= 3) {
            int count = r.ReadInt32();
            int i = 0;
            while (count > 0 && i < exchangeInfoMaxCount) {
                ExchangeInfo info = new();
                info.Import(r);
                exchangeInfos.Add(info);
                count--;
                i++;
            }
            while (i < exchangeInfoMaxCount) {
                exchangeInfos.Add(new());
                i++;
            }
            while (count > 0) {
                new ExchangeInfo().Import(r);
                count--;
            }
        } else {
            for (int i = 0; i < exchangeInfoMaxCount; i++) {
                exchangeInfos.Add(new());
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(3);
        w.Write(nextFreshTick);
        w.Write(exchangeInfos.Count);
        foreach (ExchangeInfo info in exchangeInfos) {
            info.Export(w);
        }
    }

    public static void IntoOtherSave() {
        nextFreshTick = 0;
        //无论何时打开界面，都会自动触发重新生成交换信息的方法，所以exchangeInfos不用处理
    }

    #endregion
}
