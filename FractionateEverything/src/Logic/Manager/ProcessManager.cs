using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FE.Logic.Building;
using FE.Logic.Recipe;
using FE.UI.View;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 修改所有分馏塔的处理逻辑，以及对应的显示。
/// </summary>
public static class ProcessManager {
    #region Field

    private static int totalUIUpdateTimes = 0;
    private static int fractionatorID = 0;
    private static bool isFirstUpdateUI = true;
    private static float productProbTextBaseY;
    private static float oriProductProbTextBaseY;
    private static string lastSpeedText = "";
    private static string lastProductProbText = "";
    private static string lastOriProductProbText = "";
    public static int MaxOutputTimes = 2;
    public static int MaxBeltSpeed = 30;
    public static int FracFluidInputMax = 40;
    public static int FracProductOutputMax = 20;
    public static int FracFluidOutputMax = 20;
    private static double[] incTableFixedRatio;
    private static Dictionary<int, int> emptyOutputs = [];

    #endregion

    public static void Init() {
        //获取传送带的最大速度，以此决定循环的最大次数以及缓存区大小
        //游戏逻辑帧只有60，就算传送带再快，也只能取放一个槽位的物品，也就是最多4个，再多也取不到
        //所以下面均以60/s的传送带速率作为极限值考虑
        MaxBeltSpeed = (from item in LDB.items.dataArray
            where item.Type == EItemType.Logistics && item.prefabDesc.isBelt
            select item.prefabDesc.beltSpeed * 6).Prepend(0).Max();
        MaxBeltSpeed = Math.Min(MaxBeltSpeed, 60);
        MaxOutputTimes = (int)Math.Ceiling(MaxBeltSpeed / 15.0);
        float ratio = MaxBeltSpeed / 30.0f;
        PrefabDesc desc = LDB.models.Select(M分馏塔).prefabDesc;
        FracFluidInputMax = (int)(desc.fracFluidInputMax * ratio);
        FracProductOutputMax = (int)(desc.fracProductOutputMax * ratio);
        FracFluidOutputMax = (int)(desc.fracFluidOutputMax * ratio);

        //增产剂的增产效果修复，因为增产点数对于增产的加成不是线性的，但对于加速的加成是线性的
        incTableFixedRatio = new double[Cargo.incTableMilli.Length];
        for (int i = 1; i < Cargo.incTableMilli.Length; i++) {
            incTableFixedRatio[i] = Cargo.accTableMilli[i] / Cargo.incTableMilli[i];
        }
    }

    #region 分馏配方与科技状态检测

    // /// <summary>
    // /// 更新分馏塔处理需要的部分数值。
    // /// </summary>
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
    // public static void GameData_GameTick_Postfix(long time) {
    //     //使用3作为特殊值，每10逻辑帧更新一次
    //     if (time % 10 != 3 || GameMain.history == null) {
    //         return;
    //     }
    //     // //从科技获取流动输出最大堆叠数目、产物输出最大堆叠数目
    //     // EnableFluidOutputStack = GameMain.history.TechUnlocked(TFE分馏流动输出集装);
    //     // int maxStack = 1;
    //     // for (int i = 0; i < 3; i++) {
    //     //     if (GameMain.history.TechUnlocked(TFE分馏产物输出集装 + i)) {
    //     //         maxStack++;
    //     //     }
    //     // }
    //     // MaxProductOutputStack = maxStack;
    //     // //从科技获取是否分馏永动
    //     // EnableFracForever = GameMain.history.TechUnlocked(TFE分馏永动);
    //     // EnableFluidOutputStack = false;
    //     // MaxProductOutputStack = 1;
    //     // EnableFracForever = true;
    // }

    #endregion

    #region 分馏塔处理逻辑

    /// <summary>
    /// 返回增产加成、加速加成中二者最大的值。
    /// </summary>
    public static double MaxTableMilli(int fluidInputIncAvg) {
        int avgPoint = fluidInputIncAvg < 10 ? fluidInputIncAvg : 10;
        double ratioAcc = Cargo.accTableMilli[avgPoint];
        double ratioInc = Cargo.incTableMilli[avgPoint] * incTableFixedRatio[avgPoint];
        return ratioAcc > ratioInc ? ratioAcc : ratioInc;
    }

