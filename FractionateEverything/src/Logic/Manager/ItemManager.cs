using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommonAPI.Systems;
using UnityEngine;
using static FE.Utils.ProtoID;
using static FE.FractionateEverything;
using static FE.Utils.LogUtils;

namespace FE.Logic.Manager;

public static class ItemManager {
    /// <summary>
    /// 添加部分物品
    /// </summary>
    public static void AddFractionalPrototypeAndEssence() {
        //分馏原胚
        ProtoRegistry.RegisterItem(IFE分馏原胚普通, "分馏原胚（普通）", "分馏原胚（普通）描述",
            "Assets/fracicons/fractional-prototype-normal.png", tab分馏 * 1000 + 201, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.white, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE分馏原胚普通, ERecipeType.Assemble, 300,
            [I电磁矩阵], [20], [IFE分馏原胚普通], [1], "分馏原胚（普通）描述");
        ProtoRegistry.RegisterItem(IFE分馏原胚精良, "分馏原胚（精良）", "分馏原胚（精良）描述",
            "Assets/fracicons/fractional-prototype-uncommon.png", tab分馏 * 1000 + 202, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.green, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE分馏原胚精良, ERecipeType.Assemble, 300,
            [I能量矩阵], [17], [IFE分馏原胚精良], [1], "分馏原胚（精良）描述");
        ProtoRegistry.RegisterItem(IFE分馏原胚稀有, "分馏原胚（稀有）", "分馏原胚（稀有）描述",
            "Assets/fracicons/fractional-prototype-rare.png", tab分馏 * 1000 + 203, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.blue, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE分馏原胚稀有, ERecipeType.Assemble, 300,
            [I结构矩阵], [14], [IFE分馏原胚稀有], [1], "分馏原胚（稀有）描述");
        ProtoRegistry.RegisterItem(IFE分馏原胚史诗, "分馏原胚（史诗）", "分馏原胚（史诗）描述",
            "Assets/fracicons/fractional-prototype-epic.png", tab分馏 * 1000 + 204, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.magenta, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE分馏原胚史诗, ERecipeType.Assemble, 300,
            [I信息矩阵], [11], [IFE分馏原胚史诗], [1], "分馏原胚（史诗）描述");
        ProtoRegistry.RegisterItem(IFE分馏原胚传说, "分馏原胚（传说）", "分馏原胚（传说）描述",
            "Assets/fracicons/fractional-prototype-legendary.png", tab分馏 * 1000 + 205, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.yellow, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE分馏原胚传说, ERecipeType.Assemble, 300,
            [I引力矩阵], [8], [IFE分馏原胚传说], [1], "分馏原胚（传说）描述");
        ProtoRegistry.RegisterItem(IFE分馏原胚定向, "分馏原胚（定向）", "分馏原胚（定向）描述",
            "Assets/fracicons/fractional-prototype-directional.png", tab分馏 * 1000 + 206, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.red, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE分馏原胚定向, ERecipeType.Assemble, 300,
            [IFE分馏原胚普通, IFE分馏原胚精良, IFE分馏原胚稀有, IFE分馏原胚史诗, IFE分馏原胚传说], [1, 1, 1, 1, 1],
            [IFE分馏原胚定向], [1], "分馏原胚（定向）描述");
        //各种精华
        ProtoRegistry.RegisterItem(IFE复制精华, "复制精华", "复制精华描述",
            "Assets/fracicons/copy-essence.png", 3301, 100, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE复制精华, ERecipeType.Assemble, 300,
            [IFE复制精华], [2], [IFE复制精华], [1], "复制精华描述");
        ProtoRegistry.RegisterItem(IFE点金精华, "点金精华", "点金精华描述",
            "Assets/fracicons/alchemy-essence.png", 3302, 100, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE点金精华, ERecipeType.Assemble, 300,
            [IFE点金精华], [2], [IFE点金精华], [1], "点金精华描述");
        ProtoRegistry.RegisterItem(IFE分解精华, "分解精华", "分解精华描述",
            "Assets/fracicons/deconstruction-essence.png", 3303, 100, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE分解精华, ERecipeType.Assemble, 300,
            [IFE分解精华], [2], [IFE分解精华], [1], "分解精华描述");
        ProtoRegistry.RegisterItem(IFE转化精华, "转化精华", "转化精华描述",
            "Assets/fracicons/conversion-essence.png", 3304, 100, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.gray));
        ProtoRegistry.RegisterRecipe(RFE转化精华, ERecipeType.Assemble, 300,
            [IFE转化精华], [2], [IFE转化精华], [1], "转化精华描述");
    }

    #region 计算物品价值

    /// <summary>
    /// 物品总价值（原材料价值 + 制作价值）
    /// </summary>
    public static readonly Dictionary<int, float> itemValueDic = [];
    /// <summary>
    /// 物品转化率，物品价值越高则转化率越低
    /// </summary>
    public static readonly Dictionary<int, float> itemRatioDic = [];
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
        //初始化价值字典
        itemValueDic.Clear();
        //设置普通原矿价值
        itemValueDic.Add(I铁矿, 1.0f);
        itemValueDic.Add(I铜矿, 1.0f);
        itemValueDic.Add(IGB铝矿, 1.0f);
        itemValueDic.Add(IGB钨矿, 1.0f);
        itemValueDic.Add(I煤矿, 1.0f);
        itemValueDic.Add(I石矿, 1.0f);
        itemValueDic.Add(IGB硫矿, 1.2f);
        itemValueDic.Add(IGB放射性矿物, 1.2f);
        itemValueDic.Add(I木材, 1.0f);
        itemValueDic.Add(I植物燃料, 1.0f);
        itemValueDic.Add(I沙土, 1.0f);
        //设置母星系其他星球普通原矿价值
        itemValueDic.Add(I硅石, 2.0f);
        itemValueDic.Add(I钛石, 2.0f);
        //设置其他星系珍奇矿物价值
        itemValueDic.Add(I可燃冰, 5.0f);
        itemValueDic.Add(I金伯利矿石, 8.0f);
        itemValueDic.Add(I分形硅石, 8.0f);
        itemValueDic.Add(I有机晶体, 8.0f);
        itemValueDic.Add(I光栅石, 20.0f);
        itemValueDic.Add(I刺笋结晶, 20.0f);
        itemValueDic.Add(I单极磁石, 200.0f);
        //设置气巨、冰巨可开采物品价值
        itemValueDic.Add(I氢, 2.0f);
        itemValueDic.Add(I重氢, 5.0f);
        itemValueDic.Add(IGB氦, 20.0f);
        //dic.Add(I可燃冰, 2.0f);
        //dic.Add(IGB氨, 3.0f);
        //设置可直接抽取的物品价值
        itemValueDic.Add(I原油, 1.0f);
        itemValueDic.Add(IGB海水, 1.0f);
        itemValueDic.Add(I水, 1.0f);
        itemValueDic.Add(IGB盐酸, 5.0f);
        itemValueDic.Add(I硫酸, 5.0f);
        itemValueDic.Add(IGB硝酸, 5.0f);
        itemValueDic.Add(IGB氨, 5.0f);
        //设置黑雾掉落价值
        itemValueDic.Add(I能量碎片, 2f);
        itemValueDic.Add(I黑雾矩阵, 2.5f);
        itemValueDic.Add(I物质重组器, 4.5f);
        itemValueDic.Add(I硅基神经元, 6.0f);
        itemValueDic.Add(I负熵奇点, 7.5f);
        itemValueDic.Add(I核心素, 30f);
        //设置临界光子价值
        itemValueDic.Add(I临界光子, 400.0f);
        //设置多功能集成组件价值，已知有许多配方为“多功能集成组件*1 -> 某个建筑*n”，所以暂定高价值
        //dic.Add(IMS多功能集成组件, 999999f);
        //移除不存在的物品
        var keysToRemove = itemValueDic.Keys.Where(key => !LDB.items.Exist(key)).ToList();
        foreach (var key in keysToRemove) {
            itemValueDic.Remove(key);
        }
        //将剩余的物品价值都设为最大值
        foreach (var item in LDB.items.dataArray) {
            if (!itemValueDic.ContainsKey(item.ID)) {
                itemValueDic.Add(item.ID, float.MaxValue);
            }
        }
        //获取所有配方（排除分馏配方）
        var recipes = LDB.recipes.dataArray
            .Where(r => r.Type != ERecipeType.Fractionate)
            .ToArray();

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
                    if (itemValueDic[itemId] == double.MaxValue) {
                        canProcess = false;
                        break;
                    }
                }
                if (!canProcess) continue;

                // 计算输入总价值和输出总单位数
                float inputValue = 0;
                for (int i = 0; i < inputIDs.Count; i++) {
                    inputValue += inputCounts[i] * itemValueDic[inputIDs[i]];
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
                for (int i = 0; i < outputIDs.Count; i++) {
                    int itemId = outputIDs[i];
                    if (!itemValueDic.ContainsKey(itemId)) {
                        LogWarning($"物品ID {itemId} 在itemValueDic中不存在，跳过更新价值");
                        continue;
                    }
                    if (unitValue < itemValueDic[itemId]) {
                        itemValueDic[itemId] = unitValue;
                        ItemProto item = LDB.items.Select(itemId);
                        LogDebug($"更新物品{item.name}({itemId})价值为{unitValue:F3}("
                                 + $"{inputValue / outputUnits:F3}+{adjustedTimeValue / outputUnits:F3})");
                        if (itemId == I蓄电器) {
                            itemValueDic[I蓄电器满] = unitValue * 2;
                        }
                        changed = true;
                    }
                }
            }
        } while (changed && iteration < 10);

        //根据物品价值构建概率表
        foreach (ItemProto item in LDB.items.dataArray) {
            itemRatioDic.Add(item.ID, (float)(a * Math.Pow(itemValueDic[item.ID] * 100, b)));
        }
#if DEBUG
        //按照从小到大的顺序输出所有物品的原材料点数
        if (Directory.Exists(ITEM_VALUE_CSV_DIR)) {
            using StreamWriter sw = new StreamWriter(ITEM_VALUE_CSV_PATH);
            sw.WriteLine("ID,名称,价值,量子复制概率最大值");
            foreach (var p in itemValueDic.OrderBy(p => p.Value)) {
                ItemProto item = LDB.items.Select(p.Key);
                // LogDebug($"物品{item.name}({p.Key})价值保存至表格...");
                sw.WriteLine(
                    $"{p.Key},{item.name},{p.Value:F2},{itemRatioDic[p.Key]:P5}");
            }
        }
#endif
    }

    #endregion
}
