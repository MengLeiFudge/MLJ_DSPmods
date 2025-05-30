using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Utils.ProtoID;
using static FE.FractionateEverything;

namespace FE.Logic.Item;

public static class ItemValue {
    /// <summary>
    /// 物品原材料价值
    /// </summary>
    public static readonly Dictionary<int, float> itemMaterialCostDic = [];
    /// <summary>
    /// 物品制作价值
    /// </summary>
    public static readonly Dictionary<int, float> itemCraftCostDic = [];
    /// <summary>
    /// 物品总价值（原材料价值 + 制作价值）
    /// </summary>
    public static readonly Dictionary<int, float> itemCostDic = [];
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
    public static void CalaulateItemValue() {
        itemMaterialCostDic.Clear();
        itemCraftCostDic.Clear();
        itemCostDic.Clear();
        itemRatioDic.Clear();
        //设置部分原矿物品的价值
        UpdateItemValue(I能量碎片, 50);
        UpdateItemValue(I黑雾矩阵, 60);
        UpdateItemValue(I物质重组器, 110);
        UpdateItemValue(I硅基神经元, 150);
        UpdateItemValue(I负熵奇点, 180);
        UpdateItemValue(I核心素, 680);
        UpdateItemValue(I临界光子, 8000);
        List<int> point100 = [
            I硅石,
            I水, I原油, I精炼油_GB焦油, I硫酸, I氢, I重氢,
            IGB铝矿, IGB硫矿, IGB放射性矿物, IGB钨矿, IGB氦, IGB氨, IGB氮, IGB氧,
        ];
        foreach (int id in point100) {
            UpdateItemValue(id, 100);
        }
        List<int> point200 = [
            I可燃冰, I金伯利矿石, I分形硅石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
        ];
        foreach (int id in point200) {
            UpdateItemValue(id, 200);
        }
        //设置集成组件价值，定高以便后续迭代
        UpdateItemValue(IMS多功能集成组件, 99999999);
        //设置无制作配方的物品的价值与原矿价值相同，也为100
        //即使有mod新增一些物品、矿物什么的，也能按照100先处理
        foreach (ItemProto item in LDB.items.dataArray) {
            if (itemCostDic.ContainsKey(item.ID) || item.ID == I蓄电器满) {
                continue;
            }
            if (item.maincraft == null) {
                LogWarning($"物品{item.name}无制作配方，可能是原矿，物品价值设置为100");
                UpdateItemValue(item.ID, 100);
            }
        }
        //迭代计算物品价值
        bool updated;
        do {
            updated = false;
            foreach (RecipeProto recipe in LDB.recipes.dataArray) {
                //排除分馏配方
                if (recipe.Type == ERecipeType.Fractionate) {
                    continue;
                }
                //将配方信息添加到list，并确认制作时间
                List<int> inputIDs = recipe.Items.ToList();
                List<int> outputIDs = recipe.Results.ToList();
                List<int> inputCounts = recipe.ItemCounts.ToList();
                List<int> outputCounts = recipe.ResultCounts.ToList();
                int time = recipe.TimeSpend;
                //有可能原材料和产物包含同一个物品，需要避免此情况
                bool haveSameItem = false;
                do {
                    for (int i = 0; i < inputIDs.Count; i++) {
                        for (int j = 0; j < outputIDs.Count; j++) {
                            if (inputIDs[i] == outputIDs[j]) {
                                //比较数目大小
                                if (inputCounts[i] > outputCounts[j]) {
                                    inputCounts[i] -= outputCounts[j];
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                } else if (inputCounts[i] < outputCounts[j]) {
                                    outputCounts[j] -= inputCounts[i];
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                } else {
                                    inputIDs.RemoveAt(i);
                                    inputCounts.RemoveAt(i);
                                    outputIDs.RemoveAt(j);
                                    outputCounts.RemoveAt(j);
                                }
                                haveSameItem = true;
                                break;
                            }
                        }
                        if (haveSameItem) {
                            break;
                        }
                    }
                } while (haveSameItem);
                //排除无法用于计算的配方
                if (inputIDs.Count == 0 || outputIDs.Count != 1) {
                    continue;
                }
                //排除不知道部分原料价值的配方
                bool allMaterialValueExists = true;
                for (int i = 0; i < inputIDs.Count; i++) {
                    if (!itemMaterialCostDic.ContainsKey(inputIDs[i])) {
                        allMaterialValueExists = false;
                        break;
                    }
                }
                if (!allMaterialValueExists) {
                    continue;
                }
                //从原料向产物推算，得到产物价值
                //原材料成本
                float materialCost = 0;
                for (int i = 0; i < inputIDs.Count; i++) {
                    materialCost += inputCounts[i] * itemMaterialCostDic[inputIDs[i]];
                }
                //制作成本，0.4表示单位时间系数
                float craftCost = (float)(time * 0.4 * (1 + Math.Log(1 + materialCost * 0.1)));
                //判断总价值是否变低，如果更低则使用新的价值
                float newValue = craftCost + materialCost;
                if (itemCostDic.ContainsKey(outputIDs[0])) {
                    float oldValue = itemCostDic[outputIDs[0]];
                    if (newValue >= oldValue) {
                        continue;
                    }
                }
                UpdateItemValue(outputIDs[0], materialCost, craftCost);
                updated = true;
                if (outputIDs[0] == I蓄电器) {
                    UpdateItemValue(I蓄电器满, materialCost, craftCost);
                }
            }
        } while (updated);
        //根据物品价值构建概率表
        foreach (ItemProto item in LDB.items.dataArray) {
            if (!itemCostDic.ContainsKey(item.ID)) {
                LogWarning($"物品{item.name}没有计算出价值，物品价值设置为50000");
                UpdateItemValue(item.ID, 50000);
            }
            itemRatioDic.Add(item.ID, (float)(a * Math.Pow(itemCostDic[item.ID], b)));
        }
#if DEBUG
        //按照从小到大的顺序输出所有物品的原材料点数
        if (Directory.Exists(ITEM_VALUE_CSV_DIR)) {
            using StreamWriter sw = new StreamWriter(ITEM_VALUE_CSV_PATH);
            sw.WriteLine("ID,名称,物品价值,原料价值,时间价值,量子复制概率最大值");
            foreach (var p in itemCostDic.OrderBy(p => p.Value)) {
                ItemProto item = LDB.items.Select(p.Key);
                if (item == null) {
                    continue;
                }
                sw.WriteLine(
                    $"{p.Key},{item.name},{itemMaterialCostDic[p.Key]:P2},{itemCraftCostDic[p.Key]:P2},{p.Value:P2},{itemRatioDic[p.Key]:P5}");
            }
        }
#endif
    }

    private static void UpdateItemValue(int id, float materialValue, float craftValue = 0) {
        if (!itemCostDic.ContainsKey(id)) {
            itemMaterialCostDic.Add(id, materialValue);
            itemCraftCostDic.Add(id, craftValue);
            itemCostDic.Add(id, materialValue + craftValue);
        } else {
            itemMaterialCostDic[id] = materialValue;
            itemCraftCostDic[id] = craftValue;
            itemCostDic[id] = materialValue + craftValue;
        }
        ItemProto item = LDB.items.Select(id);
        LogDebug($"更新物品{item.name}原料价值{materialValue:F2}，制作价值{craftValue:F2}，物品价值{itemCostDic[id]:F2}");
    }
}
