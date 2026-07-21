using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Fractionation.Fractionators;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Items;

/// <summary>
/// FE 物品原型、翻译和物品价值注册入口。
/// </summary>
public static class ItemManager {
    public static void AddTranslations() {
        Register("残片", "Archive Fragment", "残片");
        Register("I残片",
            "Common archive currency recovered from damaged or invalid protocol data. It can narrow retrieval direction.",
            "从损坏或无效协议数据中整理出的普通档案货币，可用于收窄检索方向。");
        Register("记忆源点", "Memory Anchor", "记忆源点");
        Register("I记忆源点",
            "A high-integrity archive anchor used to locate and advance one eligible protocol precisely.",
            "高完整度的文明档案锚点，可用于精确定位并推进一项符合阶段条件的协议。");

        RegisterAnalysisDataTranslations("电磁解析数据", "Electromagnetic Analysis Data", "电磁");
        RegisterAnalysisDataTranslations("能量解析数据", "Energy Analysis Data", "能量");
        RegisterAnalysisDataTranslations("结构解析数据", "Structure Analysis Data", "结构");
        RegisterAnalysisDataTranslations("信息解析数据", "Information Analysis Data", "信息");
        RegisterAnalysisDataTranslations("引力解析数据", "Gravity Analysis Data", "引力");
        RegisterAnalysisDataTranslations("宇宙解析数据", "Universe Analysis Data", "宇宙");

        Register("通用原胚", "Common Tower Proto", "通用原胚");
        Register("I通用原胚",
            "An active proto whose lineage is not fixed. An Interaction Tower can incubate it into any tower type.",
            "尚未固定谱系的活性原胚，可在交互塔中随机培养为任意一种分馏塔。");
        RegisterTowerProtoTranslations("交互塔原胚", "Interaction Tower Proto", "交互塔");
        RegisterTowerProtoTranslations("解析塔原胚", "Analysis Tower Proto", "解析塔");
        RegisterTowerProtoTranslations("资源塔原胚", "Resource Tower Proto", "资源塔");
        RegisterTowerProtoTranslations("转化塔原胚", "Conversion Tower Proto", "转化塔");
    }

    private static void RegisterAnalysisDataTranslations(string name, string englishName, string stageName) {
        Register(name, englishName, name);
        Register($"I{name}",
            $"Physical analysis data extracted from {stageName} matrix records. Upload it to generate retrieval opportunities.",
            $"从{stageName}矩阵中解析出的实体数据；上传后会积累{stageName}阶段的协议检索机会。");
    }

    private static void RegisterTowerProtoTranslations(string name, string englishName, string towerName) {
        Register(name, englishName, name);
        Register($"I{name}",
            $"A lineage-fixed proto. Cultivating it in an Interaction Tower produces a {towerName}.",
            $"已经固定为{towerName}谱系的原胚，在交互塔中培养后会稳定产出{towerName}。");
    }

    #region 添加新物品