    /// <summary>
    /// 修改分馏塔的运行逻辑。
    /// </summary>
    /// <remarks>
    /// <para>需要特别注意分馏塔产物输出的拓展。现在产物相关内容有：</para>
    /// <ul>
    /// <li>int __instance.productId: 当前主产物ID</li>
    /// <li>int __instance.productOutputCount: 当前主产物数目</li>
    /// <li>Dictionary&lt;int, int&gt; __instance.otherProductOutput(factory): 当前所有副产物拓展</li>
    /// <li>int __instance.productOutputTotal: 统计主产物数目</li>
    /// </ul>
    /// <para>其中第三项为输出拓展字段，Key表示副产物ID，Value表示副产物数目。</para>
    /// <para>输出拓展字段不参与分馏塔产物输出数目是否超限的判断。</para>
    /// <para>除此之外，分馏判定结果由<see cref="FE.Logic.Recipe.BaseRecipe.GetOutputs"/>得到。</para>
    /// </remarks>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.InternalUpdate))]
    public static bool FractionatorComponent_InternalUpdate_Prefix(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result) {
        int buildingID = factory.entityPool[__instance.entityId].protoId;
        switch (buildingID) {
            case IFE交互塔:
                InternalUpdate<BuildingTrainRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.BuildingTrain);
                return false;
            case IFE矿物复制塔:
                InternalUpdate<MineralCopyRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.MineralCopy);
                return false;
            case IFE点数聚集塔:
                PointAggregateTower.InternalUpdate(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result);
                return false;
            case IFE量子复制塔:
                InternalUpdate<QuantumCopyRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.QuantumDuplicate);
                return false;
            case IFE点金塔:
                InternalUpdate<AlchemyRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.Alchemy);
                return false;
            case IFE分解塔:
                InternalUpdate<DeconstructionRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.Deconstruction);
                return false;
            case IFE转化塔:
                InternalUpdate<ConversionRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.Conversion);
                return false;
        }
        //原版分馏塔不做处理
        return true;

        //         //分馏成功率，越接近0表示成功率越高
        //         float randomVal = (float)(__instance.seed / 2147483646.0);
        //         //分馏成功率加成，2.0表示加成100%
        //         float successRatePlus = 1.0f;
        //         if (buildingID == IFE点数聚集塔) {
        //             //总增产小于10点则概率为0
        //             //总增产大于等于10点时，平均增产0点则概率为0，平均增产4点则概率为10%，其余点数线性计算
        //             successRatePlus = __instance.fluidInputInc >= 10
        //                 ? successRatePlus * __instance.fluidInputInc / __instance.fluidInputCount * 2.5f
        //                 : 0;
        //         } else if (buildingID == IFE量子复制塔) {
        //             //平均增产0点则概率为0，平均增产10点则概率为IPFDic，其余点数线性计算
        //             //如果其他mod对增产剂效果有调整，会自动选取加速、增产里面最高的效果
        //             successRatePlus = (float)MaxTableMilli(fluidInputIncAvg) / 2.5f;
        //         } else {
        //             //根据平均增产点数给予概率加成
        //             //如果其他mod对增产剂效果有调整，会自动选取加速、增产里面最高的效果
        //             successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
        //         }
        //         //outputIDNum[0]表示产物ID（负数损毁，0不变，其他生成产物），[1]表示产物数目（仅在[0]为正数时有意义）
        //         int[] outputIDNum = GetOutput(randomVal, successRatePlus, recipe);
        //         //如果分馏永动已研究，并且任何一个产物缓存达到上限的一半，则不会分馏出物品
        //         if (EnableFracForever) {
        //             if (__instance.productOutputCount >= __instance.productOutputMax / 2) {
        //                 outputIDNum[0] = 0;
        //                 outputIDNum[1] = 0;
        //             }
        //             foreach (var p in otherProductOutput) {
        //                 if (p.Value >= __instance.productOutputMax / 2) {
        //                     outputIDNum[0] = 0;
        //                     outputIDNum[1] = 0;
        //                     break;
        //                 }
        //             }
        //         }
        //         __instance.fractionSuccess = outputIDNum[0] > 0;
        //
        //         #endregion
        //
        //         #region 根据分馏结果处理原料输入、原料输出、产物输出
        //
        //         if (outputIDNum[0] > 0) {
        //             //分馏出产物
        //             __instance.fluidInputCount--;
        //             if (buildingID == IFE点数聚集塔) {
        //                 __instance.fluidInputInc -= 10;
        //             } else {
        //                 __instance.fluidInputInc -= fluidInputIncAvg;
        //             }
        //             __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
        //             if (__instance.fluidInputCargoCount < 0f) {
        //                 __instance.fluidInputCargoCount = 0f;
        //             }
        //
        //             if (outputIDNum[0] == __instance.productId) {
        //                 __instance.productOutputCount += outputIDNum[1];
        //             } else {
        //                 if (otherProductOutput.ContainsKey(outputIDNum[0])) {
        //                     otherProductOutput[outputIDNum[0]] += outputIDNum[1];
        //                 } else {
        //                     otherProductOutput.Add(outputIDNum[0], outputIDNum[1]);
        //                 }
        //             }
        //
        //             __instance.productOutputTotal += outputIDNum[1];
        //             if (outputIDNum[0] == inputItemID) {
        //                 //如果分馏出来的产物与原料相同，只计算多生成的部分
        //                 lock (productRegister) {
        //                     productRegister[outputIDNum[0]] += outputIDNum[1] - 1;
        //                 }
        //                 if (buildingID == IFE矿物复制塔) {
        //                     lock (NRFracSuccessCount) {
        //                         NRFracSuccessCount[outputIDNum[0]] += outputIDNum[1];
        //                     }
        //                 } else if (buildingID == IFE量子复制塔) {
        //                     lock (IFracSuccessCount) {
        //                         IFracSuccessCount[outputIDNum[0]] += outputIDNum[1];
        //                     }
        //                 }
        //             } else {
        //                 //正常计算
        //                 lock (consumeRegister) {
        //                     consumeRegister[inputItemID]++;
        //                 }
        //                 lock (productRegister) {
        //                     productRegister[outputIDNum[0]] += outputIDNum[1];
        //                 }
        //                 if (buildingID == IFE矿物复制塔) {
        //                     lock (NRSuperFracSuccessCount) {
        //                         NRSuperFracSuccessCount[outputIDNum[0]] += outputIDNum[1];
        //                     }
        //                 } else if (buildingID == IFE转化塔) {
        //                     if (outputIDNum[0] == recipe.mainOutput) {
        //                         lock (UpgradeFracSuccessCount) {
        //                             UpgradeFracSuccessCount[outputIDNum[0]] += outputIDNum[1];
        //                         }
        //                     } else {
        //                         lock (UpgradeSuperFracSuccessCount) {
        //                             UpgradeSuperFracSuccessCount[outputIDNum[0]] += outputIDNum[1];
        //                         }
        //                     }
        //                 }
        //             }
        //         } else {
        //             //没分馏出物品，正常流出或者原料损毁
        //             __instance.fluidInputCount--;
        //             __instance.fluidInputInc -= fluidInputIncAvg;
        //             __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
        //             if (__instance.fluidInputCargoCount < 0f) {
        //                 __instance.fluidInputCargoCount = 0f;
        //             }
        //             if (outputIDNum[1] == 0) {
        //                 //分馏失败，保留原料
        //                 __instance.fluidOutputCount++;
        //                 __instance.fluidOutputTotal++;
        //                 __instance.fluidOutputInc += fluidInputIncAvg;
        //             } else {
        //                 //分馏失败，损毁原料
        //                 lock (consumeRegister) {
        //                     consumeRegister[inputItemID]++;
        //                 }
        //             }
        //         }
        //
        //         #endregion
        //
        //         __instance.progress -= 10000;
        //     }
        // } else {
        //     __instance.fractionSuccess = false;
        // }
        //
        // CargoTraffic cargoTraffic = factory.cargoTraffic;
        // byte stack;
        // byte inc;
        // int beltId;
        // bool isOutput;
        // if (__instance.belt1 > 0) {
        //     beltId = __instance.belt1;
        //     isOutput = __instance.isOutput1;
        //     if (isOutput) {
        //         if (__instance.fluidOutputCount > 0) {
        //             CargoPath cargoPath =
        //                 cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        //             if (cargoPath != null) {
        //                 //原版传送带最大速率为30，如果每次尝试放1个物品到传送带上，需要每帧判定2次（30速*4堆叠/60帧）
        //                 //创世传送带最大速率为60，如果每次尝试放1个物品到传送带上，需要每帧判定4次（60速*4堆叠/60帧）
        //                 //每帧至少尝试一次，尝试就会lock buffer进而影响效率，所以这里尝试减少输出的次数
        //                 int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
        //                 if (!EnableFluidOutputStack) {
        //                     //未研究流动输出集装科技，根据传送带速率每帧判定2-4次
        //                     for (int i = 0; i < MaxOutputTimes; i++) {
        //                         if (__instance.fluidOutputCount <= 0) {
        //                             break;
        //                         }
        //                         if (buildingID == IFE点数聚集塔
        //                             && fluidOutputAvgInc < 4
        //                             && __instance.fluidOutputCount > 1) {
        //                             fluidOutputAvgInc = __instance.fluidOutputInc >= 4 ? 4 : 0;
        //                         }
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
        //                                 Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
        //                                 (byte)fluidOutputAvgInc)) {
        //                             __instance.fluidOutputCount--;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                         } else {
        //                             break;
        //                         }
        //                     }
        //                 } else {
        //                     //已研究流动输出集装科技
        //                     if (__instance.fluidOutputCount > 4) {
        //                         //超过4个，则输出4个
        //                         if (buildingID == IFE点数聚集塔 && fluidOutputAvgInc < 4) {
        //                             fluidOutputAvgInc = __instance.fluidOutputInc >= 16 ? 4 : 0;
        //                         }
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
        //                                 4, 4, (byte)(fluidOutputAvgInc * 4))) {
        //                             __instance.fluidOutputCount -= 4;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc * 4;
        //                         }
        //                     } else if (__instance.fluidInputCount == 0) {
        //                         //未超过4个且输入为空，剩几个输出几个
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
        //                                 4, (byte)__instance.fluidOutputCount,
        //                                 (byte)__instance.fluidOutputInc)) {
        //                             __instance.fluidOutputCount = 0;
        //                             __instance.fluidOutputInc = 0;
        //                         }
        //                     }
        //                 }
        //             }
        //         }
        //     } else if (!isOutput
        //                && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
        //         if (inputItemID > 0) {
        //             if (cargoTraffic.TryPickItemAtRear(beltId, inputItemID, null,
        //                     out stack, out inc)
        //                 > 0) {
        //                 __instance.fluidInputCount += stack;
        //                 __instance.fluidInputInc += inc;
        //                 __instance.fluidInputCargoCount += 1f;
        //             }
        //         } else {
        //             if (buildingID == I分馏塔) {
        //                 int input = cargoTraffic.TryPickItemAtRear(beltId, I氢, null, out stack, out inc);
        //                 if (input > 0) {
        //                     __instance.fluidInputCount += stack;
        //                     __instance.fluidInputInc += inc;
        //                     __instance.fluidInputCargoCount += 1f;
        //                     __instance.fluidId = I氢;
        //                     __instance.productId = I重氢;
        //                     __instance.produceProb = 0.01f;
        //                     signPool[__instance.entityId].iconId0 = I重氢;
        //                     signPool[__instance.entityId].iconType = 1U;
        //                 }
        //             } else {
        //                 //新增的分馏塔均可输入任何物品，如果不存在分馏配方则会正常流出
        //                 int input = cargoTraffic.TryPickItemAtRear(beltId, 0, null, out stack, out inc);
        //                 if (input > 0) {
        //                     __instance.fluidInputCount += stack;
        //                     __instance.fluidInputInc += inc;
        //                     __instance.fluidInputCargoCount += 1f;
        //                     __instance.fluidId = input;
        //                     recipe = buildingID switch {
        //                         IFE矿物复制塔 => GetNaturalResourceRecipe(input),
        //                         IFE转化塔 => GetUpgradeRecipe(input),
        //                         _ => null,
        //                     };
        //                     __instance.productId = recipe?.mainOutput ?? input;
        //                     __instance.produceProb = 0.01f;
        //                     signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
        //                     signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
        //                 }
        //             }
        //         }
        //     }
        // }
        // if (__instance.belt0 > 0) {
        //     beltId = __instance.belt0;
        //     isOutput = __instance.isOutput0;
        //     if (isOutput) {
        //         //指示是否尝试输出副产物
        //         bool outputByproduct = true;
        //         //输出主产物
        //         for (int i = 0; i < MaxOutputTimes; i++) {
        //             //只有产物数目到达堆叠要求，或者没有正在处理的物品，才输出，且一次输出最大堆叠个数的物品
        //             if (__instance.productOutputCount >= MaxProductOutputStack) {
        //                 //产物达到最大堆叠数目，直接尝试输出
        //                 outputByproduct = false;
        //                 if (cargoTraffic.TryInsertItemAtHead(beltId, __instance.productId,
        //                         (byte)MaxProductOutputStack,
        //                         (byte)(buildingID == IFE点数聚集塔 ? 10 * MaxProductOutputStack : 0))) {
        //                     __instance.productOutputCount -= MaxProductOutputStack;
        //                 } else {
        //                     break;
        //                 }
        //             } else if (__instance is { productOutputCount: > 0, fluidInputCount: 0 }) {
        //                 //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
        //                 if (cargoTraffic.TryInsertItemAtHead(beltId, __instance.productId,
        //                         (byte)__instance.productOutputCount,
        //                         (byte)(buildingID == IFE点数聚集塔 ? 10 * __instance.productOutputCount : 0))) {
        //                     __instance.productOutputCount = 0;
        //                 } else {
        //                     break;
        //                 }
        //             } else {
        //                 break;
        //             }
        //         }
        //         //输出副产物
        //         if (outputByproduct) {
        //             //每个物品都要尝试输出
        //             List<int> keys = [..otherProductOutput.Keys];
        //             foreach (int outputID in keys) {
        //                 if (otherProductOutput[outputID] == 0) {
        //                     continue;
        //                 }
        //                 for (int j = 0; j < MaxOutputTimes; j++) {
        //                     if (otherProductOutput[outputID] >= MaxProductOutputStack) {
        //                         if (cargoTraffic.TryInsertItemAtHead(beltId, outputID,
        //                                 (byte)MaxProductOutputStack,
        //                                 (byte)(buildingID == IFE点数聚集塔 ? 10 * MaxProductOutputStack : 0))) {
        //                             otherProductOutput[outputID] -= MaxProductOutputStack;
        //                         } else {
        //                             break;
        //                         }
        //                     } else if (otherProductOutput[outputID] > 0 && __instance.fluidInputCount == 0) {
        //                         if (cargoTraffic.TryInsertItemAtHead(beltId, outputID,
        //                                 (byte)otherProductOutput[outputID],
        //                                 (byte)(buildingID == IFE点数聚集塔
        //                                     ? 10 * otherProductOutput[outputID]
        //                                     : 0))) {
        //                             otherProductOutput[outputID] = 0;
        //                         } else {
        //                             break;
        //                         }
        //                     } else {
        //                         break;
        //                     }
        //                 }
        //             }
        //         }
        //     }
        // }
        //
        // //分馏塔无输入、输出、产物时，清除图标显示，重置分馏塔状态
        // if (__instance is { fluidInputCount: 0, fluidOutputCount: 0, productOutputCount: 0 }) {
        //     __instance.fluidId = 0;
        //     __instance.productId = 0;
        //     signPool[__instance.entityId].iconId0 = 0U;
        //     signPool[__instance.entityId].iconType = 0U;
        // }
        //
        // __instance.isWorking = __instance.fluidInputCount > 0
        //                        && __instance.productOutputCount < __instance.productOutputMax
        //                        && __instance.fluidOutputCount < __instance.fluidOutputMax;
        //
        // if (!__instance.isWorking) {
        //     __result = 0u;
        //     return false;
        // }
        //
        // __result = 1u;
        // return false;
    }

    /// <summary>
    /// InternalUpdate的默认实现。
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="factory"></param>
    /// <param name="power"></param>
    /// <param name="signPool"></param>
    /// <param name="productRegister"></param>
    /// <param name="consumeRegister"></param>
    /// <param name="__result"></param>
    /// <param name="recipeType"></param>
    /// <typeparam name="T"></typeparam>
    public static void InternalUpdate<T>(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result, ERecipe recipeType) where T : BaseRecipe {
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
        T recipe = GetRecipe<T>(recipeType, fluidId);
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
            //指示是否已启用分馏永动并且某个产物达到上限的一半
            bool fracForever = false;
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                int fluidInputIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;
                if (!__instance.incUsed)
                    __instance.incUsed = fluidInputIncAvg > 0;

                if (recipe == null || !recipe.IsUnlocked) {
                    __instance.fluidInputInc -= fluidInputIncAvg;
                    __instance.fractionSuccess = false;
                    __instance.fluidInputCount--;
                    __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                    if (__instance.fluidInputCargoCount < 0f) {
                        __instance.fluidInputCargoCount = 0f;
                    }
                    __instance.fluidOutputCount++;
                    __instance.fluidOutputInc += fluidInputIncAvg;
                    // LogDebug($"配方为空，当前流动输入{__instance.fluidInputCount}个, 当前流动输出{__instance.fluidOutputCount}个, "
                    //          + $"当前产物输出{__instance.productOutputCount}个");
                    continue;
                }
                Dictionary<int, int> outputs = emptyOutputs;
                if (!fracForever && building.EnableFracForever()) {
                    //如果已启用分馏永动，并且所有产物都少于上限的一半，重新检查后者是否满足
                    if (__instance.productOutputCount >= __instance.productOutputMax / 2) {
                        fracForever = true;
                    } else {
                        foreach (var p in otherProductOutput) {
                            if (p.Value >= __instance.productOutputMax / 2) {
                                fracForever = true;
                                break;
                            }
                        }
                    }
                }
                if (!fracForever) {
                    //如果所有产物仍然少于上限的一半，正常处理
                    float successRatePlus = 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                    outputs = recipe.GetOutputs(ref __instance.seed, successRatePlus);
                }
                //如果是量子复制塔，消耗各种精华各n个（n取决于物品价值）
                if (buildingID == IFE量子复制塔) {
                    QuantumCopyRecipe recipe0 = recipe as QuantumCopyRecipe;
                    int count = (int)Math.Ceiling(recipe0.EssenceCost - 0.0001f);
                    float leftCount = recipe0.EssenceCost - count;
                    if (leftCount > 0.0001f) {
                        if (GetRandDouble(ref __instance.seed) < leftCount) {
                            count++;
                        }
                    }
                    if (!TakeEssenceFromModData(count)) {
                        outputs = [];
                    }
                }
                __instance.fluidInputInc -= fluidInputIncAvg;
                __instance.fractionSuccess = outputs != null && outputs.Count > 0;
                __instance.fluidInputCount--;
                __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                if (__instance.fluidInputCargoCount < 0f) {
                    __instance.fluidInputCargoCount = 0f;
                }
                if (outputs != null) {
                    if (outputs.Count == 0) {
                        __instance.fluidOutputCount++;
                        if (!fracForever) {
                            __instance.fluidOutputTotal++;
                        }
                        __instance.fluidOutputInc += fluidInputIncAvg;
                        // LogDebug($"原料不变，当前流动输入{__instance.fluidInputCount}个, 当前流动输出{__instance.fluidOutputCount}个, "
                        //          + $"当前产物输出{__instance.productOutputCount}个");
                    } else {
                        lock (consumeRegister) {
                            consumeRegister[fluidId]++;
                        }
                        foreach (KeyValuePair<int, int> p in outputs) {
                            int itemID = p.Key;
                            int itemCount = p.Value;
                            // LogDebug($"转化得到产物ID{itemID}，数目{itemCount}");
                            if (itemID == I沙土) {
                                //不要用AddItem，会导致UI显示问题
                                GameMain.mainPlayer.sandCount += itemCount;
                                continue;
                            }
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
                } else {
                    lock (consumeRegister) {
                        consumeRegister[fluidId]++;
                    }
                    // LogDebug($"原料损毁，当前流动输入{__instance.fluidInputCount}个, 当前流动输出{__instance.fluidOutputCount}个, "
                    //          + $"当前产物输出{__instance.productOutputCount}个");
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
                        recipe = GetRecipe<T>(recipeType, needId);
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
                        recipe = GetRecipe<T>(recipeType, needId);
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
                int productStack = building.MaxProductOutputStack();
                for (int i = 0; i < MaxOutputTimes; i++) {
                    //只有产物数目到达堆叠要求，或者没有正在处理的物品，才输出，且一次输出最大堆叠个数的物品
                    if (__instance.productOutputCount >= productStack) {
                        //产物达到最大堆叠数目，直接尝试输出
                        mainProductOutput = true;
                        if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)productStack, 0)) {
                            break;
                        }
                        __instance.productOutputCount -= productStack;
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
                            if (otherProductOutput[outputID] >= productStack) {
                                if (!cargoTraffic.TryInsertItemAtHead(__instance.belt0, outputID,
                                        (byte)productStack, 0)) {
                                    break;
                                }
                                otherProductOutput[outputID] -= productStack;
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
                    // //移除所有数目为0的缓存物品
                    // foreach (int outputID in keys) {
                    //     if (otherProductOutput[outputID] == 0) {
                    //         otherProductOutput.Remove(outputID);
                    //     }
                    // }
                }
            } else if (buildingID == IFE交互塔 && __instance.belt1 <= 0 && __instance.belt2 <= 0) {
                //正面作为输入，数据传到数据中心。仅接受奖券。
                int itemId = cargoTraffic.TryPickItemAtRear(__instance.belt0, 0, TabRaffle.TicketIds, out stack, out _);
                if (itemId > 0) {
                    AddItemToModData(itemId, stack);
                }
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

    #endregion

    #region 分馏塔耗电调整

    private static void SetPCState(this FractionatorComponent fractionator,
        PowerConsumerComponent[] pcPool, EntityData[] entityPool) {
        int buildingID = entityPool[fractionator.entityId].protoId;
        double num1 = fractionator.fluidInputCargoCount > 0.0001
            ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
            : 4.0;
        double num2 = ((double)fractionator.fluidInputCargoCount < MaxBeltSpeed
            ? (double)fractionator.fluidInputCargoCount
            : MaxBeltSpeed);
        num2 = num2 * num1 - MaxBeltSpeed;
        if (num2 < 0.0)
            num2 = 0.0;
        double powerRatio;
        if (buildingID == IFE点数聚集塔) {
            powerRatio = 1.0;
        } else if (buildingID == IFE量子复制塔) {
            powerRatio = (Cargo.powerTableRatio[fractionator.incLevel] - 1.0) * 0.5 + 1.0;
        } else {
            powerRatio = Cargo.powerTableRatio[fractionator.incLevel];
        }
        int permillage = (int)((num2 * 50.0 * 30.0 / MaxBeltSpeed + 1000.0) * powerRatio + 0.5);
        pcPool[fractionator.pcId].SetRequiredEnergy(fractionator.isWorking, permillage);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickBeforePower))]
    public static void FactorySystem_GameTickBeforePower_Postfix(ref FactorySystem __instance) {
        EntityData[] entityPool = __instance.factory.entityPool;
        PowerConsumerComponent[] consumerPool = __instance.factory.powerSystem.consumerPool;
        for (int index = 1; index < __instance.fractionatorCursor; ++index) {
            if (__instance.fractionatorPool[index].id == index)
                __instance.fractionatorPool[index].SetPCState(consumerPool, entityPool);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.ParallelGameTickBeforePower))]
    public static void FactorySystem_ParallelGameTickBeforePower_Postfix(ref FactorySystem __instance,
        int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt) {
        EntityData[] entityPool = __instance.factory.entityPool;
        PowerConsumerComponent[] consumerPool = __instance.factory.powerSystem.consumerPool;
        int _start;
        int _end;
        if (WorkerThreadExecutor.CalculateMissionIndex(1, __instance.fractionatorCursor - 1, _usedThreadCnt,
                _curThreadIdx, _minimumMissionCnt, out _start, out _end)) {
            for (int index = _start; index < _end; ++index) {
                if (__instance.fractionatorPool[index].id == index)
                    __instance.fractionatorPool[index].SetPCState(consumerPool, entityPool);
            }
        }
    }

    #endregion

    #region 分馏塔简洁提示信息窗口

    /// <summary>
    /// 修改分馏塔简洁提示信息窗口中的速率。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityBriefInfo), nameof(EntityBriefInfo.SetBriefInfo))]
    public static void EntityBriefInfo_SetBriefInfo_Postfix(ref EntityBriefInfo __instance, PlanetFactory _factory,
        int _entityId) {
        if (_factory == null || _entityId == 0)
            return;
        EntityData entityData = _factory.entityPool[_entityId];
        if (entityData.id == 0)
            return;
        if (entityData.fractionatorId > 0) {
            int fractionatorId = entityData.fractionatorId;
            FractionatorComponent fractionator = _factory.factorySystem.fractionatorPool[fractionatorId];
            int fluidId = fractionator.fluidId;
            int productId = fractionator.productId;
            if (fluidId > 0 && productId > 0) {
                PowerConsumerComponent powerConsumer = _factory.powerSystem.consumerPool[fractionator.pcId];
                int networkId = powerConsumer.networkId;
                PowerNetwork powerNetwork = _factory.powerSystem.netPool[networkId];
                float consumerRatio = powerNetwork == null || networkId <= 0
                    ? 0.0f
                    : (float)powerNetwork.consumerRatio;
                double fluidInputCountPerCargo = 1.0;
                if (fractionator.fluidInputCount == 0)
                    fractionator.fluidInputCargoCount = 0.0f;
                else
                    fluidInputCountPerCargo = fractionator.fluidInputCargoCount > 1E-07
                        ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
                        : 4.0;
                double speed = consumerRatio
                               * (fractionator.fluidInputCargoCount < MaxBeltSpeed
                                   ? fractionator.fluidInputCargoCount
                                   : MaxBeltSpeed)
                               * fluidInputCountPerCargo
                               * 60.0;
                if (!fractionator.isWorking)
                    speed = 0.0;
                __instance.reading0 = speed;
            }
        }
    }

    #endregion

    #region 分馏塔详情窗口

    /// <summary>
    /// 修改分馏塔详情窗口中的部分内容。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnUpdate))]
    public static void UIFractionatorWindow__OnUpdate_Postfix(ref UIFractionatorWindow __instance) {
        if (isFirstUpdateUI) {
            isFirstUpdateUI = false;
            //标题可以横向拓展，避免英文无法完全显示
            __instance.titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            //概率可以横向拓展
            __instance.productProbText.horizontalOverflow = HorizontalWrapMode.Overflow;
            __instance.oriProductProbText.horizontalOverflow = HorizontalWrapMode.Overflow;
            //概率可以纵向拓展，记录初始偏移量
            __instance.productProbText.verticalOverflow = VerticalWrapMode.Overflow;
            __instance.oriProductProbText.verticalOverflow = VerticalWrapMode.Overflow;
            productProbTextBaseY = __instance.productProbText.transform.localPosition.y;
            oriProductProbTextBaseY = __instance.oriProductProbText.transform.localPosition.y;
            //支持富文本
            __instance.productProbText.supportRichText = true;
            __instance.oriProductProbText.supportRichText = true;
        }
        if (__instance.fractionatorId == 0 || __instance.factory == null) {
            return;
        }
        FractionatorComponent fractionator =
            __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
        if (fractionator.id != __instance.fractionatorId) {
            return;
        }
        if (fractionator.fluidId == 0) {
            return;
        }
        int buildingID = __instance.factory.entityPool[fractionator.entityId].protoId;
        if (buildingID == I分馏塔) {
            return;
        }
        ItemProto building = LDB.items.Select(buildingID);
        //当持续查看同一个塔的状态时，每20帧（通常为0.333s）刷新UI，防止UI变化过快导致无法看清
        if (__instance.fractionatorId == fractionatorID) {
            totalUIUpdateTimes++;
            if (totalUIUpdateTimes < 20) {
                __instance.speedText.text = lastSpeedText;
                __instance.productProbText.text = lastProductProbText;
                __instance.oriProductProbText.text = lastOriProductProbText;
                return;
            }
        } else {
            fractionatorID = __instance.fractionatorId;
        }
        totalUIUpdateTimes = 0;

        //修改速率计算
        PowerConsumerComponent powerConsumer = __instance.powerSystem.consumerPool[fractionator.pcId];
        int networkId = powerConsumer.networkId;
        PowerNetwork powerNetwork = __instance.powerSystem.netPool[networkId];
        float consumerRatio = powerNetwork == null || networkId <= 0
            ? 0.0f
            : (float)powerNetwork.consumerRatio;
        double fluidInputCountPerCargo = 1.0;
        if (fractionator.fluidInputCount == 0)
            fractionator.fluidInputCargoCount = 0.0f;
        else
            fluidInputCountPerCargo = fractionator.fluidInputCargoCount > 1E-07
                ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
                : 4.0;
        double speed = consumerRatio
                       * (fractionator.fluidInputCargoCount < MaxBeltSpeed
                           ? fractionator.fluidInputCargoCount
                           : MaxBeltSpeed)
                       * fluidInputCountPerCargo
                       * 60.0;
        if (!fractionator.isWorking)
            speed = 0.0;
        __instance.speedText.text = string.Format("次分馏每分".Translate(), Math.Round(speed));
        lastSpeedText = __instance.speedText.text;
        //根据分馏塔以及配方情况，计算实际处理情况，生成上方字符串s1以及下方字符串s2
        string s1 = "";
        string s2 = "";
        int fluidInputIncAvg = fractionator.fluidInputCount > 0
            ? fractionator.fluidInputInc / fractionator.fluidInputCount
            : 0;
        float successRatePlus = 1.0f;
        BaseRecipe recipe;
        switch (buildingID) {
            case IFE交互塔:
                successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                recipe = GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, fractionator.fluidId);
                break;
            case IFE矿物复制塔:
                successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                recipe = GetRecipe<MineralCopyRecipe>(ERecipe.MineralCopy, fractionator.fluidId);
                break;
            case IFE点数聚集塔:
                successRatePlus = fractionator.fluidInputInc >= 10
                    ? successRatePlus * fractionator.fluidInputInc / fractionator.fluidInputCount * 2.5f
                    : 0;
                recipe = null;
                break;
            case IFE量子复制塔:
                successRatePlus = (float)MaxTableMilli(fluidInputIncAvg) / 2.5f;
                recipe = GetRecipe<QuantumCopyRecipe>(ERecipe.QuantumDuplicate, fractionator.fluidId);
                break;
            case IFE点金塔:
                successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                recipe = GetRecipe<AlchemyRecipe>(ERecipe.Alchemy, fractionator.fluidId);
                break;
            case IFE分解塔:
                successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                recipe = GetRecipe<DeconstructionRecipe>(ERecipe.Deconstruction, fractionator.fluidId);
                break;
            case IFE转化塔:
                successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                recipe = GetRecipe<ConversionRecipe>(ERecipe.Conversion, fractionator.fluidId);
                break;
            default:
                return;
        }
        float flowRatio = 1.0f;
        if (recipe == null) {
            s1 = "无配方".Translate();
            s1 = s1.WithColor(Red);
            s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
        } else if (!recipe.IsUnlocked) {
            s1 = "未解锁".Translate();//todo：注意，“配方未解锁”已经被原版游戏注册过
            s1 = s1.WithColor(Red);
            s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
        } else {
            StringBuilder sb1 = new StringBuilder();
            bool fracForever = false;
            if (building.EnableFracForever()) {
                if (fractionator.productOutputCount >= fractionator.productOutputMax / 2) {
                    fracForever = true;
                }
                Dictionary<int, int> productExpansion = fractionator.otherProductOutput(__instance.factory);
                foreach (var p in productExpansion) {
                    if (p.Value >= fractionator.productOutputMax / 2) {
                        fracForever = true;
                        break;
                    }
                }
            }
            if (fracForever) {
                s1 = recipe.LvExpWC + "\n" + "永动".Translate();
                s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
            } else {
                foreach (var output in recipe.OutputMain) {
                    float ratio = recipe.SuccessRate * successRatePlus * output.SuccessRate;
                    string name = FormatName(LDB.items.Select(output.OutputID).Name);
                    sb1.Append($"{name}x{output.OutputCount} ({ratio.FormatP()})\n");
                    flowRatio -= ratio;
                }
                float destroyRatio = recipe.DestroyRate;
                flowRatio -= destroyRatio;
                s1 = recipe.LvExpWC + "\n" + sb1.ToString().Substring(0, sb1.Length - 1);
                s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
                if (destroyRatio > 0) {
                    string destroy = $"{"损毁".Translate()}({destroyRatio.FormatP()})";
                    s2 += $"\n{destroy.WithColor(Red)}";
                }
            }
        }
        //刷新概率显示内容
        __instance.productProbText.text = s1;
        lastProductProbText = s1;
        __instance.oriProductProbText.text = s2;
        lastOriProductProbText = s2;
        //刷新概率显示位置
        float upY = productProbTextBaseY + 9f * (s1.Split('\n').Length - 1);
        __instance.productProbText.transform.localPosition = new(0, upY, 0);
        float downY = oriProductProbTextBaseY - (s2.Split('\n').Length > 1 ? 9f : 0);
        __instance.oriProductProbText.transform.localPosition = new(0, downY, 0);
    }

    #endregion
}
