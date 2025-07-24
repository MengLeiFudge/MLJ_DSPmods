using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class ItemManager {
    public static void AddTranslations() {
        Register("分馏原胚普通", "Frac Proto(Normal)", "分馏原胚（普通）");
        Register("I分馏原胚普通", "-", "随处可见的分馏原胚，可以通过转化塔变为各种分馏建筑。");

        Register("分馏原胚精良", "Frac Proto(Uncommon)", "分馏原胚（精良）");
        Register("I分馏原胚精良", "-", "工艺精美的分馏原胚，可以通过转化塔变为各种分馏建筑。");

        Register("分馏原胚稀有", "Frac Proto(Rare)", "分馏原胚（稀有）");
        Register("I分馏原胚稀有", "-", "较为罕见的分馏原胚，可以通过转化塔变为各种分馏建筑。");

        Register("分馏原胚史诗", "Frac Proto(Epic)", "分馏原胚（史诗）");
        Register("I分馏原胚史诗", "-", "历史长河中也难得一见的分馏原胚，可以通过转化塔变为各种分馏建筑。");

        Register("分馏原胚传说", "Frac Proto(Legendary)", "分馏原胚（传说）");
        Register("I分馏原胚传说", "-", "仅存在于传说中的分馏原胚，可以通过转化塔变为各种分馏建筑。");

        Register("分馏原胚定向", "Frac Proto(Directional)", "分馏原胚（定向）");
        Register("I分馏原胚定向", "-", "高科技人工制作的分馏原胚，可以直接加工为指定的分馏建筑。");


        Register("复制精华", "Copy Essence");
        Register("I复制精华", "-", "矿物复制塔产出的精华，有特殊的用途。");

        Register("点金精华", "Alchemy Essence");
        Register("I点金精华", "-", "点金塔产出的精华，有特殊的用途。");

        Register("分解精华", "Deconstruction Essence");
        Register("I分解精华", "-", "分解塔产出的精华，有特殊的用途。");

        Register("转化精华", "Conversion Essence");
        Register("I转化精华", "-", "转化塔产出的精华，有特殊的用途。");


        Register("电磁奖券", "Electromagnetic Ticket");
        Register("I电磁奖券", "-", "一张高科技奖券，里面似乎封装了大量电磁矩阵。可以用于抽奖。");

        Register("能量奖券", "Energy Ticket");
        Register("I能量奖券", "-", "一张高科技奖券，里面似乎封装了大量能量矩阵。可以用于抽奖。");

        Register("结构奖券", "Structure Ticket");
        Register("I结构奖券", "-", "一张高科技奖券，里面似乎封装了大量结构矩阵。可以用于抽奖。");

        Register("信息奖券", "Information Ticket");
        Register("I信息奖券", "-", "一张高科技奖券，里面似乎封装了大量信息矩阵。可以用于抽奖。");

        Register("引力奖券", "Gravity Ticket");
        Register("I引力奖券", "-", "一张高科技奖券，里面似乎封装了大量引力矩阵。可以用于抽奖。");

        Register("宇宙奖券", "Universe Ticket");
        Register("I宇宙奖券", "-", "一张高科技奖券，里面似乎封装了大量宇宙矩阵。可以用于抽奖。");

        Register("黑雾奖券", "Dark Fog Ticket");
        Register("I黑雾奖券", "-", "一张高科技奖券，里面似乎封装了大量黑雾矩阵。可以用于抽奖。");

        Register("分馏配方核心", "Frac Recipe Core");
        Register("I分馏配方核心", "-", "主脑研发的高科技立方体，具有神奇的力量。可以用于兑换任何分馏配方。");

        Register("建筑增幅芯片", "Building Increase Chip");
        Register("I建筑增幅芯片", "-", "高度集成的电子芯片，可以增强各种分馏建筑的效果，带来特殊的提升。");

        Register("残破核心", "Destroyed Core");
        Register("I残破核心", "-", "损毁的分馏配方核心，两个残破核心可以合成一个新的分馏配方核心。");
    }

    #region 添加新物品

    /// <summary>
    /// 添加部分物品
    /// </summary>
    public static void AddFractionalPrototypeAndEssence() {
        // EItemType
        // Unknown,
        // Resource,   原矿（铁矿、铜矿等）
        // Material,   原矿熔炼的材料（铁板、铜板、增产剂、钛合金等）
        // Component,  材料加工的产物（磁线圈、电路板等）
        // Product,    消耗品（弹药、燃料棒、无人机等）
        // Logistics,  运输相关（传送带、分拣器、储物仓、电线杆等）
        // Production, 实体机器（发电机、制作台等）
        // Decoration, 地基
        // Turret,     进攻建筑
        // Defense,    防御建筑
        // DarkFog,    黑雾掉落
        // Matrix,     矩阵

        // item.UnlockKey 未设置的话，为正数，表示物品解锁需要看配方是否解锁
        // item.UnlockKey = -1 表示物品直接解锁
        // item.UnlockKey = -2 表示黑雾物品
        // recipe.IconPath = "" 表示配方不需要独有图标，直接使用产物[0]的图标
        // recipe.Handcraft = false 表示配方禁止手动制造
        // recipe.NonProductive = true 表示增产剂仅能加速，不能增产

        ItemProto item;
        RecipeProto recipe;

        item = ProtoRegistry.RegisterItem(IFE分馏原胚普通, "分馏原胚普通", "I分馏原胚普通",
            "Assets/fe/frac-proto-normal", tab分馏 * 1000 + 201, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.white, Color.gray));
        item.UnlockKey = -1;

        // //树、草、石头有20%概率掉落普通原胚
        // foreach (VegeProto vege in LDB.veges.dataArray) {
        //     vege.MiningItem = [..vege.MiningItem, IFE分馏原胚普通];
        //     vege.MiningCount = [..vege.MiningCount, 1];
        //     vege.MiningChance = [..vege.MiningChance, 0.2f];
        // }

        item = ProtoRegistry.RegisterItem(IFE分馏原胚精良, "分馏原胚精良", "I分馏原胚精良",
            "Assets/fe/frac-proto-uncommon", tab分馏 * 1000 + 202, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.green, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏原胚稀有, "分馏原胚稀有", "I分馏原胚稀有",
            "Assets/fe/frac-proto-rare", tab分馏 * 1000 + 203, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏原胚史诗, "分馏原胚史诗", "I分馏原胚史诗",
            "Assets/fe/frac-proto-epic", tab分馏 * 1000 + 204, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏原胚传说, "分馏原胚传说", "I分馏原胚传说",
            "Assets/fe/frac-proto-legendary", tab分馏 * 1000 + 205, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.yellow, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分馏原胚定向, "分馏原胚定向", "I分馏原胚定向",
            "Assets/fe/frac-proto-directional", tab分馏 * 1000 + 206, 30, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.red, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE分馏原胚定向, ERecipeType.Assemble, 300,
            [IFE分馏原胚普通, IFE分馏原胚精良, IFE分馏原胚稀有, IFE分馏原胚史诗, IFE分馏原胚传说], [1, 1, 1, 1, 1],
            [IFE分馏原胚定向], [1], "I分馏原胚定向");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;


        item = ProtoRegistry.RegisterItem(IFE复制精华, "复制精华", "I复制精华",
            "Assets/fe/copy-essence", tab分馏 * 1000 + 301, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE点金精华, "点金精华", "I点金精华",
            "Assets/fe/alchemy-essence", tab分馏 * 1000 + 302, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE分解精华, "分解精华", "I分解精华",
            "Assets/fe/deconstruction-essence", tab分馏 * 1000 + 303, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE转化精华, "转化精华", "I转化精华",
            "Assets/fe/conversion-essence", tab分馏 * 1000 + 304, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        item.UnlockKey = -1;


        item = ProtoRegistry.RegisterItem(IFE电磁奖券, "电磁奖券", "I电磁奖券",
            "Assets/fe/electromagnetic-ticket", tab分馏 * 1000 + 401, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE电磁奖券, ERecipeType.Assemble, 360,
            [I电磁矩阵], [70], [IFE电磁奖券], [1], "I电磁奖券");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE能量奖券, "能量奖券", "I能量奖券",
            "Assets/fe/energy-ticket", tab分馏 * 1000 + 402, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.red, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE能量奖券, ERecipeType.Assemble, 360,
            [I能量矩阵], [90], [IFE能量奖券], [1], "I能量奖券");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE结构奖券, "结构奖券", "I结构奖券",
            "Assets/fe/structure-ticket", tab分馏 * 1000 + 403, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.yellow, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE结构奖券, ERecipeType.Assemble, 360,
            [I结构矩阵], [140], [IFE结构奖券], [1], "I结构奖券");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE信息奖券, "信息奖券", "I信息奖券",
            "Assets/fe/information-ticket", tab分馏 * 1000 + 404, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE信息奖券, ERecipeType.Assemble, 360,
            [I信息矩阵], [110], [IFE信息奖券], [1], "I信息奖券");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE引力奖券, "引力奖券", "I引力奖券",
            "Assets/fe/gravity-ticket", tab分馏 * 1000 + 405, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.green, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE引力奖券, ERecipeType.Assemble, 360,
            [I引力矩阵], [50], [IFE引力奖券], [1], "I引力奖券");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE宇宙奖券, "宇宙奖券", "I宇宙奖券",
            "Assets/fe/universe-ticket", tab分馏 * 1000 + 406, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.white, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE宇宙奖券, ERecipeType.Assemble, 360,
            [I宇宙矩阵], [40], [IFE宇宙奖券], [1], "I宇宙奖券");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;

        item = ProtoRegistry.RegisterItem(IFE黑雾奖券, "黑雾奖券", "I黑雾奖券",
            "Assets/fe/dark-fog-ticket", tab分馏 * 1000 + 407, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        recipe = ProtoRegistry.RegisterRecipe(RFE黑雾奖券, ERecipeType.Assemble, 360,
            [I黑雾矩阵], [777], [IFE黑雾奖券], [1], "I黑雾奖券");
        recipe.IconPath = "";
        recipe.Handcraft = false;
        recipe.NonProductive = true;


        item = ProtoRegistry.RegisterItem(IFE分馏配方核心, "分馏配方核心", "I分馏配方核心",
            "Assets/fe/frac-recipe-core", tab分馏 * 1000 + 408, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE建筑增幅芯片, "建筑增幅芯片", "I建筑增幅芯片",
            "Assets/fe/building-increase-chip", tab分馏 * 1000 + 409, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        item.UnlockKey = -1;

        item = ProtoRegistry.RegisterItem(IFE残破核心, "残破核心", "I残破核心",
            "Assets/fe/frac-recipe-core-destroyed", tab分馏 * 1000 + 410, 100, EItemType.Component,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        item.UnlockKey = -1;
    }

    #endregion

    #region 计算物品价值

    public static float maxValue = 1000000;
    /// <summary>
    /// 物品总价值（原材料价值 + 制作价值）
    /// </summary>
    public static readonly float[] itemValue = new float[12000];
    /// <summary>
    /// 物品转化率，物品价值越高则转化率越低
    /// </summary>
    public static readonly float[] itemRatio = new float[12000];
    //计算物品转化率。f(x)=a*x^b，由MATLAB拟合得到
    //原始数据：
    //100.000000 	    0.100000
    //200.000000 	    0.070000
    //400.000000 	    0.049000
    //800.000000 	    0.034300
    //1600.000000 	    0.024010
    //3200.000000 	    0.016807
    //6400.000000 	    0.011765
    //12800.000000 	    0.008235
    //25600.000000 	    0.005765
    //51200.000000 	    0.004035
    //102400.000000 	0.002825
    //204800.000000 	0.001977
    //409600.000000 	0.001384
    //819200.000000 	0.000969
    //1638400.000000 	0.000678
    //3276800.000000 	0.000475
    private const double a = 1.069415182912524;
    private const double b = -0.5145731728297580;
#if DEBUG
    private const string ITEM_VALUE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
    private const string ITEM_VALUE_CSV_PATH = $@"{ITEM_VALUE_CSV_DIR}\itemPoint.csv";
#endif

    /// <summary>
    /// 计算所有物品的价值
    /// </summary>
    public static void CalculateItemValues() {
        //初始化价值字典，将所有物品价值都设为特定的大值
        for (int i = 0; i < itemValue.Length; i++) {
            itemValue[i] = maxValue;
        }
        //设置普通原矿价值
        itemValue[I铁矿] = 1.0f;
        itemValue[I铜矿] = 1.0f;
        itemValue[IGB铝矿] = 1.0f;
        itemValue[IGB钨矿] = 1.0f;
        itemValue[I煤矿] = 1.0f;
        itemValue[I石矿] = 1.0f;
        itemValue[IGB硫矿] = 1.2f;
        itemValue[IGB放射性矿物] = 1.2f;
        itemValue[I木材] = 1.0f;
        itemValue[I植物燃料] = 1.0f;
        itemValue[I沙土] = 1.0f;
        //设置母星系其他星球普通原矿价值
        itemValue[I硅石] = 2.0f;
        itemValue[I钛石] = 2.0f;
        //设置其他星系珍奇矿物价值
        itemValue[I可燃冰] = 5.0f;
        itemValue[I金伯利矿石] = 8.0f;
        itemValue[I分形硅石] = 8.0f;
        itemValue[I有机晶体] = 8.0f;
        itemValue[I光栅石] = 20.0f;
        itemValue[I刺笋结晶] = 20.0f;
        itemValue[I单极磁石] = 200.0f;
        //设置气巨、冰巨可开采物品价值
        itemValue[I氢] = 2.0f;
        itemValue[I重氢] = 5.0f;
        itemValue[IGB氦] = 20.0f;
        //itemValue[I可燃冰] = 2.0f;
        //itemValue[IGB氨] = 3.0f;
        //设置可直接抽取的物品价值
        itemValue[I原油] = 1.0f;
        itemValue[IGB海水] = 1.0f;
        itemValue[I水] = 1.0f;
        itemValue[IGB盐酸] = 5.0f;
        itemValue[I硫酸] = 5.0f;
        itemValue[IGB硝酸] = 5.0f;
        itemValue[IGB氨] = 5.0f;
        itemValue[IGB二氧化硫] = 5.0f;
        itemValue[IGB二氧化碳] = 5.0f;
        itemValue[IGB氮] = 3.0f;
        //设置黑雾掉落价值
        itemValue[I能量碎片] = 2f;
        itemValue[I黑雾矩阵] = 2.5f;
        itemValue[I物质重组器] = 4.5f;
        itemValue[I硅基神经元] = 6.0f;
        itemValue[I负熵奇点] = 7.5f;
        itemValue[I核心素] = 30f;
        //设置临界光子价值
        itemValue[I临界光子] = 400.0f;
        //设置分馏原胚价值
        itemValue[IFE分馏原胚普通] = 200.0f;
        itemValue[IFE分馏原胚精良] = 400.0f;
        itemValue[IFE分馏原胚稀有] = 800.0f;
        itemValue[IFE分馏原胚史诗] = 1600.0f;
        itemValue[IFE分馏原胚传说] = 3200.0f;
        //设置精华、核心、芯片价值
        itemValue[IFE复制精华] = 500.0f;
        itemValue[IFE点金精华] = 500.0f;
        itemValue[IFE分解精华] = 500.0f;
        itemValue[IFE转化精华] = 500.0f;
        itemValue[IFE分馏配方核心] = 500000.0f;
        itemValue[IFE建筑增幅芯片] = 888888.0f;
        itemValue[IFE残破核心] = 200000.0f;
        //获取所有配方（排除分馏配方、含有多功能集成组件的配方、GridIndex超限配方）
        var iEnumerable = LDB.recipes.dataArray.Where(r =>
            r.Type != ERecipeType.Fractionate
            && !r.Items.Contains(IMS多功能集成组件)
            && !r.Results.Contains(IMS多功能集成组件));
        if (GenesisBook.Enable) {
            iEnumerable = iEnumerable.Where(r =>
                r.GridIndex % 1000 / 100 >= 1
                && r.GridIndex % 1000 / 100 <= 7
                && r.GridIndex % 100 >= 1
                && r.GridIndex % 100 <= 17);
        } else {
            iEnumerable = iEnumerable.Where(r =>
                r.GridIndex % 1000 / 100 >= 1
                && r.GridIndex % 1000 / 100 <= 8
                && r.GridIndex % 100 >= 1
                && r.GridIndex % 100 <= 14);
        }
        var recipes = iEnumerable.ToArray();

        //迭代计算价值
        bool changed;
        int iteration = 0;

        do {
            changed = false;
            iteration++;

            foreach (var recipe in recipes) {
                // 复制配方数据
                List<int> inputIDs = recipe.Items.ToList();
                List<int> outputIDs = recipe.Results.ToList();
                List<int> inputCounts = recipe.ItemCounts.ToList();
                List<int> outputCounts = recipe.ResultCounts.ToList();
                // 抵消输入输出中的相同物品
                bool haveSameItem;
                do {
                    haveSameItem = false;
                    for (int i = 0; i < inputIDs.Count; i++) {
                        for (int j = 0; j < outputIDs.Count; j++) {
                            if (inputIDs[i] == outputIDs[j]) {
                                // 比较数量大小并抵消
                                if (inputCounts[i] > outputCounts[j]) {
                                    inputCounts[i] -= outputCounts[j];
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                } else if (inputCounts[i] < outputCounts[j]) {
                                    outputCounts[j] -= inputCounts[i];
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                } else {
                                    // 数量相等，完全抵消
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                }
                                haveSameItem = true;
                                break;
                            }
                        }
                        if (haveSameItem) break;
                    }
                } while (haveSameItem);

                // 检查输入物品是否都有已知价值
                bool canProcess = true;
                foreach (int itemId in inputIDs) {
                    if (Math.Abs(itemValue[itemId] - maxValue) < 0.001f) {
                        canProcess = false;
                        break;
                    }
                }
                if (!canProcess) continue;

                // 计算输入总价值和输出总单位数
                float inputValue = 0;
                for (int i = 0; i < inputIDs.Count; i++) {
                    inputValue += inputCounts[i] * itemValue[inputIDs[i]];
                }

                int outputUnits = outputCounts.Sum();

                // 如果输出总单位数为0，则跳过（没有净产出）
                if (outputUnits <= 0) continue;

                // 计算时间成本，原料价值越高则单位时间的价值越高
                // 别问为什么参数是 0.03 和 1.5，问就是经验
                float adjustedTimeValue = recipe.TimeSpend / 60.0f * (0.03f * inputValue + 1.5f);

                // 计算单位价值
                float unitValue = (inputValue + adjustedTimeValue) / outputUnits;

                // 更新输出物品价值（取最小值）
                foreach (int itemId in outputIDs) {
                    if (unitValue < itemValue[itemId]) {
                        itemValue[itemId] = unitValue;
                        ItemProto item = LDB.items.Select(itemId);
                        LogDebug($"更新物品{item.name}({itemId})价值为{unitValue:F3}("
                                 + $"{inputValue / outputUnits:F3}+{adjustedTimeValue / outputUnits:F3})");
                        if (itemId == I蓄电器) {
                            itemValue[I蓄电器满] = unitValue * 2;
                        }
                        changed = true;
                    }
                }
            }
        } while (changed && iteration < 10);

        //根据物品价值构建概率表
        foreach (ItemProto item in LDB.items.dataArray) {
            itemRatio[item.ID] = (float)(a * Math.Pow(itemValue[item.ID] * 100, b));
        }
#if DEBUG
        //按照从小到大的顺序输出所有物品的原材料点数
        if (Directory.Exists(ITEM_VALUE_CSV_DIR)) {
            using StreamWriter sw = new StreamWriter(ITEM_VALUE_CSV_PATH);
            sw.WriteLine("ID,名称,价值,量子复制概率最大值");
            Dictionary<int, float> dic = [];
            for (int i = 0; i < itemValue.Length; i++) {
                if (LDB.items.Exist(i)) {
                    dic[i] = itemValue[i];
                }
            }
            foreach (var p in dic.OrderBy(p => p.Value)) {
                ItemProto item = LDB.items.Select(p.Key);
                sw.WriteLine(
                    $"{p.Key},{item.name},{p.Value:F2},{itemRatio[p.Key]:P5}");
            }
        }
#endif
    }

    #endregion

    #region 将物品根据前置科技分类到不同矩阵层级

    public static readonly int[] itemToMatrix = new int[12000];

    public static void ClassifyItemsToMatrix() {
        //       物品状态                         missingTech    preTech
        //         正常                              false        tech
        //黑雾特有材料（UnlockKey=-2），或资源        false        null
        // 找不到主制作配方的对应科技                 true         null
        foreach (var item in LDB.items.dataArray) {
            int topMatrixID;
            if (item.Type == EItemType.Matrix) {
                //矩阵归到自己的层级，而非上一层级
                topMatrixID = item.ID switch {
                    IGB玻色矩阵 => I能量矩阵,
                    IGB耗散矩阵 => I信息矩阵,
                    IGB奇点矩阵 => I引力矩阵,
                    _ => item.ID
                };
            } else if (item.UnlockKey == -1 || item.Type == EItemType.Resource || item.ID == I沙土) {
                //原矿归到电磁矩阵
                topMatrixID = I电磁矩阵;
            } else if (item.UnlockKey == -2) {
                //黑雾特有掉落归到黑雾矩阵
                topMatrixID = I黑雾矩阵;
            } else if (item.preTech != null) {
                //大部分物品归到前置科技所属的矩阵层级。如果找不到前置科技所属的矩阵层级，归到电磁矩阵
                int id = GetTopMatrixID(item.preTech);
                topMatrixID = id > 0 ? id : I电磁矩阵;
            } else if (!item.missingTech) {
                //黑雾特有材料或资源
                topMatrixID = item.UnlockKey == -2 ? I黑雾矩阵 : I电磁矩阵;
            } else {
                //主制作配方无前置科技（铁块），或没有主制作配方（分馏配方核心）
                //此时尝试从其他配方的原料确认该物品可能的层级。如果仍未找到，归到黑雾矩阵
                List<RecipeProto> recipes = LDB.recipes.dataArray
                    .Where(r => r.Items.Contains(item.ID)).ToList();
                if (recipes.Count == 0) {
                    topMatrixID = I黑雾矩阵;
                } else {
                    topMatrixID = int.MaxValue;
                    foreach (RecipeProto recipe in recipes) {
                        if (recipe.preTech != null) {
                            int id = GetTopMatrixID(recipe.preTech);
                            if (id > 0 && id < topMatrixID) {
                                topMatrixID = id;
                            }
                        }
                    }
                    if (topMatrixID == int.MaxValue) {
                        topMatrixID = I黑雾矩阵;
                    }
                }
            }
            itemToMatrix[item.ID] = topMatrixID;
            LogDebug($"物品{item.name}({item.ID})归类到{LDB.items.Select(topMatrixID).name}({topMatrixID})");
        }
    }

    private static int GetTopMatrixID(TechProto tech) {
        if (tech.IsHiddenTech || tech.Items.Contains(I黑雾矩阵)) {
            return I黑雾矩阵;
        }
        int topMatrixID = 0;
        for (int j = 0; j < tech.Items.Length; j++) {
            int matrixID = tech.Items[j];
            if (LDB.items.Select(matrixID).Type == EItemType.Matrix) {
                matrixID = matrixID switch {
                    IGB玻色矩阵 => I能量矩阵,
                    IGB耗散矩阵 => I信息矩阵,
                    IGB奇点矩阵 => I引力矩阵,
                    _ => matrixID
                };
                topMatrixID = Math.Max(topMatrixID, matrixID);
            }
        }
        return topMatrixID;
    }

    #endregion

    #region 分馏中心背包（也就是Mod物品缓存区数据）

    public static readonly Dictionary<int, int> itemModDataCount = [];

    #endregion

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        int itemDataDicSize = r.ReadInt32();
        for (int i = 0; i < itemDataDicSize; i++) {
            int itemId = r.ReadInt32();
            int count = r.ReadInt32();
            itemModDataCount[itemId] = count;
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(itemModDataCount.Count);
        foreach (var p in itemModDataCount) {
            w.Write(p.Key);
            w.Write(p.Value);
        }
    }

    public static void IntoOtherSave() {
        itemModDataCount.Clear();
    }

    #endregion
}
