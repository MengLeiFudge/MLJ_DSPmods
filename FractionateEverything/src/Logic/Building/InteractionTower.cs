using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using BuildBarTool;
using CommonAPI.Systems;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.ItemManager;

namespace FE.Logic.Building;

/// <summary>
/// 交互塔
/// </summary>
public static class InteractionTower {
    public static void AddTranslations() {
        Register("交互塔", "Interaction Tower");
        Register("I交互塔",
            "-",
            $"将分馏原胚转换为各种分馏建筑。{"正面连接口作为输入时".WithColor(Orange)}，物品将以数据形式传递到主脑，这些物品可以进行兑换、抽奖等操作。");
    }

    public static ConfigEntry<bool> EnableFluidOutputStackEntry;
    public static ConfigEntry<int> MaxProductOutputStackEntry;
    public static ConfigEntry<bool> EnableFracForeverEntry;

    public static void LoadConfig(ConfigFile configFile) {
        string className = "InteractionTower";
        EnableFluidOutputStackEntry = configFile.Bind(className, "Enable Fluid Output Stack", false);
        MaxProductOutputStackEntry = configFile.Bind(className, "Max Product Output Stack", 1);
        EnableFracForeverEntry = configFile.Bind(className, "Enable Frac Forever", false);
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.8f, 0.3f, 0.6f);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE交互塔, "交互塔", "I交互塔",
            "Assets/fe/interaction-tower", tab分馏 * 1000 + 101, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE交互塔,
            ERecipeType.Assemble, 60, [IFE分馏原胚定向], [1], [IFE交互塔], [1],
            "I交互塔", TFE物品交互);
        model = ProtoRegistry.RegisterModel(MFE交互塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(5, item.GridIndex % 10, true);
    }

    public static void SetMaterials() {
        Material m_main = new(model.prefabDesc.lodMaterials[0][0]) { color = color };
        Material m_black = model.prefabDesc.lodMaterials[0][1];
        Material m_glass = model.prefabDesc.lodMaterials[0][2];
        Material m_glass1 = model.prefabDesc.lodMaterials[0][3];
        Material m_lod = new(model.prefabDesc.lodMaterials[1][0]) { color = color };
        Material m_lod2 = new(model.prefabDesc.lodMaterials[2][0]) { color = color };
        model.prefabDesc.materials = [m_main, m_black];
        model.prefabDesc.lodMaterials = [
            [m_main, m_black, m_glass, m_glass1],
            [m_lod, m_black, m_glass, m_glass1],
            [m_lod2, m_black, m_glass, m_glass1],
            null,
        ];
        SetHpAndEnergy(1);
    }

    public static void SetHpAndEnergy(int level) {
        model.HpMax = LDB.models.Select(M分馏塔).HpMax + level * 50;
        double energyRatio = 3.0 * (1 - level * 0.1);
        model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
    }

    public static void InternalUpdate(ref FractionatorComponent __instance, PlanetFactory factory,
        float power, SignData[] signPool, int[] productRegister, int[] consumeRegister, ref uint __result) {
        if (power < 0.1) {
            __result = 0;
            return;
        }
        int buildingID = factory.entityPool[__instance.entityId].protoId;
        ItemProto building = LDB.items.Select(buildingID);
        int fluidId = __instance.fluidId;
        int mainProductId = __instance.productId;
        float fluidInputCountPerCargo = 1.0f;
        if (__instance.fluidInputCount == 0)
            __instance.fluidInputCargoCount = 0f;
        else
            fluidInputCountPerCargo = __instance.fluidInputCargoCount > 0.0001
                ? __instance.fluidInputCount / __instance.fluidInputCargoCount
                : 4f;
        Dictionary<int, int> otherProductOutput = __instance.otherProductOutput(factory);
        BuildingTrainRecipe recipe = GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, fluidId);
        if (__instance.fluidInputCount > 0
            && __instance.productOutputCount < __instance.productOutputMax
            && __instance.fluidOutputCount < __instance.fluidOutputMax) {
            __instance.progress += (int)(power
                                         * (500.0 / 3.0)
                                         * (__instance.fluidInputCargoCount < MaxBeltSpeed
                                             ? __instance.fluidInputCargoCount
                                             : MaxBeltSpeed)
                                         * fluidInputCountPerCargo
                                         + 0.75);
            if (__instance.progress > 100000)
                __instance.progress = 100000;
            bool fracForever = false;
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                if (!fracForever && building.EnableFracForever()) {
                    //如果分馏永动已研究，并且任何一个产物缓存达到上限的一半，则不会分馏出物品
                    if (__instance.productOutputCount >= __instance.productOutputMax / 2) {
                        fracForever = true;
                    }
                    foreach (var p in otherProductOutput) {
                        if (p.Value >= __instance.productOutputMax / 2) {
                            fracForever = true;
                            break;
                        }
                    }
                }
                if (fracForever) {
                    continue;
                }
                int fluidInputIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;
                if (!__instance.incUsed)
                    __instance.incUsed = fluidInputIncAvg > 0;

                if (recipe == null) {
                    __instance.fluidInputInc -= fluidInputIncAvg;
                    __instance.fractionSuccess = false;
                    __instance.fluidInputCount--;
                    __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                    if (__instance.fluidInputCargoCount < 0f) {
                        __instance.fluidInputCargoCount = 0f;
                    }
                    __instance.fluidOutputCount++;
                    __instance.fluidOutputTotal++;
                    __instance.fluidOutputInc += fluidInputIncAvg;
                    // LogDebug($"配方为空，当前流动输入{__instance.fluidInputCount}个, 当前流动输出{__instance.fluidOutputCount}个, "
                    //          + $"当前产物输出{__instance.productOutputCount}个");
                    continue;
                }

                float successRatePlus = 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                Dictionary<int, int> outputs = recipe.GetOutputs(ref __instance.seed, successRatePlus);
                __instance.fluidInputInc -= fluidInputIncAvg;
                __instance.fractionSuccess = outputs != null && outputs.Count > 0;
                __instance.fluidInputCount--;
                __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                if (__instance.fluidInputCargoCount < 0f) {
                    __instance.fluidInputCargoCount = 0f;
                }
                lock (consumeRegister) {
                    consumeRegister[fluidId]++;
                }
                if (outputs != null) {
                    if (outputs.Count == 0) {
                        __instance.fluidOutputCount++;
                        __instance.fluidOutputTotal++;
                        __instance.fluidOutputInc += fluidInputIncAvg;
                        // LogDebug($"原料不变，当前流动输入{__instance.fluidInputCount}个, 当前流动输出{__instance.fluidOutputCount}个, "
                        //          + $"当前产物输出{__instance.productOutputCount}个");
                    } else {
                        foreach (KeyValuePair<int, int> p in outputs) {
                            int itemID = p.Key;
                            int itemCount = p.Value;
                            // LogDebug($"转化得到产物ID{itemID}，数目{itemCount}");
                            lock (productRegister) {
                                productRegister[itemID] += itemCount;
                            }
                            if (itemID == mainProductId) {
                                __instance.productOutputCount++;
                                __instance.productOutputTotal++;
                            } else {
                                if (otherProductOutput.ContainsKey(itemID)) {
                                    otherProductOutput[itemID] += itemCount;
                                } else {
                                    otherProductOutput[itemID] = itemCount;
                                }
                            }
                        }
                        // LogDebug($"原料转化，当前流动输入{__instance.fluidInputCount}个, 当前流动输出{__instance.fluidOutputCount}个, "
                        //          + $"当前产物输出{__instance.productOutputCount}个");
                    }
                }
                // LogDebug($"原料损毁，当前流动输入{__instance.fluidInputCount}个, 当前流动输出{__instance.fluidOutputCount}个, "
                //          + $"当前产物输出{__instance.productOutputCount}个");
            }
        } else {
            __instance.fractionSuccess = false;
        }
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack;
        byte inc;
        if (__instance.belt1 > 0) {
            if (__instance.isOutput1) {
                if (__instance.fluidOutputCount > 0) {
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                    if (cargoPath != null) {
                        //原版传送带最大速率为30，如果每次尝试放1个物品到传送带上，需要每帧判定2次（30速*4堆叠/60帧）
                        //创世传送带最大速率为60，如果每次尝试放1个物品到传送带上，需要每帧判定4次（60速*4堆叠/60帧）
                        //每帧至少尝试一次，尝试就会lock buffer进而影响效率，所以这里尝试减少输出的次数
                        int fluidOutputIncAvg = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        if (!building.EnableFluidOutputStack()) {
                            //未研究流动输出集装科技，根据传送带最大速率每帧判定2-4次
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount > 0; i++) {
                                if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                        (byte)fluidOutputIncAvg)) {
                                    break;
                                }
                                __instance.fluidOutputCount--;
                                __instance.fluidOutputInc -= fluidOutputIncAvg;
                            }
                        } else {
                            //已研究流动输出集装科技
                            if (__instance.fluidOutputCount >= 4) {
                                //超过4个，则输出4个
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, 4, (byte)(fluidOutputIncAvg * 4))) {
                                    __instance.fluidOutputCount -= 4;
                                    __instance.fluidOutputInc -= fluidOutputIncAvg * 4;
                                }
                            } else if (__instance.fluidInputCount == 0) {
                                //未超过4个且输入为空，剩几个输出几个
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, (byte)__instance.fluidOutputCount,
                                        (byte)__instance.fluidOutputInc)) {
                                    __instance.fluidOutputCount = 0;
                                    __instance.fluidOutputInc = 0;
                                }
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
                if (fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(__instance.belt1, fluidId, null, out stack, out inc) > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                        __instance.fluidId = needId;
                        recipe = GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, needId);
                        __instance.productId = recipe == null ? __instance.fluidId : recipe.OutputMain[0].OutputID;
                        __instance.produceProb = 0.01f;
                        signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                        signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                    }
                }
            }
        }
        if (__instance.belt2 > 0) {
            if (__instance.isOutput2) {
                if (__instance.fluidOutputCount > 0) {
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                    if (cargoPath != null) {
                        int fluidOutputIncAvg = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        if (!building.EnableFluidOutputStack()) {
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount > 0; i++) {
                                if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                        (byte)fluidOutputIncAvg)) {
                                    break;
                                }
                                __instance.fluidOutputCount--;
                                __instance.fluidOutputInc -= fluidOutputIncAvg;
                            }
                        } else {
                            if (__instance.fluidOutputCount >= 4) {
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, 4, (byte)(fluidOutputIncAvg * 4))) {
                                    __instance.fluidOutputCount -= 4;
                                    __instance.fluidOutputInc -= fluidOutputIncAvg * 4;
                                }
                            } else if (__instance.fluidInputCount == 0) {
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        4, (byte)__instance.fluidOutputCount,
                                        (byte)__instance.fluidOutputInc)) {
                                    __instance.fluidOutputCount = 0;
                                    __instance.fluidOutputInc = 0;
                                }
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
                if (fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(__instance.belt2, fluidId, null, out stack, out inc) > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                        __instance.fluidId = needId;
                        recipe = GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, needId);
                        __instance.productId = recipe == null ? __instance.fluidId : recipe.OutputMain[0].OutputID;
                        __instance.produceProb = 0.01f;
                        signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                        signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                    }
                }
            }
        }
        if (__instance.belt0 > 0) {
            if (__instance.isOutput0) {
                //指示是否已输出主产物。如果主产物成功输出，则不判定副产物是否输出
                bool mainProductOutput = false;
                //输出主产物
                for (int i = 0; i < MaxOutputTimes; i++) {
                    //只有产物数目到达堆叠要求，或者没有正在处理的物品，才输出，且一次输出最大堆叠个数的物品
                    if (__instance.productOutputCount >= building.MaxProductOutputStack()) {
                        //产物达到最大堆叠数目，直接尝试输出
                        mainProductOutput = true;
                        if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)building.MaxProductOutputStack(), 0)) {
                            break;
                        }
                        __instance.productOutputCount -= building.MaxProductOutputStack();
                    } else if (__instance.productOutputCount > 0 && __instance.fluidInputCount == 0) {
                        //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                        if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)__instance.productOutputCount, 0)) {
                            break;
                        }
                        __instance.productOutputCount = 0;
                    } else {
                        break;
                    }
                }
                //输出副产物
                if (!mainProductOutput) {
                    //每个物品都要尝试输出
                    List<int> keys = [..otherProductOutput.Keys];
                    foreach (int outputID in keys) {
                        if (otherProductOutput[outputID] == 0) {
                            continue;
                        }
                        for (int j = 0; j < MaxOutputTimes; j++) {
                            if (otherProductOutput[outputID] >= building.MaxProductOutputStack()) {
                                if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, outputID,
                                        (byte)building.MaxProductOutputStack(), 0)) {
                                    break;
                                }
                                otherProductOutput[outputID] -= building.MaxProductOutputStack();
                            } else if (otherProductOutput[outputID] > 0 && __instance.fluidInputCount == 0) {
                                if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, outputID,
                                        (byte)otherProductOutput[outputID], 0)) {
                                    break;
                                }
                                otherProductOutput[outputID] = 0;
                            } else {
                                break;
                            }
                        }
                    }
                    //移除所有数目为0的缓存物品
                    foreach (int outputID in keys) {
                        if (otherProductOutput[outputID] == 0) {
                            otherProductOutput.Remove(outputID);
                        }
                    }
                }
            } else {
                //正面作为输入，数据传到数据中心
                int itemId = cargoTraffic.TryPickItemAtRear(__instance.belt0, 0, null, out stack, out _);
                AddItem(itemId, stack);
            }
        }

        // 如果缓存区全部清空，重置输入id
        if (__instance.fluidInputCount == 0
            && __instance.fluidOutputCount == 0
            && __instance.productOutputCount == 0
            && otherProductOutput.Count == 0)
            __instance.fluidId = 0;

        // 更新工作状态
        __instance.isWorking = __instance.fluidInputCount > 0
                               && __instance.productOutputCount < __instance.productOutputMax
                               && __instance.fluidOutputCount < __instance.fluidOutputMax;

        __result = !__instance.isWorking ? 0U : 1U;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        EnableFluidOutputStackEntry.Value = r.ReadBoolean();
        MaxProductOutputStackEntry.Value = Math.Min(r.ReadInt32(), 4);
        EnableFracForeverEntry.Value = r.ReadBoolean();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(EnableFluidOutputStackEntry.Value);
        w.Write(MaxProductOutputStackEntry.Value);
        w.Write(EnableFracForeverEntry.Value);
    }

    public static void IntoOtherSave() {
        EnableFluidOutputStackEntry.Value = false;
        MaxProductOutputStackEntry.Value = 1;
        EnableFracForeverEntry.Value = false;
    }

    #endregion
}
