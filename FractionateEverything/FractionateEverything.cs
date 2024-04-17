using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
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
        public const string VERSION = "1.2.0";
        public static ManualLogSource logger;

        /// <summary>
        /// 指示分馏基础速率，值越小分馏越快。
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
        private AssetBundle ab;

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

            ab = AssetBundle.LoadFromStream(
                Assembly.GetExecutingAssembly().GetManifestResourceStream("FractionateEverything.fracicons"));

            LDBTool.EditDataAction += EditDataAction;
            LDBTool.PreAddDataAction += PreAddDataAction;
            LDBTool.PostAddDataAction += PostAddDataAction;
            Harmony.CreateAndPatchAll(typeof(FractionateEverything), GUID);
        }

        private DateTime lastUpdateTime = DateTime.Now;

        public void Update()
        {
            DateTime time = DateTime.Now;
            if ((time - lastUpdateTime).TotalMilliseconds > 200)
            {
                lastUpdateTime = time;
                //running为true表示游戏已经加载好（不是在某存档内部，仅仅是加载好能显示主界面了）
                if (GameMain.instance != null && GameMain.instance.running)
                {
                    //不断更新分馏塔可接受哪些物品
                    int oldLen = RecipeProto.fractionatorRecipes.Length;
                    if (usePreTech)
                    {
                        //仅添加已解锁的配方
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
                    else
                    {
                        //添加所有配方
                        RecipeProto.InitFractionatorNeeds();
                    }
                    int currLen = RecipeProto.fractionatorRecipes.Length;
                    if (oldLen != currLen)
                    {
                        logger.LogInfo($"RecipeProto.fractionatorRecipes.Length: {oldLen} -> {currLen}");
                    }
                }
            }
        }

        private void EditDataAction(Proto proto) { }

        private void PreAddDataAction()
        {
#if DEBUG
            File.Delete(SPRITE_CSV_PATH);
#endif
            //添加分馏配方。每个分馏配方只能有一种输入和一种输出；不同分馏配方原料必须唯一，产物可以相同。
            //建筑I
            AddCycleFracChain(Item.电力感应塔, Item.无线输电塔, Item.卫星配电站);
            AddCycleFracChain(Item.风力涡轮机, Item.太阳能板, Item.蓄电器, Item.蓄电器满, Item.能量枢纽);
            AddCycleFracChain(Item.火力发电厂, Item.地热发电站, Item.微型聚变发电站, Item.人造恒星);
            //建筑II
            AddCycleFracChain(Item.传送带, Item.高速传送带, Item.极速传送带);
            AddCycleFracChain(Item.四向分流器, Item.自动集装机, Item.流速监测器, Item.喷涂机);
            AddCycleFracChain(Item.小型储物仓, Item.大型储物仓, Item.储液罐);
            AddCycleFracChain(Item.物流配送器, Item.行星内物流运输站, Item.星际物流运输站, Item.轨道采集器);
            //建筑III
            AddCycleFracChain(Item.分拣器, Item.高速分拣器, Item.极速分拣器, Item.集装分拣器);
            AddCycleFracChain(Item.采矿机, Item.大型采矿机);
            AddCycleFracChain(Item.抽水站, Item.原油萃取站, Item.原油精炼厂);
            AddCycleFracChain(Item.分馏塔, Item.微型粒子对撞机);
            AddCycleFracChain(Item.化工厂, Item.量子化工厂);
            //建筑IV
            AddCycleFracChain(Item.电弧熔炉, Item.位面熔炉, Item.负熵熔炉);
            AddCycleFracChain(Item.制造台MkI, Item.制造台MkII, Item.制造台MkIII, Item.重组式制造台);
            AddCycleFracChain(Item.矩阵研究站, Item.自演化研究站);
            AddCycleFracChain(Item.电磁轨道弹射器, Item.射线接收站, Item.垂直发射井);
            //建筑V
            AddCycleFracChain(Item.高斯机枪塔, Item.导弹防御塔, Item.聚爆加农炮);
            AddCycleFracChain(Item.高频激光塔, Item.磁化电浆炮, Item.近程电浆塔);
            AddCycleFracChain(Item.战场分析基站, Item.信号塔, Item.干扰塔, Item.行星护盾发生器);
            //物品左侧区域
            //矿物自增值比冶炼更有意义
            AddFracRecipe(Item.铁矿, Item.铁矿, 4);
            AddFracRecipe(Item.铜矿, Item.铜矿, 4);
            AddFracRecipe(Item.硅石, Item.硅石, 4);
            AddFracRecipe(Item.钛石, Item.钛石, 4);
            AddFracRecipe(Item.石矿, Item.石矿, 4);
            AddFracRecipe(Item.煤矿, Item.煤矿, 4);
            AddFracRecipe(Item.可燃冰, Item.可燃冰, 2);
            AddFracRecipe(Item.金伯利矿石, Item.金伯利矿石, 2);
            AddFracRecipe(Item.分形硅石, Item.分形硅石, 2);
            AddFracRecipe(Item.光栅石, Item.光栅石, 2);
            AddFracRecipe(Item.刺笋结晶, Item.刺笋结晶, 2);
            AddFracRecipe(Item.单极磁石, Item.单极磁石, 2);
            //部分非循环链
            AddFracChain(Item.铁块, Item.钢材, Item.钛合金, Item.框架材料, Item.戴森球组件, Item.小型运载火箭);
            AddFracChain(Item.高纯硅块, Item.晶格硅);
            AddFracChain(Item.石材, Item.地基);
            AddFracChain(Item.玻璃, Item.钛化玻璃, Item.位面过滤器);
            AddFracChain(Item.棱镜, Item.电浆激发器, Item.光子合并器, Item.太阳帆);
            AddFracChain(Item.高能石墨, Item.金刚石);
            AddFracChain(Item.石墨烯, Item.碳纳米管, Item.粒子宽带);
            AddFracChain(Item.粒子容器, Item.奇异物质, Item.引力透镜, Item.空间翘曲器);
            AddFracChain(Item.精炼油, Item.塑料, Item.有机晶体, Item.钛晶石, Item.卡西米尔晶体);
            //部分循环链
            AddCycleFracChain(Item.水, Item.硫酸);
            AddFracChain(Item.磁铁, Item.磁线圈, Item.电动机);
            AddCycleFracChain(Item.电动机, Item.电磁涡轮, Item.超级磁场环);
            AddCycleFracChain(Item.电路板, Item.处理器, Item.量子芯片);
            AddCycleFracChain(Item.原型机, Item.精准无人机, Item.攻击无人机);
            AddCycleFracChain(Item.护卫舰, Item.驱逐舰);
            AddFracChain(Item.能量碎片, Item.硅基神经元, Item.物质重组器, Item.负熵奇点, Item.核心素, Item.黑雾矩阵);
            AddCycleFracChain(Item.黑雾矩阵, Item.电磁矩阵, Item.能量矩阵, Item.结构矩阵, Item.信息矩阵, Item.引力矩阵);
            AddFracRecipe(Item.宇宙矩阵, Item.宇宙矩阵, 2);
            AddFracRecipe(Item.临界光子, Item.反物质);
            AddFracRecipe(Item.反物质, Item.临界光子, 2);
            //物品右侧区域
            AddCycleFracChain(Item.增产剂MkI, Item.增产剂MkII, Item.增产剂MkIII);
            AddCycleFracChain(Item.燃烧单元, Item.爆破单元, Item.晶石爆破单元);
            AddCycleFracChain(Item.动力引擎, Item.推进器, Item.加力推进器);
            AddCycleFracChain(Item.配送运输机, Item.物流运输机, Item.星际物流运输船);
            AddCycleFracChain(Item.液氢燃料棒, Item.氘核燃料棒, Item.反物质燃料棒, Item.奇异湮灭燃料棒);
            AddCycleFracChain(Item.机枪弹箱, Item.钛化弹箱, Item.超合金弹箱);
            AddCycleFracChain(Item.炮弹组, Item.高爆炮弹组, Item.晶石炮弹组);
            AddCycleFracChain(Item.导弹组, Item.超音速导弹组, Item.引力导弹组);
            AddCycleFracChain(Item.等离子胶囊, Item.反物质胶囊);
            AddCycleFracChain(Item.干扰胶囊, Item.压制胶囊);
        }

        private void PostAddDataAction()
        {
        }

        /// <summary>
        /// 添加一个分馏链的所有分馏配方。
        /// 第i个物品分馏出第i+1个物品，前置科技为第i+1个物品的前置科技；
        /// 链尾物品会分馏为第1个物品，前置科技使用链尾物品的前置科技。
        /// </summary>
        /// <param name="itemChain">分馏链</param>
        private void AddCycleFracChain(params Item[] itemChain)
        {
            AddFracChain([.. itemChain, itemChain[0]], true);
        }

        /// <summary>
        /// 添加一个分馏链的所有分馏配方。
        /// 第i个物品分馏出第i+1个物品，前置科技为第i+1个物品的前置科技。
        /// </summary>
        /// <param name="itemChain">分馏链</param>
        private void AddFracChain(params Item[] itemChain)
        {
            AddFracChain(itemChain, false);
        }

        /// <summary>
        /// 添加一个分馏链的所有分馏配方。
        /// 第i个物品分馏出第i+1个物品，前置科技为第i+1个物品的前置科技；
        /// 链尾物品会分馏为第1个物品，前置科技根据传入的参数选择链尾或第1个物品的前置科技。
        /// </summary>
        /// <param name="itemChain">分馏链</param>
        /// <param name="lastUseInputTech">如果为true，链尾物品分馏时前置科技使用链尾物品的前置科技；否则使用第1个物品的前置科技</param>
        private void AddFracChain(Item[] itemChain, bool lastUseInputTech)
        {
            for (int i = 0; i < itemChain.Length - 1; i++)
            {
                AddFracRecipe(itemChain[i], itemChain[i + 1], 1, lastUseInputTech && i == itemChain.Length - 2);
            }
        }

        /// <summary>
        /// 存储所有配方的显示位置，以确保没有显示位置冲突
        /// </summary>
        private readonly List<int> gridIndexList = [];
        /// <summary>
        /// 如果发生显示位置冲突，从这里开始显示冲突的配方
        /// </summary>
        private int currLastIdx = 4601;
        /// <summary>
        /// 存储所有分馏产物个数大于1的配方
        /// </summary>
        private static readonly Dictionary<int, int> specialOutputNumDic = [];
#if DEBUG
        /// <summary>
        /// sprite名称将被记录在该文件中。
        /// </summary>
        private readonly string SPRITE_CSV_PATH = "D:\\project\\csharp\\DSP MOD\\MLJ_DSPmods\\gamedata\\fracIconPath.csv";
#endif

        /// <summary>
        /// 添加一个分馏配方。
        /// </summary>
        /// <param name="input">分馏原料</param>
        /// <param name="output">分馏产物</param>
        /// <param name="outputNum">分馏产物的数目，大于1表示物品增值（个数凭空增加）</param>
        /// <param name="useInputTech">如果为true，表示前置科技使用原料的前置科技；否则前置科技使用产物的前置科技</param>
        private void AddFracRecipe(Item input, Item output, int outputNum = 1, bool useInputTech = false)
        {
            //LDB.ItemName 等价于 itemproto.name，itemproto.name 等价于 itemproto.Name.Translate()
            //name: 推进器  name.Translate: <0xa0>-<0xa0>推进器  Name: 推进器2  Name.Translate: 推进器
            //name: Thruster  name.Translate: Thruster  Name: 推进器2  Name.Translate: Thruster
            //name: 制造台<0xa0>Mk.I  name.Translate: 制造台<0xa0>Mk.I  Name: 制造台 Mk.I  Name.Translate: 制造台<0xa0>Mk.I
            //name: Assembling Machine Mk.I  name.Translate: Assembling Machine Mk.I  Name: 制造台 Mk.I  Name.Translate: Assembling Machine Mk.I
            try
            {
                int inputItemID = (int)input;
                int outputItemID = (int)output;
                int recipeID = nextRecipeID++;
                ItemProto inputItem = LDB.items.Select(inputItemID);
                ItemProto outputItem = LDB.items.Select(outputItemID);
                //尝试获取前置科技
                TechProto preTech = null;
                if (usePreTech)
                {
                    preTech = useInputTech ? inputItem.preTech : outputItem.preTech;
                }
                //前置科技如果为null，【必须】修改为戴森球计划，才能确保某些配方能正常解锁、显示
                preTech ??= LDB.techs.Select(1);
                //调整部分配方的显示位置，包括产物无配方能生成产物、显示位置重合、分馏循环链影响的情况
                int gridIndex = input switch
                {
                    Item.铁矿 => 3101,
                    Item.铜矿 => 3102,
                    Item.硅石 => 3103,
                    Item.钛石 => 3104,
                    Item.石矿 => 3105,
                    Item.煤矿 => 3106,
                    Item.可燃冰 => 3208,
                    Item.金伯利矿石 => 3306,
                    Item.分形硅石 => 3303,
                    Item.光栅石 => 3605,
                    Item.刺笋结晶 => 3508,
                    Item.单极磁石 => 3606,
                    Item.硫酸 => 3407,
                    Item.磁铁 => 3201,
                    Item.磁线圈 => 3202,
                    Item.临界光子 => 3707,
                    Item.反物质 => 3708,
                    Item.黑雾矩阵 => 3801,
                    Item.引力矩阵 => 3806,
                    Item.宇宙矩阵 => 3807,
                    Item.能量碎片 => 3808,
                    Item.硅基神经元 => 3809,
                    Item.物质重组器 => 3810,
                    Item.负熵奇点 => 3811,
                    Item.核心素 => 3812,
                    Item.蓄电器 => 4113,
                    _ => outputItem.recipes.Count == 0
                         ? 0
                         : outputItem.recipes[0].GridIndex + (firstPage - 1) * 1000,
                };
                if (gridIndex == 0 || gridIndexList.Contains(gridIndex))
                {
                    logger.LogWarning($"配方{outputItem.Name + "分馏"}显示位置{gridIndex}已被占用，调整至{currLastIdx}！");
                    gridIndex = currLastIdx++;
                }
                gridIndexList.Add(gridIndex);
                //获取重氢分馏类似样式的图标。图标由python拼接，由unity打包
                string inputIconName = inputItem.IconPath.Substring(inputItem.IconPath.LastIndexOf("/") + 1);
                string outputIconName = outputItem.IconPath.Substring(outputItem.IconPath.LastIndexOf("/") + 1);
                Sprite sprite = ab.LoadAsset<Sprite>(inputIconName + "-" + outputIconName + "-formula");
                if (sprite == null)
                {
                    sprite = outputItem.iconSprite;
                    logger.LogWarning($"缺失{inputItem.name}->{outputItem.name}的图标，使用产物图标代替！");
                }
                //ProtoRegistry.RegisterRecipe用起来有很多问题，自己创建不容易出bug
                RecipeProto r = new()
                {
                    Type = ERecipeType.Fractionate,
                    Handcraft = false,
                    Explicit = true,
                    TimeSpend = 60,
                    Items = [inputItemID],
                    //输入产物乘outputNum，以保证配方分馏成功率不变
                    ItemCounts = [ratio * outputNum],
                    Results = [outputItemID],
                    //outputNum大于1时，仅起到配方显示成功率与分馏成功率提升的效果，并不代表能分出多个；
                    //实际分馏出多个是通过FractionatorInternalUpdatePatch方法达成的
                    ResultCounts = [outputNum],
                    Description = "R" + inputItem.Name + "分馏" + outputItem.Name,
                    description = "从f".Translate() + inputItem.name + "中分馏出f".Translate() + outputItem.name + "。f".Translate(),
                    GridIndex = gridIndex,
                    //IconPath = iconPath,
                    Name = inputItem.Name + "分馏" + outputItem.Name,
                    name = outputItem.name + "分馏f".Translate(),
                    preTech = preTech,
                    ID = recipeID
                };
                Traverse.Create(r).Field("_iconSprite").SetValue(sprite);
                LDBTool.PreAddProto(r);
                //add之后要再次设定ID，不然id会莫名其妙变化。不知道这个bug怎么回事，反正这样就正常了。
                r.ID = recipeID;
                //FractionatorInternalUpdatePatch需要用到该数据实现某些配方分馏出多个产物
                specialOutputNumDic.Add(inputItemID, outputNum);
#if DEBUG
                //logger.LogDebug(
                //    $"\nID{r.ID} index{r.index} {outputItem.name + "分馏f".Translate()}\n" +
                //    $"Handcraft:{r.Handcraft} Explicit:{r.Handcraft} GridIndex:{r.GridIndex}\n" +
                //    $"hasIcon:{r.hasIcon} IconPath:{r.IconPath} iconSprite:{r.iconSprite}\n" +
                //    $"preTech:{r.preTech} preTech.ID:{r.preTech?.ID}\n" +
                //    $"NonProductive:{r.NonProductive} productive:{r.productive}\n");
                //输出分馏配方需要的图标的路径，以便于制作图标
                if (Directory.Exists(SPRITE_CSV_PATH.Substring(0, SPRITE_CSV_PATH.LastIndexOf('\\'))))
                {
                    using StreamWriter sw = new(SPRITE_CSV_PATH, true);
                    sw.WriteLine(inputIconName + "," + outputIconName);
                }
#endif
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }

        /// <summary>
        /// 用于判定分馏产物是否翻倍输出。
        /// </summary>
        private static uint seed2 = (uint)new System.Random().Next(int.MinValue, int.MaxValue);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FractionatorComponent), "InternalUpdate")]
        public static bool FractionatorInternalUpdatePatch(ref FractionatorComponent __instance, PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister, ref uint __result)
        {
            if (power < 0.1f)
            {
                __result = 0u;
                return false;
            }

            double num = 1.0;
            if (__instance.fluidInputCount == 0)
            {
                __instance.fluidInputCargoCount = 0f;
            }
            else
            {
                num = (((double)__instance.fluidInputCargoCount > 0.0001) ? ((float)__instance.fluidInputCount / __instance.fluidInputCargoCount) : 4f);
            }

            if (__instance.fluidInputCount > 0 && __instance.productOutputCount < __instance.productOutputMax && __instance.fluidOutputCount < __instance.fluidOutputMax)
            {
                int num2 = (int)((double)power * 166.66666666666666 * (double)((__instance.fluidInputCargoCount < 30f) ? __instance.fluidInputCargoCount : 30f) * num + 0.75);
                __instance.progress += num2;
                if (__instance.progress > 100000)
                {
                    __instance.progress = 100000;
                }

                while (__instance.progress >= 10000)
                {
                    int num3 = ((__instance.fluidInputInc > 0 && __instance.fluidInputCount > 0) ? (__instance.fluidInputInc / __instance.fluidInputCount) : 0);
                    __instance.seed = (uint)((int)((ulong)((long)(__instance.seed % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                    //修改部分配方的输出数目，默认输出个数为1，特殊配方使用outputNumDic中的输出个数
                    //注意，分馏配方无法从分馏产物推断，但是可以从分馏原料推断；即__instance.fluidId可以找到唯一对应的配方
                    int outputNum = specialOutputNumDic.ContainsKey(__instance.fluidId) ? specialOutputNumDic[__instance.fluidId] : 1;
                    //如果输出数目大于2，应该提升分馏概率，并降低每次输出的产物至2个
                    //个数为1的话，100 -> 100
                    //个数为2的话，100 -> 101，改为1次，每次多一个，对应分馏成功率应该调整为之前的1倍
                    //个数为3的话，100 -> 102，改为2次，每次多一个，对应分馏成功率应该调整为之前的2倍
                    //个数为4的话，100 -> 103，改为3次，每次多一个，对应分馏成功率应该调整为之前的3倍
                    //个数为n的话，100 -> 100 + n，改为n-1次，每次多一个，对应分馏成功率应该调整为之前的n-1倍
                    __instance.fractionSuccess = (double)__instance.seed / 2147483646.0 < (double)__instance.produceProb * (1.0 + Cargo.accTableMilli[(num3 < 10) ? num3 : 10]) * Math.Max(1, (outputNum - 1));
                    //降低分馏数目至2
                    outputNum = Math.Min(2, outputNum);
                    if (__instance.fractionSuccess)
                    {
                        //任何配方分馏成功时，都有5%概率分馏出配方两倍的产物（概率受设置影响）
                        seed2 = (uint)((int)((ulong)((long)(seed2 % 2147483646 + 1) * 48271L) % 2147483647uL) - 1);
                        bool fracOutputDouble = (double)seed2 / 2147483646.0 < 0.05 * 100 / ratio;
                        if (fracOutputDouble)
                        {
                            outputNum *= 2;
                        }

                        __instance.productOutputCount += outputNum;
                        __instance.productOutputTotal += outputNum;
                        lock (productRegister)
                        {
                            productRegister[__instance.productId] += outputNum;
                        }
                        lock (consumeRegister)
                        {
                            consumeRegister[__instance.fluidId]++;
                        }
                    }
                    else
                    {
                        __instance.fluidOutputCount++;
                        __instance.fluidOutputTotal++;
                        __instance.fluidOutputInc += num3;
                    }

                    __instance.fluidInputCount--;
                    __instance.fluidInputInc -= num3;
                    __instance.fluidInputCargoCount -= (float)(1.0 / num);
                    if (__instance.fluidInputCargoCount < 0f)
                    {
                        __instance.fluidInputCargoCount = 0f;
                    }

                    __instance.progress -= 10000;
                }
            }
            else
            {
                __instance.fractionSuccess = false;
            }

            CargoTraffic cargoTraffic = factory.cargoTraffic;
            byte stack;
            byte inc;
            if (__instance.belt1 > 0)
            {
                if (__instance.isOutput1)
                {
                    if (__instance.fluidOutputCount > 0)
                    {
                        int num4 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt1].segPathId);
                        if (cargoPath != null && cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                        {
                            __instance.fluidOutputCount--;
                            __instance.fluidOutputInc -= num4;
                            if (__instance.fluidOutputCount > 0)
                            {
                                num4 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                if (cargoPath.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num4))
                                {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= num4;
                                }
                            }
                        }
                    }
                }
                else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < (float)__instance.fluidInputMax)
                {
                    if (__instance.fluidId > 0)
                    {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt1, __instance.fluidId, null, out stack, out inc) > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    }
                    else
                    {
                        int num5 = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, RecipeProto.fractionatorNeeds, out stack, out inc);
                        if (num5 > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                            __instance.SetRecipe(num5, signPool);
                        }
                    }
                }
            }

            if (__instance.belt2 > 0)
            {
                if (__instance.isOutput2)
                {
                    if (__instance.fluidOutputCount > 0)
                    {
                        int num6 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                        CargoPath cargoPath2 = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[__instance.belt2].segPathId);
                        if (cargoPath2 != null && cargoPath2.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                        {
                            __instance.fluidOutputCount--;
                            __instance.fluidOutputInc -= num6;
                            if (__instance.fluidOutputCount > 0)
                            {
                                num6 = __instance.fluidOutputInc / __instance.fluidOutputCount;
                                if (cargoPath2.TryUpdateItemAtHeadAndFillBlank(__instance.fluidId, Mathf.CeilToInt((float)(num - 0.1)), 1, (byte)num6))
                                {
                                    __instance.fluidOutputCount--;
                                    __instance.fluidOutputInc -= num6;
                                }
                            }
                        }
                    }
                }
                else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < (float)__instance.fluidInputMax)
                {
                    if (__instance.fluidId > 0)
                    {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt2, __instance.fluidId, null, out stack, out inc) > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                        }
                    }
                    else
                    {
                        int num7 = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, RecipeProto.fractionatorNeeds, out stack, out inc);
                        if (num7 > 0)
                        {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount += 1f;
                            __instance.SetRecipe(num7, signPool);
                        }
                    }
                }
            }

            if (__instance.belt0 > 0 && __instance.isOutput0 && __instance.productOutputCount > 0 && cargoTraffic.TryInsertItemAtHead(__instance.belt0, __instance.productId, 1, 0))
            {
                __instance.productOutputCount--;
            }

            if (__instance.fluidInputCount == 0 && __instance.fluidOutputCount == 0 && __instance.productOutputCount == 0)
            {
                __instance.fluidId = 0;
            }

            __instance.isWorking = __instance.fluidInputCount > 0 && __instance.productOutputCount < __instance.productOutputMax && __instance.fluidOutputCount < __instance.fluidOutputMax;
            if (!__instance.isWorking)
            {
                __result = 0u;
                return false;
            }

            __result = 1u;
            return false;
        }
    }
}
