using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using HarmonyLib;
using System;
using System.Collections.Generic;
using xiaoye97;

namespace FractionateEverything
{
    class AcceptableIntValue(int defval, int min, int max) : AcceptableValueBase(typeof(int))
    {
        private readonly int defval = defval >= min && defval <= max ? defval : min;
        private readonly int min = min;
        private readonly int max = max;
        public override object Clamp(object value) => IsValid(value) ? (int)value : defval;
        public override bool IsValid(object value) => value.GetType() == ValueType && (int)value >= min && (int)value <= max;
        public override string ToDescriptionString() => null;
    }

    class AcceptableBoolValue(bool defval) : AcceptableValueBase(typeof(bool))
    {
        private readonly bool defval = defval;
        public override object Clamp(object value) => IsValid(value) ? (bool)value : defval;
        public override bool IsValid(object value) => value.GetType() == ValueType;
        public override string ToDescriptionString() => null;
    }

    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(TabSystem), nameof(LocalizationModule))]
    public class FractionateEverything : BaseUnityPlugin
    {
        public const string GUID = "com.menglei.dsp." + NAME;
        public const string NAME = "FractionateEverything";
        public const string VERSION = "1.1.0";
        public static ManualLogSource logger;

        /// <summary>
        /// 分馏比例，值越小分馏越快。
        /// </summary>
        private static int ratio;
        /// <summary>
        /// 是否启用前置科技。如果不启用，所有分馏配方将在开局可用。
        /// </summary>
        private static bool usePreTech;
        /// <summary>
        /// 是否显示所有分馏配方。
        /// </summary>
        private static bool showRecipes;
        /// <summary>
        /// 分馏配方从哪页开始显示。
        /// </summary>
        private static int firstPage;
        /// <summary>
        /// 新增分馏配方的ID，使用后应+1。
        /// </summary>
        private static int nextRecipeID;

        public enum Item
        {
            铁矿 = 1001,
            铜矿 = 1002,
            硅石 = 1003,
            钛石 = 1004,
            石矿 = 1005,
            煤矿 = 1006,
            木材 = 1030,
            植物燃料 = 1031,
            可燃冰 = 1011,
            金伯利矿石 = 1012,
            分形硅石 = 1013,
            光栅石 = 1014,
            刺笋结晶 = 1015,
            单极磁石 = 1016,
            铁块 = 1101,
            铜块 = 1104,
            高纯硅块 = 1105,
            钛块 = 1106,
            石材 = 1108,
            高能石墨 = 1109,
            钢材 = 1103,
            钛合金 = 1107,
            玻璃 = 1110,
            钛化玻璃 = 1119,
            棱镜 = 1111,
            金刚石 = 1112,
            晶格硅 = 1113,
            齿轮 = 1201,
            磁铁 = 1102,
            磁线圈 = 1202,
            电动机 = 1203,
            电磁涡轮 = 1204,
            超级磁场环 = 1205,
            粒子容器 = 1206,
            奇异物质 = 1127,
            电路板 = 1301,
            处理器 = 1303,
            量子芯片 = 1305,
            微晶元件 = 1302,
            位面过滤器 = 1304,
            粒子宽带 = 1402,
            电浆激发器 = 1401,
            光子合并器 = 1404,
            太阳帆 = 1501,
            水 = 1000,
            原油 = 1007,
            精炼油 = 1114,
            硫酸 = 1116,
            氢 = 1120,
            重氢 = 1121,
            反物质 = 1122,
            临界光子 = 1208,
            液氢燃料棒 = 1801,
            氘核燃料棒 = 1802,
            反物质燃料棒 = 1803,
            奇异湮灭燃料棒 = 1804,
            塑料 = 1115,
            石墨烯 = 1123,
            碳纳米管 = 1124,
            有机晶体 = 1117,
            钛晶石 = 1118,
            卡西米尔晶体 = 1126,
            燃烧单元 = 1128,
            爆破单元 = 1129,
            晶石爆破单元 = 1130,
            引力透镜 = 1209,
            空间翘曲器 = 1210,
            湮灭约束球 = 1403,
            动力引擎 = 1407,
            推进器 = 1405,
            加力推进器 = 1406,
            配送运输机 = 5003,
            物流运输机 = 5001,
            星际物流运输船 = 5002,
            框架材料 = 1125,
            戴森球组件 = 1502,
            小型运载火箭 = 1503,
            地基 = 1131,
            增产剂MkI = 1141,
            增产剂MkII = 1142,
            增产剂MkIII = 1143,
            机枪弹箱 = 1601,
            钛化弹箱 = 1602,
            超合金弹箱 = 1603,
            炮弹组 = 1604,
            高爆炮弹组 = 1605,
            晶石炮弹组 = 1606,
            等离子胶囊 = 1607,
            反物质胶囊 = 1608,
            导弹组 = 1609,
            超音速导弹组 = 1610,
            引力导弹组 = 1611,
            干扰胶囊 = 1612,
            压制胶囊 = 1613,
            原型机 = 5101,
            精准无人机 = 5102,
            攻击无人机 = 5103,
            护卫舰 = 5111,
            驱逐舰 = 5112,
            黑雾矩阵 = 5201,
            硅基神经元 = 5202,
            物质重组器 = 5203,
            负熵奇点 = 5204,
            核心素 = 5205,
            能量碎片 = 5206,
            传送带 = 2001,
            高速传送带 = 2002,
            极速传送带 = 2003,
            分拣器 = 2011,
            高速分拣器 = 2012,
            极速分拣器 = 2013,
            集装分拣器 = 2014,
            四向分流器 = 2020,
            自动集装机 = 2040,
            流速监测器 = 2030,
            喷涂机 = 2313,
            物流配送器 = 2107,
            小型储物仓 = 2101,
            大型储物仓 = 2102,
            储液罐 = 2106,
            制造台MkI = 2303,
            制造台MkII = 2304,
            制造台MkIII = 2305,
            重组式制造台 = 2318,
            电力感应塔 = 2201,
            无线输电塔 = 2202,
            卫星配电站 = 2212,
            风力涡轮机 = 2203,
            火力发电厂 = 2204,
            微型聚变发电站 = 2211,
            地热发电站 = 2213,
            采矿机 = 2301,
            大型采矿机 = 2316,
            抽水站 = 2306,
            电弧熔炉 = 2302,
            位面熔炉 = 2315,
            负熵熔炉 = 2319,
            原油萃取站 = 2307,
            原油精炼厂 = 2308,
            化工厂 = 2309,
            量子化工厂 = 2317,
            分馏塔 = 2314,
            太阳能板 = 2205,
            蓄电器 = 2206,
            蓄电器满 = 2207,
            电磁轨道弹射器 = 2311,
            射线接收站 = 2208,
            垂直发射井 = 2312,
            能量枢纽 = 2209,
            微型粒子对撞机 = 2310,
            人造恒星 = 2210,
            行星内物流运输站 = 2103,
            星际物流运输站 = 2104,
            轨道采集器 = 2105,
            矩阵研究站 = 2901,
            自演化研究站 = 2902,
            高斯机枪塔 = 3001,
            高频激光塔 = 3002,
            聚爆加农炮 = 3003,
            磁化电浆炮 = 3004,
            导弹防御塔 = 3005,
            干扰塔 = 3006,
            信号塔 = 3007,
            行星护盾发生器 = 3008,
            战场分析基站 = 3009,
            近程电浆塔 = 3010,
            电磁矩阵 = 6001,
            能量矩阵 = 6002,
            结构矩阵 = 6003,
            信息矩阵 = 6004,
            引力矩阵 = 6005,
            宇宙矩阵 = 6006,
            沙土 = 1099,
        }

        public void Awake()
        {
            logger = Logger;

            LocalizationModule.RegisterTranslation("分馏页面1f", "ItemFrac", "物品分馏", "ItemFrac");
            LocalizationModule.RegisterTranslation("分馏页面2f", "BuildingFrac", "建筑分馏", "BuildingFrac");
            LocalizationModule.RegisterTranslation("分馏f", " Fractionation", "分馏", " Fractionation");
            LocalizationModule.RegisterTranslation("从f", "Fractionate ", "从", "Fractionate ");
            LocalizationModule.RegisterTranslation("中分馏出f", " to ", "中分馏出", " to ");
            LocalizationModule.RegisterTranslation("。f", ".", "。", ".");

            ConfigEntry<int> FractionateDifficulty = Config.Bind("config", "FractionateDifficulty", 5,
                new ConfigDescription("Lower means easier and faster to fractionate (1-5).\n" +
                "值越小代表越简单，能更高效地分馏出产物（1-5）。", new AcceptableIntValue(5, 1, 5), null));
            ratio = new List<int> { 20, 30, 44, 67, 100 }[FractionateDifficulty.Value - 1];

            ConfigEntry<bool> UsePreTech = Config.Bind("config", "UsePreTech", true,
                new ConfigDescription("Whether or not to use front-end tech.\n" +
                "If set to false, all fractionation recipes are unlocked at the beginning.\n" +
                "是否使用前置科技。\n" +
                "如果设为false，所有分馏配方都会在开局解锁。", new AcceptableBoolValue(true), null));
            usePreTech = UsePreTech.Value;

            ConfigEntry<bool> ShowFractionateRecipes = Config.Bind("config", "ShowFractionateRecipes", true,
                new ConfigDescription("Whether show all fractionate recipes or not.\n" +
                "是否显示所有的分馏配方。", new AcceptableBoolValue(true), null));
            showRecipes = ShowFractionateRecipes.Value;

            ConfigEntry<int> FirstPage = Config.Bind("config", "FirstPage", 3,
                new ConfigDescription("If ShowFractionateRecipes is turned on, new fractionated recipes will be displayed starting from this page (3-7).\n" +
                "Used to avoid possible recipe display issues between mods.\n" +
                "Tip: This mod requires *TWO pages* to show all the added fractionation recipes.\n" +
                "如果ShowFractionateRecipes已开启，新的分馏配方将从此页开始显示（3-7）。\n" +
                "用于避免mod之间可能存在的配方显示问题。\n" +
                "提示：本MOD需要*两页*来显示所有新增的分馏配方。", new AcceptableIntValue(3, 3, 7), null));
            firstPage = FirstPage.Value;

            //配方ID是int型，没有限制
            ConfigEntry<int> FirstRecipeID = Config.Bind("config", "FirstRecipeID", 1000,
                new ConfigDescription("Which recipe ID to start adding fractionated recipes (1000-100000).\n" +
                "Can be used to avoid recipe conflicts with other mods.\n" +
                "从哪个ID开始添加分馏配方（1000-100000）。\n" +
                "用于避免mod之间可能存在的配方ID冲突。", new AcceptableIntValue(1000, 1000, 100000), null));
            nextRecipeID = FirstRecipeID.Value;

            Config.Save();

            if (showRecipes)
            {
                string iconPath = LDB.items.Select((int)Item.分馏塔).IconPath;
                TabSystem.RegisterTab(GUID + "Tab1", new TabData("分馏页面1f".Translate(), iconPath));
                TabSystem.RegisterTab(GUID + "Tab2", new TabData("分馏页面2f".Translate(), iconPath));
            }

            LDBTool.PreAddDataAction += PreAddDataAction;
            LDBTool.PostAddDataAction += PostAddDataAction;
            Harmony.CreateAndPatchAll(typeof(FractionateEverything), GUID);
        }

        private void PreAddDataAction()
        {
            //添加分馏配方，注意分馏只能单产物，不能多产物！
            AddFracChain(Item.采矿机, Item.大型采矿机);
            AddFracChain(Item.小型储物仓, Item.大型储物仓);
            AddFracChain(Item.传送带, Item.高速传送带, Item.极速传送带);
            AddFracChain(Item.分拣器, Item.高速分拣器, Item.极速分拣器, Item.集装分拣器);
            AddFracChain(Item.电弧熔炉, Item.位面熔炉, Item.负熵熔炉);
            AddFracChain(Item.制造台MkI, Item.制造台MkII, Item.制造台MkIII, Item.重组式制造台);
            AddFracChain(Item.化工厂, Item.量子化工厂);
            AddFracChain(Item.矩阵研究站, Item.自演化研究站);
            AddFracChain(Item.动力引擎, Item.推进器, Item.加力推进器);
            AddFracChain(Item.配送运输机, Item.物流运输机, Item.星际物流运输船);
            AddFracChain(Item.物流配送器, Item.行星内物流运输站, Item.星际物流运输站, Item.轨道采集器);
            AddFracChain(Item.增产剂MkI, Item.增产剂MkII, Item.增产剂MkIII);
            AddFracChain(Item.液氢燃料棒, Item.氘核燃料棒, Item.反物质燃料棒, Item.奇异湮灭燃料棒);
            AddFracChain(Item.电力感应塔, Item.无线输电塔, Item.卫星配电站);
            AddFracChain(Item.风力涡轮机, Item.太阳能板, Item.能量枢纽);
            AddFracChain(Item.火力发电厂, Item.地热发电站, Item.微型聚变发电站, Item.人造恒星);
            AddFracChain(Item.燃烧单元, Item.爆破单元, Item.晶石爆破单元);
            AddFracChain(Item.机枪弹箱, Item.钛化弹箱, Item.超合金弹箱);
            AddFracChain(Item.炮弹组, Item.高爆炮弹组, Item.晶石炮弹组);
            AddFracChain(Item.等离子胶囊, Item.反物质胶囊);
            AddFracChain(Item.导弹组, Item.超音速导弹组, Item.引力导弹组);
            AddFracChain(Item.干扰胶囊, Item.压制胶囊);
            AddFracChain(Item.原型机, Item.精准无人机, Item.攻击无人机, Item.护卫舰, Item.驱逐舰);
            AddFracChain(Item.能量碎片, Item.硅基神经元, Item.物质重组器, Item.负熵奇点, Item.核心素, Item.黑雾矩阵, Item.电磁矩阵, Item.能量矩阵, Item.结构矩阵, Item.信息矩阵, Item.引力矩阵, Item.宇宙矩阵);
            AddFracChain(Item.战场分析基站, Item.干扰塔, Item.信号塔, Item.行星护盾发生器, Item.高斯机枪塔, Item.高频激光塔, Item.聚爆加农炮, Item.磁化电浆炮, Item.导弹防御塔, Item.近程电浆塔);
            AddFracChain(Item.水, Item.氢);
            AddFracChain(Item.临界光子, Item.反物质);
            AddFracChain(Item.电磁轨道弹射器, Item.垂直发射井, Item.射线接收站);
            AddFracChain(Item.铁矿, Item.铁块, Item.钢材);
            AddFracChain(Item.磁铁, Item.磁线圈, Item.电动机, Item.电磁涡轮, Item.超级磁场环);
            AddFracChain(Item.铜矿, Item.铜块, Item.粒子容器, Item.奇异物质, Item.引力透镜);
            AddFracChain(Item.电路板, Item.处理器, Item.量子芯片);
            AddFracChain(Item.硅石, Item.高纯硅块, Item.微晶元件);
            AddFracChain(Item.钛石, Item.钛块, Item.钛合金, Item.框架材料, Item.戴森球组件, Item.小型运载火箭);
            AddFracChain(Item.石矿, Item.石材, Item.地基);
            AddFracChain(Item.玻璃, Item.钛化玻璃, Item.位面过滤器);
            AddFracChain(Item.棱镜, Item.光子合并器, Item.太阳帆);
            AddFracChain(Item.煤矿, Item.高能石墨, Item.石墨烯, Item.碳纳米管, Item.粒子宽带);
            AddFracChain(Item.原油, Item.精炼油, Item.塑料, Item.有机晶体, Item.钛晶石, Item.卡西米尔晶体);
        }

        private void PostAddDataAction()
        {
        }

        /// <summary>
        /// 每次读取游戏后重置分塔馏可接受的物品
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "Import")]
        public static void ImportPostPatch()
        {
            int oldLen = RecipeProto.fractionatorRecipes.Length;
            ReloadFractionateNeeds();
            logger.LogDebug($"[ImportPostPatch]RecipeProto.fractionatorRecipes.Length: {oldLen} -> {RecipeProto.fractionatorRecipes.Length}");
        }

        /// <summary>
        /// 解锁科技后重置分塔馏可接受的物品
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameHistoryData), "UnlockTech")]
        public static void UnlockRecipePostPatch()
        {
            int oldLen = RecipeProto.fractionatorRecipes.Length;
            ReloadFractionateNeeds();
            logger.LogDebug($"[UnlockRecipePostPatch]RecipeProto.fractionatorRecipes.Length: {oldLen} -> {RecipeProto.fractionatorRecipes.Length}");
        }

        private static void ReloadFractionateNeeds()
        {
            RecipeProto[] dataArray = LDB.recipes.dataArray;
            List<RecipeProto> list = [];
            List<int> list2 = [];
            for (int i = 0; i < dataArray.Length; i++)
            {
                if (dataArray[i].Type == ERecipeType.Fractionate && GameMain.history.RecipeUnlocked(dataArray[i].ID))
                {
                    list.Add(dataArray[i]);
                    list2.Add(dataArray[i].Items[0]);
                }
            }
            RecipeProto.fractionatorRecipes = [.. list];
            RecipeProto.fractionatorNeeds = [.. list2];
        }

        /// <summary>
        /// 在post时，添加一个分馏链的所有分馏配方。
        /// </summary>
        /// <param name="itemChain">分馏产物链，第i个物品可以分馏出第i+1个物品</param>
        private void AddFracChain(params Item[] itemChain)
        {
            //如果有x个产品，则有x-1个分馏配方
            for (int i = 0; i < itemChain.Length - 1; i++)
            {
                try
                {
                    //LDB.ItemName 等价于 itemproto.name
                    //name: 推进器  name.Translate: <0xa0>-<0xa0>推进器  Name: 推进器2  Name.Translate: 推进器
                    //name: Thruster  name.Translate: Thruster  Name: 推进器2  Name.Translate: Thruster
                    //name: 制造台<0xa0>Mk.I  name.Translate: 制造台<0xa0>Mk.I  Name: 制造台 Mk.I  Name.Translate: 制造台<0xa0>Mk.I
                    //name: Assembling Machine Mk.I  name.Translate: Assembling Machine Mk.I  Name: 制造台 Mk.I  Name.Translate: Assembling Machine Mk.I
                    int inputItemID = (int)itemChain[i];
                    int outputItemID = (int)itemChain[i + 1];
                    int recipeID = nextRecipeID++;
                    ItemProto inputItem = LDB.items.Select(inputItemID);
                    ItemProto outputItem = LDB.items.Select(outputItemID);
                    //如果没有前置科技，将前置科技设为解锁分馏塔的科技
                    int preTech = outputItem.preTech == null || !usePreTech ? 1 : outputItem.preTech.ID;
                    //调整无可用配方、以及首个配方与其他分馏物品相同的部分物品
                    int gridIndex;
                    switch (outputItemID)
                    {
                        case (int)Item.精炼油: gridIndex = 3207; break;
                        case (int)Item.硅基神经元: gridIndex = 3807; break;
                        case (int)Item.物质重组器: gridIndex = 3808; break;
                        case (int)Item.负熵奇点: gridIndex = 3809; break;
                        case (int)Item.核心素: gridIndex = 3810; break;
                        case (int)Item.黑雾矩阵: gridIndex = 3811; break;
                        default:
                            gridIndex = outputItem.recipes.Count == 0
                                ? 0
                                : outputItem.recipes[0].GridIndex + (firstPage - 1) * 1000; break;
                    }
                    RecipeProto r = ProtoRegistry.RegisterRecipe(
                        recipeID, ERecipeType.Fractionate, 60,
                        [inputItemID], [ratio], [outputItemID], [1],
                        "从f".Translate() + inputItem.name + "中分馏出f".Translate() + outputItem.name + "。f".Translate(),
                        preTech,
                        gridIndex,
                        outputItem.name + "分馏".Translate(),
                        outputItem.IconPath);
                    r.ID = recipeID;//重新设定ID，直接Register的id不对
                    r.Handcraft = false;//不能手动制作
                    r.Explicit = true;//作为公式显示
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }
            }
        }
    }
}
