using FractionateEverything.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
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

        private static readonly Dictionary<int, float> defaultDicNoDestroy = new() { { 1, 0.01f } };
        private static uint seed2 = (uint)new Random().Next(int.MinValue, int.MaxValue);
        private static int totalUIUpdateTimes = 0;
        private static int fractionatorID = 0;
        private static bool isFirstUpdateUI = true;
        private static float productProbTextBaseY;
        private static float oriProductProbTextBaseY;
        private static string lastSpeedText = "";
        private static string lastProductProbText = "";
        private static string lastOriProductProbText = "";
        private static int MaxProductOutputStack = 1;
        private static bool EnableFracForever = false;
        private static bool EnableFluidOutputStack = false;
        private static int MaxOutputTimes = 2;
        private static int MaxBeltSpeed = 30;
        public static int FracFluidInputMax = 40;
        public static int FracProductOutputMax = 20;
        public static int FracFluidOutputMax = 20;
        private static readonly Dictionary<int, int> itemPointDic = new();
        private static int foundationConvertPoint = (int)(660 * 1.25);
        private static double[] incTableFixedRatio;
        //f(x)=a*x^b，由MATLAB拟合得到
        //原始数据：
        //100.000000 	    0.100000
        //200.000000 	    0.070000
        //400.000000 	    0.049000
        //800.000000 	    0.034300
        //1600.000000 	    0.024010
        //3200.000000 	    0.016807
        //6400.000000 	    0.011765
        //12800.000000 	    0.008235
        //25600.000000 	    0.005765
        //51200.000000 	    0.004035
        //102400.000000 	0.002825
        //204800.000000 	0.001977
        //409600.000000 	0.001384
        //819200.000000 	0.000969
        //1638400.000000 	0.000678
        //3276800.000000 	0.000475
        private const double a = 1.069415182912524;
        private const double b = -0.5145731728297580;
        private static readonly Dictionary<int, Dictionary<int, float>> IPFDic = [];
        private static readonly Dictionary<int, float> defaultIPFDic = new() { { 2, 0.005f } };
        private static int[] veinNeeds = [];
        private static int[] upgradeNeeds = [];
        private static int[] downgradeNeeds = [];
        private static int[] trashRecycleNeeds = [];
#if DEBUG
        private const string ITEM_VALUE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
        private const string ITEM_VALUE_CSV_PATH = $@"{ITEM_VALUE_CSV_DIR}\itemPoint.csv";
