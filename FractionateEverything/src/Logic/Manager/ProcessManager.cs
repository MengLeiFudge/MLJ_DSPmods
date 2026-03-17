using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
        Register("交互模式", "Interaction mode");
        Register("原料堆积", "Fluid overflow");
        Register("搬运模式", "Transport mode");
        Register("缺少精华", "Lack of essence");
        Register("分馏永动", "Frac forever");
        Register("无配方", "No recipe");
        Register("主产物", "Main product");
        Register("副产物", "Append product");
        Register("流动", "Flow");
        Register("损毁", "Destroy");
        Register("流体输出", "Fluid output");
        Register("配方强化", "Recipe enhancement");
    }
    #region Field

    public static int MaxOutputTimes = 2;
    public static int MaxBeltSpeed = 30;
    public static int BaseFracFluidInputCargoMax = 40;
    public static int BaseFracProductOutputMax = 20;
    public static int BaseFracFluidOutputMax = 20;
    private static readonly double[] incTableFixedRatio = new double[Cargo.incTableMilli.Length];
    public static readonly List<ProductOutputInfo> emptyOutputs = [];
    public static readonly int MaxLevel = 12;
    public static readonly float[] ReinforcementSuccessRatioArr = new float[MaxLevel + 1];
    public static readonly float[] ReinforcementBonusArr = new float[MaxLevel + 1];
    public const byte OutputFlagMain = 1 << 0;
    public const byte OutputFlagSide = 1 << 1;
    public const byte OutputFlagFluid = 1 << 2;
    /// <summary>
    /// 累计分馏成功次数（用于任务系统）
    /// </summary>
    public static long totalFractionSuccesses;

    private static readonly ConcurrentDictionary<(int, int), byte> outputFlagDic = [];

    public static byte GetCurrentOutputFlags(this FractionatorComponent fractionator,
        PlanetFactory factory) {

        if (factory == null) return 0;
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return outputFlagDic.TryGetValue((planetId, entityId), out byte flags) ? flags : (byte)0;
    }

    private static void SetCurrentOutputFlags(PlanetFactory factory, int entityId,
        bool main, bool side, bool fluid) {

        if (factory == null) return;
        byte flags = 0;
        if (main) flags |= OutputFlagMain;
        if (side) flags |= OutputFlagSide;
        if (fluid) flags |= OutputFlagFluid;
        outputFlagDic[(factory.planetId, entityId)] = flags;
    }

    #endregion

    static ProcessManager() {
        //强化成功率
        int index = 0;
        float ratio = 0.5f;
        for (int loopCount = 1; index < ReinforcementSuccessRatioArr.Length - 1 && ratio > 0; loopCount++) {
            for (int j = 0; j < loopCount && index < ReinforcementSuccessRatioArr.Length - 1; j++) {
                ReinforcementSuccessRatioArr[index++] = ratio;
            }
            ratio -= 0.05f;
        }
        //强化加成
        for (int i = 1; i < ReinforcementBonusArr.Length; i++) {
            ReinforcementBonusArr[i] = i < 10
                ? 0.001f * i * i + 0.019f * i
                : 0.003f * i * i - 0.019f * i + 0.18f;
        }
    }

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
        BaseFracFluidInputCargoMax = (int)(desc.fracFluidInputMax * ratio);
        BaseFracProductOutputMax = (int)(desc.fracProductOutputMax * ratio * 12 / 4);//todo: 最大堆叠12改为全局
        BaseFracFluidOutputMax = (int)(desc.fracFluidOutputMax * ratio * 12 / 4);

        //增产剂的增产效果修复，因为增产点数对于增产的加成不是线性的，但对于加速的加成是线性的
        for (int i = 1; i < Cargo.incTableMilli.Length; i++) {
            incTableFixedRatio[i] = Cargo.accTableMilli[i] / Cargo.incTableMilli[i];
        }
    }

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
                InternalUpdate<PointAggregateRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.PointAggregate);
                return false;
            case IFE转化塔:
                InternalUpdate<ConversionRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.Conversion);
                return false;
            case IFE回收塔:
                InternalUpdate<RecycleRecipe>(ref __instance, factory, power, signPool, productRegister,
                    consumeRegister, ref __result, ERecipe.Recycle);
                return false;
        }
        //原版分馏塔不做处理
        return true;
    }

    /// <summary>
    /// InternalUpdate的默认实现。
    /// </summary>
    public static void InternalUpdate<T>(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result, ERecipe recipeType) where T : BaseRecipe {
        int buildingID = factory.entityPool[__instance.entityId].protoId;
        //所有产物输出
        List<ProductOutputInfo> products = __instance.products(factory);
        int fluidId = __instance.fluidId;
        T recipe = GetRecipe<T>(recipeType, fluidId);
        //检测products和recipe的输出是否一致
        if (recipe == null) {
            if (products.Count > 0 || __instance.productId != fluidId || __instance.productOutputCount != 0) {
                products.Clear();
                __instance.productId = fluidId;
                __instance.productOutputCount = 0;
                __instance.produceProb = 0.01f;
                signPool[__instance.entityId].iconId0 = 0;
                signPool[__instance.entityId].iconType = 0U;
            }
        } else {
            bool needResetProducts =
                (recipe.OutputMain.Count > 0 && __instance.productId != recipe.OutputMain[0].OutputID)
                || products.Count != recipe.OutputMain.Count + recipe.OutputAppend.Count;
            if (!needResetProducts) {
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
                __instance.productId = recipe.OutputMain.Count > 0 ? recipe.OutputMain[0].OutputID : recipe.InputID;
                __instance.productOutputCount = 0;
                __instance.produceProb = 0.01f;
                signPool[__instance.entityId].iconId0 = (uint)__instance.fluidId;
                signPool[__instance.entityId].iconType = 1U;
                foreach (OutputInfo info in recipe.OutputMain) {
                    products.Add(new(true, info.OutputID, 0));
                }
                foreach (OutputInfo info in recipe.OutputAppend) {
                    products.Add(new(false, info.OutputID, 0));
                }
                // C8: 单路锁定 - 配方变化时清除锁定
                if (buildingID == IFE转化塔) {
                    __instance.SetLockedOutput(factory, 0);
                }
            }
        }
        //第一个主输出，recipe有则必定有，recipe没有则必定没有
        int product0Id = __instance.productId;
        ProductOutputInfo product0 = products.Find(p => p.itemId == product0Id);
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
        ItemProto building = LDB.items.Select(buildingID);
        int fluidInputCargoMax = building.FluidInputCargoMax();
        int productOutputMax = building.ProductOutputMax();
        int fluidOutputMax = building.FluidOutputMax();
        bool enableFracForever = building.EnableFluidEnhancement();
        bool moveDirectly = recipe == null || recipe.Locked;
        bool producedMainThisTick = false;
        bool producedSideThisTick = false;
        bool producedFluidThisTick = false;
        if (__instance.fluidInputCount > 0
            && (products.All(p => p.count < productOutputMax) || enableFracForever)
            && __instance.fluidOutputCount < fluidOutputMax) {
            //分馏塔正常运转时，计算进度，10000点进度可以处理一次
            __instance.progress += (int)(power
                                         * (500.0 / 3.0)
                                         * (__instance.fluidInputCargoCount < MaxBeltSpeed
                                             ? __instance.fluidInputCargoCount
                                             : MaxBeltSpeed)
                                         * fluidInputCountPerCargo
                                         + 0.75);
            if (__instance.progress > 300000) {
                __instance.progress = 300000;
            }
            // 虚空喷涂 - 点数聚集塔在 Level >= 6 时自动补充增产点数
            if (buildingID == IFE点数聚集塔 && PointAggregateTower.EnableVoidSpray) {
                AddIncToItem(__instance.fluidInputCount, ref __instance.fluidInputInc);
            }
            // 质能裂变 - 矿物复制塔在 Level >= 6 时，维持池中点数在目标值以上；
            // 当池量不足时，批量消耗原料填满点数池（每个原料+25点，零压循环激活时+50点）。
            // 取用时：若平均增产点数不足10，从池中补足至10。
            if (buildingID == IFE矿物复制塔
                && MineralReplicationTower.EnableMassEnergyFission
                && __instance.fluidInputCount > 0) {
                int pointsPerItem = MineralReplicationTower.EnableZeroPressureCycle ? 50 : 25;
                int poolTarget = __instance.fluidInputCount * 15;
                int pool = __instance.GetFissionPointPool(factory);
                // 池量不足时批量消耗原料补满
                if (pool <= 0) {
                    int pointsNeeded = poolTarget - pool;
                    int itemsToConsume = (pointsNeeded + pointsPerItem - 1) / pointsPerItem;// 向上取整
                    int itemsAvail = __instance.fluidInputCount;
                    int itemsConsumed = Math.Min(itemsToConsume, itemsAvail);
                    if (itemsConsumed > 0) {
                        int incAvgForConsume = __instance.fluidInputInc > 0 && __instance.fluidInputCount > 0
                            ? __instance.fluidInputInc / __instance.fluidInputCount
                            : 0;
                        __instance.fluidInputCount -= itemsConsumed;
                        if (__instance.fluidInputCount < 0) __instance.fluidInputCount = 0;
                        __instance.fluidInputCargoCount -= (float)itemsConsumed / fluidInputCountPerCargo;
                        if (__instance.fluidInputCargoCount < 0f) __instance.fluidInputCargoCount = 0f;
                        __instance.fluidInputInc -= incAvgForConsume * itemsConsumed;
                        if (__instance.fluidInputInc < 0) __instance.fluidInputInc = 0;
                        pool += itemsConsumed * pointsPerItem;
                        __instance.SetFissionPointPool(factory, pool);
                    }
                }
                // 取用：若输入平均点数不足10，从池中补足
                if (__instance.fluidInputCount > 0) {
                    int avgInc = __instance.fluidInputInc / __instance.fluidInputCount;
                    if (avgInc < 10) {
                        pool = __instance.GetFissionPointPool(factory);
                        int needed = (10 - avgInc) * __instance.fluidInputCount;
                        int toUse = Math.Min(pool, needed);
                        if (toUse > 0) {
                            __instance.fluidInputInc += toUse;
                            __instance.SetFissionPointPool(factory, pool - toUse);
                        }
                    }
                }
            }
            for (; __instance.progress >= 10000; __instance.progress -= 10000) {
                int fluidInputIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;
                if (!__instance.incUsed)
                    __instance.incUsed = fluidInputIncAvg > 0;

                int inputChange;
                List<ProductOutputInfo> outputs;

                // 判断是否直通（永动且满了，或者无配方/配方锁定）
                bool isForcedPassthrough =
                    moveDirectly || (enableFracForever && products.Any(p => p.count >= productOutputMax));
                if (isForcedPassthrough) {
                    inputChange = -1;
                    outputs = emptyOutputs;
                } else {
                    float pointsBonus = (float)MaxTableMilli(fluidInputIncAvg) * building.PlrRatio();
                    float successBoost = building.SuccessBoost();
                    // C8: 单路锁定 - 在调用 GetOutputs 前设置当前锁定产物ID
                    if (buildingID == IFE转化塔) {
                        ConversionRecipe.CurrentLockedOutputId = __instance.GetLockedOutput(factory);
                    }
                    recipe.GetOutputs(ref __instance.seed, pointsBonus, successBoost,
                        fluidInputIncAvg, ref __instance.fluidInputInc, out inputChange, out outputs);
                }

                __instance.fractionSuccess = outputs != null && outputs.Count > 0;

                // 统一结算输入变化
                if (inputChange < 0) {
                    __instance.fluidInputCount--;
                    if (__instance.fluidInputCount < 0) __instance.fluidInputCount = 0;
                    __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                    if (__instance.fluidInputCargoCount < 0f) __instance.fluidInputCargoCount = 0f;
                    // fluidInputInc 的扣除已由 GetOutputs 处理；强制直通时 GetOutputs 未被调用，在此扣除
                    if (outputs == emptyOutputs && isForcedPassthrough) {
                        __instance.fluidInputInc -= fluidInputIncAvg;
                        if (__instance.fluidInputInc < 0) __instance.fluidInputInc = 0;
                    }
                }

                if (outputs == null) {
                    // 因果溯源 - 转化塔在 Level >= 6 时，50%概率不消耗原料
                    bool materialConsumed = true;
                    if (buildingID == IFE转化塔 && ConversionTower.EnableCausalTracing) {
                        if (GetRandDouble(ref __instance.seed) < 0.5f) {
                            materialConsumed = false;
                            __instance.fluidInputCount++;
                            __instance.fluidInputCargoCount += 1.0f / fluidInputCountPerCargo;
                            __instance.fluidInputInc += fluidInputIncAvg;
                        }
                    }
                    if (materialConsumed) {
                        // 损毁，原料消失
                        lock (consumeRegister) {
                            consumeRegister[fluidId]++;
                        }
                    }
                } else if (outputs.Count == 0) {
                    // 直通（无变化）
                    if (inputChange < 0) {
                        __instance.fluidOutputCount++;
                        __instance.fluidOutputTotal++;
                        __instance.fluidOutputInc += fluidInputIncAvg;
                        producedFluidThisTick = true;
                    }
                } else {
                    // 成功产出，产出到产物列表
                    totalFractionSuccesses++;
                    lock (consumeRegister) {
                        consumeRegister[fluidId]++;
                    }
                    __instance.productOutputTotal++;
                    foreach (var p in outputs) {
                        int itemID = p.itemId;
                        int itemCount = p.count;
                        if (p.isMainOutput) producedMainThisTick = true;
                        else producedSideThisTick = true;
                        lock (productRegister) {
                            productRegister[itemID] += itemCount;
                        }
                        if (itemID == product0Id) {
                            product0.count += itemCount;
                            __instance.productOutputCount = product0.count;
                        } else {
                            var target = products.Find(product => product.itemId == itemID);
                            if (target != null) target.count += itemCount;
                            else products.Add(new ProductOutputInfo(p.isMainOutput, itemID, itemCount));
                        }
                    }
                }
            }
        } else {
            __instance.fractionSuccess = false;
        }

        SetCurrentOutputFlags(factory, __instance.entityId,
            producedMainThisTick, producedSideThisTick, producedFluidThisTick);

        // 零压循环 - 矿物复制塔在 Level >= 12 时，将产物和流动输出回流到输入
        if (buildingID == IFE矿物复制塔
            && MineralReplicationTower.EnableZeroPressureCycle) {
            // 稳定运转目标：fluidInput ≈ 360（MaxBeltSpeed×MaxStack），fluidOutput ≈ 24（2×MaxStack）
            int fluidInputTarget = MaxBeltSpeed * MineralReplicationTower.MaxStack;// 360
            int fluidOutputTarget = 2 * MineralReplicationTower.MaxStack;// 24
            bool hasFluidOutputBelt = __instance.belt1 > 0 && __instance.isOutput1
                                      || __instance.belt2 > 0 && __instance.isOutput2;

            // 步骤1：无传送带时，把 fluidOutput 超过 fluidOutputTarget 的部分回填到 fluidInput
            if (!hasFluidOutputBelt) {
                int fluidMoveCount = Math.Max(0, __instance.fluidOutputCount - fluidOutputTarget);
                if (fluidMoveCount > 0) {
                    int fluidOutputIncAvg = __instance.fluidOutputCount > 0
                        ? __instance.fluidOutputInc / __instance.fluidOutputCount
                        : 0;
                    int moveInc = fluidOutputIncAvg * fluidMoveCount;
                    __instance.fluidInputCount += fluidMoveCount;
                    __instance.fluidInputCargoCount = Math.Min(fluidInputCargoMax,
                        __instance.fluidInputCargoCount + (float)fluidMoveCount / fluidInputCountPerCargo);
                    __instance.fluidInputInc += moveInc;
                    __instance.fluidOutputCount -= fluidMoveCount;
                    __instance.fluidOutputInc -= moveInc;
                }
            }

            // 步骤2 & 3：从产物补 fluidOutput 到 fluidOutputTarget，再补 fluidInput 到 fluidInputTarget
            if (recipe != null) {
                ProductOutputInfo mainProduct = products.Find(p => p.itemId == fluidId && p.isMainOutput);
                if (mainProduct != null && mainProduct.count > 0) {
                    int productIncPerItem = recipe.GetOutputInc(fluidId);

                    // 步骤2：补 fluidOutput 到 fluidOutputTarget（无论有无传送带，始终确保24）
                    int needForOutput = Math.Max(0, fluidOutputTarget - __instance.fluidOutputCount);
                    int moveToOutput = Math.Min(mainProduct.count, needForOutput);
                    if (moveToOutput > 0) {
                        __instance.fluidOutputCount += moveToOutput;
                        __instance.fluidOutputInc += productIncPerItem * moveToOutput;
                        mainProduct.count -= moveToOutput;
                        if (mainProduct.itemId == product0Id) {
                            __instance.productOutputCount = mainProduct.count;
                        }
                    }

                    // 步骤3：补 fluidInput 到 fluidInputTarget
                    if (mainProduct.count > 0) {
                        int needForInput = Math.Max(0, fluidInputTarget - __instance.fluidInputCount);
                        int moveToInput = Math.Min(mainProduct.count, needForInput);
                        if (moveToInput > 0) {
                            __instance.fluidInputCount += moveToInput;
                            __instance.fluidInputCargoCount = Math.Min(fluidInputCargoMax,
                                __instance.fluidInputCargoCount + (float)moveToInput / fluidInputCountPerCargo);
                            __instance.fluidInputInc += productIncPerItem * moveToInput;
                            mainProduct.count -= moveToInput;
                            if (mainProduct.itemId == product0Id) {
                                __instance.productOutputCount = mainProduct.count;
                            }
                        }
                    }
                }
            }
        }
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack;
        byte inc;
        if (__instance.belt1 > 0) {
            if (__instance.isOutput1) {
                if (__instance.fluidOutputCount > 0) {
                    // 准备增产点数
                    int fluidOutputIncAvg = __instance.fluidOutputInc / __instance.fluidOutputCount;

                    if (building.EnableFluidEnhancement()) {
                        int fluidStack = building.MaxStack();
                        for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount >= fluidStack; i++) {
                            if (buildingID == IFE点数聚集塔)
                                fluidOutputIncAvg = __instance.fluidOutputInc >= 4 * fluidStack ? 4 : 0;
                            if (cargoTraffic.TryInsertItemAtHead(__instance.belt1, fluidId, (byte)fluidStack,
                                    (byte)Math.Min(255, fluidOutputIncAvg * fluidStack))) {
                                __instance.fluidOutputCount -= fluidStack;
                                __instance.fluidOutputInc -= fluidOutputIncAvg * fluidStack;
                            } else {
                                break;
                            }
                        }
                    } else {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                        if (cargoPath != null) {
                            int outputStack = Mathf.Max(1, Mathf.RoundToInt(fluidInputCountPerCargo));
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount >= outputStack; i++) {
                                if (buildingID == IFE点数聚集塔)
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 4 * outputStack ? 4 : 0;
                                if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo / outputStack - 0.1)),
                                        (byte)outputStack,
                                        (byte)Math.Min(255, fluidOutputIncAvg * outputStack))) {
                                    break;
                                }
                                __instance.fluidOutputCount -= outputStack;
                                __instance.fluidOutputInc -= fluidOutputIncAvg * outputStack;
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < fluidInputCargoMax) {
                if (fluidId > 0) {
                    for (int i = 0; i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax; i++) {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt1, fluidId, null, out stack, out inc) > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount++;
                        } else {
                            break;
                        }
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
                            __instance.productId = needId;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = 0;
                            signPool[__instance.entityId].iconType = 0U;
                        } else {
                            __instance.productId = recipe.OutputMain.Count > 0
                                ? recipe.OutputMain[0].OutputID
                                : recipe.InputID;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = (uint)__instance.fluidId;
                            signPool[__instance.entityId].iconType = 1U;
                            foreach (OutputInfo info in recipe.OutputMain) {
                                products.Add(new(true, info.OutputID, 0));
                            }
                            foreach (OutputInfo info in recipe.OutputAppend) {
                                products.Add(new(false, info.OutputID, 0));
                            }
                        }
                        // 初始拾取一个后，尝试继续拾取同类物品以快速填满
                        for (int i = 1;
                             i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax;
                             i++) {
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt1, needId, null, out stack, out inc)
                                > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount++;
                            } else {
                                break;
                            }
                        }
                    }
                }
            }
        }
        if (__instance.belt2 > 0) {
            if (__instance.isOutput2) {
                if (__instance.fluidOutputCount > 0) {
                    // 准备增产点数
                    int fluidOutputIncAvg = __instance.fluidOutputInc / __instance.fluidOutputCount;

                    if (building.EnableFluidEnhancement()) {
                        int fluidStack = building.MaxStack();
                        for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount >= fluidStack; i++) {
                            if (buildingID == IFE点数聚集塔)
                                fluidOutputIncAvg = __instance.fluidOutputInc >= 4 * fluidStack ? 4 : 0;
                            if (cargoTraffic.TryInsertItemAtHead(__instance.belt2, fluidId, (byte)fluidStack,
                                    (byte)Math.Min(255, fluidOutputIncAvg * fluidStack))) {
                                __instance.fluidOutputCount -= fluidStack;
                                __instance.fluidOutputInc -= fluidOutputIncAvg * fluidStack;
                            } else {
                                break;
                            }
                        }
                    } else {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                        if (cargoPath != null) {
                            int outputStack = Mathf.Max(1, Mathf.RoundToInt(fluidInputCountPerCargo));
                            for (int i = 0; i < MaxOutputTimes && __instance.fluidOutputCount >= outputStack; i++) {
                                if (buildingID == IFE点数聚集塔)
                                    fluidOutputIncAvg = __instance.fluidOutputInc >= 4 * outputStack ? 4 : 0;
                                if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fluidId,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo / outputStack - 0.1)),
                                        (byte)outputStack,
                                        (byte)Math.Min(255, fluidOutputIncAvg * outputStack))) {
                                    break;
                                }
                                __instance.fluidOutputCount -= outputStack;
                                __instance.fluidOutputInc -= fluidOutputIncAvg * outputStack;
                            }
                        }
                    }
                }
            } else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < fluidInputCargoMax) {
                if (fluidId > 0) {
                    for (int i = 0; i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax; i++) {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt2, fluidId, null, out stack, out inc) > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount++;
                        } else {
                            break;
                        }
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
                            __instance.productId = needId;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = 0;
                            signPool[__instance.entityId].iconType = 0U;
                        } else {
                            __instance.productId = recipe.OutputMain.Count > 0
                                ? recipe.OutputMain[0].OutputID
                                : recipe.InputID;
                            __instance.produceProb = 0.01f;
                            signPool[__instance.entityId].iconId0 = (uint)__instance.fluidId;
                            signPool[__instance.entityId].iconType = 1U;
                            foreach (OutputInfo info in recipe.OutputMain) {
                                products.Add(new(true, info.OutputID, 0));
                            }
                            foreach (OutputInfo info in recipe.OutputAppend) {
                                products.Add(new(false, info.OutputID, 0));
                            }
                        }
                        // 初始拾取一个后，尝试继续拾取同类物品以快速填满
                        for (int i = 1;
                             i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax;
                             i++) {
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt2, needId, null, out stack, out inc)
                                > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount++;
                            } else {
                                break;
                            }
                        }
                    }
                }
            }
        }
        bool interactionMode = false;
        if (__instance.belt0 > 0) {
            if (__instance.isOutput0) {
                if (products.Count > 0) {
                    //获取分馏塔产物输出堆叠
                    int productStack = building.MaxStack();
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
                                    (byte)(productStack * (recipe?.GetOutputInc(product.itemId) ?? 0)))) {
                                product.count -= productStack;
                                if (product.itemId == product0Id) {
                                    __instance.productOutputCount = product.count;
                                }
                            }
                        } else if (product.count > 0 && __instance.fluidInputCount == 0) {
                            //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, product.itemId, (byte)product.count,
                                    (byte)(product.count * (recipe?.GetOutputInc(product.itemId) ?? 0)))) {
                                product.count = 0;
                                if (product.itemId == product0Id) {
                                    __instance.productOutputCount = product.count;
                                }
                            }
                        }
                    }
                }
            } else if (buildingID == IFE交互塔
                       && __instance.belt1 <= 0
                       && __instance.belt2 <= 0
                       && products.All(p => p.count == 0)) {
                //正面作为输入，数据传到数据中心。可接受未到最大价值，且GridIndex可见的物品。
                interactionMode = true;
                int interactionItemId =
                    cargoTraffic.TryPickItemAtRear(__instance.belt0, 0, ItemManager.needs, out stack, out inc);
                if (interactionItemId > 0) {
                    AddItemToModData(interactionItemId, stack, inc);
                    __instance.fluidId = interactionItemId;
                    __instance.productId = interactionItemId;
                    __instance.produceProb = 0.01f;
                    signPool[__instance.entityId].iconId0 = (uint)__instance.fluidId;
                    signPool[__instance.entityId].iconType = 1U;
                }
            }
        }

        if (interactionMode) {
            __instance.isWorking = true;
        } else {
            // 如果缓存区全部清空，重置全部
            if (__instance.fluidInputCount == 0
                && __instance.fluidOutputCount == 0
                && products.All(p => p.count == 0)) {
                __instance.fluidId = 0;
                __instance.productId = 0;
                products.Clear();
                signPool[__instance.entityId].iconId0 = 0;
                signPool[__instance.entityId].iconType = 0U;
                // C8: 单路锁定 - 缓存区清空时清除锁定
                if (buildingID == IFE转化塔) {
                    __instance.SetLockedOutput(factory, 0);
                }
            }
            __instance.isWorking = __instance.fluidInputCount > 0
                                   && products.All(p => p.count < productOutputMax)
                                   && __instance.fluidOutputCount < fluidOutputMax
                                   && !moveDirectly;
        }

        __result = !__instance.isWorking ? 0U : 1U;
    }

    #endregion

    #region 分馏塔耗电调整

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTick))]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic._fractionator_parallel))]
    public static IEnumerable<CodeInstruction> FactorySystem_SetPCState_Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase original) {
        CodeMatcher matcher = new(instructions);
        MethodInfo targetMethod =
            AccessTools.Method(typeof(FractionatorComponent), nameof(FractionatorComponent.SetPCState));
        MethodInfo replacementMethod = AccessTools.Method(typeof(ProcessManager), nameof(SetPCStateWithEntityPool));
        FieldInfo factoryField = AccessTools.Field(typeof(FactorySystem), "factory");
        FieldInfo entityPoolField = AccessTools.Field(typeof(PlanetFactory), "entityPool");

        matcher.MatchForward(false, new CodeMatch(i => i.Calls(targetMethod)))
            .Repeat(m => {
                if (original.DeclaringType == typeof(FactorySystem)) {
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0));// FactorySystem
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, factoryField));// PlanetFactory
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, entityPoolField));// EntityData[]
                } else {
                    // GameLogic._fractionator_parallel: planetFactory 是局部变量，通过方法体局部变量列表定位
                    var locals = original.GetMethodBody()!.LocalVariables;
                    var planetFactoryLocal = locals.First(v => v.LocalType == typeof(PlanetFactory));
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S,
                        (byte)planetFactoryLocal.LocalIndex));// PlanetFactory (local var)
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, entityPoolField));// EntityData[]
                }
                m.SetInstruction(new CodeInstruction(OpCodes.Call, replacementMethod));
            });

        return matcher.InstructionEnumeration();
    }

    public static void SetPCStateWithEntityPool(ref FractionatorComponent fractionator, PowerConsumerComponent[] pcPool,
        EntityData[] entityPool) {
        fractionator.SetPCState(pcPool);// 调用原版
        fractionator.SetPCState(pcPool, entityPool);// 调用我们的逻辑
    }

    public static void SetPCState(this ref FractionatorComponent fractionator,
        PowerConsumerComponent[] pcPool, EntityData[] entityPool) {
        int buildingID = entityPool[fractionator.entityId].protoId;
        if (buildingID < IFE交互塔 || buildingID > IFE回收塔) {
            return;
        }
        ItemProto building = LDB.items.Select(buildingID);
        double num1 = fractionator.fluidInputCargoCount > 0.0001
            ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
            : 4.0;
        double num2 = (double)fractionator.fluidInputCargoCount < MaxBeltSpeed
            ? (double)fractionator.fluidInputCargoCount
            : MaxBeltSpeed;
        num2 = num2 * num1 - MaxBeltSpeed;
        if (num2 < 0.0)
            num2 = 0.0;
        double powerRatio = buildingID switch {
            IFE点数聚集塔 => 1.0,
            _ => Cargo.powerTableRatio[fractionator.incLevel] * building.EnergyRatio()
        };
        pcPool[fractionator.pcId].workEnergyPerTick = building.workEnergyPerTick();
        pcPool[fractionator.pcId].idleEnergyPerTick = building.idleEnergyPerTick();
        int permillage = (int)((num2 * 50.0 * 30.0 / MaxBeltSpeed + 1000.0) * powerRatio + 0.5);
        pcPool[fractionator.pcId].SetRequiredEnergy(fractionator.isWorking, permillage);
    }

    /// <summary>
    /// 交互塔特质
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameData_FixedUpdate_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (GameMain.gameTick % 60 != 3) {
            return;
        }
        if (!InteractionTower.EnableSacrificeTrait) {
            return;
        }
        int buffCount = 0;
        int[] takeCounts = new int[IFE回收塔 - IFE交互塔 + 1];
        for (int i = 0; i < takeCounts.Length; i++) {
            takeCounts[i] = Take10PercentTower(IFE交互塔 + i);
            if (takeCounts[i] > 0) {
                buffCount++;
            }
        }
        if (InteractionTower.EnableDimensionalResonance) {
            for (int i = 0; i < takeCounts.Length; i++) {
                takeCounts[i] = (int)(takeCounts[i] * (1 + 0.1 * buffCount));
            }
        }
        InteractionTower.SuccessBoost = Mathf.Sqrt(takeCounts[0]) / 10.0f;
        MineralReplicationTower.SuccessBoost = Mathf.Sqrt(takeCounts[1]) / 10.0f;
        PointAggregateTower.SuccessBoost = Mathf.Sqrt(takeCounts[2]) / 10.0f;
        ConversionTower.SuccessBoost = Mathf.Sqrt(takeCounts[3]) / 10.0f;
        RecycleTower.SuccessBoost = Mathf.Sqrt(takeCounts[4]) / 10.0f;
    }

    #endregion

    #region IModCanSave

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalFractionSuccesses", bw => bw.Write(totalFractionSuccesses))
        );
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("TotalFractionSuccesses", br => totalFractionSuccesses = br.ReadInt64())
        );
    }

    public static void IntoOtherSave() {
        totalFractionSuccesses = 0;
    }

    #endregion

}
