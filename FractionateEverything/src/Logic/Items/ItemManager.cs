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
    /// <summary>
    /// 注册 Frag/Memory、运输包裹、黑雾样本、通用原胚与四种定向塔原胚的名称和说明本地化键。
    /// </summary>
    public static void AddTranslations() {
        Register("残片", "Frag", "残片");
        Register("I残片",
            "Base Frag balance unit. Analysis towers output it or larger Frag packages; upload packages normalize into this balance.",
            "残片基础余额单位。解析塔会输出基础残片或更大面额包裹；包裹上传后会折算回该基础余额。");
        RegisterCurrencyPackageTranslations("残片", "Frag");

        Register("记忆源点", "Memory", "记忆源点");
        Register("I记忆源点",
            "Base Memory balance unit. Every 2000 Frag analyzed yields 1 Memory for formal protocol unlocks.",
            "记忆源点基础余额单位。每累计解析 2000 残片确定性产出 1 记忆源点，用于正式协议解锁。");
        RegisterCurrencyPackageTranslations("记忆源点", "Memory");

        Register("黑雾样本A", "Dark Fog Sample A", "黑雾样本A");
        Register("I黑雾样本A",
            "A low-density dark-fog sample. Analyze it for Frag scaled by ground and space cognition.",
            "低密度黑雾样本。送入解析塔可产出受地表与星域认知影响的残片。");
        Register("黑雾样本B", "Dark Fog Sample B", "黑雾样本B");
        Register("I黑雾样本B",
            "A mid-density dark-fog sample. Analyze it for Frag scaled by ground and space cognition.",
            "中密度黑雾样本。送入解析塔可产出受地表与星域认知影响的残片。");
        Register("黑雾样本C", "Dark Fog Sample C", "黑雾样本C");
        Register("I黑雾样本C",
            "A high-density dark-fog sample. Analyze it for Frag scaled by ground and space cognition.",
            "高密度黑雾样本。送入解析塔可产出受地表与星域认知影响的残片。");

        Register("通用原胚", "Common Tower Proto", "通用原胚");
        Register("I通用原胚",
            "An active proto whose lineage is not fixed. Cultivating it in a Cultivation Tower produces a random tower type.",
            "尚未固定谱系的活性原胚，可在培养塔中随机培养为任意一种分馏塔。");
        RegisterTowerProtoTranslations("培养塔原胚", "Cultivation Tower Proto", "a Cultivation Tower", "培养塔");
        RegisterTowerProtoTranslations("解析塔原胚", "Analysis Tower Proto", "an Analysis Tower", "解析塔");
        RegisterTowerProtoTranslations("复制塔原胚", "Replication Tower Proto", "a Replication Tower", "复制塔");
        RegisterTowerProtoTranslations("转化塔原胚", "Conversion Tower Proto", "a Conversion Tower", "转化塔");
    }

    /// <summary>
    /// 注册 10/100/1000 运输包裹的名称与说明。
    /// </summary>
    private static void RegisterCurrencyPackageTranslations(string baseName, string englishBaseName) {
        RegisterPackageTranslation(baseName, englishBaseName, 10);
        RegisterPackageTranslation(baseName, englishBaseName, 100);
        RegisterPackageTranslation(baseName, englishBaseName, 1000);
    }

    private static void RegisterPackageTranslation(string baseName, string englishBaseName, int faceValue) {
        string name = $"{baseName}×{faceValue}";
        Register(name, $"{englishBaseName} ×{faceValue}", name);
        Register($"I{name}",
            $"Upload-only {englishBaseName} package worth {faceValue}. Analysis towers create it; the data centre normalizes it into base {englishBaseName}.",
            $"面额为 {faceValue} 的{baseName}单向运输包裹，仅由解析塔产出；上传到数据中心后折算为基础{baseName}余额。");
    }

    /// <summary>
    /// 注册一种定向塔原胚的名称与说明，并保持名称键和 <c>I{name}</c> 说明键成对。
    /// </summary>
    /// <param name="name">中文名称键，同时作为中文显示名。</param>
    /// <param name="englishName">英文原胚显示名。</param>
    /// <param name="englishTowerName">插入英文培养结果说明的塔名短语。</param>
    /// <param name="towerName">插入中文谱系与培养结果说明的塔名。</param>
    private static void RegisterTowerProtoTranslations(string name, string englishName, string englishTowerName,
        string towerName) {
        Register(name, englishName, name);
        Register($"I{name}",
            $"A lineage-fixed proto. Cultivating it in a Cultivation Tower produces {englishTowerName}.",
            $"已经固定为{towerName}谱系的原胚，在培养塔中培养后会稳定产出{towerName}。");
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

        RegisterInternalItem(IFE残片, "残片", "I残片", 101, 9999,
            Color.gray, Color.black, "cpfragment");
        RegisterInternalItem(IFE记忆源点, "记忆源点", "I记忆源点", 102, 100,
            new Color(0.55f, 0.82f, 1f), new Color(0.08f, 0.18f, 0.34f), "memory-core");
        RegisterCurrencyPackage(IFE残片10, "残片×10", "I残片×10", 10,
            Color.gray, new Color(0.12f, 0.12f, 0.12f), "frag-pack-10");
        RegisterCurrencyPackage(IFE残片100, "残片×100", "I残片×100", 100,
            Color.gray, new Color(0.18f, 0.18f, 0.18f), "frag-pack-100");
        RegisterCurrencyPackage(IFE残片1000, "残片×1000", "I残片×1000", 1000,
            Color.gray, new Color(0.24f, 0.24f, 0.24f), "frag-pack-1000");
        RegisterCurrencyPackage(IFE记忆源点10, "记忆源点×10", "I记忆源点×10", 10,
            new Color(0.55f, 0.82f, 1f), new Color(0.10f, 0.20f, 0.36f), "memory-pack-10");
        RegisterCurrencyPackage(IFE记忆源点100, "记忆源点×100", "I记忆源点×100", 100,
            new Color(0.55f, 0.82f, 1f), new Color(0.12f, 0.24f, 0.40f), "memory-pack-100");
        RegisterCurrencyPackage(IFE记忆源点1000, "记忆源点×1000", "I记忆源点×1000", 1000,
            new Color(0.55f, 0.82f, 1f), new Color(0.14f, 0.28f, 0.46f), "memory-pack-1000");
        // 掉落区间在 InitEnemyDropTables 前写入 ItemProto；三段互不重叠且避开原版 0..0.300。
        RegisterDarkFogSample(IFE黑雾样本A, "黑雾样本A", "I黑雾样本A", 103,
            new Color(0.72f, 0.92f, 0.42f), new Color(0.10f, 0.18f, 0.04f), "dfs-sample-a",
            enemyDropLevel: 1, enemyDropRange: new Vector2(0.300f, 0.018f), enemyDropCount: 1.2f);
        RegisterDarkFogSample(IFE黑雾样本B, "黑雾样本B", "I黑雾样本B", 104,
            new Color(0.42f, 0.86f, 0.72f), new Color(0.04f, 0.16f, 0.14f), "dfs-sample-b",
            enemyDropLevel: 4, enemyDropRange: new Vector2(0.318f, 0.010f), enemyDropCount: 0.8f);
        RegisterDarkFogSample(IFE黑雾样本C, "黑雾样本C", "I黑雾样本C", 105,
            new Color(0.92f, 0.56f, 0.86f), new Color(0.18f, 0.06f, 0.16f), "dfs-sample-c",
            enemyDropLevel: 7, enemyDropRange: new Vector2(0.328f, 0.004f), enemyDropCount: 0.4f);
        RegisterTowerProto(IFE培养塔原胚, "培养塔原胚", "I培养塔原胚", 201,
            "Assets/fe/frac-proto-normal", CultivationTower.color, "cultivation-proto");
        RegisterTowerProto(IFE解析塔原胚, "解析塔原胚", "I解析塔原胚", 202,
            "Assets/fe/frac-proto-legendary", AnalysisTower.color, "analysis-proto");
        RegisterTowerProto(IFE复制塔原胚, "复制塔原胚", "I复制塔原胚", 203,
            "Assets/fe/frac-proto-uncommon", ReplicationTower.color, "replication-proto");
        RegisterTowerProto(IFE转化塔原胚, "转化塔原胚", "I转化塔原胚", 204,
            "Assets/fe/frac-proto-epic", ConversionTower.color, "conversion-proto");
        RegisterTowerProto(IFE通用原胚, "通用原胚", "I通用原胚", 205,
            "Assets/fe/frac-proto-directional", Color.red, "common-proto");
    }

    /// <summary>
    /// 注册分馏塔原胚物品原型。
    /// </summary>
    /// <param name="itemId">物品原型 ID。</param>
    /// <param name="name">本地化名称键。</param>
    /// <param name="description">本地化描述键。</param>
    /// <param name="gridOffset">分馏页内网格偏移。</param>
    /// <param name="iconPath">图标资源路径。</param>
    /// <param name="color">默认图标主色。</param>
    /// <param name="iconTag">图标资源标签。</param>
    /// <returns>已注册的原胚物品原型。</returns>
    private static ItemProto RegisterTowerProto(int itemId, string name, string description, int gridOffset,
        string iconPath, Color color, string iconTag) {
        ItemProto item = ProtoRegistry.RegisterItem(itemId, name, description, iconPath,
            tab分馏 * 1000 + gridOffset, 30, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(color, Color.gray));
        item.UnlockKey = -1;
        item.IconTag = iconTag;
        return item;
    }

    /// <summary>
    /// 注册 Frag/Memory 基础文明货币；类型为 Material，UnlockKey=-1。
    /// </summary>
    private static void RegisterInternalItem(int itemId, string name, string description, int gridOffset,
        int stackSize, Color iconColor, Color iconBackgroundColor, string iconTag) {
        ItemProto item = ProtoRegistry.RegisterItem(itemId, name, description,
            "Assets/fe/copy-essence", tab分馏 * 1000 + gridOffset, stackSize, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(iconColor, iconBackgroundColor));
        item.UnlockKey = -1;
        item.IconTag = iconTag;
    }

    /// <summary>
    /// 注册黑雾样本 A/B/C：EItemType.DarkFog、UnlockKey=-2、StackSize100，并配置地面掉落。
    /// EnemyDropMask=0x1000000、EnemyDropMaskRatio=1；须在 ItemProto.InitEnemyDropTables 前调用。
    /// </summary>
    private static void RegisterDarkFogSample(int itemId, string name, string description, int gridOffset,
        Color iconColor, Color iconBackgroundColor, string iconTag,
        int enemyDropLevel, Vector2 enemyDropRange, float enemyDropCount) {
        ItemProto item = ProtoRegistry.RegisterItem(itemId, name, description,
            "Assets/fe/copy-essence", tab分馏 * 1000 + gridOffset, 100, EItemType.DarkFog,
            ProtoRegistry.GetDefaultIconDesc(iconColor, iconBackgroundColor));
        item.UnlockKey = -2;
        item.IconTag = iconTag;
        item.EnemyDropLevel = enemyDropLevel;
        item.EnemyDropRange = enemyDropRange;
        item.EnemyDropCount = enemyDropCount;
        item.EnemyDropMask = 0x1000000;
        item.EnemyDropMaskRatio = 1f;
    }

    /// <summary>
    /// 注册仅由解析塔产出的单向货币包裹；GridIndex 无效，不可进入普通选择器。
    /// </summary>
    private static void RegisterCurrencyPackage(int itemId, string name, string description, int faceValue,
        Color iconColor, Color iconBackgroundColor, string iconTag) {
        ItemProto item = ProtoRegistry.RegisterItem(itemId, name, description,
            "Assets/fe/copy-essence", 0, 100, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(iconColor, iconBackgroundColor));
        item.UnlockKey = -1;
        item.IconTag = iconTag;
    }

    #endregion

    #region 计算物品价值，以及培养塔可接受物品范围

    public const float maxValue = float.MaxValue;
    /// <summary>
    /// 物品总价值（原材料价值 + 制作价值）
    /// </summary>
    public static readonly float[] itemValue = new float[12000];
    /// <summary>
    /// 培养塔可接收的所有物品 ID。
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
        itemValue[IFE培养塔] = modFractionatorValue;
        itemValue[IFE解析塔] = modFractionatorValue;
        itemValue[IFE复制塔] = modFractionatorValue;
        itemValue[IFE转化塔] = modFractionatorValue;
        itemValue[IFE通用原胚] = commonProtoValue;
        itemValue[IFE培养塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
        itemValue[IFE解析塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
        itemValue[IFE复制塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
        itemValue[IFE转化塔原胚] = 0.96f * modFractionatorValue + 0.04f * commonProtoValue;
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

        // 根据物品价值构建培养塔可接受物品列表。
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

    /// <summary>
    /// Frag 基础与 10/100/1000 包裹 ID。
    /// </summary>
    public static readonly int[] FragItemIds = [
        IFE残片,
        IFE残片10,
        IFE残片100,
        IFE残片1000,
    ];

    /// <summary>
    /// Memory 基础与 10/100/1000 包裹 ID。
    /// </summary>
    public static readonly int[] MemoryItemIds = [
        IFE记忆源点,
        IFE记忆源点10,
        IFE记忆源点100,
        IFE记忆源点1000,
    ];

    /// <summary>
    /// 黑雾样本 A/B/C。
    /// </summary>
    public static readonly int[] DarkFogSampleItemIds = [
        IFE黑雾样本A,
        IFE黑雾样本B,
        IFE黑雾样本C,
    ];

    /// <summary>
    /// 判断是否为 Frag 基础余额物品。
    /// </summary>
    public static bool IsFragBaseItem(int itemId) => itemId == IFE残片;

    /// <summary>
    /// 判断是否为 Memory 基础余额物品。
    /// </summary>
    public static bool IsMemoryBaseItem(int itemId) => itemId == IFE记忆源点;

    /// <summary>
    /// 判断是否为 Frag/Memory 任一运输面额实体。
    /// </summary>
    public static bool IsCivilizationCurrencyItem(int itemId) =>
        IsFragBaseItem(itemId) || IsMemoryBaseItem(itemId) || IsCurrencyPackageItem(itemId);

    /// <summary>
    /// 判断是否为仅上传的 10/100/1000 货币包裹。
    /// </summary>
    public static bool IsCurrencyPackageItem(int itemId) =>
        itemId is IFE残片10 or IFE残片100 or IFE残片1000
            or IFE记忆源点10 or IFE记忆源点100 or IFE记忆源点1000;

    /// <summary>
    /// 判断是否为黑雾样本 A/B/C。
    /// </summary>
    public static bool IsDarkFogSampleItem(int itemId) =>
        itemId is IFE黑雾样本A or IFE黑雾样本B or IFE黑雾样本C;

    /// <summary>
    /// 判断是否为文明货币、包裹或样本；这些物品不进入通用可估值候选。
    /// </summary>
    public static bool IsCivilizationResourceItem(int itemId) =>
        IsCivilizationCurrencyItem(itemId) || IsDarkFogSampleItem(itemId);

    /// <summary>
    /// 读取货币包裹面额；非包裹返回 0。
    /// </summary>
    public static int GetCurrencyPackageFaceValue(int itemId) => itemId switch {
        IFE残片10 or IFE记忆源点10 => 10,
        IFE残片100 or IFE记忆源点100 => 100,
        IFE残片1000 or IFE记忆源点1000 => 1000,
        _ => 0,
    };

    /// <summary>
    /// 将货币包裹解析为基础物品与面额；基础物品自身返回 faceValue=1。
    /// </summary>
    public static bool TryGetCurrencyNormalization(int itemId, out int baseItemId, out int faceValue) {
        switch (itemId) {
            case IFE残片:
                baseItemId = IFE残片;
                faceValue = 1;
                return true;
            case IFE残片10:
                baseItemId = IFE残片;
                faceValue = 10;
                return true;
            case IFE残片100:
                baseItemId = IFE残片;
                faceValue = 100;
                return true;
            case IFE残片1000:
                baseItemId = IFE残片;
                faceValue = 1000;
                return true;
            case IFE记忆源点:
                baseItemId = IFE记忆源点;
                faceValue = 1;
                return true;
            case IFE记忆源点10:
                baseItemId = IFE记忆源点;
                faceValue = 10;
                return true;
            case IFE记忆源点100:
                baseItemId = IFE记忆源点;
                faceValue = 100;
                return true;
            case IFE记忆源点1000:
                baseItemId = IFE记忆源点;
                faceValue = 1000;
                return true;
            default:
                baseItemId = 0;
                faceValue = 0;
                return false;
        }
    }

    /// <summary>
    /// 获取主线矩阵阶段索引。黑雾矩阵不是主线矩阵，返回 -1 供调用方显式分支处理。
    /// </summary>
    public static int GetMatrixStageIndex(int matrixId) {
        int currentId = matrixId;
        for (int depth = 0; depth < 16; depth++) {
            int stage = currentId switch {
                I电磁矩阵 => 0,
                I能量矩阵 => 1,
                I结构矩阵 => 2,
                I信息矩阵 => 3,
                I引力矩阵 => 4,
                I宇宙矩阵 => 5,
                I黑雾矩阵 => -1,
                _ => int.MinValue,
            };
            if (stage != int.MinValue) {
                return stage;
            }
            if (currentId <= 0 || currentId >= itemToMatrix.Length) {
                return 0;
            }
            int nextId = itemToMatrix[currentId];
            if (nextId <= 0 || nextId == currentId) {
                return 0;
            }
            currentId = nextId;
        }
        return 0;
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

    public static void ClassifyItemsToMatrix() {
        //       物品状态                         missingTech    preTech
        //         正常                              false        tech
        //黑雾特有材料（UnlockKey=-2），或资源        false        null
        // 找不到主制作配方的对应科技                 true         null
        foreach (var item in LDB.items.dataArray) {
            if (item == null || item.ID <= 0 || item.ID >= itemToMatrix.Length) {
                continue;
            }
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
        }
        SetCivilizationResourceMatrixStages();
    }

    /// <summary>
    /// 文明货币、包裹与样本保持不可估值，避免进入培养/复制/转化候选。
    /// </summary>
    private static void SetCivilizationResourceValues() {
        foreach (int itemId in FragItemIds) {
            itemValue[itemId] = maxValue;
        }
        foreach (int itemId in MemoryItemIds) {
            itemValue[itemId] = maxValue;
        }
        foreach (int itemId in DarkFogSampleItemIds) {
            itemValue[itemId] = maxValue;
        }
    }

    /// <summary>
    /// 为文明资源指定矩阵分层，确保解析配方能够注册。
    /// </summary>
    private static void SetCivilizationResourceMatrixStages() {
        itemToMatrix[IFE残片] = I电磁矩阵;
        itemToMatrix[IFE残片10] = I电磁矩阵;
        itemToMatrix[IFE残片100] = I电磁矩阵;
        itemToMatrix[IFE残片1000] = I电磁矩阵;
        itemToMatrix[IFE记忆源点] = I电磁矩阵;
        itemToMatrix[IFE记忆源点10] = I电磁矩阵;
        itemToMatrix[IFE记忆源点100] = I电磁矩阵;
        itemToMatrix[IFE记忆源点1000] = I电磁矩阵;
        itemToMatrix[IFE黑雾样本A] = I黑雾矩阵;
        itemToMatrix[IFE黑雾样本B] = I黑雾矩阵;
        itemToMatrix[IFE黑雾样本C] = I黑雾矩阵;
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