#endif

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
            FracFluidInputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracFluidInputMax * ratio);
            FracProductOutputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracProductOutputMax * ratio);
            FracFluidOutputMax = (int)(FractionatorBuildings.FractionatorPrefabDesc.fracFluidOutputMax * ratio);

            //增产剂的增产效果修复，因为增产点数对于增产的加成不是线性的，但对于加速的加成是线性的
            incTableFixedRatio = new double[Cargo.incTableMilli.Length];
            for (int i = 1; i < Cargo.incTableMilli.Length; i++) {
                incTableFixedRatio[i] = Cargo.accTableMilli[i] / Cargo.incTableMilli[i];
            }

            //生成垃圾塔使用的非建筑列表
            List<int> trashRecycleList = [];
            foreach (ItemProto item in LDB.items.dataArray) {
                //0是普通物品，4是地基
                if (item.BuildMode is 0 or 4) {
                    trashRecycleList.Add(item.ID);
                }
            }
            trashRecycleNeeds = trashRecycleList.ToArray();

            //计算所有物品的原材料点数
            itemPointDic.Add(I能量碎片, 50);
            itemPointDic.Add(I黑雾矩阵, 60);
            itemPointDic.Add(I物质重组器, 110);
            itemPointDic.Add(I硅基神经元, 150);
            itemPointDic.Add(I负熵奇点, 180);
            itemPointDic.Add(I核心素, 680);
            itemPointDic.Add(I临界光子, 8000);
            List<int> point10 = [
                I硅石,
                I水, I原油, I精炼油_GB焦油, I硫酸, I氢, I重氢,
                IGB铝矿, IGB硫矿, IGB放射性矿物, IGB钨矿, IGB氦, IGB氨, IGB氮, IGB氧,
            ];
            foreach (int id in point10) {
                itemPointDic.Add(id, 100);
            }
            List<int> point20 = [
                I可燃冰, I金伯利矿石, I分形硅石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
            ];
            foreach (int id in point20) {
                itemPointDic.Add(id, 200);
            }
            foreach (ItemProto item in LDB.items.dataArray) {
                if (itemPointDic.ContainsKey(item.ID) || item.ID == I蓄电器满) {
                    continue;
                }
                if (item.maincraft == null) {
                    itemPointDic.Add(item.ID, 100);
                }
            }
            while (true) {
                bool fresh = false;
                foreach (RecipeProto recipe in LDB.recipes.dataArray) {
                    if (recipe.Type == ERecipeType.Fractionate
                        || recipe.Items.Length == 0
                        || recipe.Results.Length == 0) {
                        continue;
                    }
                    int totalPoints = 0;
                    int totalCounts = 0;
                    bool canCompute = true;
                    if (recipe.Items[0] == IMS多功能集成组件 && !recipe.Results.ToList().Contains(IMS多功能集成组件)) {
                        //totalPoints：输出的增产点数总数
                        //totalCounts：输入的物品总数
                        for (int i = 0; i < recipe.Results.Length; i++) {
                            int id = recipe.Results[i];
                            if (!itemPointDic.ContainsKey(id)) {
                                canCompute = false;
                                break;
                            }
                            totalPoints += itemPointDic[id] * recipe.ResultCounts[i];
                        }
                        if (!canCompute) {
                            continue;
                        }
                        foreach (int count in recipe.ItemCounts) {
                            totalCounts += count;
                        }
                        totalPoints = (int)Math.Ceiling((double)totalPoints / totalCounts - recipe.TimeSpend);
                        foreach (int id in recipe.Items) {
                            if (!itemPointDic.ContainsKey(id)) {
                                itemPointDic.Add(id, totalPoints);
                                fresh = true;
                            } else if (totalPoints < itemPointDic[id]) {
                                //这里想了一下，还是取价值低的吧
                                itemPointDic[id] = totalPoints;
                                fresh = true;
                            }
                        }
                    } else {
                        //totalPoints：输入的增产点数总数
                        //totalCounts：输出的物品总数
                        for (int i = 0; i < recipe.Items.Length; i++) {
                            int id = recipe.Items[i];
                            if (!itemPointDic.ContainsKey(id)) {
                                canCompute = false;
                                break;
                            }
                            totalPoints += itemPointDic[id] * recipe.ItemCounts[i];
                        }
                        if (!canCompute) {
                            continue;
                        }
                        foreach (int t in recipe.ResultCounts) {
                            totalCounts += t;
                        }
                        totalPoints = (int)((double)totalPoints / totalCounts + recipe.TimeSpend);
                        foreach (int id0 in recipe.Results) {
                            int id = id0;
                            label:
                            if (!itemPointDic.ContainsKey(id)) {
                                itemPointDic.Add(id, totalPoints);
                                fresh = true;
                            } else if (totalPoints < itemPointDic[id]) {
                                itemPointDic[id] = totalPoints;
                                fresh = true;
                            }
                            if (id == I蓄电器) {
                                id = I蓄电器满;
                                goto label;
                            }
                        }
                    }
                }
                if (!fresh) {
                    break;
                }
            }
            foreach (ItemProto item in LDB.items.dataArray) {
                if (!itemPointDic.ContainsKey(item.ID)) {
                    itemPointDic.Add(item.ID, 100);
                }
            }
            foundationConvertPoint = (int)(itemPointDic[I地基] * 1.25);
            //根据物品点数，构建增产塔使用的概率表
            foreach (KeyValuePair<int, int> p in itemPointDic) {
                IPFDic.Add(p.Key, new() { { 2, (float)(a * Math.Pow(p.Value, b)) } });
            }
