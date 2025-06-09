using FE.Logic.Manager;
using System.Linq;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 分解塔
/// </summary>
public static class DeconstructionTower {
    /// <summary>
    /// 创建分解塔
    /// </summary>
    /// <returns>创建的分解塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateAndPreAddNewFractionator(
            "分解塔", RFE分解塔, IFE分解塔, MFE分解塔,
            [IFE分馏原胚定向], [3], [1],
            3107, new(0.4f, 1.0f, 0.5f), 0, 0.9f, TFE分解塔
        );
    }

    public static void InternalUpdate(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result) {
        // 没电就不工作
        if (power < 0.1) {
            __result = 0;
            return;
        }

        // 计算输入缓存区物品的平均堆叠
        double itemStackAvg = 1.0;
        if (__instance.fluidInputCount == 0)
            __instance.fluidInputCargoCount = 0.0f;
        else
            itemStackAvg = __instance.fluidInputCargoCount > 0.0001
                ? __instance.fluidInputCount / (double)__instance.fluidInputCargoCount
                : 4.0;

        // 只有输入缓存大于0且输出不堵的情况下才进行处理
        if (__instance.fluidInputCount > 0
            && __instance.productOutputCount < __instance.productOutputMax
            && __instance.fluidOutputCount < __instance.fluidOutputMax) {

            // 分解塔的处理速度
            __instance.progress += (int)(power
                                         * (600.0 / 3.0)
                                         * (__instance.fluidInputCargoCount < 30.0
                                             ? __instance.fluidInputCargoCount
                                             : 30.0)
                                         * itemStackAvg
                                         + 0.75);

            // 限制最大进度
            if (__instance.progress > 100000)
                __instance.progress = 100000;

            // 处理每次分解
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                // 计算平均每个物品携带的增产点数
                int itemIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;

                if (!__instance.incUsed)
                    __instance.incUsed = itemIncAvg > 0;

                // 随机数生成逻辑
                __instance.seed = (uint)((ulong)(__instance.seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue)
                                  - 1U;

                // 分解塔主产物概率
                double baseProb = __instance.produceProb;
                double incFactor = 1.0 + Cargo.accTableMilli[itemIncAvg < 10 ? itemIncAvg : 10];
                __instance.fractionSuccess = __instance.seed / 2147483646.0 < baseProb * incFactor;

                // 分解塔分解成功
                if (__instance.fractionSuccess) {
                    ++__instance.productOutputCount;
                    ++__instance.productOutputTotal;
                    lock (productRegister)
                        ++productRegister[__instance.productId];
                    lock (consumeRegister)
                        ++consumeRegister[__instance.fluidId];

                    // 获取扩展产物字典
                    var productExpansion = __instance.productExpansion(factory);

                    // 沙土概率 - 20%概率获得沙土
                    uint sandRandom =
                        (uint)((ulong)(__instance.seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
                    if (sandRandom / 2147483646.0 < 0.2) {
                        // 石矿ID为1004
                        int stoneId = 1004;
                        if (!productExpansion.ContainsKey(stoneId)) {
                            productExpansion[stoneId] = 0;
                        }
                        productExpansion[stoneId]++;
                    }
                } else {
                    // 分解失败，80%概率回收原材料
                    if (__instance.seed % 5 != 0) {
                        ++__instance.fluidOutputCount;
                        ++__instance.fluidOutputTotal;
                        __instance.fluidOutputInc += itemIncAvg;
                    }
                }

                // 消耗输入物品
                --__instance.fluidInputCount;
                __instance.fluidInputInc -= itemIncAvg;
                __instance.fluidInputCargoCount -= (float)(1.0 / itemStackAvg);
                if (__instance.fluidInputCargoCount < 0.0)
                    __instance.fluidInputCargoCount = 0.0f;
            }
        } else {
            __instance.fractionSuccess = false;
        }

        // 处理传送带交互
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack;
        byte inc1;

        // 处理belt1（左侧接口）
        if (__instance.belt1 > 0) {
            if (__instance.isOutput1) {
                // 左侧作为输出处理
                if (__instance.fluidOutputCount > 0) {
                    int inc2 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                    if (cargoPath != null
                        && cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                            Mathf.CeilToInt((float)(itemStackAvg - 0.1)), (byte)1, (byte)inc2)) {
                        --__instance.fluidOutputCount;
                        __instance.fluidOutputInc -= inc2;
                        // 尝试输出第二个物品
                        if (__instance.fluidOutputCount > 0) {
                            int inc3 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                            if (cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                                    Mathf.CeilToInt((float)(itemStackAvg - 0.1)), (byte)1, (byte)inc3)) {
                                --__instance.fluidOutputCount;
                                __instance.fluidOutputInc -= inc3;
                            }
                        }
                    }
                } else {
                    // 当主输出流没有物品时，尝试输出扩展产物（如沙土）
                    var productExpansion = __instance.productExpansion(factory);
                    if (productExpansion.Count > 0) {
                        foreach (var kvp in productExpansion.ToArray()) {
                            if (kvp.Value > 0) {
                                int itemId = kvp.Key;
                                CargoPath cargoPath =
                                    cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                                if (cargoPath != null
                                    && cargoPath.TryUpdateItemAtHeadAndFillBlank(itemId, 1, (byte)1, (byte)0)) {
                                    productExpansion[itemId]--;
                                    break;
                                }
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < (double)__instance.fluidInputMax) {
                // 作为输入处理
                if (__instance.fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(__instance.belt1, __instance.fluidId, null, out stack, out inc1)
                        > 0) {
                        __instance.fluidInputCount += (int)stack;
                        __instance.fluidInputInc += (int)inc1;
                        ++__instance.fluidInputCargoCount;
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, null,
                        out stack, out inc1);
                    if (needId > 0) {
                        __instance.fluidInputCount += (int)stack;
                        __instance.fluidInputInc += (int)inc1;
                        ++__instance.fluidInputCargoCount;
                        __instance.SetRecipe(needId, signPool);
                    }
                }
            }
        }

        // 处理belt2（右侧接口）
        if (__instance.belt2 > 0) {
            if (__instance.isOutput2) {
                // 右侧作为输出口，输出扩展产物（如沙土）
                var productExpansion = __instance.productExpansion(factory);
                if (productExpansion.Count > 0) {
                    foreach (var kvp in productExpansion.ToArray()) {
                        if (kvp.Value > 0) {
                            int itemId = kvp.Key;
                            CargoPath cargoPath =
                                cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                            if (cargoPath != null
                                && cargoPath.TryUpdateItemAtHeadAndFillBlank(itemId, 1, (byte)1, (byte)0)) {
                                productExpansion[itemId]--;
                                break;
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < (double)__instance.fluidInputMax) {
                if (__instance.fluidId > 0) {
                    if (cargoTraffic.TryPickItemAtRear(__instance.belt2, __instance.fluidId, null, out stack, out inc1)
                        > 0) {
                        __instance.fluidInputCount += (int)stack;
                        __instance.fluidInputInc += (int)inc1;
                        ++__instance.fluidInputCargoCount;
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, null,
                        out stack, out inc1);
                    if (needId > 0) {
                        __instance.fluidInputCount += (int)stack;
                        __instance.fluidInputInc += (int)inc1;
                        ++__instance.fluidInputCargoCount;
                        __instance.SetRecipe(needId, signPool);
                    }
                }
            }
        }

        // 处理belt0（正面输出口）
        if (__instance.belt0 > 0
            && __instance.isOutput0
            && __instance.productOutputCount > 0
            && cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId, (byte)1, (byte)0)) {
            --__instance.productOutputCount;
        }

        // 如果缓存区全部清空，重置输入id
        if (__instance.fluidInputCount == 0 && __instance.fluidOutputCount == 0 && __instance.productOutputCount == 0) {
            var productExpansion = __instance.productExpansion(factory);
            bool hasItems = false;
            foreach (var count in productExpansion.Values) {
                if (count > 0) {
                    hasItems = true;
                    break;
                }
            }
            if (!hasItems) {
                __instance.fluidId = 0;
            }
        }

        // 更新工作状态
        __instance.isWorking = __instance.fluidInputCount > 0
                               && __instance.productOutputCount < __instance.productOutputMax
                               && __instance.fluidOutputCount < __instance.fluidOutputMax;

        __result = !__instance.isWorking ? 0U : 1U;
    }
}
