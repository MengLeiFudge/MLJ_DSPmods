using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Fractionation.Recipes;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Items;

/// <summary>
/// FE 物品原型、翻译和物品价值注册入口。
/// </summary>
public static class ItemManager {
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

        RegisterFrag();
        RegisterInternalItem(IFE记忆源点, "记忆源点", "I记忆源点", 109, 100,
            new Color(0.55f, 0.82f, 1f), new Color(0.08f, 0.18f, 0.34f), "memory-core");
        RegisterTowerProto(IFE通用原胚, "通用原胚", "I通用原胚", 110,
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

    private static void RegisterFrag() {
        ItemProto item = ProtoRegistry.RegisterItem(IFE残片, "残片", "I残片",
            "Assets/fe/copy-essence", tab分馏 * 1000 + 108, 100, EItemType.DarkFog,
            ProtoRegistry.GetDefaultIconDesc(Color.gray, Color.black));
        item.UnlockKey = -2;
        item.IconTag = "cpfragment";
    }

    /// <summary>
    /// 注册 Memory 内部资源；类型为 Material，UnlockKey=-1。
    /// </summary>
    private static void RegisterInternalItem(int itemId, string name, string description, int gridOffset,
        int stackSize, Color iconColor, Color iconBackgroundColor, string iconTag) {
        ItemProto item = ProtoRegistry.RegisterItem(itemId, name, description,
            "Assets/fe/copy-essence", tab分馏 * 1000 + gridOffset, stackSize, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(iconColor, iconBackgroundColor));
        item.UnlockKey = -1;
        item.IconTag = iconTag;
    }

    #endregion

    #region 计算物品价值与基础塔正面上传候选

    public const float maxValue = float.MaxValue;
    /// <summary>
    /// 物品总价值（原材料价值 + 制作价值）
    /// </summary>
    public static readonly float[] itemValue = new float[12000];
    /// <summary>
    /// 基础塔正面上传可接收的物品，按物品 ID 索引；热路径每帧查询，用数组代替线性查找。
    /// </summary>
    private static readonly bool[] uploadableItems = new bool[12000];

    /// <summary>判断物品能否由基础塔正面上传：已注册的实体物品，且不是历史面额包裹。</summary>
    /// <param name="itemId">带上货物的物品 ID。</param>
    /// <returns>可上传时为 true；越界 ID 为 false。</returns>
    public static bool IsUploadableItem(int itemId) => itemId > 0 && itemId < 12000 && uploadableItems[itemId];

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
        itemValue[IFE基础矿物塔] = modFractionatorValue;
        itemValue[IFE基础解析塔] = modFractionatorValue;
        itemValue[IFE高级矿物塔] = modFractionatorValue;
        itemValue[IFE高级交互塔] = modFractionatorValue;
        itemValue[IFE高级转化塔] = modFractionatorValue;
        itemValue[IFE通用原胚] = commonProtoValue;
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
            !AdvancedRecipeCatalog.IsAdvancedProto(r.ID)
            && !r.Items.Contains(IMS多功能集成组件)
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

        // 基础塔正面上传可接收全部已注册实体物品；上传路由负责容量和历史包裹规范化。
        Array.Clear(uploadableItems, 0, uploadableItems.Length);
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item != null && item.ID > 0 && item.ID < 12000 && !IsCurrencyPackageItem(item.ID)) {
                uploadableItems[item.ID] = true;
            }
        }
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
    /// 判断是否为 2.x 存档可能遗留的 Frag/Memory 面额包裹。
    /// </summary>
    public static bool IsCurrencyPackageItem(int itemId) =>
        itemId is IFE残片10 or IFE残片100 or IFE残片1000
            or IFE记忆源点10 or IFE记忆源点100 or IFE记忆源点1000;

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
    /// Frag 与 Memory 不参与普通物品价值推导。
    /// </summary>
    private static void SetCivilizationResourceValues() {
        itemValue[IFE残片] = maxValue;
        itemValue[IFE记忆源点] = maxValue;
    }

    /// <summary>
    /// 为 Frag 与 Memory 指定基础矩阵阶段。
    /// </summary>
    private static void SetCivilizationResourceMatrixStages() {
        itemToMatrix[IFE残片] = I电磁矩阵;
        itemToMatrix[IFE记忆源点] = I电磁矩阵;
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
