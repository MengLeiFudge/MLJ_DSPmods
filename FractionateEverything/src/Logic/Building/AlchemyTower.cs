using FE.Logic.Manager;
using FE.Logic.Recipe;
using System.Collections.Generic;
using UnityEngine;
using static FE.Utils.ProtoID;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;

namespace FE.Logic.Building;

/// <summary>
/// 点金塔
/// </summary>
public static class AlchemyTower {
    /// <summary>
    /// 创建点金塔
    /// </summary>
    /// <returns>创建的点金塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateFractionator(
            "点金塔", RFE点金塔, IFE点金塔, MFE点金塔,
            [IFE分馏原胚定向], [3], [1],
            3106, new(1.0f, 0.7019f, 0.4f), 0, 0.75f, TFE点金塔
        );
    }

    public static void InternalUpdate(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result) {
        if (power < 0.1) {
            __result = 0;
            return;
        }
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
        AlchemyRecipe recipe = GetRecipe<AlchemyRecipe>(ERecipe.Alchemy, fluidId);
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
                if (!fracForever && EnableFracForever) {
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

                Dictionary<int, int> outputs;
                if (recipe.RecipeType == ERecipe.PointAggregator) {
                    float successRatePlus = 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                    outputs = recipe.GetOutputs(ref __instance.seed, successRatePlus);
                    __instance.fractionSuccess = outputs.Count > 0;
                    __instance.fluidInputInc -= 10;
                } else {
                    float successRatePlus = 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                    outputs = recipe.GetOutputs(ref __instance.seed, successRatePlus);
                    __instance.fractionSuccess = outputs.Count > 0;
                    __instance.fluidInputInc -= fluidInputIncAvg;
                }
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
                    } else {
                        foreach (KeyValuePair<int, int> p in outputs) {
                            int itemID = p.Key;
                            int itemCount = p.Value;
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
                    }
                }
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
                        if (!EnableFluidOutputStack) {
                            //未研究流动输出集装科技，根据传送带最大速率每帧判定2-4次
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount > 0; i++) {
                                if (recipe.RecipeType == ERecipe.PointAggregator
                                    && fluidOutputIncAvg < 4
                                    && __instance.fluidOutputCount > 1) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 4 ? 4 : 0;
                                }
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
                                //优化输出，只会输出4增产点数或0增产点数
                                if (recipe.RecipeType == ERecipe.PointAggregator && fluidOutputIncAvg < 4) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 16 ? 4 : 0;
                                }
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
                    //可输入任何物品，无配方会直接流出
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;


                        __instance.fluidId = needId;
                        recipe = GetRecipe<AlchemyRecipe>(ERecipe.Alchemy, needId);
                        if (recipe.RecipeType == ERecipe.PointAggregator) {
                            __instance.productId = needId;
                        } else {
                            __instance.productId = recipe.OutputMain[0].OutputID;
                        }
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
                        //原版传送带最大速率为30，如果每次尝试放1个物品到传送带上，需要每帧判定2次（30速*4堆叠/60帧）
                        //创世传送带最大速率为60，如果每次尝试放1个物品到传送带上，需要每帧判定4次（60速*4堆叠/60帧）
                        //每帧至少尝试一次，尝试就会lock buffer进而影响效率，所以这里尝试减少输出的次数
                        int fluidOutputIncAvg = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        if (!EnableFluidOutputStack) {
                            //未研究流动输出集装科技，根据传送带最大速率每帧判定2-4次
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount > 0; i++) {
                                if (recipe.RecipeType == ERecipe.PointAggregator
                                    && fluidOutputIncAvg < 4
                                    && __instance.fluidOutputCount > 1) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 4 ? 4 : 0;
                                }
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
                                //优化输出，只会输出4增产点数或0增产点数
                                if (recipe.RecipeType == ERecipe.PointAggregator && fluidOutputIncAvg < 4) {
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 16 ? 4 : 0;
                                }
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
            } else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
                if (fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(__instance.belt2, fluidId, null, out stack, out inc) > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                    }
                } else {
                    //可输入任何物品，无配方会直接流出
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;


                        __instance.fluidId = needId;
                        recipe = GetRecipe<AlchemyRecipe>(ERecipe.Alchemy, needId);
                        if (recipe.RecipeType == ERecipe.PointAggregator) {
                            __instance.productId = needId;
                        } else {
                            __instance.productId = recipe.OutputMain[0].OutputID;
                        }
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
                    if (__instance.productOutputCount >= MaxProductOutputStack) {
                        //产物达到最大堆叠数目，直接尝试输出
                        mainProductOutput = true;
                        if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)MaxProductOutputStack,
                                (byte)(recipe.RecipeType == ERecipe.PointAggregator
                                    ? 10 * MaxProductOutputStack
                                    : 0))) {
                            break;
                        }
                        __instance.productOutputCount -= MaxProductOutputStack;
                    } else if (__instance.productOutputCount > 0 && __instance.fluidInputCount == 0) {
                        //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                        if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)__instance.productOutputCount,
                                (byte)(recipe.RecipeType == ERecipe.PointAggregator
                                    ? 10 * __instance.productOutputCount
                                    : 0))) {
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
                            if (otherProductOutput[outputID] >= MaxProductOutputStack) {
                                if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, outputID,
                                        (byte)MaxProductOutputStack,
                                        (byte)(recipe.RecipeType == ERecipe.PointAggregator
                                            ? 10 * MaxProductOutputStack
                                            : 0))) {
                                    break;
                                }
                                if (otherProductOutput[outputID] == MaxProductOutputStack) {
                                    otherProductOutput.Remove(outputID);
                                } else {
                                    otherProductOutput[outputID] -= MaxProductOutputStack;
                                }
                            } else if (otherProductOutput[outputID] > 0 && __instance.fluidInputCount == 0) {
                                if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, outputID,
                                        (byte)otherProductOutput[outputID],
                                        (byte)(recipe.RecipeType == ERecipe.PointAggregator
                                            ? 10 * otherProductOutput[outputID]
                                            : 0))) {
                                    break;
                                }
                                otherProductOutput.Remove(outputID);
                            } else {
                                break;
                            }
                        }
                    }
                }
            } else {
                //正面作为输入
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
}
