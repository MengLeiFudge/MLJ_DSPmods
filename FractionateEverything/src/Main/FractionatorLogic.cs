using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.FractionateEverything;
using static FractionateEverything.Main.FractionateRecipes;
using Random = System.Random;

namespace FractionateEverything.Main {
    /// <summary>
    /// 修改所有分馏塔的处理逻辑，以及对应的显示。
    /// </summary>
    public static class FractionatorLogic {
        /// <summary>
        /// 默认分馏概率，即1%分馏出1个物品。在此处定义可以
        /// </summary>
        public static readonly Dictionary<int, float> defaultFracNumRatioDic = new() { { 1, 0.01f } };
        private static uint seed2 = (uint)new Random().Next(int.MinValue, int.MaxValue);
        private static uint seed3 = (uint)new Random().Next(int.MinValue, int.MaxValue);
        private static int totalUIUpdateTimes = 0;
        private static int fractionatorID = 0;
        private static bool isFirstUpdateUI = true;
        private static Vector3 productProbTextPos;
        private static Vector3 oriProductProbTextPos;
        private static string lastProductProbText = "";
        private static string lastOriProductProbText = "";
        private static int MaxStackSize = 1;
        public static int MaxInputTimes = 1;
        public static int MaxOutputTimes = 2;
        public static int[] beltSpeed = [6, 12, 30];
        public static int MaxBeltSpeed = 30;
        public static float k1, b1, k2, b2;
        public static int FracFluidInputMax = 40;
        public static int FracProductOutputMax = 20;
        public static int FracFluidOutputMax = 20;