    /// <summary>
    /// 添加部分物品
    /// </summary>
    public static void AddCoreItemsAndPrototypes() {
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
        // RecipeUnlocked Patch 用于调整配方解锁状态（Item直接用UnlockKey=-1，就不需要patch item的）

        RegisterCivilizationItem(IFE残片, "残片", "I残片", 101,
            Color.gray, Color.black, "cpfragment");
        RegisterCivilizationItem(IFE电磁解析数据, "电磁解析数据", "I电磁解析数据", 102,
            new Color(0.32f, 0.72f, 1f), new Color(0.04f, 0.14f, 0.28f), "emanalysis");
        RegisterCivilizationItem(IFE能量解析数据, "能量解析数据", "I能量解析数据", 103,
            new Color(1f, 0.52f, 0.18f), new Color(0.26f, 0.08f, 0.02f), "energyanalysis");
        RegisterCivilizationItem(IFE结构解析数据, "结构解析数据", "I结构解析数据", 104,
            new Color(0.86f, 0.68f, 1f), new Color(0.16f, 0.06f, 0.28f), "structureanalysis");
        RegisterCivilizationItem(IFE信息解析数据, "信息解析数据", "I信息解析数据", 105,
            new Color(0.36f, 0.96f, 0.78f), new Color(0.02f, 0.20f, 0.12f), "infoanalysis");
        RegisterCivilizationItem(IFE引力解析数据, "引力解析数据", "I引力解析数据", 106,
            new Color(0.76f, 0.82f, 1f), new Color(0.10f, 0.10f, 0.30f), "gravityanalysis");
        RegisterCivilizationItem(IFE宇宙解析数据, "宇宙解析数据", "I宇宙解析数据", 107,
            new Color(1f, 0.92f, 0.42f), new Color(0.28f, 0.20f, 0.04f), "universeanalysis");
        RegisterCivilizationItem(IFE记忆源点, "记忆源点", "I记忆源点", 109,
            new Color(0.45f, 0.75f, 1f), new Color(0.1f, 0.2f, 0.4f), "memory");

        RegisterTowerProto(IFE交互塔原胚, "交互塔原胚", "I交互塔原胚", 201,
            "Assets/fe/frac-proto-normal", InteractionTower.color, "interaction-proto");
        RegisterTowerProto(IFE解析塔原胚, "解析塔原胚", "I解析塔原胚", 202,
            "Assets/fe/frac-proto-legendary", RectificationTower.color, "analysis-proto");
        RegisterTowerProto(IFE资源塔原胚, "资源塔原胚", "I资源塔原胚", 203,
            "Assets/fe/frac-proto-uncommon", MineralReplicationTower.color, "resource-proto");
        RegisterTowerProto(IFE转化塔原胚, "转化塔原胚", "I转化塔原胚", 204,
            "Assets/fe/frac-proto-epic", ConversionTower.color, "conversion-proto");
        RegisterTowerProto(IFE通用原胚, "通用原胚", "I通用原胚", 205,
            "Assets/fe/frac-proto-directional", Color.red, "common-proto");
    }

    private static ItemProto RegisterTowerProto(int itemId, string name, string description, int gridOffset,
        string iconPath, Color color, string iconTag) {
        ItemProto item = ProtoRegistry.RegisterItem(itemId, name, description, iconPath,
            tab分馏 * 1000 + gridOffset, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(color, Color.gray));
        item.UnlockKey = -1;
        item.IconTag = iconTag;
        return item;
    }

    private static void RegisterCivilizationItem(int itemId, string name, string description, int gridOffset,
        Color iconColor, Color iconBackgroundColor, string iconTag) {
        RegisterFeInternalItem(itemId, name, description, gridOffset, iconColor, iconBackgroundColor, iconTag);
    }

    private static void RegisterFeInternalItem(int itemId, string name, string description, int gridOffset,
        Color iconColor, Color iconBackgroundColor, string iconTag) {
        ItemProto item = ProtoRegistry.RegisterItem(itemId, name, description,
            "Assets/fe/copy-essence", tab分馏 * 1000 + gridOffset, 100, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(iconColor, iconBackgroundColor));
        item.UnlockKey = -1;
        item.IconTag = iconTag;
    }

    #endregion

    #region 计算物品价值，以及交互塔可接受物品范围

    public const float maxValue = float.MaxValue;
    /// <summary>
    /// 物品总价值（原材料价值 + 制作价值）
    /// </summary>
    public static readonly float[] itemValue = new float[12000];
    /// <summary>
    /// 交互塔可接收的所有物品id
    /// </summary>
    public static int[] needs = [];

