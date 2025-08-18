using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Recipe;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class LimitedTimeStore {
    private static RectTransform window;
    private static RectTransform tab;

    //基础刷新间隔，10分钟，也就是10*60*60=36000tick；实际刷新间隔需要考虑VIP
    private static readonly long baseFreshTs = 36000;
    private static long nextFreshTick = baseFreshTs;
    private static Text textLeftTime;
    //如果是物品，只需要物品ID；如果是配方，需要物品ID和配方类型
    private static int exchangeInfoCount = 15;
    private static int[] exchangeItemId = new int[exchangeInfoCount];
    private static ERecipe[] exchangeRecipeType = new ERecipe[exchangeInfoCount];
    private static Text[] textExchangeInfo = new Text[exchangeInfoCount];
    private static readonly int[] matrixCosts = [100, 300, 500, 700, 900, 900];

    public static void AddTranslations() {
        Register("限时商店", "Limited Time Store");

        Register("刷新", "Fresh");
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
        for (int i = 0; i < exchangeInfoCount; i++) {
            int j = i;
            textExchangeInfo[i] = wnd.AddText2(x, y, tab, "动态刷新", 15, $"textLeftTime{j}");
            wnd.AddButton(2, 3, y, tab, "兑换", 16, $"btn-exchange{j}",
                () => Exchange(j));
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
        for (int i = 0; i < exchangeInfoCount; i++) {
            if (exchangeRecipeType[i] == ERecipe.Unknown) {
                ItemProto item = LDB.items.Select(exchangeItemId[i]);
                if (item == null || itemToMatrix[item.ID] < I电磁矩阵 || itemToMatrix[item.ID] > I宇宙矩阵) {
                    ModifyExchangeItemInfo();
                    break;
                }
            } else {
                BaseRecipe recipe = GetRecipe<BaseRecipe>(exchangeRecipeType[i], exchangeItemId[i]);
                if (recipe == null || recipe.IsMaxMemory) {
                    ModifyExchangeItemInfo();
                    break;
                }
            }
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
        if (manual) {
            //todo: 需要消耗一些物品
            //todo: 添加vip影响
            nextFreshTick = gameTick + baseFreshTs;
        } else {
            if (gameTick >= nextFreshTick) {
                long tickDiff = gameTick - nextFreshTick;
                long skipCycles = tickDiff / baseFreshTs + 1;
                //todo: 添加vip影响
                nextFreshTick += skipCycles * baseFreshTs;
            }
        }
        //构建奖池
        //配方科技只能小于等于当前矩阵科技，出现未解锁配方的概率高达50%；不包含黑雾配方
        //由于配方的最大价值为90抽（90奖券，900矩阵），所以兑换所需矩阵为：100,300,500,700,900,900*vip折扣
        //根据矩阵类型、数目（此数目不考虑vip），可以得到矩阵总价值；
        //物品只包含分馏添加的所有物品，价值决定概率；但是高价值物品概率需要适当增加（需要一个价值=>概率的转化函数）
        //物品数目=ceiling(矩阵总价值/物品价值)*vip折扣
        //兑换所需矩阵类型=物品科技层次，矩阵数目=ceiling(物品数目*物品价值/矩阵价值*vip折扣)
        //配方概率=50%，物品概率=50%

        //获取当前解锁的最高级矩阵
        int currentMatrixID;
        if (GameMain.history.ItemUnlocked(I宇宙矩阵)) {
            currentMatrixID = I宇宙矩阵;
        } else if (GameMain.history.ItemUnlocked(I引力矩阵)) {
            currentMatrixID = I引力矩阵;
        } else if (GameMain.history.ItemUnlocked(I信息矩阵)) {
            currentMatrixID = I信息矩阵;
        } else if (GameMain.history.ItemUnlocked(I结构矩阵)) {
            currentMatrixID = I结构矩阵;
        } else if (GameMain.history.ItemUnlocked(I能量矩阵)) {
            currentMatrixID = I能量矩阵;
        } else {
            currentMatrixID = I电磁矩阵;
        }
        //获取矩阵对应的数目
        int matrixCost = matrixCosts[currentMatrixID - I电磁矩阵];
        //计算矩阵总价值
        float matrixTotalValue = itemValue[currentMatrixID] * matrixCost;
        //vip折扣，未实装
        float vipDiscount = 1.0f;

        List<(int, ERecipe)> rewardList = [];
        for (int i = 0; i < exchangeInfoCount; i++) {
            //todo: 记得移除
            exchangeRecipeType[i] = ERecipe.Unknown;
            exchangeItemId[i] = I电磁矩阵;
            continue;

            //随机生成兑换信息
            double randVal = GetRandDouble();
            if (randVal < 0.5) {
                // 50%概率生成配方
                // 配方科技只能小于等于当前矩阵科技，出现未解锁配方的概率高达50%；不包含黑雾配方
                List<BaseRecipe> recipeTotal = GetRecipesUnderMatrix(I电磁矩阵);
                List<BaseRecipe> recipeLocked = [];

                // TODO: 实现配方选择逻辑
                // 这里需要根据当前矩阵科技等级选择合适的配方
                // 暂时设置为Unknown，等待具体实现
                exchangeRecipeType[i] = ERecipe.Unknown;// 临时占位
                exchangeItemId[i] = 0;// 临时占位
            } else {
                // 50%概率生成物品
                // 物品只包含分馏添加的所有物品，价值决定概率；但是高价值物品概率需要适当增加
                // TODO: 实现物品选择逻辑，需要根据价值=>概率的转化函数
                exchangeRecipeType[i] = ERecipe.Unknown;// 表示这是物品而不是配方
                // 这里需要根据价值权重随机选择分馏添加的物品
                exchangeItemId[i] = 0;// 临时占位，等待具体实现
            }
            //显示兑换信息
            if (exchangeRecipeType[i] == ERecipe.Unknown) {
                //物品
                ItemProto item = LDB.items.Select(exchangeItemId[i]);
                int itemCount = (int)Math.Ceiling(matrixTotalValue / itemValue[item.ID]);
                ItemProto matrix = LDB.items.Select(itemToMatrix[item.ID]);
                int matrixCount =
                    (int)Math.Ceiling(itemCount * itemValue[item.ID] / itemValue[matrix.ID] * vipDiscount);
                textExchangeInfo[i].text = $"{item.name} x {itemCount} <= {matrix.name} x {matrixCount}";
            } else {
                //配方
                BaseRecipe recipe = GetRecipe<BaseRecipe>(exchangeRecipeType[i], exchangeItemId[i]);
                ItemProto matrix = LDB.items.Select(itemToMatrix[exchangeItemId[i]]);
                textExchangeInfo[i].text = $"{recipe.RecipeType.GetName()} <= {matrix.name} x {matrixCost}";
            }
        }
    }

    /// <summary>
    /// 购买限时物品/配方
    /// </summary>
    private static void Exchange(int index) { }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        nextFreshTick = version >= 2 ? r.ReadInt64() : baseFreshTs;
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(nextFreshTick);
    }

    public static void IntoOtherSave() { }

    #endregion
}
