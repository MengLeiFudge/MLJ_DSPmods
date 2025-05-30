using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 转化塔
/// </summary>
public static class ConversionTower {
    /// <summary>
    /// 创建转化塔
    /// </summary>
    /// <returns>创建的转化塔原型元组数组</returns>
    public static (RecipeProto, ModelProto, ItemProto)[] CreateAll() {
        var towers = new (RecipeProto, ModelProto, ItemProto)[1];

        towers[0] = BuildingManager.CreateAndPreAddNewFractionator(
            "转化塔",
            IFE转化塔,
            MFE转化塔,
            2603,
            new Color(0.5f, 0.9f, 1.0f),
            0,
            1.0f
        );

        return towers;
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

            // 转化塔的处理速度更快
            __instance.progress += (int)(power
                                         * (800.0 / 3.0)// 比原版快一些
                                         * (__instance.fluidInputCargoCount < 30.0
                                             ? __instance.fluidInputCargoCount
                                             : 30.0)
                                         * itemStackAvg
                                         + 0.75);

            // 限制最大进度
            if (__instance.progress > 100000)
                __instance.progress = 100000;

            // 处理每次转化
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                // 计算平均每个物品携带的增产点数
                int itemIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;

                if (!__instance.incUsed)
                    __instance.incUsed = itemIncAvg > 0;

                // 随机数生成逻辑保持不变
                __instance.seed = (uint)((ulong)(__instance.seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue)
                                  - 1U;

                // 转化塔增加成功率
                double baseProb = __instance.produceProb;
                double incFactor = 1.0 + 1.5 * Cargo.accTableMilli[itemIncAvg < 10 ? itemIncAvg : 10];
                __instance.fractionSuccess = __instance.seed / 2147483646.0 < baseProb * incFactor;

                if (__instance.fractionSuccess) {
                    // 成功时的处理
                    ++__instance.productOutputCount;
                    ++__instance.productOutputTotal;
                    lock (productRegister)
                        ++productRegister[__instance.productId];
                    lock (consumeRegister)
                        ++consumeRegister[__instance.fluidId];
                } else {
                    // 转化塔特性：失败时有80%的回收率
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
                // 作为输出处理
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
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, RecipeProto.fractionatorNeeds,
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

        // 处理belt2（右侧接口）- 逻辑与belt1相同
        if (__instance.belt2 > 0) {
            if (__instance.isOutput2) {
                if (__instance.fluidOutputCount > 0) {
                    int inc4 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                    CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                    if (cargoPath != null
                        && cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                            Mathf.CeilToInt((float)(itemStackAvg - 0.1)), (byte)1, (byte)inc4)) {
                        --__instance.fluidOutputCount;
                        __instance.fluidOutputInc -= inc4;
                        if (__instance.fluidOutputCount > 0) {
                            int inc5 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                            if (cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                                    Mathf.CeilToInt((float)(itemStackAvg - 0.1)), (byte)1, (byte)inc5)) {
                                --__instance.fluidOutputCount;
                                __instance.fluidOutputInc -= inc5;
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
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, RecipeProto.fractionatorNeeds,
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
        if (__instance.fluidInputCount == 0 && __instance.fluidOutputCount == 0 && __instance.productOutputCount == 0)
            __instance.fluidId = 0;

        // 更新工作状态
        __instance.isWorking = __instance.fluidInputCount > 0
                               && __instance.productOutputCount < __instance.productOutputMax
                               && __instance.fluidOutputCount < __instance.fluidOutputMax;

        __result = !__instance.isWorking ? 0U : 1U;
    }
}