    /// <summary>
    /// 计算所有物品的价值
    /// </summary>
    public static void CalculateItemValues() {
        //所有矿物都设置价值为1
        foreach (VeinProto vein in LDB.veins.dataArray) {
            itemValue[vein.MiningItem] = 1.0f;
        }
        //设置普通原矿价值
        itemValue[I木材] = 1.0f;
        itemValue[I植物燃料] = 1.0f;
        itemValue[I沙土] = 1.0f;
        if (GenesisBook.Enable) {
            itemValue[IGB硫矿] = 1.2f;
            itemValue[IGB放射性矿物] = 1.2f;
        }
        //设置母星系其他星球普通原矿价值
        itemValue[I硅石] = 2.0f;
        itemValue[I钛石] = 2.0f;
        //设置其他星系珍奇矿物价值
        if (OrbitalRing.Enable) {
            itemValue[IOR黄铁矿] = 5f;
            itemValue[IOR铀矿] = 5f;
            itemValue[IOR石墨矿] = 5f;
        }
        itemValue[I可燃冰] = 5.0f;
        itemValue[I金伯利矿石] = 8.0f;
        itemValue[I分形硅石] = 8.0f;
        itemValue[I有机晶体] = 8.0f;
        itemValue[I光栅石] = 20.0f;
        itemValue[I刺笋结晶] = 20.0f;
        itemValue[I单极磁石] = 200.0f;
        //设置气巨、冰巨、可直接抽取的物品价值
        itemValue[I氢] = 2.0f;
        itemValue[I重氢] = 5.0f;
        itemValue[I原油] = 1.0f;
        itemValue[I水] = 1.0f;
        itemValue[I硫酸] = 5.0f;
        if (GenesisBook.Enable) {
            itemValue[IGB氦] = 20.0f;
            itemValue[IGB海水] = 2.0f;
            itemValue[IGB盐酸] = 5.0f;
            itemValue[IGB硝酸] = 5.0f;
            itemValue[IGB氨] = 5.0f;
            itemValue[IGB二氧化硫] = 5.0f;
            itemValue[IGB二氧化碳] = 5.0f;
            itemValue[IGB氮] = 3.0f;
        }
        //设置黑雾掉落价值
        itemValue[I能量碎片] = 2f;
        itemValue[I黑雾矩阵] = 2.5f;
        itemValue[I物质重组器] = 4.5f;
        itemValue[I硅基神经元] = 6.0f;
        itemValue[I负熵奇点] = 7.5f;
        itemValue[I核心素] = 30f;
        //设置临界光子价值
        itemValue[I临界光子] = 400.0f;
        //设置分馏塔、分馏塔原胚价值
        float modFractionatorValue = 400.0f;
        float commonProtoValue = 2000.0f;
        itemValue[IFE资源塔] = modFractionatorValue;
        itemValue[IFE交互塔] = modFractionatorValue;
        itemValue[IFE转化塔] = modFractionatorValue;
        itemValue[IFE解析塔] = modFractionatorValue;
        itemValue[IFE通用原胚] = commonProtoValue;
        itemValue[IFE交互塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
        itemValue[IFE资源塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
        itemValue[IFE转化塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
        itemValue[IFE解析塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
        SetCivilizationResourceValues();
        //不存在的物品价值都设为特定值，这样也会将上面某些物品重置为maxValue（某些Mod未开启的情况下会有）
        for (int i = 0; i < itemValue.Length; i++) {
            if (itemValue[i] == 0 || !LDB.items.Exist(i)) {
                itemValue[i] = maxValue;
            }
        }
        CalculateItemValue:
        //获取所有配方（排除含有多功能集成组件的配方、GridIndex超限配方）
        var iEnumerable = LDB.recipes.dataArray.Where(r =>
            !r.Items.Contains(IMS多功能集成组件)
            && !r.Results.Contains(IMS多功能集成组件)
            && !r.Items.Contains(IFE通用原胚)
            && !r.Results.Contains(IFE通用原胚)
            && r.GridIndexValid());
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
                    if (Math.Abs(itemValue[itemId] - maxValue) < 0.0001f) {
                        canProcess = false;
                        break;
                    }
                }
                if (!canProcess) continue;

                // 计算这种产物的单位价值
                float unitValue;
                if (recipe.Type == ERecipeType.Fractionate) {
                    if (inputIDs.Count != 1 || outputIDs.Count != 1) {
                        // 无法处理非A=>B的分馏配方
                        LogWarning($"无法处理非A=>B的分馏配方：{recipe.Name}({recipe.ID})，"
                                   + $"inputIDs.Count={inputIDs.Count}，outputIDs.Count={outputIDs.Count}");
                        continue;
                    }
                    // 分馏配方的原料数目、产物数目表示比例，需要用其他方式计算价值
                    float produceProb = recipe.ResultCounts[0] / (float)recipe.ItemCounts[0];
                    // 假设1%概率对应的时间价值为1个原材料的1.5倍，p概率对应1.5/(p/0.01)，即0.015/p
                    unitValue = itemValue[inputIDs[0]] * (1 + 0.015f / produceProb);
                } else {
                    int outputUnits = outputCounts.Sum();
                    if (outputUnits <= 0) continue;
                    // 计算原材料总价值
                    float inputValue = 0;
                    for (int i = 0; i < inputIDs.Count; i++) {
                        inputValue += inputCounts[i] * itemValue[inputIDs[i]];
                    }
                    // 计算配方时间成本，原料价值越高则单位时间的价值越高
                    // 别问为什么参数是 0.03 和 1.5，问就是经验
                    float adjustedTimeValue = recipe.TimeSpend / 60.0f * (0.03f * inputValue + 1.5f);
                    // 计算单位价值
                    unitValue = (inputValue + adjustedTimeValue) / outputUnits;
                }

                // 更新输出物品价值（取最小值）
                foreach (int itemId in outputIDs) {
                    if (unitValue < itemValue[itemId]) {
                        itemValue[itemId] = unitValue;
                        // ItemProto item = LDB.items.Select(itemId);
                        // LogDebug($"更新物品{item.name}({itemId})价值为{unitValue:F3}("
                        //          + $"{inputValue / outputUnits:F3}+{adjustedTimeValue / outputUnits:F3})");
                        if (OrbitalRing.Enable) {
                            if (itemId == IOR蓄电器) {
                                itemValue[IOR蓄电器满] = unitValue * 2;
                            } else if (itemId == IOR蓄电器mk2) {
                                itemValue[IOR蓄电器mk2满] = unitValue * 2;
                            }
                        } else {
                            if (itemId == I蓄电器) {
                                itemValue[I蓄电器满] = unitValue * 2;
                            }
                        }
                        changed = true;
                    }
                }
            }
        } while (changed && iteration < 10);

        //根据分馏配方计算未知价值物品的价值
        iEnumerable = LDB.recipes.dataArray.Where(r => r.Type == ERecipeType.Fractionate && r.GridIndexValid());
        recipes = iEnumerable.ToArray();
        foreach (var recipe in recipes) {
            // 复制配方数据
            List<int> inputIDs = recipe.Items.ToList();
            List<int> outputIDs = recipe.Results.ToList();
            List<int> inputCounts = recipe.ItemCounts.ToList();
            List<int> outputCounts = recipe.ResultCounts.ToList();

            // 检查输入物品是否都有已知价值
            bool canProcess = true;
            foreach (int itemId in inputIDs) {
                if (Math.Abs(itemValue[itemId] - maxValue) < 0.0001f) {
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

            // 计算时间成本
            // 分馏成功率为p时，时间成本为 inputValue*0.01/p
            float adjustedTimeValue = inputValue * 0.01f / (recipe.ResultCounts[0] / (float)recipe.ItemCounts[0]);

            // 计算单位价值
            float unitValue = (inputValue + adjustedTimeValue) / outputUnits;

            // 更新输出物品价值（取最小值）
            foreach (int itemId in outputIDs) {
                if (unitValue < itemValue[itemId]) {
                    itemValue[itemId] = unitValue;
                    // ItemProto item = LDB.items.Select(itemId);
                    // LogDebug($"更新物品{item.name}({itemId})价值为{unitValue:F3}("
                    //          + $"{inputValue / outputUnits:F3}+{adjustedTimeValue / outputUnits:F3})");
                    changed = true;
                }
            }
        }
        if (changed) {
            goto CalculateItemValue;
        }


        //设置多功能集成组件的价值
        iEnumerable = LDB.recipes.dataArray.Where(r => r.Items.Length == 1
                                                       && r.Items[0] == IMS多功能集成组件
                                                       && r.Results.Length > 0
                                                       && !r.Results.Contains(IMS多功能集成组件));
        float maxCalculatedValue = 0f;
        // 为每个配方分别计算多功能集成组件的价值
        foreach (var recipe in iEnumerable) {
            // 计算产物总价值
            float outputValue = 0f;
            for (int i = 0; i < recipe.Results.Length; i++) {
                outputValue += recipe.ResultCounts[i] * itemValue[recipe.Results[i]];
            }
            float inputCount = recipe.ItemCounts[0];
            float timeSpend = recipe.TimeSpend / 60.0f;
            // 根据公式反向推算多功能集成组件的价值
            // 产物价值 = 原材料价值 + 制作时间价值
            // outputValue = inputCount * x + timeSpend * (0.03f * inputCount * x + 1.5f)
            // 其中 x 是 itemValue[IMS多功能集成组件]
            //
            // 展开得到：
            // outputValue = inputCount * x + timeSpend * 0.03f * inputCount * x + timeSpend * 1.5f
            // outputValue = x * inputCount * (1 + timeSpend * 0.03f) + timeSpend * 1.5f
            //
            // 解得：
            // x = (outputValue - timeSpend * 1.5f) / (inputCount * (1 + timeSpend * 0.03f))
            if (inputCount > 0 && (inputCount * (1 + timeSpend * 0.03f)) > 0) {
                float calculatedValue = (outputValue - timeSpend * 1.5f)
                                        / (inputCount * (1 + timeSpend * 0.03f));
                maxCalculatedValue = Math.Max(maxCalculatedValue, calculatedValue);
            }
        }
        // 使用所有配方计算结果的最大值
        if (maxCalculatedValue > 0) {
            itemValue[IMS多功能集成组件] = maxCalculatedValue;
        }

        //根据物品价值构建交互塔可接受物品列表
        needs = LDB.items.dataArray
            .Where(item => itemValue[item.ID] < maxValue)
            .Select(item => item.ID)
            .ToArray();
    }

    #endregion

    #region 将物品根据前置科技分类到不同矩阵层级

    public static readonly int[] MainProgressMatrixIds = [
        I电磁矩阵,
        I能量矩阵,
        I结构矩阵,
        I信息矩阵,
        I引力矩阵,
        I宇宙矩阵,
    ];

    public static readonly int[] itemToMatrix = new int[12000];

    public static readonly int[] AnalysisDataItemIds = [
        IFE电磁解析数据,
        IFE能量解析数据,
        IFE结构解析数据,
        IFE信息解析数据,
        IFE引力解析数据,
        IFE宇宙解析数据,
    ];

    public static bool IsAnalysisDataItem(int itemId) {
        return itemId >= IFE电磁解析数据 && itemId <= IFE宇宙解析数据;
    }

    public static bool IsCivilizationResourceItem(int itemId) {
        return itemId == IFE残片 || IsAnalysisDataItem(itemId);
    }

    public static bool IsMemoryAnchorItem(int itemId) {
        return itemId == IFE记忆源点;
    }

    public static int GetAnalysisDataLevel(int itemId) {
        return IsAnalysisDataItem(itemId) ? itemId - IFE电磁解析数据 : -1;
    }

    public static int GetAnalysisDataItemId(int level) {
        if (level < 0) {
            level = 0;
        } else if (level >= AnalysisDataItemIds.Length) {
            level = AnalysisDataItemIds.Length - 1;
        }
        return AnalysisDataItemIds[level];
    }

    public static int GetAnalysisDataFaceValue(int itemId) {
        int level = GetAnalysisDataLevel(itemId);
        return level < 0 ? 0 : 1 << (level + 1);
    }

    /// <summary>
    /// 获取主线矩阵阶段索引。黑雾矩阵不是主线矩阵，返回 -1 供调用方显式分支处理。
    /// </summary>
    public static int GetMatrixStageIndex(int matrixId) {
        return matrixId switch {
            I电磁矩阵 => 0,
            I能量矩阵 => 1,
            I结构矩阵 => 2,
            I信息矩阵 => 3,
            I引力矩阵 => 4,
            I宇宙矩阵 => 5,
            I黑雾矩阵 => -1,
            _ => matrixId > 0 && matrixId < itemToMatrix.Length
                ? GetMatrixStageIndex(itemToMatrix[matrixId])
                : 0,
        };
    }

    public static int GetCurrentProgressMatrixId() {
        if (GameMain.history == null) {
            return I电磁矩阵;
        }

        for (int i = MainProgressMatrixIds.Length - 1; i >= 0; i--) {
            int matrixId = MainProgressMatrixIds[i];
            if (GameMain.history.ItemUnlocked(matrixId)) {
                return matrixId;
            }
        }

        return I电磁矩阵;
    }

    public static int GetCurrentProgressStageIndex() {
        return GetMatrixStageIndex(GetCurrentProgressMatrixId());
    }

    public static float GetStageDecayFactor(int sourceMatrixId) {
        int sourceStage = GetMatrixStageIndex(sourceMatrixId);
        if (sourceStage < 0) {
            return 1.0f;
        }
        int stageDelta = GetCurrentProgressStageIndex() - sourceStage;
        return stageDelta switch {
            <= 0 => 1.0f,
            1 => 0.70f,
            2 => 0.45f,
            _ => 0.25f,
        };
    }

    public static int GetRectificationBaseFragmentYield(int matrixId) {
        return matrixId switch {
            I电磁矩阵 => 2,
            I能量矩阵 => 4,
            I结构矩阵 => 8,
            I信息矩阵 => 16,
            I引力矩阵 => 32,
            I宇宙矩阵 => 64,
            _ => 1,
        };
    }

    public static int GetRectificationFragmentYield(int matrixId, float ratio = 1f) {
        float value = GetRectificationBaseFragmentYield(matrixId) * ratio;
        return Mathf.Max(1, Mathf.RoundToInt(value));
    }

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
                int id = GetTechTopMatrixID(item.preTech);
                topMatrixID = id > 0 ? id : I电磁矩阵;
            } else if (!item.missingTech) {
                //黑雾特有材料或资源
                topMatrixID = item.UnlockKey == -2 ? I黑雾矩阵 : I电磁矩阵;
            } else {
                //主制作配方无前置科技（铁块），或没有主制作配方
                //此时尝试从其他配方的原料确认该物品可能的层级。如果仍未找到，归到黑雾矩阵
                List<RecipeProto> recipes = LDB.recipes.dataArray
                    .Where(r => r.Items.Contains(item.ID)).ToList();
                if (recipes.Count == 0) {
                    topMatrixID = I黑雾矩阵;
                } else {
                    topMatrixID = int.MaxValue;
                    foreach (RecipeProto recipe in recipes) {
                        if (recipe.preTech != null) {
                            int id = GetTechTopMatrixID(recipe.preTech);
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
            // LogDebug($"物品{item.name}({item.ID})归类到{LDB.items.Select(topMatrixID).name}({topMatrixID})");
        }
        SetCivilizationResourceMatrixStages();
    }

    private static void SetCivilizationResourceValues() {
        itemValue[IFE残片] = 1.0f;
        itemValue[IFE电磁解析数据] = 2.0f;
        itemValue[IFE能量解析数据] = 4.0f;
        itemValue[IFE结构解析数据] = 8.0f;
        itemValue[IFE信息解析数据] = 16.0f;
        itemValue[IFE引力解析数据] = 32.0f;
        itemValue[IFE宇宙解析数据] = 64.0f;
        itemValue[IFE记忆源点] = 256.0f;
    }

    private static void SetCivilizationResourceMatrixStages() {
        itemToMatrix[IFE残片] = I电磁矩阵;
        itemToMatrix[IFE电磁解析数据] = I电磁矩阵;
        itemToMatrix[IFE能量解析数据] = I能量矩阵;
        itemToMatrix[IFE结构解析数据] = I结构矩阵;
        itemToMatrix[IFE信息解析数据] = I信息矩阵;
        itemToMatrix[IFE引力解析数据] = I引力矩阵;
        itemToMatrix[IFE宇宙解析数据] = I宇宙矩阵;
        itemToMatrix[IFE记忆源点] = I引力矩阵;
    }

    public static int GetTechTopMatrixID(TechProto tech) {
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
}