#if DEBUG
            //按照从小到大的顺序输出所有物品的原材料点数
            if (Directory.Exists(ITEM_VALUE_CSV_DIR)) {
                using StreamWriter sw = new StreamWriter(ITEM_VALUE_CSV_PATH);
                sw.WriteLine("ID,名称,物品价值,增产分馏概率最大值");
                foreach (KeyValuePair<int, int> p in itemPointDic.OrderBy(p => p.Value)) {
                    ItemProto item = LDB.items.Select(p.Key);
                    if (item == null) {
                        continue;
                    }
                    sw.WriteLine($"{p.Key},{item.name},{p.Value},{IPFDic[p.Key][2]:P5}");
                }
            }
#endif
        }

        #region 分馏配方与科技状态检测

        /// <summary>
        /// 如果科技已解锁但是配方未解锁，则解锁配方。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.Import))]
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
        [HarmonyPatch(typeof(GameData), nameof(GameData.GameTick))]
        public static void GameData_GameTick_Postfix(long time) {
            //使用3作为特殊值，每10逻辑帧更新一次
            if (time % 10 != 3 || GameMain.history == null) {
                return;
            }
            //从科技获取流动输出最大堆叠数目、产物输出最大堆叠数目
            EnableFluidOutputStack = GameMain.history.TechUnlocked(TFE分馏流动输出集装);
            int maxStack = 1;
            for (int i = 0; i < 3; i++) {
                if (GameMain.history.TechUnlocked(TFE分馏产物输出集装 + i)) {
                    maxStack++;
                }
            }
            MaxProductOutputStack = maxStack;
            //从科技获取是否分馏永动
            EnableFracForever = GameMain.history.TechUnlocked(TFE分馏永动);

            //更新分馏塔可接受的物品
            RecipeProto[] dataArray = LDB.recipes.dataArray;
            List<RecipeProto> list = [];
            List<int> list2 = [];
            List<int> veinList = [];
            List<int> upgradeList = [];
            List<int> downgradeList = [];
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
                    if (GetItemNaturalResource(r.Items[0]) > 0) {
                        veinList.Add(r.Items[0]);
                    } else if (GetItemUpgrade(r.Items[0]) != 0) {
                        //只要不是vein，就一定是升降级；需要考虑矩阵分馏、燃料棒分馏的开关
                        upgradeList.Add(r.Items[0]);
                        downgradeList.Add(r.Results[0]);
                    }
                }
            }
            if (RecipeProto.fractionatorRecipes.Length != list.Count
                || veinNeeds.Length != veinList.Count
                || upgradeNeeds.Length != upgradeList.Count
                || downgradeNeeds.Length != downgradeList.Count) {
                LogInfo($"fractionatorRecipes.Length: {RecipeProto.fractionatorRecipes.Length} -> {list.Count}");
                RecipeProto.fractionatorRecipes = list.ToArray();
                RecipeProto.fractionatorNeeds = list2.ToArray();
                LogInfo($"veinNeeds.Length: {veinNeeds.Length} -> {veinList.Count}");
                veinNeeds = veinList.ToArray();
                LogInfo($"upgradeNeeds.Length: {upgradeNeeds.Length} -> {upgradeList.Count}");
                upgradeNeeds = upgradeList.ToArray();
                LogInfo($"downgradeNeeds.Length: {downgradeNeeds.Length} -> {downgradeList.Count}");
                downgradeNeeds = downgradeList.ToArray();
            }
        }

        #endregion

        #region 分馏塔处理逻辑

        /// <summary>
        /// 返回增产加成、加速加成中二者最大的值。
        /// </summary>
        public static double MaxTableMilli(int fluidInputAvgInc) {
            int avgPoint = fluidInputAvgInc < 10 ? fluidInputAvgInc : 10;
            double ratioAcc = Cargo.accTableMilli[avgPoint];
            double ratioInc = Cargo.incTableMilli[avgPoint] * incTableFixedRatio[avgPoint];
            return ratioAcc > ratioInc ? ratioAcc : ratioInc;
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
            //原料丢失判定
            if (enableDestroy && dic.TryGetValue(-1, out float missRate)) {
                seed2 = (uint)((int)((ulong)((seed2 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                if (seed2 / 2147483646.0 < missRate) {
                    return -1;
                }
            }
            //分馏是否成功判定
            float value = 0;
            foreach (KeyValuePair<int, float> p in dic) {
                if (p.Key == -1) {
                    continue;//不要用linq，效率低
                }
                value += p.Value * successRatePlus;
                if (randomVal < value) {
                    return p.Key;
                }
            }
            return 0;
        }

        /// <summary>
        /// 修改分馏塔的运行逻辑。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.InternalUpdate))]
        public static bool FractionatorComponent_InternalUpdate_Prefix(ref FractionatorComponent __instance,
            PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
            ref uint __result) {
            if (power < 0.1f) {
                __result = 0u;
                return false;
            }

            int buildingID = factory.entityPool[__instance.entityId].protoId;
            //配方状态错误的情况下，修改配方状态（在分馏塔升降级时会有这样的现象）
            bool abnormalProcess = false;
            if (buildingID == I分馏塔) {
                if (__instance.fluidId != I氢 || __instance.productId != I重氢) {
                    abnormalProcess = true;
                    __instance.fluidOutputCount += __instance.fluidInputCount;
                    __instance.fluidInputCount = 0;
                    __instance.productOutputCount = 0;
                    __instance.produceProb = 0.00f;
                    signPool[__instance.entityId].iconId0 = 0U;
                    signPool[__instance.entityId].iconType = 0U;
                }
            } else if (buildingID is IFE自然资源分馏塔 or IFE升级分馏塔 or IFE降级分馏塔) {
                int tempProductId = buildingID switch {
                    IFE自然资源分馏塔 => GetItemNaturalResource(__instance.fluidId),
                    IFE升级分馏塔 => GetItemUpgrade(__instance.fluidId),
                    _ => GetItemDowngrade(__instance.fluidId),
                };
                if (__instance.fluidId > 0 && tempProductId == 0) {
                    //如果正在处理一个当前无法处理的物品
                    abnormalProcess = true;
                    __instance.fluidOutputCount += __instance.fluidInputCount;
                    __instance.fluidInputCount = 0;
                    __instance.productOutputCount = 0;
                    __instance.produceProb = 0.00f;
                    signPool[__instance.entityId].iconId0 = 0U;
                    signPool[__instance.entityId].iconType = 0U;
                } else if (__instance.fluidId > 0 && tempProductId != 0 && __instance.productId != tempProductId) {
                    //如果正在处理一个当前可以处理的物品，但是产物id不对
                    __instance.productId = tempProductId;
                    __instance.produceProb = 0.01f;
                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                }
            } else if (buildingID == IFE垃圾回收分馏塔) {
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
            } else {
                if (__instance.productId != __instance.fluidId) {
                    __instance.productId = __instance.fluidId;
                    __instance.produceProb = 0.01f;
                    signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                    signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                }
            }

            bool isSpecialFractionator = buildingID is IFE点数聚集分馏塔 or IFE增产分馏塔;
            int inputItemID = __instance.fluidId;
            int outputItemID = __instance.productId;

            //输入货物的平均堆叠数目，后面输出时候会用
            float fluidInputCountPerCargo = 1.0f;
            if (__instance.fluidInputCount == 0) {
                __instance.fluidInputCargoCount = 0f;
            } else {
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
                    //按照地基价值*1.25计算，因为输入也有可能是地基
                    //-1是为了确保塔内至少有1点，以便于详情显示不会提示未输入原料
                    int foundationNum = (__instance.fluidInputCount - 1) / foundationConvertPoint;
                    //除以2是为了避免提示产物堆积
                    int addNum = Math.Min(foundationNum,
                        __instance.productOutputMax / 2 - __instance.productOutputCount);
                    if (addNum > 0) {
                        __instance.fluidInputCount -= foundationConvertPoint * addNum;
                        __instance.productOutputCount += addNum;
                        __instance.productOutputTotal += addNum;
                        __instance.fluidOutputTotal -= addNum;
                        lock (productRegister) {
                            productRegister[I地基] += addNum;
                        }
                    }
                    __instance.fractionSuccess = true;
                }
                if (__instance.fluidInputCount > foundationConvertPoint) {
                    //转换为沙土
                    //按照物品价值*5计算，系数5是一个相对合理的范围
                    //-1是为了确保塔内至少有foundationConvertPoint点，以便于详情显示不会提示未输入原料
                    int sandCount = (__instance.fluidInputCount - foundationConvertPoint) * 5;
                    if (GameMain.mainPlayer != null) {
                        GameMain.mainPlayer.sandCount += sandCount;
                    }
                    lock (productRegister) {
                        productRegister[I沙土] += sandCount;
                    }
                    __instance.fluidInputCount = foundationConvertPoint;
                    __instance.fractionSuccess = false;
                }
            } else if (__instance.fluidInputCount > 0
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
                    int outputNum;
                    if (buildingID == IFE增产分馏塔) {
                        //增产0点则概率为0，增产10点则概率为IPFDic
                        //增产点数以40%为基准，按加速或增产的最高效果提升分馏成功率
                        successRatePlus = (float)MaxTableMilli(fluidInputAvgInc) / 2.5f;
                        //分馏基础概率由物品点数决定，点数越高的物品越难分馏
                        if (!IPFDic.TryGetValue(inputItemID, out Dictionary<int, float> dic)) {
                            dic = defaultIPFDic;
                        }
                        outputNum = GetOutputNum(randomVal, successRatePlus, dic);
                    } else if (buildingID == IFE点数聚集分馏塔) {
                        //增产点数与分馏成功率成正比，与加速或增产效果无关
                        //基础概率为10%
                        successRatePlus = __instance.fluidInputInc >= 10
                            ? successRatePlus * __instance.fluidInputInc / __instance.fluidInputCount * 2.5f
                            : 0;
                        outputNum = GetOutputNum(randomVal, successRatePlus, defaultDicNoDestroy);
                    } else {
                        //增产点数以100%为基准，按加速或增产的最高效果提升分馏成功率
                        successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputAvgInc);
                        //根据配方确定输出
                        Dictionary<int, float> dic = buildingID switch {
                            IFE自然资源分馏塔 => GetNumRatioNaturalResource(inputItemID),
                            IFE升级分馏塔 => GetNumRatioUpgrade(inputItemID),
                            IFE降级分馏塔 => GetNumRatioDowngrade(inputItemID),
                            _ => defaultDicNoDestroy//I分馏塔
                        };
                        outputNum = GetOutputNum(randomVal, successRatePlus, dic);
                    }
                    //如果分馏永动已研究，并且输出缓达到上限的一半，则不会分馏出物品
                    if (EnableFracForever
                        && buildingID != IFE升级分馏塔
                        && buildingID != IFE降级分馏塔
                        && __instance.productOutputCount >= __instance.productOutputMax / 2) {
                        outputNum = 0;
                    }
                    __instance.fractionSuccess = outputNum > 0;

                    #endregion

                    #region 根据分馏结果处理原料输入、原料输出、产物输出

                    if (outputNum > 0) {
                        //分馏成功
                        __instance.fluidInputCount--;
                        if (buildingID == IFE点数聚集分馏塔) {
                            __instance.fluidInputInc -= 10;
                        } else {
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
                                //不计算生成、消耗
                                break;
                            case IFE自然资源分馏塔:
                            case IFE增产分馏塔:
                                //只计算多生成的部分
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
                    } else {
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
                        } else {
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
            } else {
                __instance.fractionSuccess = false;
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
                        int itemID = cargoTraffic.TryPickItemAtRear(beltId, 0,
                            enableBuildingAsTrash ? null : trashRecycleNeeds, out stack, out inc);
                        if (itemID > 0) {
                            __instance.fluidId = I沙土;
                            __instance.productId = I地基;
                            __instance.fluidInputCount += stack * itemPointDic[itemID];
                            __instance.fluidOutputTotal += stack;
                            lock (consumeRegister) {
                                consumeRegister[itemID] += stack;
                            }
                        }
                    }
                } else if (isOutput) {
                    if (__instance.fluidOutputCount > 0) {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
                        if (cargoPath != null) {
                            //原版传送带最大速率为30，如果每次尝试放1个物品到传送带上，需要每帧判定2次（30速*4堆叠/60帧）
                            //创世传送带最大速率为60，如果每次尝试放1个物品到传送带上，需要每帧判定4次（60速*4堆叠/60帧）
                            //每帧至少尝试一次，尝试就会lock buffer进而影响效率，所以这里尝试减少输出的次数
                            int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                            if (!EnableFluidOutputStack) {
                                //未研究流动输出集装科技，根据传送带速率每帧判定2-4次
                                for (int i = 0; i < MaxOutputTimes; i++) {
                                    if (__instance.fluidOutputCount <= 0) {
                                        break;
                                    }
                                    if (buildingID == IFE点数聚集分馏塔
                                        && fluidOutputAvgInc < 4
                                        && __instance.fluidOutputCount > 1) {
                                        fluidOutputAvgInc = __instance.fluidOutputInc >= 4 ? 4 : 0;
                                    }
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                            (byte)fluidOutputAvgInc)) {
                                        __instance.fluidOutputCount--;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc;
                                    } else {
                                        break;
                                    }
                                }
                            } else {
                                //已研究流动输出集装科技
                                if (__instance.fluidOutputCount > 4) {
                                    //超过4个，则输出4个
                                    if (buildingID == IFE点数聚集分馏塔 && fluidOutputAvgInc < 4) {
                                        fluidOutputAvgInc = __instance.fluidOutputInc >= 16 ? 4 : 0;
                                    }
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            4, 4, (byte)(fluidOutputAvgInc * 4))) {
                                        __instance.fluidOutputCount -= 4;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc * 4;
                                    }
                                } else if (__instance.fluidInputCount == 0) {
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
                } else if (!isOutput
                           && __instance.fluidInputCargoCount < __instance.fluidInputMax
                           && !abnormalProcess) {
                    if (inputItemID > 0) {
                        if (cargoTraffic.TryPickItemAtRear(beltId, inputItemID, null,
                                out stack, out inc)
                            > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    } else {
                        if (isSpecialFractionator) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, null, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = input;
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else if (buildingID == IFE自然资源分馏塔) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, veinNeeds, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = GetItemNaturalResource(input);
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else if (buildingID == IFE升级分馏塔) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, upgradeNeeds, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = GetItemUpgrade(input);
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else if (buildingID == IFE降级分馏塔) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, downgradeNeeds, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = GetItemDowngrade(input);
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else {
                            //I分馏塔
                            int input = cargoTraffic.TryPickItemAtRear(beltId, I氢, null, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = I氢;
                                __instance.productId = I重氢;
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        }
                    }
                }
            }

            if (__instance.belt2 > 0) {
                beltId = __instance.belt2;
                isOutput = __instance.isOutput2;
                if (buildingID == IFE垃圾回收分馏塔) {
                    if (!isOutput) {
                        int itemID = cargoTraffic.TryPickItemAtRear(beltId, 0,
                            enableBuildingAsTrash ? null : trashRecycleNeeds, out stack, out inc);
                        if (itemID > 0) {
                            __instance.fluidId = I沙土;
                            __instance.productId = I地基;
                            __instance.fluidInputCount += stack * itemPointDic[itemID];
                            __instance.fluidOutputTotal += stack;
                            lock (consumeRegister) {
                                consumeRegister[itemID] += stack;
                            }
                        }
                    }
                } else if (isOutput) {
                    if (__instance.fluidOutputCount > 0) {
                        CargoPath cargoPath =
                            cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
                        if (cargoPath != null) {
                            int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
                            if (!EnableFluidOutputStack) {
                                for (int i = 0; i < MaxOutputTimes; i++) {
                                    if (__instance.fluidOutputCount <= 0) {
                                        break;
                                    }
                                    if (buildingID == IFE点数聚集分馏塔
                                        && fluidOutputAvgInc < 4
                                        && __instance.fluidOutputCount > 1) {
                                        fluidOutputAvgInc = __instance.fluidOutputInc >= 4 ? 4 : 0;
                                    }
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
                                            (byte)fluidOutputAvgInc)) {
                                        __instance.fluidOutputCount--;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc;
                                    } else {
                                        break;
                                    }
                                }
                            } else {
                                if (__instance.fluidOutputCount > 4) {
                                    if (buildingID == IFE点数聚集分馏塔 && fluidOutputAvgInc < 4) {
                                        fluidOutputAvgInc = __instance.fluidOutputInc >= 16 ? 4 : 0;
                                    }
                                    if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
                                            4, 4, (byte)(fluidOutputAvgInc * 4))) {
                                        __instance.fluidOutputCount -= 4;
                                        __instance.fluidOutputInc -= fluidOutputAvgInc * 4;
                                    }
                                } else if (__instance.fluidInputCount == 0) {
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
                } else if (!isOutput
                           && __instance.fluidInputCargoCount < __instance.fluidInputMax
                           && !abnormalProcess) {
                    if (inputItemID > 0) {
                        if (cargoTraffic.TryPickItemAtRear(beltId, inputItemID, null,
                                out stack, out inc)
                            > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    } else {
                        if (isSpecialFractionator) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, null, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = input;
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else if (buildingID == IFE自然资源分馏塔) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, veinNeeds, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = GetItemNaturalResource(input);
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else if (buildingID == IFE升级分馏塔) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, upgradeNeeds, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = GetItemUpgrade(input);
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else if (buildingID == IFE降级分馏塔) {
                            int input = cargoTraffic.TryPickItemAtRear(beltId, 0, downgradeNeeds, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = input;
                                __instance.productId = GetItemDowngrade(input);
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        } else {
                            //I分馏塔
                            int input = cargoTraffic.TryPickItemAtRear(beltId, I氢, null, out stack, out inc);
                            if (input > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount += 1f;
                                __instance.fluidId = I氢;
                                __instance.productId = I重氢;
                                __instance.produceProb = 0.01f;
                                signPool[__instance.entityId].iconId0 = (uint)__instance.productId;
                                signPool[__instance.entityId].iconType = __instance.productId == 0 ? 0U : 1U;
                            }
                        }
                    }
                }
            }

            if (__instance.belt0 > 0) {
                beltId = __instance.belt0;
                isOutput = __instance.isOutput0;
                if (buildingID == IFE垃圾回收分馏塔) {
                    //垃圾回收分馏塔正面也可以输入
                    if (!isOutput) {
                        int itemID = cargoTraffic.TryPickItemAtRear(beltId, 0,
                            enableBuildingAsTrash ? null : trashRecycleNeeds, out stack, out inc);
                        if (itemID > 0) {
                            __instance.fluidId = I沙土;
                            __instance.productId = I地基;
                            __instance.fluidInputCount += stack * itemPointDic[itemID];
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
                            } else {
                                break;
                            }
                        } else if (__instance is { productOutputCount: > 0, fluidInputCount: 0 }) {
                            //产物未达到最大堆叠数目且大于0，且没有正在处理的物品，尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(beltId, __instance.productId,
                                    (byte)__instance.productOutputCount,
                                    (byte)(buildingID == IFE点数聚集分馏塔 ? 10 * __instance.productOutputCount : 0))) {
                                __instance.productOutputCount = 0;
                            } else {
                                break;
                            }
                        } else {
                            break;
                        }
                    }
                }
            }

            //分馏塔无输入、输出、产物时，清除图标显示，重置分馏塔状态
            if (__instance is { fluidInputCount: 0, fluidOutputCount: 0, productOutputCount: 0 }) {
                __instance.fluidId = 0;
                __instance.productId = 0;
                signPool[__instance.entityId].iconId0 = 0U;
                signPool[__instance.entityId].iconType = 0U;
            }
            if (buildingID == IFE垃圾回收分馏塔) {
                __instance.isWorking = __instance.fluidInputCount > foundationConvertPoint;
            } else {
                __instance.isWorking = __instance.fluidInputCount > 0
                                       && __instance.productOutputCount < __instance.productOutputMax
                                       && __instance.fluidOutputCount < __instance.fluidOutputMax;
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
                double num1 = fractionator.fluidInputCount;
                if (num1 < 1)
                    num1 = 1;
                double num2 = Math.Log10(num1) - 2;
                if (num2 < 0.0)
                    num2 = 0.0;
                int permillage = (int)(num2 * 10000.0 + 1000 + 0.5);
                int permillage2 = (int)(num1 * 10 + 0.5);
                if (permillage2 < permillage)
                    permillage = permillage2;
                pcPool[fractionator.pcId].SetRequiredEnergy(fractionator.isWorking, permillage);
            } else {
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
                } else if (buildingID == IFE增产分馏塔) {
                    powerRatio = (Cargo.powerTableRatio[fractionator.incLevel] - 1.0) * 0.5 + 1.0;
                } else {
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
            int buildingID = __instance.factory.entityPool[fractionator.entityId].protoId;
            if (buildingID == IFE垃圾回收分馏塔) {
                //屏蔽垃圾回收塔的原料增产箭头
                __instance.needIncs[0].enabled = false;
                __instance.needIncs[1].enabled = false;
                __instance.needIncs[2].enabled = false;
                //屏蔽垃圾回收塔的分馏速率
                __instance.speedText.enabled = false;
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
            //整理要显示的内容
            int fluidInputAvgInc = fractionator.fluidInputCount > 0
                ? fractionator.fluidInputInc / fractionator.fluidInputCount
                : 0;
            float successRatePlus = 1.0f;
            Dictionary<int, float> dic;
            switch (buildingID) {
                case IFE自然资源分馏塔:
                    successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputAvgInc);
                    dic = GetNumRatioNaturalResource(fractionator.fluidId);
                    break;
                case IFE升级分馏塔:
                    successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputAvgInc);
                    dic = GetNumRatioUpgrade(fractionator.fluidId);
                    break;
                case IFE降级分馏塔:
                    successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputAvgInc);
                    dic = GetNumRatioDowngrade(fractionator.fluidId);
                    break;
                case IFE点数聚集分馏塔:
                    successRatePlus = fractionator.fluidInputInc >= 10
                        ? successRatePlus * fractionator.fluidInputInc / fractionator.fluidInputCount * 2.5f
                        : 0;
                    dic = defaultDicNoDestroy;
                    break;
                case IFE增产分馏塔:
                    successRatePlus = (float)MaxTableMilli(fluidInputAvgInc) / 2.5f;
                    dic = IPFDic[fractionator.fluidId];
                    break;
                default://I分馏塔，I垃圾回收分馏塔
                    successRatePlus *= 1.0f + (float)MaxTableMilli(fluidInputAvgInc);
                    dic = defaultDicNoDestroy;
                    break;
            }
            //根据建筑类型、初步的分馏概率表、输入情况，计算实际的分馏概率表
            float flowRatio = 1.0f;
            StringBuilder sb1 = new StringBuilder();
            if (EnableFracForever
                && buildingID != IFE升级分馏塔
                && fractionator.productOutputCount >= fractionator.productOutputMax / 2) {
                sb1.Append("0(0%)\n");
            } else if (buildingID == IFE点数聚集分馏塔) {
                float ratio = 0.01f * successRatePlus;
                sb1.Append($"1({ratio.FormatP()})\n");
                flowRatio -= ratio;
            } else {
                foreach (var p in dic) {
                    if (p.Key < 0) {
                        continue;
                    }
                    float ratio = p.Value * successRatePlus;
                    sb1.Append($"{p.Key}({ratio.FormatP()})\n");
                    flowRatio -= ratio;
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
            } else {
                s1 = sb1.ToString().Substring(0, sb1.Length - 1);
                s2 = $"{"流动".Translate()}({flowRatio.FormatP()})";
                if (destroyRatio > 0) {
                    s2 += $"\n{"损毁".Translate()}({destroyRatio.FormatP()})";
                }
            }
            __instance.productProbText.text = s1;
            lastProductProbText = s1;
            __instance.oriProductProbText.text = s2;
            lastOriProductProbText = s2;
            //刷新概率显示位置
            float upY = productProbTextBaseY + 9f * (s1.Split('\n').Length - 1);
            __instance.productProbText.transform.localPosition = new(0, upY, 0);
            float downY = oriProductProbTextBaseY - (destroyRatio > 0 ? 9f : 0);
            __instance.oriProductProbText.transform.localPosition = new(0, downY, 0);
        }

        #endregion
    }
}
