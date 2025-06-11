using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 矿物复制塔
/// </summary>
public static class MineralCopyTower {
    /// <summary>
    /// 创建矿物复制塔
    /// </summary>
    /// <returns>创建的矿物复制塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateFractionator(
            "矿物复制塔", RFE矿物复制塔, IFE矿物复制塔, MFE矿物复制塔,
            [IFE分馏原胚定向], [1], [10],
            3102, new(0.4f, 1.0f, 0.949f), -20, 0.4f, TFE矿物复制塔
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

        // 矿物复制塔：平衡的成功率和处理速度
        if (__instance.fluidInputCount > 0
            && __instance.productOutputCount < __instance.productOutputMax
            && __instance.fluidOutputCount < __instance.fluidOutputMax) {

            // 矿物复制塔处理速度适中
            __instance.progress += (int)(power
                                         * (600.0 / 3.0)// 比原版快一些
                                         * (__instance.fluidInputCargoCount < 30.0
                                             ? __instance.fluidInputCargoCount
                                             : 30.0)
                                         * itemStackAvg
                                         + 0.75);

            // 限制最大进度
            if (__instance.progress > 100000)
                __instance.progress = 100000;

            // 处理每次复制
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                // 计算平均每个物品携带的增产点数
                int itemIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;

                if (!__instance.incUsed)
                    __instance.incUsed = itemIncAvg > 0;


                __instance.fractionSuccess = __instance.seed / 2147483646.0 < 1;

                if (__instance.fractionSuccess) {
                    // 成功时的处理 - 矿物复制塔特性：成功时有几率产出双倍产物
                    ++__instance.productOutputCount;

                    // 20%几率产出额外产物
                    if (__instance.seed % 5 == 0) {
                        ++__instance.productOutputCount;
                    }

                    ++__instance.productOutputTotal;
                    lock (productRegister)
                        ++productRegister[__instance.productId];
                    lock (consumeRegister)
                        ++consumeRegister[__instance.fluidId];
                } else {
                    // 失败时的处理，矿物复制塔有85%回收率
                    if (__instance.seed % 20 < 17) {
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

        // 处理传送带交互 - 与原版相同
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
        if (__instance.fluidInputCount == 0 && __instance.fluidOutputCount == 0 && __instance.productOutputCount == 0)
            __instance.fluidId = 0;

        // 更新工作状态
        __instance.isWorking = __instance.fluidInputCount > 0
                               && __instance.productOutputCount < __instance.productOutputMax
                               && __instance.fluidOutputCount < __instance.fluidOutputMax;

        __result = !__instance.isWorking ? 0U : 1U;
    }
}
