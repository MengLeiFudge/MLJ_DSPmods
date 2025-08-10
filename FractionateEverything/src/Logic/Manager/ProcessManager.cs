using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FE.Logic.Building;
using FE.Logic.Recipe;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 修改所有分馏塔的处理逻辑，以及对应的显示。
/// </summary>
public static class ProcessManager {
    public static void AddTranslations() {
        Register("搬运中", "Transporting");
        Register("永动", "Forever");
        Register("流动", "Flow");
        Register("无配方", "No recipe");
        Register("损毁", "Destroy");
    }

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
    public static readonly List<ProductOutputInfo> emptyOutputs = [];

    #endregion

    public static void Init() {
        //获取传送带的最大速度，以此决定循环的最大次数以及缓存区大小
        //游戏逻辑帧只有60，就算传送带再快，也只能取放一个槽位的物品，也就是最多4个，再多也取不到
        //所以下面均以60/s的传送带速率作为极限值考虑
        MaxBeltSpeed = (from item in LDB.items.dataArray
            where item.Type == EItemType.Logistics && item.prefabDesc.isBelt
            select item.prefabDesc.beltSpeed * 6).Prepend(0).Max();
        MaxBeltSpeed = Math.Min(60, MaxBeltSpeed);
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
    /// <para>注意：新增分馏塔产物输出使用Mod拓展存储。数据结构如下：</para>
    /// <ul>
    /// <li>int __instance.productId: 第一个主输出的ID，无用</li>
    /// <li>int __instance.productOutputCount: 第一个主输出的数目，无用</li>
    /// <li>int __instance.productOutputTotal: 第一个主输出的统计数目</li>
    /// <li>List&lt;ProductOutputInfo&gt; __instance.productOutputs(factory): 存储所有产物输出</li>
    /// </ul>
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
                    consumeRegister, ref __result, ERecipe.QuantumCopy);
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
        //所有产物输出
        List<ProductOutputInfo> products = __instance.products(factory);
        int fluidId = __instance.fluidId;
        T recipe = GetRecipe<T>(recipeType, fluidId);
        //检测products和recipe的输出是否一致
        if (recipe == null) {
            products.Clear();
            __instance.productOutputCount = 0;
        } else {
            bool needResetProducts = false;
            if (products.Count != recipe.OutputMain.Count + recipe.OutputAppend.Count) {
                needResetProducts = true;
            } else {
                foreach (OutputInfo info in recipe.OutputMain) {
                    if (products.All(p => p.itemId != info.OutputID)) {
                        needResetProducts = true;
                        break;
                    }
                }
                foreach (OutputInfo info in recipe.OutputAppend) {
                    if (products.All(p => p.itemId != info.OutputID)) {
                        needResetProducts = true;
                        break;
                    }
                }
            }
            if (needResetProducts) {
                products.Clear();
                __instance.productOutputCount = 0;
                foreach (OutputInfo info in recipe.OutputMain) {
                    products.Add(new(true, info.OutputID, 0));
                }
                foreach (OutputInfo info in recipe.OutputAppend) {
                    products.Add(new(false, info.OutputID, 0));
                }
            }
        }
        //第一个主输出，recipe有则必定有，recipe没有则必定没有
        int product0Id = __instance.productId;
        ProductOutputInfo product0 = products.FirstOrDefault(p => p.itemId == product0Id);
        //如果通过面板取了物品，需要同步数目到products
        if (product0 != null) {
            product0.count = __instance.productOutputCount;
        }
        if (power < 0.1) {
            __result = 0;
            return;
        }
        float fluidInputCountPerCargo = 1.0f;
        if (__instance.fluidInputCount == 0)
            __instance.fluidInputCargoCount = 0f;
        else
            fluidInputCountPerCargo = __instance.fluidInputCargoCount > 0.0001
                ? __instance.fluidInputCount / __instance.fluidInputCargoCount
                : 4f;
        int productOutputMax = __instance.productOutputMax;
        int buildingID = factory.entityPool[__instance.entityId].protoId;
        ItemProto building = LDB.items.Select(buildingID);
        if (__instance.fluidInputCount > 0
            && products.All(p => p.count < productOutputMax)
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
            //是否直接将输入搬运到输出，不进行任何处理
            bool moveDirectly = recipe == null || recipe.Locked;
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                int fluidInputIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;
                if (!__instance.incUsed)
                    __instance.incUsed = fluidInputIncAvg > 0;

                MoveDirectly:
                if (moveDirectly) {
                    //直接将输入搬运到输出，不进行任何处理
                    __instance.fractionSuccess = false;
                    __instance.fluidInputCount--;
                    __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                    if (__instance.fluidInputCargoCount < 0f) {
                        __instance.fluidInputCargoCount = 0f;
                    }
                    __instance.fluidInputInc -= fluidInputIncAvg;
                    __instance.fluidOutputCount++;
                    __instance.fluidOutputInc += fluidInputIncAvg;
                    continue;
                }
                //启用分馏永动且某个产物达到输出上限的一半，则分馏塔进入分馏永动状态
                if (building.EnableFracForever() && products.Any(p => p.count >= productOutputMax / 2)) {
                    moveDirectly = true;
                    goto MoveDirectly;
                }
                //正常处理，获取处理结果
                float successRatePlus = 1.0f + (float)MaxTableMilli(fluidInputIncAvg);
                List<ProductOutputInfo> outputs =
                    recipe.GetOutputs(ref __instance.seed, successRatePlus, consumeRegister);
                __instance.fluidInputInc -= fluidInputIncAvg;
                __instance.fractionSuccess = outputs != null && outputs.Count > 0;
                __instance.fluidInputCount--;
                __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                if (__instance.fluidInputCargoCount < 0f) {
                    __instance.fluidInputCargoCount = 0f;
                }
                if (outputs != null) {
                    if (outputs.Count == 0) {
                        //无处理，直接流出
                        __instance.fluidOutputCount++;
                        __instance.fluidOutputTotal++;
                        __instance.fluidOutputInc += fluidInputIncAvg;
                    } else {
                        //处理为其他物品
                        lock (consumeRegister) {
                            consumeRegister[fluidId]++;
                        }
                        foreach (ProductOutputInfo p in outputs) {
                            int itemID = p.itemId;
                            int itemCount = p.count;
                            lock (productRegister) {
                                productRegister[itemID] += itemCount;
                            }
                            if (itemID == I沙土) {
                                //不要用AddItem，会导致UI显示问题
                                GameMain.mainPlayer.sandCount += itemCount;
                                continue;
                            }
                            if (itemID == product0Id) {
                                product0.count += itemCount;
                                __instance.productOutputCount = product0.count;
                                //todo: 现在面板显示的输出统计是第一个主输出的统计
                                __instance.productOutputTotal += itemCount;
                            } else {
                                products.FirstOrDefault(product => product.itemId == itemID).count += itemCount;
                            }
                        }
                    }
                } else {
                    //损毁，原料消失
                    lock (consumeRegister) {
                        consumeRegister[fluidId]++;
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
                        if (recipe == null) {
                            __instance.productId = __instance.fluidId;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = 0;
                            signPool[__instance.entityId].iconType = 0U;
                        } else {
                            __instance.productId = recipe.OutputMain[0].OutputID;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                            signPool[__instance.entityId].iconType = 1U;
                            foreach (OutputInfo info in recipe.OutputMain) {
                                products.Add(new(true, info.OutputID, 0));
                            }
                            foreach (OutputInfo info in recipe.OutputAppend) {
                                products.Add(new(false, info.OutputID, 0));
                            }
                        }
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
                        if (recipe == null) {
                            __instance.productId = __instance.fluidId;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = 0;
                            signPool[__instance.entityId].iconType = 0U;
                        } else {
                            __instance.productId = recipe.OutputMain[0].OutputID;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                            signPool[__instance.entityId].iconType = 1U;
                            foreach (OutputInfo info in recipe.OutputMain) {
                                products.Add(new(true, info.OutputID, 0));
                            }
                            foreach (OutputInfo info in recipe.OutputAppend) {
                                products.Add(new(false, info.OutputID, 0));
                            }
                        }
                    }
                }
            }
        }
        if (__instance.belt0 > 0) {
            if (__instance.isOutput0) {
                if (products.Count > 0) {
                    //获取分馏塔产物输出堆叠
                    int productStack = building.MaxProductOutputStack();
                    //查找数目最多的附加产物
                    ProductOutputInfo product = null;
                    foreach (var p in products) {
                        if (!p.isMainOutput && (product == null || p.count > product.count)) {
                            product = p;
                        }
                    }
                    //如果数目小于productStack，继续查找数目最多的主产物
                    if (product == null || product.count < productStack) {
                        foreach (var p in products) {
                            if (p.isMainOutput && (product == null || p.count > product.count)) {
                                product = p;
                            }
                        }
                    }
                    //输出产物
                    if (product.count > 0) {
                        if (product.count >= productStack) {
                            //产物达到最大堆叠数目，直接尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, product.itemId, (byte)productStack,
                                    0)) {
                                product.count -= productStack;
                                if (product.itemId == product0Id) {
                                    __instance.productOutputCount = product.count;
                                }
                            }
                        } else if (product.count > 0 && __instance.fluidInputCount == 0) {
                            //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, product.itemId, (byte)product.count,
                                    0)) {
                                product.count = 0;
                                if (product.itemId == product0Id) {
                                    __instance.productOutputCount = product.count;
                                }
                            }
                        }
                    }
                }
            } else if (buildingID == IFE交互塔 && __instance.belt1 <= 0 && __instance.belt2 <= 0) {
                //正面作为输入，数据传到数据中心。可接受所有物品。
                int itemId = cargoTraffic.TryPickItemAtRear(__instance.belt0, 0, null, out stack, out _);
                if (itemId > 0) {
                    AddItemToModData(itemId, stack);
                }
            }
        }

        // 如果缓存区全部清空，重置全部
        if (__instance.fluidInputCount == 0
            && __instance.fluidOutputCount == 0
            && products.All(p => p.count == 0)) {
            __instance.fluidId = 0;
            __instance.productId = 0;
            products.Clear();
            signPool[__instance.entityId].iconId0 = 0;
            signPool[__instance.entityId].iconType = 0U;
        }

        __instance.isWorking = __instance.fluidInputCount > 0
                               && products.All(p => p.count < productOutputMax)
                               && __instance.fluidOutputCount < __instance.fluidOutputMax;
        if (building.EnableFracForever()) {
            __instance.isWorking &= products.All(p => p.count < productOutputMax / 2);
        }

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
        if (ParallelUtils.CalculateWorkSegmentOldFunction(1, __instance.fractionatorCursor - 1, _usedThreadCnt,
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
        if (!fractionator.isWorking
            && !(fractionator.productOutputCount >= fractionator.productOutputMax
                 || fractionator.fluidOutputCount >= fractionator.fluidOutputMax)
            && !(fractionator.fluidId > 0)
            && fractionator.fluidInputCount > 0) {
            __instance.stateText.text = "搬运中".Translate();
        }
        //当持续查看同一个塔的状态时，每20帧（通常为0.333s）刷新数字，防止变化过快导致数字无法看清
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
                successRatePlus = PointAggregateTower.SuccessRate;
                recipe = null;
                break;
            case IFE量子复制塔:
                successRatePlus = (float)MaxTableMilli(fluidInputIncAvg) / 2.5f;
                recipe = GetRecipe<QuantumCopyRecipe>(ERecipe.QuantumCopy, fractionator.fluidId);
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
        List<ProductOutputInfo> products = fractionator.products(__instance.factory);
        int productOutputMax = fractionator.productOutputMax;
        float flowRatio = 1.0f;
        if (recipe == null) {
            if (buildingID == IFE点数聚集塔) {
                StringBuilder sb1 = new StringBuilder();
                if (building.EnableFracForever() && products.Any(p => p.count > productOutputMax / 2)) {
                    s1 = "永动".Translate();
                    s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
                } else {
                    float ratio = successRatePlus;
                    string name = FormatName(LDB.items.Select(fractionator.fluidId).Name);
                    sb1.Append($"{name}x1 ({ratio.FormatP()})\n");
                    flowRatio -= ratio;
                    s1 = PointAggregateTower.LvWC + "\n" + sb1.ToString().Substring(0, sb1.Length - 1);
                    s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
                }
            } else {
                s1 = "无配方".Translate();
                s1 = s1.WithColor(Red);
                s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
            }
        } else if (recipe.Locked) {
            s1 = "配方未解锁".Translate();//“配方未解锁”已经被原版游戏注册过
            s1 = s1.WithColor(Red);
            s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
        } else {
            StringBuilder sb1 = new StringBuilder();
            if (building.EnableFracForever() && products.Any(p => p.count > productOutputMax / 2)) {
                s1 = recipe.LvExpWC + "\n" + "永动".Translate();
                s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
            } else {
                foreach (var output in recipe.OutputMain) {
                    float ratio = recipe.SuccessRate * successRatePlus * output.SuccessRate;
                    string name = output.ShowOutputName ? FormatName(LDB.items.Select(output.OutputID).Name) : "???";
                    string count = output.ShowOutputCount ? output.OutputCount.ToString("F3") : "???";
                    string ratioStr = output.ShowSuccessRate ? ratio.FormatP() : "???";
                    sb1.Append($"{name}x{count} ({ratioStr})\n");
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

    // /// <summary>
    // /// 暂时屏蔽分馏塔详情窗口的物品取出
    // /// </summary>
    // [HarmonyTranspiler]
    // [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow.OnProductUIButtonClick))]
    // private static IEnumerable<CodeInstruction> FastStartOptionPatches_SetForNewGame_Transpiler(
    //     IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
    //     //if (itemId == fractionatorComponent.productId)
    //     //变为
    //     //if (false)
    //     var matcher = new CodeMatcher(instructions);
    //     //寻找: if (itemId == fractionatorComponent.productId)
    //     matcher.MatchForward(false,
    //         new CodeMatch(OpCodes.Ldarg_1),// itemId
    //         new CodeMatch(OpCodes.Ldloc_0),// fractionatorComponent
    //         new CodeMatch(OpCodes.Ldfld)// .productId
    //     );
    //     if (matcher.IsInvalid) {
    //         LogError("Failed to find itemId == fractionatorComponent.productId");
    //         return instructions;
    //     }
    //     //找到要跳转的标签
    //     var matcher2 = matcher.Clone();
    //     matcher2.MatchForward(false, new CodeMatch(OpCodes.Bne_Un));
    //     if (matcher2.IsInvalid) {
    //         LogError("Failed to find OpCodes.Bne_Un");
    //         return instructions;
    //     }
    //     // 改为if (false)，也就是IL改为br IL_00b5
    //     matcher.SetAndAdvance(OpCodes.Br, matcher2.Operand);
    //     matcher.SetAndAdvance(OpCodes.Nop, null);
    //     matcher.SetAndAdvance(OpCodes.Nop, null);
    //     matcher.SetAndAdvance(OpCodes.Nop, null);
    //     return matcher.InstructionEnumeration();
    // }

    #endregion
}
