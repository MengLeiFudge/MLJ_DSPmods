using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        #region Field

        /// <summary>
        /// 默认分馏概率，广泛使用在代码中，例如创建配方等等。
        /// </summary>
        public static readonly Dictionary<int, float> defaultDic = new() { { 1, 0.01f }, { -1, 0.01f } };
        /// <summary>
        /// 去除损毁后的分馏概率，仅用于特殊分馏塔的相关计算。
        /// </summary>
        public static readonly Dictionary<int, float> defaultDicNoDestroy = new() { { 1, 0.01f } };
        private static uint seed2 = (uint)new Random().Next(int.MinValue, int.MaxValue);
        private static uint seed3 = (uint)new Random().Next(int.MinValue, int.MaxValue);
        private static int totalUIUpdateTimes = 0;
        private static int fractionatorID = 0;
        private static bool isFirstUpdateUI = true;
        private static float productProbTextBaseY;
        private static float oriProductProbTextBaseY;
        private static string lastSpeedText = "";
        private static string lastProductProbText = "";
        private static string lastOriProductProbText = "";
        private static int MaxProductOutputStack = 1;
        private static bool EnableFluidOutputStack = false;
        private static int MaxOutputTimes = 2;
        private static int[] beltSpeed = [6, 12, 30];
        private static int MaxBeltSpeed = 30;
        private static float k1, b1, k2, b2;
        public static int FracFluidInputMax = 40;
        public static int FracProductOutputMax = 20;
        public static int FracFluidOutputMax = 20;
        private static readonly Dictionary<int, int> itemPointDic = new();
        static Stopwatch swtotal = new Stopwatch();
        static Stopwatch sw1 = new Stopwatch();
        static Stopwatch sw2 = new Stopwatch();
        static Stopwatch sw3 = new Stopwatch();
        static Stopwatch sw4 = new Stopwatch();

        #endregion

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
            //游戏逻辑帧只有60，就算传送带再快，也只能取放一个槽位的物品，也就是最多4个，再多也取不到
            //所以下面均以60/s的传送带速率作为极限值考虑
            MaxBeltSpeed = (from item in LDB.items.dataArray
                where item.Type == EItemType.Logistics && item.prefabDesc.isBelt
                select item.prefabDesc.beltSpeed * 6).Prepend(0).Max();
            MaxBeltSpeed = Math.Min(MaxBeltSpeed, 60);
            MaxOutputTimes = (int)Math.Ceiling(MaxBeltSpeed / 15.0);
            float ratio = MaxBeltSpeed / 30.0f;
            FracFluidInputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracFluidInputMax * ratio);
            FracProductOutputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracProductOutputMax * ratio);
            FracFluidOutputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracFluidOutputMax * ratio);

            //计算所有物品的原材料点数，以供垃圾回收分馏塔使用

            List<int> point1 = [
                I硅石, I高纯硅块,
                I水, I原油, I精炼油_GB焦油, I硫酸, I氢, I重氢,
                I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点,
                IGB铝矿, IGB硫矿, IGB放射性矿物, IGB钨矿, IGB氮, IGB氧,
            ];
            foreach (int id in point1) {
                itemPointDic.Add(id, 1);
            }
            List<int> point2 = [
                I可燃冰, I金伯利矿石, I分形硅石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
                I核心素,
                IGB铀矿, IGB钚矿,
            ];
            foreach (int id in point2) {
                itemPointDic.Add(id, 2);
            }
            foreach (ItemProto item in LDB.items.dataArray) {
                if (item.isRaw && !itemPointDic.ContainsKey(item.ID) && item.ID != I蓄电器满) {
                    itemPointDic.Add(item.ID, 1);
                }
            }
            while (true) {
                bool fresh = false;
                foreach (RecipeProto recipe in LDB.recipes.dataArray) {
                    if (recipe.Type == ERecipeType.Fractionate) {
                        continue;
                    }
                    int point = 0;
                    bool canCompute = true;
                    for (int i = 0; i < recipe.Items.Length; i++) {
                        int id = recipe.Items[i];
                        if (!itemPointDic.ContainsKey(id) || id == IMS多功能集成组件) {
                            canCompute = false;
                            break;
                        }
                        point += itemPointDic[id] * recipe.ItemCounts[i];
                    }
                    if (!canCompute) {
                        continue;
                    }
                    int outputTotal = 0;
                    for (int i = 0; i < recipe.ResultCounts.Length; i++) {
                        outputTotal += recipe.ResultCounts[i];
                    }
                    //向下取整但是最低为1
                    point = Math.Max(1, point / outputTotal);
                    for (int i = 0; i < recipe.Results.Length; i++) {
                        int id = recipe.Results[i];
                        label:
                        if (!itemPointDic.ContainsKey(id)) {
                            itemPointDic.Add(id, point);
                            fresh = true;
                        }
                        else if (point < itemPointDic[id]) {
                            itemPointDic[id] = point;
                            fresh = true;
                        }
                        if (id == I蓄电器) {
                            id = I蓄电器满;
                            goto label;
                        }
                    }
                }
                if (!fresh) {
                    break;
                }
            }
            foreach (ItemProto item in LDB.items.dataArray) {
                if (!itemPointDic.ContainsKey(item.ID)) {
                    itemPointDic.Add(item.ID, 1);
                }
            }
        }

        #region 分馏配方与科技状态检测

        /// <summary>
        /// 如果科技已解锁但是配方未解锁，则解锁配方。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "Import")]
        public static void GameHistoryData_Import_Postfix() {
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

        /// <summary>
        /// 更新分馏塔处理需要的部分数值。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameData), "GameTick")]
        public static void GameData_GameTick_Postfix(long time) {
            //使用3作为特殊值，每10逻辑帧更新一次
            if (time % 10 != 3 || GameMain.history == null) {
                return;
            }
            //从科技获取流动输出最大堆叠数目、产物输出最大堆叠数目
            EnableFluidOutputStack = GameMain.history.TechUnlocked(TFE分馏塔流动输出集装);
            int maxStack = 1;
            for (int i = 0; i < 3; i++) {
                if (GameMain.history.TechUnlocked(TFE分馏塔产物输出集装 + i)) {
                    maxStack++;
                }
            }
            MaxProductOutputStack = maxStack;
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

        #endregion

        #region 分馏塔处理逻辑

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

        /// <summary>
        /// 获取输出产物数目。
        /// -1表示原料消耗；0表示原料不变，流动输出；＞0表示生成的产物数目。
        /// 如果丢失概率为x%且分馏概率为1%，生成物大约是产物的1/(1+x)
        /// </summary>
        /// <param name="randomVal">0-1的随机数</param>
        /// <param name="successRatePlus">概率加成，例如增产剂提速100%，则传入2.0</param>
        /// <param name="dic">分馏成功率与产物数关系的字典</param>
        /// <returns>产物数目。-1表示原料损毁；0表示原料不变，流动输出；＞0表示生成产物</returns>
        private static int GetOutputNum(float randomVal, float successRatePlus, Dictionary<int, float> dic) {
            dic ??= defaultDic;
            //原料丢失判定
            if (enableDestroy && dic.TryGetValue(-1, out float missRate)) {
                seed3 = (uint)((int)((ulong)((seed3 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                if (seed3 / 2147483646.0 < missRate) {
                    return -1;
                }
            }
            //分馏是否成功判定
            float value = 0;
            randomVal /= successRatePlus;
            foreach (KeyValuePair<int, float> p in dic) {
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
        /// 修改分馏塔的运行逻辑。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FractionatorComponent), "InternalUpdate")]
        public static bool FractionatorComponent_InternalUpdate_Prefix(ref FractionatorComponent __instance,
            PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
            ref uint __result) {
            //bool record = __instance.seed < 100000;
            bool record = false;
            if (record) {
                swtotal.Restart();
                sw1.Restart();
            }
            if (power < 0.1f) {
                __result = 0u;
                return false;
            }

            int buildingID = factory.entityPool[__instance.entityId].protoId;
            bool isSpecialFractionator = buildingID is IFE点数聚集分馏塔 or IFE增产分馏塔;
            //配方状态错误的情况下，修改配方状态（在分馏塔升降级时会有这样的现象）
            if (buildingID == IFE垃圾回收分馏塔) {
                //物品ID
                //  fluidId              无用，固定为沙土
                //  productId            必须是地基
                //流动输入
                //  fluidInputCount      重要，表示总点数
                //  fluidInputInc        无用，固定为int.max以防止全球喷涂修改此值浪费增产
                //  fluidInputCargoCount 无用，固定为1
                //  fluidInputMax        常量，无用
                //流动输出
                //  fluidOutputCount     无用，固定为0
                //  fluidOutputInc       无用，固定为0
                //  fluidOutputMax       常量，无用
                //产物输出
                //  productOutputCount   重要，表示地基数目
                //  productOutputMax     常量，地基数目存储受此限制
                //统计信息
                //  fluidOutputTotal     消耗物品总数
                //  productOutputTotal   产出地基总数
                if (__instance.fluidId != I沙土) {
                    __instance.fluidId = I沙土;
                    __instance.productId = I地基;
                    __instance.produceProb = 0.01f;
                    signPool[__instance.entityId].iconId0 = I沙土;
                    signPool[__instance.entityId].iconType = 1U;

                    __instance.fluidInputCount = 0;
                    __instance.fluidInputInc = int.MaxValue;
                    __instance.fluidInputCargoCount = 1;
                    __instance.fluidOutputCount = 0;
                    __instance.fluidOutputInc = 0;
                    __instance.productOutputCount = 0;
                    __instance.fluidOutputTotal = 0;
                    __instance.productOutputTotal = 0;
                }
            }
            else if (isSpecialFractionator) {
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

            if (buildingID == IFE垃圾回收分馏塔) {
                //无论输入、输出是什么情况，都要处理
                //如果正面有传送带且是输出，将所有输入按照点数转换为地基，直至输入点数不足，或地基数目到达productOutputMax
                //剩余输入点数全部转换为沙土（避免有点数但是没输入，导致垃圾塔一直处于空转耗电状态的情况）
                if (__instance is { belt0: > 0, isOutput0: true }) {
                    //转换为地基
                    //抓取物品的时候fluidInputCount会乘10（增产剂是小数，乘10是整数），所以这里地基也要乘10
                    int pointPerFoundation = itemPointDic[I地基] * 10;
                    int foundationNum = __instance.fluidInputCount / pointPerFoundation;
                    int addNum = foundationNum + __instance.productOutputCount > __instance.productOutputMax
                        ? __instance.productOutputMax - __instance.productOutputCount
                        : foundationNum;
                    if (addNum > 0) {
                        __instance.fluidInputCount -= pointPerFoundation * foundationNum;
                        __instance.productOutputCount += foundationNum;
                        __instance.productOutputTotal += foundationNum;
                        __instance.fluidOutputTotal -= foundationNum;
                        lock (productRegister) {
                            productRegister[I地基] += foundationNum;
                        }
                    }
                }
                __instance.fractionSuccess = true;
                if (__instance.fluidInputCount > 0) {
                    //转换为沙土
                    int sandCount = __instance.fluidInputCount * 5;
                    if (GameMain.mainPlayer != null) {
                        GameMain.mainPlayer.sandCount += sandCount;
                    }
                    lock (productRegister) {
                        productRegister[I沙土] += sandCount;
                    }
                    __instance.fluidInputCount = 0;
                    __instance.fractionSuccess = false;
                }
            }
            else if (__instance.fluidInputCount > 0
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
                        //如果有自分馏配方，则使用自分馏配方的概率；否则使用无损毁的默认概率
                        if (!fracSelfRecipeList.Contains(inputItemID)) {
                            outputNum = GetOutputNum(randomVal, successRatePlus, defaultDicNoDestroy);
                        }
                        else {
                            outputNum = GetOutputNum(randomVal, successRatePlus, fracRecipeNumRatioDic[inputItemID]);
                        }
                        //增产剂可以提升产物数目
                        seed2 = (uint)((int)((ulong)((seed2 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                        bool outputDouble = seed2 / 2147483646.0
                                            < Cargo.accTableMilli[fluidInputAvgInc < 10 ? fluidInputAvgInc : 10] * 0.4;
                        if (outputDouble) {
                            outputNum *= 2;
                        }
                    }
                    else if (buildingID == IFE点数聚集分馏塔) {
                        //成功率与增产点数均值成正比
                        successRatePlus = __instance.fluidInputInc >= 10
                            ? successRatePlus * __instance.fluidInputInc / __instance.fluidInputCount
                            : 0;
                        outputNum = GetOutputNum(randomVal, successRatePlus, defaultDicNoDestroy);
                    }
                    else {
                        //普通塔需要根据配方确定输出
                        outputNum = GetOutputNum(randomVal, successRatePlus, fracRecipeNumRatioDic[inputItemID]);
                    }
                    __instance.fractionSuccess = outputNum > 0;

                    #endregion

                    #region 根据分馏结果处理原料输入、原料输出、产物输出

                    if (outputNum > 0) {
                        //分馏成功
                        __instance.fluidInputCount--;
                        if (buildingID == IFE点数聚集分馏塔) {
                            __instance.fluidInputInc -= 10;
                        }
                        else {
                            __instance.fluidInputInc -= fluidInputAvgInc;
                        }
                        __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                        if (__instance.fluidInputCargoCount < 0f) {
                            __instance.fluidInputCargoCount = 0f;
                        }
                        __instance.productOutputCount += outputNum;
                        __instance.productOutputTotal += outputNum;
                        switch (buildingID) {
                            case IFE点数聚集分馏塔:
                                //点数聚集分馏塔不计算生成、消耗
                                break;
                            case IFE增产分馏塔:
                                //增产分馏塔只计算多生成的部分
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
                    else {
                        //分馏失败
                        __instance.fluidInputCount--;
                        __instance.fluidInputInc -= fluidInputAvgInc;
                        __instance.fluidInputCargoCount -= 1.0f / fluidInputCountPerCargo;
                        if (__instance.fluidInputCargoCount < 0f) {
                            __instance.fluidInputCargoCount = 0f;
                        }
                        if (outputNum == 0) {
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
                    }

                    #endregion

                    __instance.progress -= 10000;
                }
            }
            else {
                __instance.fractionSuccess = false;
            }
            if (record) {
                sw1.Stop();
                sw2.Restart();
            }

            CargoTraffic cargoTraffic = factory.cargoTraffic;
            byte stack;
            byte inc;
            int beltId;
            bool isOutput;
            if (__instance.belt1 > 0) {
                beltId = __instance.belt1;
                isOutput = __instance.isOutput1;
                if (buildingID == IFE垃圾回收分馏塔) {
                    if (!isOutput) {
                        int itemID = cargoTraffic.TryPickItemAtRear(beltId, 0, null, out stack, out inc);
                        if (itemID > 0) {
                            int incPerItem = inc / stack;
                            int point = (int)(stack
                                              * itemPointDic[itemID]
                                              * (1.0 + Cargo.accTableMilli[incPerItem < 10 ? incPerItem : 10])
                                              * 4f);
                            __instance.fluidInputCount += point;
                            __instance.fluidOutputTotal += stack;
                            lock (consumeRegister) {
                                consumeRegister[itemID] += stack;
                            }
                        }
                    }
                }
                else if (isOutput) {
                    if (__instance.fluidOutputCount > 0) {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
                        if (cargoPath != null) {
                            //原版传送带最大速率为30，如果每次尝试放1个物品到传送带上，需要每帧判定2次（30速*4堆叠/60帧）
                            //创世传送带最大速率为60，如果每次尝试放1个物品到传送带上，需要每帧判定4次（60速*4堆叠/60帧）
                            //每帧至少尝试一次，尝试就会lock buffer进而影响效率，所以这里尝试减少输出的次数
                            int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                            if (!EnableFluidOutputStack || buildingID == IFE精准分馏塔) {
                                //未研究流动输出集装科技，根据传送带速率每帧判定2-4次
                                for (int i = 0; i < MaxOutputTimes; i++) {
                                    if (__instance.fluidOutputCount <= 0) {
                                        break;
                                    }
                                    fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                            (byte)fluidOutputAvgInc)) {
                                        __instance.fluidOutputCount--;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc;
                                    }
                                    else {
                                        break;
                                    }
                                }
                            }
                            else {
                                //已研究流动输出集装科技
                                if (__instance.fluidOutputCount > 4) {
                                    //超过4个，则输出4个
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            4, 4, (byte)(fluidOutputAvgInc * 4))) {
                                        __instance.fluidOutputCount -= 4;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc * 4;
                                    }
                                }
                                else if (__instance.fluidInputCount == 0) {
                                    //未超过4个且输入为空，剩几个输出几个
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            4, (byte)__instance.fluidOutputCount,
                                            (byte)__instance.fluidOutputInc)) {
                                        __instance.fluidOutputCount = 0;
                                        __instance.fluidOutputInc = 0;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (!isOutput && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
                    if (inputItemID > 0) {
                        if (cargoTraffic.TryPickItemAtRear(beltId, inputItemID, null,
                                out stack, out inc)
                            > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    }
                    else {
                        int newInputItemID = cargoTraffic.TryPickItemAtRear(beltId, 0,
                            isSpecialFractionator ? null : RecipeProto.fractionatorNeeds,
                            out stack, out inc);
                        if (newInputItemID > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
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
                    }
                }
            }
            if (record) {
                sw2.Stop();
                sw3.Restart();
            }

            if (__instance.belt2 > 0) {
                beltId = __instance.belt2;
                isOutput = __instance.isOutput2;
                if (buildingID == IFE垃圾回收分馏塔) {
                    if (!isOutput) {
                        int itemID = cargoTraffic.TryPickItemAtRear(beltId, 0, null, out stack, out inc);
                        if (itemID > 0) {
                            int incPerItem = inc / stack;
                            int point = (int)(stack
                                              * itemPointDic[itemID]
                                              * (1.0 + Cargo.accTableMilli[incPerItem < 10 ? incPerItem : 10])
                                              * 4f);
                            __instance.fluidInputCount += point;
                            __instance.fluidOutputTotal += stack;
                            lock (consumeRegister) {
                                consumeRegister[itemID] += stack;
                            }
                        }
                    }
                }
                else if (isOutput) {
                    if (__instance.fluidOutputCount > 0) {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
                        if (cargoPath != null) {
                            int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                            if (!EnableFluidOutputStack || buildingID == IFE精准分馏塔) {
                                for (int i = 0; i < MaxOutputTimes; i++) {
                                    if (__instance.fluidOutputCount <= 0) {
                                        break;
                                    }
                                    fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                            (byte)fluidOutputAvgInc)) {
                                        __instance.fluidOutputCount--;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc;
                                    }
                                    else {
                                        break;
                                    }
                                }
                            }
                            else {
                                if (__instance.fluidOutputCount > 4) {
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            4, 4, (byte)(fluidOutputAvgInc * 4))) {
                                        __instance.fluidOutputCount -= 4;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc * 4;
                                    }
                                }
                                else if (__instance.fluidInputCount == 0) {
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            4, (byte)__instance.fluidOutputCount,
                                            (byte)__instance.fluidOutputInc)) {
                                        __instance.fluidOutputCount = 0;
                                        __instance.fluidOutputInc = 0;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (!isOutput && __instance.fluidInputCargoCount < __instance.fluidInputMax) {
                    if (inputItemID > 0) {
                        if (cargoTraffic.TryPickItemAtRear(beltId, inputItemID, null,
                                out stack, out inc)
                            > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    }
                    else {
                        int newInputItemID = cargoTraffic.TryPickItemAtRear(beltId, 0,
                            isSpecialFractionator ? null : RecipeProto.fractionatorNeeds,
                            out stack, out inc);
                        if (newInputItemID > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                            if (isSpecialFractionator) {
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
                    }
                }
            }
            if (record) {
                sw3.Stop();
                sw4.Restart();
            }

            if (__instance.belt0 > 0) {
                beltId = __instance.belt0;
                isOutput = __instance.isOutput0;
                if (buildingID == IFE垃圾回收分馏塔) {
                    //垃圾回收分馏塔正面也可以输入
                    if (!isOutput) {
                        int itemID = cargoTraffic.TryPickItemAtRear(beltId, 0, null, out stack, out inc);
                        if (itemID > 0) {
                            int incPerItem = inc / stack;
                            int point = (int)(stack
                                              * itemPointDic[itemID]
                                              * (1.0 + Cargo.accTableMilli[incPerItem < 10 ? incPerItem : 10])
                                              * 4f);
                            __instance.fluidInputCount += point;
                            __instance.fluidOutputTotal += stack;
                            lock (consumeRegister) {
                                consumeRegister[itemID] += stack;
                            }
                        }
                    }
                }
                if (isOutput) {
                    for (int i = 0; i < MaxOutputTimes; i++) {
                        //只有产物数目到达堆叠要求，或者没有正在处理的物品，才输出，且一次输出最大堆叠个数的物品
                        if (__instance.productOutputCount >= MaxProductOutputStack) {
                            //产物达到最大堆叠数目，直接尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(beltId, __instance.productId,
                                    (byte)MaxProductOutputStack,
                                    (byte)(buildingID == IFE点数聚集分馏塔 ? 10 * MaxProductOutputStack : 0))) {
                                __instance.productOutputCount -= MaxProductOutputStack;
                            }
                            else {
                                break;
                            }
                        }
                        else if (__instance is { productOutputCount: > 0, fluidInputCount: 0 }) {
                            //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(beltId, __instance.productId,
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
            }

            if (buildingID == IFE垃圾回收分馏塔) {
                __instance.isWorking = __instance.fluidInputCount > 0;
            }
            else {
                if (__instance is { fluidInputCount: 0, fluidOutputCount: 0, productOutputCount: 0 }) {
                    __instance.fluidId = 0;
                    __instance.productId = 0;
                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                }
                __instance.isWorking = __instance.fluidInputCount > 0
                                       && __instance.productOutputCount < __instance.productOutputMax
                                       && __instance.fluidOutputCount < __instance.fluidOutputMax;
            }

            if (record) {
                swtotal.Stop();
                sw4.Stop();
                if (__instance.isOutput1) {
                    LogDebug(
                        $"total={swtotal.ElapsedTicks:D3} process={sw1.ElapsedTicks:D3} input={sw3.ElapsedTicks:D3} output={sw2.ElapsedTicks:D3} product={sw4.ElapsedTicks:D3}");
                }
                else {
                    LogDebug(
                        $"total={swtotal.ElapsedTicks:D3} process={sw1.ElapsedTicks:D3} input={sw2.ElapsedTicks:D3} output={sw3.ElapsedTicks:D3} product={sw4.ElapsedTicks:D3}");
                }
            }
            if (!__instance.isWorking) {
                __result = 0u;
                return false;
            }

            __result = 1u;
            return false;
        }

        #endregion

        #region 分馏塔耗电调整

        private static void SetPCState(this FractionatorComponent fractionator,
            PowerConsumerComponent[] pcPool, EntityData[] entityPool) {
            int buildingID = entityPool[fractionator.entityId].protoId;
            if (buildingID == IFE垃圾回收分馏塔) {
                double num2 = fractionator.fluidInputCount;
                if (num2 < 0.0)
                    num2 = 0.0;
                double powerRatio = 500.0;
                int permillage = (int)(num2 * powerRatio * 30.0 / MaxBeltSpeed + 0.5);
                pcPool[fractionator.pcId].SetRequiredEnergy(fractionator.isWorking, permillage);
            }
            else {
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
                if (buildingID == IFE点数聚集分馏塔) {
                    powerRatio = 1.0;
                }
                else if (buildingID == IFE增产分馏塔) {
                    powerRatio = (Cargo.powerTableRatio[fractionator.incLevel] - 1.0) * 0.5 + 1.0;
                }
                else {
                    powerRatio = Cargo.powerTableRatio[fractionator.incLevel];
                }
                int permillage = (int)((num2 * 50.0 * 30.0 / MaxBeltSpeed + 1000.0) * powerRatio + 0.5);
                pcPool[fractionator.pcId].SetRequiredEnergy(fractionator.isWorking, permillage);
            }
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

        #region 分馏塔详情窗口

        /// <summary>
        /// 修改分馏塔详情窗口中的部分内容。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnUpdate))]
        public static void UIFractionatorWindow__OnUpdate_Postfix(ref UIFractionatorWindow __instance) {
            if (isFirstUpdateUI) {
                isFirstUpdateUI = false;
                //标题改为横向拓展，避免英文无法完全显示
                __instance.titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                //概率改为纵向拓展，记录初始偏移量
                __instance.productProbText.verticalOverflow = VerticalWrapMode.Overflow;
                __instance.oriProductProbText.verticalOverflow = VerticalWrapMode.Overflow;
                productProbTextBaseY = __instance.productProbText.transform.localPosition.y;
                oriProductProbTextBaseY = __instance.oriProductProbText.transform.localPosition.y;
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
            //屏蔽垃圾回收塔的原料增产箭头
            int buildingID = __instance.factory.entityPool[fractionator.entityId].protoId;
            if (buildingID == IFE垃圾回收分馏塔) {
                __instance.needIncs[0].enabled = false;
                __instance.needIncs[1].enabled = false;
                __instance.needIncs[2].enabled = false;
            }
            //当持续查看同一个塔的状态时，每20帧（通常为0.333s）刷新UI，防止UI变化过快导致无法看清
            if (__instance.fractionatorId == fractionatorID) {
                totalUIUpdateTimes++;
                if (totalUIUpdateTimes < 20) {
                    __instance.speedText.text = lastSpeedText;
                    __instance.productProbText.text = lastProductProbText;
                    __instance.oriProductProbText.text = lastOriProductProbText;
                    return;
                }
            }
            else {
                fractionatorID = __instance.fractionatorId;
            }
            totalUIUpdateTimes = 0;

            //修改速率计算
            PowerConsumerComponent powerConsumer = __instance.powerSystem.consumerPool[fractionator.pcId];
            int networkId = powerConsumer.networkId;
            PowerNetwork powerNetwork = __instance.powerSystem.netPool[networkId];
            float num1 = powerNetwork == null || networkId <= 0 ? 0.0f : (float)powerNetwork.consumerRatio;
            double num7 = 1.0;
            if (fractionator.fluidInputCount == 0)
                fractionator.fluidInputCargoCount = 0.0f;
            else
                num7 = fractionator.fluidInputCargoCount > 1E-07
                    ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
                    : 4.0;
            double a = num1
                       * (fractionator.fluidInputCargoCount < MaxBeltSpeed
                           ? fractionator.fluidInputCargoCount
                           : MaxBeltSpeed)
                       * num7
                       * 60.0;
            if (!fractionator.isWorking)
                a = 0.0;
            double num8 = Math.Round(a);
            __instance.speedText.text = string.Format("次分馏每分".Translate(), num8);
            lastSpeedText = __instance.speedText.text;
            //整理要显示的内容
            float successRatePlus = 1.0f;
            float extraIncProduceProb = 0.0f;
            switch (buildingID) {
                case IFE精准分馏塔:
                    successRatePlus *= 1.0f + fractionator.extraIncProduceProb;
                    int currProcessNum = fractionator.fluidInputCount;
                    successRatePlus = Math.Max(successRatePlus * ProcessNum2Ratio(currProcessNum), 0);
                    break;
                case IFE建筑极速分馏塔:
                    successRatePlus *= 1.0f + fractionator.extraIncProduceProb;
                    var item = LDB.items.Select(fractionator.fluidId);
                    successRatePlus = item.BuildMode == 0 ? successRatePlus / 5.0f : successRatePlus * 5.0f;
                    break;
                case I分馏塔_FE通用分馏塔:
                    successRatePlus *= 1.0f + fractionator.extraIncProduceProb;
                    break;
                case IFE点数聚集分馏塔:
                    successRatePlus = fractionator.fluidInputInc >= 10
                        ? successRatePlus * fractionator.fluidInputInc / fractionator.fluidInputCount
                        : 0;
                    break;
                case IFE增产分馏塔:
                    if (fractionator is { fluidInputCount: > 0, fluidInputInc: > 0 }) {
                        int num = fractionator.fluidInputInc / fractionator.fluidInputCount;
                        int index = num < 10 ? num : 10;
                        extraIncProduceProb = (float)(Cargo.accTableMilli[index] * 0.4);
                    }
                    break;
            }
            //根据建筑类型，获取初步的分馏概率表
            Dictionary<int, float> dic;
            switch (buildingID) {
                case IFE点数聚集分馏塔:
                    dic = defaultDicNoDestroy;
                    break;
                case IFE增产分馏塔:
                    dic = fracSelfRecipeList.Contains(fractionator.fluidId)
                        ? fracRecipeNumRatioDic[fractionator.fluidId]
                        : defaultDicNoDestroy;
                    break;
                default:
                    if (!fracRecipeNumRatioDic.TryGetValue(fractionator.fluidId, out dic)) {
                        dic = defaultDic;
                    }
                    break;
            }
            //根据建筑类型、初步的分馏概率表、输入情况，计算实际的分馏概率表
            float flowRatio = 1.0f;
            StringBuilder sb1 = new StringBuilder();
            int sb1LineNum = 0;
            if (buildingID == IFE点数聚集分馏塔) {
                float ratio = 0.01f * successRatePlus;
                sb1.Append($"1({ratio:0.###%})\n");
                sb1LineNum++;
                flowRatio -= ratio;
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
                        //现在使用accTableMilli的0.4倍来计算增产塔的概率，刚好这里不会超过1.0导致负数
                        float ratioBase = ratio * (1.0f - Math.Min(1.0f, extraIncProduceProb));
                        if (tempDic.ContainsKey(p.Key)) {
                            tempDic[p.Key] += ratioBase;
                        }
                        else if (ratioBase > 0) {
                            tempDic.Add(p.Key, ratioBase);
                        }
                        if (extraIncProduceProb > 0) {
                            float outputDoubleRatio = ratio * extraIncProduceProb;
                            if (tempDic.ContainsKey(p.Key * 2)) {
                                tempDic[p.Key * 2] += outputDoubleRatio;
                            }
                            else {
                                tempDic.Add(p.Key * 2, outputDoubleRatio);
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

            //刷新概率显示内容
            string s1;
            string s2;
            if (buildingID == IFE垃圾回收分馏塔) {
                s1 = "";
                s2 = "";
            }
            else {
                s1 = sb1.ToString().Substring(0, sb1.Length - 1);
                s2 = $"{"流动".Translate()}({flowRatio:0.###%})";
                if (destroyRatio > 0) {
                    s2 += $"\n{"损毁".Translate()}({destroyRatio:0.###%})";
                }
            }
            __instance.productProbText.text = s1;
            lastProductProbText = s1;
            __instance.oriProductProbText.text = s2;
            lastOriProductProbText = s2;
            //刷新概率显示位置
            float upY = productProbTextBaseY + 9f * (sb1LineNum - 1);
            __instance.productProbText.transform.localPosition = new(0, upY, 0);
            float downY = oriProductProbTextBaseY - (destroyRatio > 0 ? 9f : 0);
            __instance.oriProductProbText.transform.localPosition = new(0, downY, 0);
        }

        #endregion
    }
}
