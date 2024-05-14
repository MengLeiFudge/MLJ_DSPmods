using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.FractionateEverything;
using Random = System.Random;

namespace FractionateEverything.Main {
    /// <summary>
    /// 修改所有分馏塔的处理逻辑，以及对应的显示。
    /// </summary>
    public static class FractionatorLogic {
        private static uint seed2 = (uint)new Random().Next(int.MinValue, int.MaxValue);
        private static uint seed3 = (uint)new Random().Next(int.MinValue, int.MaxValue);
        private static int totalUIUpdateTimes = 0;
        private static bool isFirstUpdateUI = true;
        private static Vector3 productProbTextPos;
        private static Vector3 oriProductProbTextPos;
        private static string lastProductProbText = "";
        private static string lastOriProductProbText = "";
        private static int MaxStackSize = 1;
        public static int MaxInputTimes = 1;
        public static int MaxOutputTimes = 2;
        public static int[] beltSpeed = [6, 12, 30];
        public static double k1, b1, k2, b2;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), "GameTick")]
        public static void GameTickPostPatch(long time) {
            if (time % 30 == 0 || GameMain.history == null) {
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
                    //全为黑雾物品，需要确保这两个黑雾物品均能拾取
                    //普通物品是否解锁使用history.ItemUnlocked，黑雾物品使用history.ItemCanDropByEnemy
                    if (!GameMain.history.ItemCanDropByEnemy(inputID)
                        || !GameMain.history.ItemCanDropByEnemy(outputID)) {
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
            RecipeProto.fractionatorRecipes = [.. list];
            RecipeProto.fractionatorNeeds = [.. list2];
            int currLen = RecipeProto.fractionatorRecipes.Length;
            if (oldLen != currLen) {
                LogInfo($"RecipeProto.fractionatorRecipes.Length: {oldLen} -> {currLen}");
            }
        }

        /// <summary>
        /// 计算精准分馏塔内部产物数目对速率的影响。
        /// </summary>
        private static double ProcessNum2Ratio(int processNum) {
            if (processNum <= beltSpeed[0]) {
                return 3.0;
            }
            if (processNum <= beltSpeed[1]) {
                return k1 * processNum + b1;
            }
            return Math.Max(0, k2 * processNum + b2);
        }

        /// <summary>
        /// 修改分馏塔的运行逻辑。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FractionatorComponent), "InternalUpdate")]
        public static bool FractionatorUpdatePatch(ref FractionatorComponent __instance, PlanetFactory factory,
            float power, SignData[] signPool, int[] productRegister, int[] consumeRegister, ref uint __result) {
            if (power < 0.1f) {
                __result = 0u;
                return false;
            }

            int buildingID = factory.entityPool[__instance.entityId].protoId;
            bool isSpecialFractionator = buildingID is IFE点数聚集分馏塔 or IFE增产分馏塔;
            int outputItemID = isSpecialFractionator ? __instance.fluidId : __instance.productId;

            double num = 1.0;
            if (__instance.fluidInputCount == 0) {
                __instance.fluidInputCargoCount = 0f;
            }
            else {
                num = __instance.fluidInputCargoCount > 0.0001
                    ? __instance.fluidInputCount / __instance.fluidInputCargoCount
                    : 4f;
            }

            if (__instance.fluidInputCount > 0
                && __instance.productOutputCount < __instance.productOutputMax
                && __instance.fluidOutputCount < __instance.fluidOutputMax) {
                int num2 = (int)(power
                                 * 166.66666666666666
                                 * (__instance.fluidInputCargoCount < 30f
                                     ? __instance.fluidInputCargoCount
                                     : 30f)
                                 * num
                                 + 0.75);
                __instance.progress += num2;
                if (__instance.progress > 100000) {
                    __instance.progress = 100000;
                }
                while (__instance.progress >= 10000) {

                    #region 判断某一个输入的分馏结果

                    //尝试处理一个输入的产物
                    int fluidInputAvgInc = __instance is { fluidInputInc: > 0, fluidInputCount: > 0 }
                        ? __instance.fluidInputInc / __instance.fluidInputCount
                        : 0;
                    __instance.seed =
                        (uint)((int)((ulong)((__instance.seed % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                    //分馏成功率，越接近0表示成功率越高
                    double randomVal = __instance.seed / 2147483646.0;
                    //分馏成功率加成，2.0表示加成100%
                    double successRatePlus = 1.0;
                    if (!isSpecialFractionator) {
                        //常规分馏塔使用增产剂可以提升分馏成功率
                        successRatePlus *= 1.0 + Cargo.accTableMilli[fluidInputAvgInc < 10 ? fluidInputAvgInc : 10];
                    }
                    if (buildingID == IFE精准分馏塔) {
                        //精准分馏塔正在处理的原料越多，效率越低
                        double ratio = ProcessNum2Ratio(__instance.fluidInputCount);
                        successRatePlus = ratio >= 0 ? successRatePlus * ratio : 1;
                    }
                    else if (buildingID == IFE建筑极速分馏塔) {
                        //建筑极速分馏塔对建筑成功率提升，对非建筑成功率下降
                        var item = LDB.items.Select(__instance.fluidId);
                        //BuildMode0-5都有，0是不可放置的物品
                        successRatePlus = item.BuildMode == 0 ? successRatePlus / 10 : successRatePlus * 12.5;
                    }
                    //根据对应配方的分馏成功率与物品数列表，获取分馏得到的产品个数
                    //不同分馏配方的产物可能相同，但原料一定不同，故__instance.fluidId可以找到唯一对应的配方
                    if (!fracRecipeNumRatioDic.TryGetValue(__instance.fluidId, out Dictionary<int, double> dic)) {
                        dic = null;
                    }
                    int outputNum = GetOutputNum(randomVal, successRatePlus, dic);
                    if (buildingID == IFE增产分馏塔) {
                        //如果是增产分馏塔，增产剂可以提升产物数目
                        seed2 = (uint)((int)((ulong)((seed2 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                        bool outputDouble = seed2 / 2147483646.0
                                            < Cargo.incTableMilli[fluidInputAvgInc < 10 ? fluidInputAvgInc : 10];
                        if (outputDouble) {
                            outputNum *= 2;
                        }
                    }
                    else if (buildingID == IFE点数聚集分馏塔) {
                        //点数聚集分馏塔需要根据点数确定输出的物品数目
                        if (outputNum > 0) {
                            //至多将点数聚集到四个物品上
                            outputNum = Math.Min(__instance.fluidInputInc / 10, 4);
                        }
                    }
                    __instance.fractionSuccess = outputNum > 0;

                    #endregion

                    #region 根据分馏结果处理输入

                    if (buildingID == IFE点数聚集分馏塔 && __instance.fractionSuccess) {
                        __instance.fluidInputCount -= outputNum;
                        __instance.fluidInputInc -= 10 * outputNum;
                    }
                    else {
                        __instance.fluidInputCount--;
                        __instance.fluidInputInc -= fluidInputAvgInc;
                    }
                    __instance.fluidInputCargoCount -= (float)(1.0 / num);
                    if (__instance.fluidInputCargoCount < 0f) {
                        __instance.fluidInputCargoCount = 0f;
                    }

                    #endregion

                    #region 根据分馏结果处理输出

                    if (outputNum > 0) {
                        __instance.productOutputCount += outputNum;
                        __instance.productOutputTotal += outputNum;
                        lock (productRegister) {
                            productRegister[outputItemID] += outputNum;
                        }
                        lock (consumeRegister) {
                            consumeRegister[__instance.fluidId] += outputNum;
                        }
                    }
                    else if (outputNum == 0) {
                        __instance.fluidOutputCount++;
                        __instance.fluidOutputTotal++;
                        __instance.fluidOutputInc += fluidInputAvgInc;
                    }

                    #endregion

                    __instance.progress -= 10000;
                }
            }
            else {
                __instance.fractionSuccess = false;
            }

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
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                                        Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)fluidOutputAvgInc)) {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= fluidOutputAvgInc;
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
                        if (__instance.fluidId > 0) {
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt1, __instance.fluidId, null,
                                    out inputStack, out inputInc)
                                > 0) {
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                            }
                            else {
                                break;
                            }
                        }
                        else {
                            //特殊分馏塔接受所有物品
                            int inputItemID = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0,
                                isSpecialFractionator ? null : RecipeProto.fractionatorNeeds,
                                out inputStack, out inputInc);
                            if (inputItemID > 0) {
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                                if (isSpecialFractionator) {
                                    //特殊处理一下，不能走SetRecipe，不然找不到配方
                                    __instance.fluidId = inputItemID;
                                    __instance.productId = inputItemID;
                                    __instance.produceProb = 0.01f;
                                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                                }
                                else {
                                    __instance.SetRecipe(inputItemID, signPool);
                                }
                            }
                            else {
                                break;
                            }
                        }
                    }
                }
            }

            if (__instance.belt2 > 0) {
                if (__instance.isOutput2) {
                    if (__instance.fluidOutputCount > 0) {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                        if (cargoPath != null) {
                            //原版传送带最大速率为60，需要每帧判定4次；创世传送带最大速率为60，需要每帧判定4次
                            for (int i = 0; i < MaxOutputTimes; i++) {
                                if (__instance.fluidOutputCount <= 0) {
                                    break;
                                }
                                int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId,
                                        Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)fluidOutputAvgInc)) {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= fluidOutputAvgInc;
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
                        if (__instance.fluidId > 0) {
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt2, __instance.fluidId, null,
                                    out inputStack, out inputInc)
                                > 0) {
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                            }
                            else {
                                break;
                            }
                        }
                        else {
                            //特殊分馏塔接受所有物品
                            int inputItemID = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0,
                                isSpecialFractionator ? null : RecipeProto.fractionatorNeeds,
                                out inputStack, out inputInc);
                            if (inputItemID > 0) {
                                __instance.fluidInputCount += inputStack;
                                __instance.fluidInputInc += inputInc;
                                __instance.fluidInputCargoCount += 1f;
                                if (isSpecialFractionator) {
                                    //特殊处理一下，不能走SetRecipe，不然找不到配方
                                    __instance.fluidId = inputItemID;
                                    __instance.productId = inputItemID;
                                    __instance.produceProb = 0.01f;
                                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                                }
                                else {
                                    __instance.SetRecipe(inputItemID, signPool);
                                }
                            }
                            else {
                                break;
                            }
                        }
                    }
                }
            }

            if (__instance is { belt0: > 0, isOutput0: true }) {
                for (int i = 0; i < MaxOutputTimes; i++) {
                    //只有产物数目到达堆叠要求，或者没有正在处理的物品，才输出，且一次输出最大堆叠个数的物品
                    if (__instance.productOutputCount >= MaxStackSize) {
                        //产物达到最大堆叠数目，直接尝试输出
                        if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId,
                                (byte)MaxStackSize,
                                (byte)(buildingID == IFE点数聚集分馏塔 ? 10 * MaxStackSize : 0))) {
                            __instance.productOutputCount -= MaxStackSize;
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
        public static int GetOutputNum(double randomVal, double successRatePlus,
            Dictionary<int, double> fracNumRatioDic = null) {
            fracNumRatioDic ??= new() { { 1, 0.01 } };
            //原料丢失判定
            if (fracNumRatioDic.TryGetValue(-1, out double missRate)) {
                seed3 = (uint)((int)((ulong)((seed3 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                if (seed3 / 2147483646.0 < missRate) {
                    return -1;
                }
            }
            //分馏是否成功判定
            double value = 0;
            randomVal /= successRatePlus;
            foreach (KeyValuePair<int, double> p in fracNumRatioDic.Where(p => p.Key > 0)) {
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
        public static void FractionatorUIUpdatePatch(ref UIFractionatorWindow __instance) {
            if (__instance.fractionatorId == 0 || __instance.factory == null) {
                totalUIUpdateTimes = 20;
                return;
            }
            FractionatorComponent fractionatorComponent =
                __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
            if (fractionatorComponent.id != __instance.fractionatorId) {
                totalUIUpdateTimes = 20;
                return;
            }
            if (fractionatorComponent.fluidId == 0) {
                totalUIUpdateTimes = 20;
                return;
            }
            totalUIUpdateTimes++;
            //每20帧（通常为0.333s）刷新一次速率显示，以确保不会出现UI闪烁导致无法看清的问题
            if (totalUIUpdateTimes < 20) {
                __instance.productProbText.text = lastProductProbText;
                __instance.oriProductProbText.text = lastOriProductProbText;
                return;
            }
            totalUIUpdateTimes = 0;
            int buildingID = __instance.factory.entityPool[fractionatorComponent.entityId].protoId;
            double successRatePlus = 1.0;
            double extraIncProduceProb = 0.0;
            switch (buildingID) {
                case IFE精准分馏塔:
                    successRatePlus *= 1.0 + fractionatorComponent.extraIncProduceProb;
                    int currProcessNum = fractionatorComponent.fluidInputCount;
                    successRatePlus = Math.Max(successRatePlus * ProcessNum2Ratio(currProcessNum), 0);
                    break;
                case IFE建筑极速分馏塔:
                    successRatePlus *= 1.0 + fractionatorComponent.extraIncProduceProb;
                    var item = LDB.items.Select(fractionatorComponent.fluidId);
                    successRatePlus = item.BuildMode == 0 ? successRatePlus / 10 : successRatePlus * 12.5;
                    break;
                case I分馏塔_FE通用分馏塔:
                    successRatePlus *= 1.0 + fractionatorComponent.extraIncProduceProb;
                    break;
                case IFE点数聚集分馏塔:
                    double ratio4 = (double)fractionatorComponent.fluidInputInc / fractionatorComponent.fluidInputCount;
                    successRatePlus *= Math.Min(ratio4, 4);
                    break;
                case IFE增产分馏塔:
                    if (fractionatorComponent.fluidInputCount > 0 && fractionatorComponent.fluidInputInc > 0) {
                        int num = fractionatorComponent.fluidInputInc / fractionatorComponent.fluidInputCount;
                        int index = num < 10 ? num : 10;
                        extraIncProduceProb = Cargo.incTableMilli[index];
                    }
                    break;
            }
            StringBuilder sb1 = new StringBuilder();
            Dictionary<int, double> dic;
            if (buildingID == IFE增产分馏塔) {
                dic = fracSelfRecipeList.Contains(fractionatorComponent.fluidId)
                    ? fracRecipeNumRatioDic[fractionatorComponent.fluidId]
                    : new() { { 1, 0.01 } };
            }
            else {
                if (!fracRecipeNumRatioDic.TryGetValue(fractionatorComponent.fluidId, out dic)) {
                    dic = new() { { 1, 0.01 } };
                }
            }
            dic.TryGetValue(-1, out double destroyRatio);
            double flowRatio = 1.0;
            var tempDic = new Dictionary<int, double>();
            int sb1LineNum = 0;
            foreach (var p in dic) {
                if (p.Key < 0) {
                    continue;
                }
                double ratio = p.Value * successRatePlus;
                //增产分馏塔比较特殊，因为增加的概率可能与原有的概率重叠，所以需要整合后再显示
                if (buildingID == IFE增产分馏塔) {
                    double ratioBase = ratio * (1.0 - extraIncProduceProb);
                    if (tempDic.ContainsKey(p.Key)) {
                        tempDic[p.Key] += ratioBase;
                    }
                    else {
                        tempDic.Add(p.Key, ratioBase);
                    }
                    if (extraIncProduceProb > 0) {
                        double ratioDouble = ratio * extraIncProduceProb;
                        if (tempDic.ContainsKey(p.Key * 2)) {
                            tempDic[p.Key * 2] += ratioDouble;
                        }
                        else {
                            tempDic.Add(p.Key * 2, ratioDouble);
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
            //改为纵向拓展，记录初始位置
            if (isFirstUpdateUI) {
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