        public static void Init() {
            //获取一二三级传送带速度，并生成精准分馏塔系数
            beltSpeed = [
                LDB.items.Select(I传送带).prefabDesc.beltSpeed * 6,
                LDB.items.Select(I高速传送带).prefabDesc.beltSpeed * 6,
                LDB.items.Select(I极速传送带).prefabDesc.beltSpeed * 6,
            ];
            k1 = (2.0f - 3.0f) / (beltSpeed[1] - beltSpeed[0]);
            b1 = 3.0f - k1 * beltSpeed[0];
            k2 = (1.0f - 2.0f) / (beltSpeed[2] - beltSpeed[1]);
            b2 = 2.0f - k2 * beltSpeed[1];

            //获取传送带的最大速度，以此决定循环的最大次数以及缓存区大小
            MaxBeltSpeed = (from item in LDB.items.dataArray
                where item.Type == EItemType.Logistics && item.prefabDesc.isBelt
                select item.prefabDesc.beltSpeed * 6).Prepend(0).Max();
            MaxInputTimes = (int)Math.Ceiling(MaxBeltSpeed / 60.0);
            MaxOutputTimes = (int)Math.Ceiling(MaxBeltSpeed / 15.0);
            float ratio = MaxBeltSpeed / 30.0f;
            FracFluidInputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracFluidInputMax * ratio);
            FracProductOutputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracProductOutputMax * ratio);
            FracFluidOutputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracFluidOutputMax * ratio);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "Import")]
        public static void GameHistoryData_Import_Postfix() {
            //遍历所有已解锁的科技，如果科技已解锁但是配方未解锁，则解锁配方
            foreach (TechProto t in LDB.techs.dataArray) {
                if (GameMain.history.TechUnlocked(t.ID)) {
                    foreach (int recipeID in t.UnlockRecipes) {
                        if (!GameMain.history.RecipeUnlocked(recipeID)) {
                            GameMain.history.recipeUnlocked.Add(recipeID);
                            RecipeProto r = LDB.recipes.Select(recipeID);
                            LogInfo($"Recipe {r.ID} {r.name} unlocked.");
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), "GameTick")]
        public static void GameData_GameTick_Postfix(long time) {
            //使用3作为特殊值，每10逻辑帧更新一次
            if (time % 10 != 3 || GameMain.history == null) {
                return;
            }
            //从科技获取产物输出的最大堆叠数目
            int maxStack = 1;
            for (int i = 0; i < 3; i++) {
                if (GameMain.history.TechUnlocked(TFE分馏塔产物集装物流 + i)) {
                    maxStack++;
                }
            }
            MaxStackSize = maxStack;
            //更新普通分馏塔可接受的物品
            int oldLen = RecipeProto.fractionatorRecipes.Length;
            RecipeProto[] dataArray = LDB.recipes.dataArray;
            List<RecipeProto> list = [];
            List<int> list2 = [];
            foreach (RecipeProto r in dataArray) {
                if (r.Type != ERecipeType.Fractionate) {
                    continue;
                }
                int inputID = r.Items[0];
                int outputID = r.Results[0];
                if (inputID is >= I黑雾矩阵 and <= I能量碎片
                    && outputID is >= I黑雾矩阵 and <= I能量碎片) {
                    //如果全为黑雾独有掉落，需要确保这两个黑雾物品都已解锁黑雾掉落
                    if (!GameMain.history.enemyDropItemUnlocked.Contains(inputID)
                        || !GameMain.history.enemyDropItemUnlocked.Contains(outputID)) {
                        GameMain.history.recipeUnlocked.Remove(r.ID);
                        continue;
                    }
                    GameMain.history.recipeUnlocked.Add(r.ID);
                }
                if (GameMain.history.RecipeUnlocked(r.ID)) {
                    list.Add(r);
                    list2.Add(r.Items[0]);
                }
            }
            int currLen = list.Count;
            if (oldLen != currLen) {
                RecipeProto.fractionatorRecipes = list.ToArray();
                RecipeProto.fractionatorNeeds = list2.ToArray();
                LogInfo($"RecipeProto.fractionatorRecipes.Length: {oldLen} -> {currLen}");
            }
        }

        /// <summary>
        /// 计算精准分馏塔内部产物数目对速率的影响。
        /// </summary>
        private static float ProcessNum2Ratio(int processNum) {
            if (processNum <= beltSpeed[0]) {
                return 3.0f;
            }
            if (processNum <= beltSpeed[1]) {
                return k1 * processNum + b1;
            }
            float ret = k2 * processNum + b2;
            return ret < 0 ? 0 : ret;
        }

        // static Stopwatch sw1 = new Stopwatch();
        // static Stopwatch sw2 = new Stopwatch();
        // static Stopwatch sw3 = new Stopwatch();
        // static Stopwatch sw4 = new Stopwatch();

        /// <summary>
        /// 修改分馏塔的运行逻辑。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FractionatorComponent), "InternalUpdate")]
        public static bool FractionatorComponent_InternalUpdate_Prefix(ref FractionatorComponent __instance,
            PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
            ref uint __result) {
            // bool record = __instance.seed < 100000;
            // if (record) {
            //     sw1.Restart();
            // }

            if (power < 0.1f) {
                __result = 0u;
                return false;
            }

            int buildingID = factory.entityPool[__instance.entityId].protoId;
            bool isSpecialFractionator = buildingID is IFE点数聚集分馏塔 or IFE增产分馏塔;
            //配方状态错误的情况下，修改配方状态（在分馏塔升降级时会有这样的现象）
            if (isSpecialFractionator) {
                if (__instance.productId != __instance.fluidId) {
                    __instance.productId = __instance.fluidId;
                    __instance.produceProb = 0.01f;
                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                }
            }
            else {
                if (__instance.productId == __instance.fluidId
                    && !fracSelfRecipeList.Contains(__instance.fluidId)) {
                    int tempID = __instance.fluidId;
                    __instance.fluidId = 0;
                    __instance.productId = 0;
                    __instance.SetRecipe(tempID, signPool);
                }
            }
            int inputItemID = __instance.fluidId;
            int outputItemID = __instance.productId;

            //输入货物的平均堆叠数目，后面输出时候会用
            float fluidInputCountPerCargo = 1.0f;
            if (__instance.fluidInputCount == 0) {
                __instance.fluidInputCargoCount = 0f;
            }
            else {
                fluidInputCountPerCargo = __instance.fluidInputCargoCount > 0.0001
                    ? __instance.fluidInputCount / __instance.fluidInputCargoCount
                    : 4f;
            }

            if (__instance.fluidInputCount > 0
                && __instance.productOutputCount < __instance.productOutputMax
                && __instance.fluidOutputCount < __instance.fluidOutputMax) {
                //缓存区每有MaxBeltSpeed个氢，就执行一次分馏，4堆叠就是4次
                int progressAdd = (int)(power
                                        * 166.66666666666666
                                        * (__instance.fluidInputCargoCount < MaxBeltSpeed
                                            ? __instance.fluidInputCargoCount
                                            : MaxBeltSpeed)
                                        * fluidInputCountPerCargo
                                        + 0.75);
                __instance.progress += progressAdd;
                //每次尝试处理一个输入的产物，至多十次；x堆叠就是xw左右，也就是处理x-1次或x次
                if (__instance.progress > 100000) {
                    __instance.progress = 100000;
                }
                while (__instance.progress >= 10000) {

                    #region 判断某一个输入的分馏结果

                    int fluidInputAvgInc = __instance is { fluidInputInc: > 0, fluidInputCount: > 0 }
                        ? __instance.fluidInputInc / __instance.fluidInputCount
                        : 0;
                    __instance.seed =
                        (uint)((int)((ulong)((__instance.seed % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                    //分馏成功率，越接近0表示成功率越高
                    float randomVal = (float)(__instance.seed / 2147483646.0);
                    //分馏成功率加成，2.0表示加成100%
                    float successRatePlus = 1.0f;
                    if (!isSpecialFractionator) {
                        //常规分馏塔使用增产剂可以提升分馏成功率
                        successRatePlus *=
                            1.0f + (float)Cargo.accTableMilli[fluidInputAvgInc < 10 ? fluidInputAvgInc : 10];
                    }
                    if (buildingID == IFE精准分馏塔) {
                        //精准分馏塔正在处理的原料越多，效率越低
                        float ratio = ProcessNum2Ratio(__instance.fluidInputCount);
                        successRatePlus *= ratio;
                    }
                    else if (buildingID == IFE建筑极速分馏塔) {
                        //建筑极速分馏塔对建筑成功率提升，对非建筑成功率下降
                        var item = LDB.items.Select(inputItemID);
                        //BuildMode0-5都有，0是不可放置的物品
                        successRatePlus = item.BuildMode == 0 ? successRatePlus / 5.0f : successRatePlus * 5;
                    }
                    //如果未找到配方，则使用默认概率
                    int outputNum;
                    if (buildingID == IFE增产分馏塔) {
                        //如果有自分馏配方，则使用自分馏配方的概率；否则使用默认概率
                        if (!fracSelfRecipeList.Contains(inputItemID)) {
                            outputNum = GetOutputNum(randomVal, successRatePlus);
                        }
                        else {
                            outputNum = GetOutputNum(randomVal, successRatePlus, fracRecipeNumRatioDic[inputItemID]);
                        }
                        //增产剂可以提升产物数目
                        seed2 = (uint)((int)((ulong)((seed2 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                        bool outputfloat = seed2 / 2147483646.0
                                           < Cargo.incTableMilli[fluidInputAvgInc < 10 ? fluidInputAvgInc : 10];
                        if (outputfloat) {
                            outputNum *= 2;
                        }
                    }
                    else if (buildingID == IFE点数聚集分馏塔) {
                        //强制使用默认概率，与配方无关
                        outputNum = GetOutputNum(randomVal, successRatePlus);
                        if (outputNum > 0) {
                            //至多将点数聚集到四个物品上
                            outputNum = Math.Min(__instance.fluidInputInc / 10, 4);
                        }
                    }
                    else {
                        //普通塔需要根据配方确定输出
                        outputNum = GetOutputNum(randomVal, successRatePlus, fracRecipeNumRatioDic[inputItemID]);
                    }
                    __instance.fractionSuccess = outputNum > 0;

                    #endregion

                    #region 根据分馏结果处理原料输入、原料输出、产物输出

                    if (buildingID == IFE点数聚集分馏塔 && __instance.fractionSuccess) {
                        //点数聚集分馏塔分馏成功时，会扣除多个原料输入
                        __instance.fluidInputCount -= outputNum;
                        __instance.fluidInputInc -= 10 * outputNum;
                        __instance.fluidInputCargoCount -= outputNum / fluidInputCountPerCargo;
                        if (__instance.fluidInputCargoCount < 0f) {
                            __instance.fluidInputCargoCount = 0f;
                        }
                    }
                    else {
                        //其他情况都扣除一个原料输入（任意塔分馏失败，或者非点数塔分馏成功）
                        __instance.fluidInputCount--;
                        __instance.fluidInputInc -= fluidInputAvgInc;
                        __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                        if (__instance.fluidInputCargoCount < 0f) {
                            __instance.fluidInputCargoCount = 0f;
                        }
                    }

                    if (outputNum > 0) {
                        //分馏成功
                        __instance.productOutputCount += outputNum;
                        __instance.productOutputTotal += outputNum;
                        //点数聚集分馏塔不计算生成、消耗；增产塔只计算多生成的部分
                        switch (buildingID) {
                            case IFE点数聚集分馏塔:
                                break;
                            case IFE增产分馏塔:
                                if (outputNum > 1) {
                                    lock (productRegister) {
                                        productRegister[outputItemID] += outputNum - 1;
                                    }
                                }
                                break;
                            default:
                                lock (consumeRegister) {
                                    consumeRegister[inputItemID]++;
                                }
                                lock (productRegister) {
                                    productRegister[outputItemID] += outputNum;
                                }
                                break;
                        }
                    }
                    else if (outputNum == 0) {
                        //分馏失败，保留原料
                        __instance.fluidOutputCount++;
                        __instance.fluidOutputTotal++;
                        __instance.fluidOutputInc += fluidInputAvgInc;
                    }
                    else {
                        //分馏失败，损毁原料
                        //这个分支只有普通分馏塔才会进入，所以无需判断是否为特殊分馏塔
                        lock (consumeRegister) {
                            consumeRegister[inputItemID]++;
                        }
                    }

                    #endregion

                    __instance.progress -= 10000;
                }
            }
            else {
                __instance.fractionSuccess = false;
            }

            // int process1 = 0;
            // int process1_ = 0;
            // int process2 = 0;
            // int process2_ = 0;
            // int process3 = 0;
            // int process3_ = 0;
            // if (record) {
            //     sw1.Stop();
            //     sw2.Restart();
            // }
            CargoTraffic cargoTraffic = factory.cargoTraffic;
            byte inputStack;
            byte inputInc;
            if (__instance.belt1 > 0) {
                if (__instance.isOutput1) {
                    if (__instance.fluidOutputCount > 0) {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                        if (cargoPath != null) {
                            //原版传送带最大速率为30，需要每帧判定2次；创世传送带最大速率为60，需要每帧判定4次
                            for (int i = 0; i < MaxOutputTimes; i++) {
                                if (__instance.fluidOutputCount <= 0) {
                                    break;
                                }
                                int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                // process1++;
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                        (byte)fluidOutputAvgInc)) {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= fluidOutputAvgInc;
                                    // process1_++;
                                }
                                else {
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
                    for (int i = 0; i < MaxInputTimes; i++) {
                        if (__instance.fluidInputCargoCount >= __instance.fluidInputMax) {
                            break;
                        }
                        if (inputItemID > 0) {
                            // process1++;
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt1, inputItemID, null,
                                    out inputStack, out inputInc)
                                > 0) {
                                // process1_++;
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                            }
                            else {
                                break;
                            }
                        }
                        else {
                            int newInputItemID = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0,
                                isSpecialFractionator ? null : RecipeProto.fractionatorNeeds,
                                out inputStack, out inputInc);
                            if (newInputItemID > 0) {
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                                if (isSpecialFractionator) {
                                    //特殊处理一下，不能走SetRecipe，不然找不到配方
                                    __instance.fluidId = newInputItemID;
                                    __instance.productId = newInputItemID;
                                    __instance.produceProb = 0.01f;
                                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                                }
                                else {
                                    __instance.SetRecipe(newInputItemID, signPool);
                                }
                            }
                            else {
                                break;
                            }
                        }
                    }
                }
            }
            // if (record) {
            //     sw2.Stop();
            //     sw3.Restart();
            // }

            if (__instance.belt2 > 0) {
                if (__instance.isOutput2) {
                    if (__instance.fluidOutputCount > 0) {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                        if (cargoPath != null) {
                            //原版传送带最大速率为30，需要每帧判定2次；创世传送带最大速率为60，需要每帧判定4次
                            for (int i = 0; i < MaxOutputTimes; i++) {
                                if (__instance.fluidOutputCount <= 0) {
                                    break;
                                }
                                int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                // process2++;
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                        Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                        (byte)fluidOutputAvgInc)) {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= fluidOutputAvgInc;
                                    // process2_++;
                                }
                                else {
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
                    for (int i = 0; i < MaxInputTimes; i++) {
                        if (__instance.fluidInputCargoCount >= __instance.fluidInputMax) {
                            break;
                        }
                        if (inputItemID > 0) {
                            // process2++;
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt2, inputItemID, null,
                                    out inputStack, out inputInc)
                                > 0) {
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                                // process2_++;
                            }
                            else {
                                break;
                            }
                        }
                        else {
                            //特殊分馏塔接受所有物品
                            int newInputItemID = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0,
                                isSpecialFractionator ? null : RecipeProto.fractionatorNeeds,
                                out inputStack, out inputInc);
                            if (newInputItemID > 0) {
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                                if (isSpecialFractionator) {
                                    //特殊处理一下，不能走SetRecipe，不然找不到配方
                                    __instance.fluidId = newInputItemID;
                                    __instance.productId = newInputItemID;
                                    __instance.produceProb = 0.01f;
                                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                                }
                                else {
                                    __instance.SetRecipe(newInputItemID, signPool);
                                }
                            }
                            else {
                                break;
                            }
                        }
                    }
                }
            }

            // if (record) {
            //     sw3.Stop();
            //     sw4.Restart();
            // }
            if (__instance is { belt0: > 0, isOutput0: true }) {
                for (int i = 0; i < MaxOutputTimes; i++) {
                    //只有产物数目到达堆叠要求，或者没有正在处理的物品，才输出，且一次输出最大堆叠个数的物品
                    if (__instance.productOutputCount >= MaxStackSize) {
                        //产物达到最大堆叠数目，直接尝试输出
                        // process3++;
                        if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)MaxStackSize,
                                (byte)(buildingID == IFE点数聚集分馏塔 ? 10 * MaxStackSize : 0))) {
                            __instance.productOutputCount -= MaxStackSize;
                            // process3_++;
                        }
                        else {
                            break;
                        }
                    }
                    else if (__instance is { productOutputCount: > 0, fluidInputCount: 0 }) {
                        //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                        if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)__instance.productOutputCount,
                                (byte)(buildingID == IFE点数聚集分馏塔 ? 10 * __instance.productOutputCount : 0))) {
                            __instance.productOutputCount = 0;
                        }
                        else {
                            break;
                        }
                    }
                    else {
                        break;
                    }
                }
            }

            if (__instance is { fluidInputCount: 0, fluidOutputCount: 0, productOutputCount: 0 }) {
                __instance.fluidId = 0;
                __instance.productId = 0;
                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
            }

            __instance.isWorking = __instance.fluidInputCount > 0
                                   && __instance.productOutputCount < __instance.productOutputMax
                                   && __instance.fluidOutputCount < __instance.fluidOutputMax;
            // if (record) {
            //     sw4.Stop();
            //     if (sw2.ElapsedTicks > 30 || sw3.ElapsedTicks > 30 || sw4.ElapsedTicks > 30) {
            //         if (__instance.isOutput1) {
            //             LogInfo(
            //                 $"input={sw3.ElapsedTicks},{process2},{process2_} output={sw2.ElapsedTicks},{process1},{process1_} product={sw4.ElapsedTicks},{process3},{process3_}");
            //         }
            //         else {
            //             LogInfo(
            //                 $"input={sw2.ElapsedTicks},{process1},{process1_} output={sw3.ElapsedTicks},{process2},{process2_} product={sw4.ElapsedTicks},{process3},{process3_}");
            //         }
            //     }
            // }
            if (!__instance.isWorking) {
                __result = 0u;
                return false;
            }

            __result = 1u;
            return false;
        }

        /// <summary>
        /// 获取输出产物数目。
        /// -1表示原料消耗；0表示原料不变，流动输出；＞0表示生成的产物数目。
        /// 如果丢失概率为x%且分馏概率为1%，生成物大约是产物的1/(1+x)
        /// </summary>
        /// <param name="randomVal">0-1的随机数</param>
        /// <param name="successRatePlus">概率加成，例如增产剂提速100%，则传入2.0</param>
        /// <param name="fracNumRatioDic">分馏成功率与产物数</param>
        /// <returns></returns>
        private static int GetOutputNum(float randomVal, float successRatePlus,
            Dictionary<int, float> fracNumRatioDic = null) {
            if (fracNumRatioDic == null) {
                randomVal /= successRatePlus;
                return randomVal < 0.01 ? 1 : 0;
            }
            //原料丢失判定
            if (enableDestroy && fracNumRatioDic.TryGetValue(-1, out float missRate)) {
                seed3 = (uint)((int)((ulong)((seed3 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                if (seed3 / 2147483646.0 < missRate) {
                    return -1;
                }
            }
            //分馏是否成功判定
            float value = 0;
            randomVal /= successRatePlus;
            foreach (KeyValuePair<int, float> p in fracNumRatioDic) {
                if (p.Key == -1) {
                    continue;//不要用linq，效率低
                }
                if (randomVal < value + p.Value) {
                    return p.Key;
                }
                value += p.Value;
            }
            return 0;
        }

        /// <summary>
        /// 修改分馏塔的分馏成功率显示。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIFractionatorWindow), "_OnUpdate")]
        public static void UIFractionatorWindow__OnUpdate_Postfix(ref UIFractionatorWindow __instance) {
            //标题改为横向拓展，避免英文无法完全显示
            __instance.titleText.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (__instance.fractionatorId == 0 || __instance.factory == null) {
                return;
            }
            FractionatorComponent fractionatorComponent =
                __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
            if (fractionatorComponent.id != __instance.fractionatorId) {
                return;
            }
            if (fractionatorComponent.fluidId == 0) {
                return;
            }
            //当持续查看同一个塔的状态时，每20帧（通常为0.333s）刷新UI，防止UI变化过快导致无法看清
            if (__instance.fractionatorId == fractionatorID) {
                totalUIUpdateTimes++;
                if (totalUIUpdateTimes < 20) {
                    __instance.productProbText.text = lastProductProbText;
                    __instance.oriProductProbText.text = lastOriProductProbText;
                    return;
                }
            }
            else {
                fractionatorID = __instance.fractionatorId;
            }
            totalUIUpdateTimes = 0;

            //整理要显示的内容
            int buildingID = __instance.factory.entityPool[fractionatorComponent.entityId].protoId;
            float successRatePlus = 1.0f;
            float extraIncProduceProb = 0.0f;
            switch (buildingID) {
                case IFE精准分馏塔:
                    successRatePlus *= 1.0f + fractionatorComponent.extraIncProduceProb;
                    int currProcessNum = fractionatorComponent.fluidInputCount;
                    successRatePlus = Math.Max(successRatePlus * ProcessNum2Ratio(currProcessNum), 0);
                    break;
                case IFE建筑极速分馏塔:
                    successRatePlus *= 1.0f + fractionatorComponent.extraIncProduceProb;
                    var item = LDB.items.Select(fractionatorComponent.fluidId);
                    successRatePlus = item.BuildMode == 0 ? successRatePlus / 5.0f : successRatePlus * 5.0f;
                    break;
                case I分馏塔_FE通用分馏塔:
                    successRatePlus *= 1.0f + fractionatorComponent.extraIncProduceProb;
                    break;
                case IFE增产分馏塔:
                    if (fractionatorComponent is { fluidInputCount: > 0, fluidInputInc: > 0 }) {
                        int num = fractionatorComponent.fluidInputInc / fractionatorComponent.fluidInputCount;
                        int index = num < 10 ? num : 10;
                        extraIncProduceProb = (float)Cargo.incTableMilli[index];
                    }
                    break;
            }
            //获取基础分馏概率表
            Dictionary<int, float> dic;
            switch (buildingID) {
                case IFE点数聚集分馏塔:
                    dic = defaultFracNumRatioDic;
                    break;
                case IFE增产分馏塔:
                    dic = fracSelfRecipeList.Contains(fractionatorComponent.fluidId)
                        ? fracRecipeNumRatioDic[fractionatorComponent.fluidId]
                        : defaultFracNumRatioDic;
                    break;
                default:
                    if (!fracRecipeNumRatioDic.TryGetValue(fractionatorComponent.fluidId, out dic)) {
                        dic = defaultFracNumRatioDic;
                    }
                    break;
            }
            //根据建筑类型以及输入情况，计算实际分馏概率表
            float flowRatio = 1.0f;
            StringBuilder sb1 = new StringBuilder();
            int sb1LineNum = 0;
            if (buildingID == IFE点数聚集分馏塔) {
                int num = Math.Min(4, fractionatorComponent.fluidInputInc / 10);
                sb1.Append($"1({num}%)\n");
                sb1LineNum++;
                flowRatio -= 0.01f * num;
            }
            else {
                var tempDic = new Dictionary<int, float>();
                foreach (var p in dic) {
                    if (p.Key < 0) {
                        continue;
                    }
                    float ratio = p.Value * successRatePlus;
                    //增产分馏塔比较特殊，因为增加的概率可能与原有的概率重叠，所以需要整合后再显示
                    if (buildingID == IFE增产分馏塔) {
                        float ratioBase = ratio * (1.0f - extraIncProduceProb);
                        if (tempDic.ContainsKey(p.Key)) {
                            tempDic[p.Key] += ratioBase;
                        }
                        else {
                            tempDic.Add(p.Key, ratioBase);
                        }
                        if (extraIncProduceProb > 0) {
                            float ratiofloat = ratio * extraIncProduceProb;
                            if (tempDic.ContainsKey(p.Key * 2)) {
                                tempDic[p.Key * 2] += ratiofloat;
                            }
                            else {
                                tempDic.Add(p.Key * 2, ratiofloat);
                            }
                        }
                    }
                    else {
                        sb1.Append($"{p.Key}({ratio:0.###%})\n");
                        sb1LineNum++;
                    }
                    flowRatio -= ratio;
                }
                if (buildingID == IFE增产分馏塔) {
                    foreach (var p in tempDic) {
                        sb1.Append($"{p.Key}({p.Value:0.###%})\n");
                        sb1LineNum++;
                    }
                }
            }
            //获取损毁概率
            dic.TryGetValue(-1, out float destroyRatio);
            if (!enableDestroy) {
                destroyRatio = 0.0f;
            }

            //显示处理后的所有概率
            if (isFirstUpdateUI) {
                //改为纵向拓展，记录初始位置
                __instance.productProbText.verticalOverflow = VerticalWrapMode.Overflow;
                __instance.oriProductProbText.verticalOverflow = VerticalWrapMode.Overflow;
                productProbTextPos = __instance.productProbText.transform.position;
                oriProductProbTextPos = __instance.oriProductProbText.transform.position;
                isFirstUpdateUI = false;
            }
            string s1 = sb1.ToString().Substring(0, sb1.Length - 1);
            Vector3 pos = productProbTextPos;
            pos.y += 0.085f * (sb1LineNum - 1);
            __instance.productProbText.transform.position = pos;
            string s2 = $"{"流动".Translate()}({flowRatio:0.###%})";
            pos = oriProductProbTextPos;
            if (destroyRatio > 0) {
                pos.y -= 0.085f;
                s2 += $"\n{"损毁".Translate()}({destroyRatio:0.###%})";
            }
            __instance.oriProductProbText.transform.position = pos;
            __instance.productProbText.text = s1;
            __instance.oriProductProbText.text = s2;
            lastProductProbText = s1;
            lastOriProductProbText = s2;
        }
    }
}
