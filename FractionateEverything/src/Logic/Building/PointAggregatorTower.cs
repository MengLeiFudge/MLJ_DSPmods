using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 点数聚集塔
/// </summary>
public static class PointAggregatorTower {
    /// <summary>
    /// 创建点数聚集塔
    /// </summary>
    /// <returns>创建的点数聚集塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateAndPreAddNewFractionator(
            "点数聚集塔", RFE点数聚集塔, IFE点数聚集塔, MFE点数聚集塔,
            [IFE分馏原胚定向], [1], [2],
            3104, new(0.2509f, 0.8392f, 1.0f), 0, 1.0f, TFE点数聚集塔
        );
    }

    public static void InternalUpdate(ref FractionatorComponent __instance, PlanetFactory factory,
        float power, SignData[] signPool, int[] productRegister, int[] consumeRegister, ref uint __result) {
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

        int productInc = 0;

        // 点数聚集塔只有在输入有物品且输出未满的情况下才工作
        if (__instance.fluidInputCount > 0
            && __instance.productOutputCount < __instance.productOutputMax
            && __instance.fluidOutputCount < __instance.fluidOutputMax) {

            // 处理速度基于输入物品数量和电力
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

            // 处理每次点数聚集
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                // 计算平均每个物品携带的增产点数
                int itemIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;

                // 如果输入物品没有增产点数，直接输出到流体输出
                if (itemIncAvg <= 0) {
                    ++__instance.fluidOutputCount;
                    ++__instance.fluidOutputTotal;
                    __instance.fluidOutputInc += 0;
                    --__instance.fluidInputCount;
                    __instance.fluidInputInc -= 0;
                    __instance.fluidInputCargoCount -= (float)(1.0 / itemStackAvg);
                    if (__instance.fluidInputCargoCount < 0.0)
                        __instance.fluidInputCargoCount = 0.0f;
                    continue;
                }

                if (!__instance.incUsed)
                    __instance.incUsed = true;

                // 随机数生成逻辑
                __instance.seed = (uint)((ulong)(__instance.seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue)
                                  - 1U;

                // 计算聚集成功率 - 基础5%成功率
                // 后期可以根据科技解锁情况提高这个值
                double baseProb = 0.05;

                // 根据输入物品的增产点数调整成功率
                // 当点数为4时，达到正常概率；低于4时概率降低，高于4时概率提高
                double pointFactor = itemIncAvg / 4.0;
                if (pointFactor > 1.5) pointFactor = 1.5;// 最多提升50%

                __instance.fractionSuccess = __instance.seed / 2147483646.0 < baseProb * pointFactor;

                // 聚集成功 - 生成高点数产物
                if (__instance.fractionSuccess) {
                    // 聚集后的点数，最多为10
                    int targetPoints = itemIncAvg * 2;
                    if (targetPoints > 10) targetPoints = 10;

                    ++__instance.productOutputCount;
                    ++__instance.productOutputTotal;
                    // 将聚集的点数加到productId物品上
                    productInc += targetPoints;
                    lock (productRegister)
                        ++productRegister[__instance.productId];
                    lock (consumeRegister)
                        ++consumeRegister[__instance.fluidId];
                } else {
                    // 聚集失败，输出到流体输出
                    ++__instance.fluidOutputCount;
                    ++__instance.fluidOutputTotal;
                    __instance.fluidOutputInc += itemIncAvg;
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
                // 作为输出处理 - 点数较低的物品从左侧输出
                if (__instance.fluidOutputCount > 0) {
                    int inc2 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                    if (cargoPath != null
                        && cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                            Mathf.CeilToInt((float)(itemStackAvg - 0.1)), (byte)1, (byte)inc2)) {
                        --__instance.fluidOutputCount;
                        __instance.fluidOutputInc -= inc2;
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
                    // 点数聚集塔接受任何带增产剂的物品
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
                // 右侧也用于输出点数较低的物品
                if (__instance.fluidOutputCount > 0) {
                    int inc4 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                    if (cargoPath != null
                        && cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                            Mathf.CeilToInt((float)(itemStackAvg - 0.1)), (byte)1, (byte)inc4)) {
                        --__instance.fluidOutputCount;
                        __instance.fluidOutputInc -= inc4;
                    }
                }
            } else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < (double)__instance.fluidInputMax) {
                // 右侧也可以输入
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

        // 处理belt0（正面输出口）- 输出高点数物品
        if (__instance.belt0 > 0
            && __instance.isOutput0
            && __instance.productOutputCount > 0) {
            int incPerItem = productInc / __instance.productOutputCount;
            if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId, (byte)1, (byte)incPerItem)) {
                --__instance.productOutputCount;
                productInc -= incPerItem;
            }
        }

        // 如果缓存区全部清空，重置输入id
        if (__instance.fluidInputCount == 0 && __instance.fluidOutputCount == 0 && __instance.productOutputCount == 0)
            __instance.fluidId = 0;

        // 更新工作状态
        __instance.isWorking = __instance.fluidInputCount > 0
                               && __instance.productOutputCount < __instance.productOutputMax
                               && __instance.fluidOutputCount < __instance.fluidOutputMax;

        __result = !__instance.isWorking ? 0U : 1U;
    }
}
