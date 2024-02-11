using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using xiaoye97;

namespace FractionateEverything
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry))]
    public class FractionateEverything : BaseUnityPlugin
    {
        public const string GUID = "com.menglei.dsp." + NAME;
        public const string NAME = "FractionateEverything";
        public const string VERSION = "1.0.0";
        public static ManualLogSource logger;
        /// <summary>
        /// 下一个新增的分馏配方应使用该ID，且使用后应+1。
        /// </summary>
        private static int nextRecipeID = 500;

        private static int ratio;
        private static int pagePlus;
        AssetBundle ab;

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

            ConfigEntry<int> FractionateDifficulty = Config.Bind("config", "FractionateDifficulty", 5, "Lower means easier and faster to fractionate (1-5). 值越小代表越简单，能更高效地分馏出产物（1-5）。");
            if (FractionateDifficulty.Value < 1 || FractionateDifficulty.Value > 5)
            {
                FractionateDifficulty.Value = 5;
            }
            ratio = new List<int> { 20, 30, 44, 67, 100 }[FractionateDifficulty.Value - 1];
            ConfigEntry<int> DefaultPage = Config.Bind("config", "DefaultPage", 3, "New fractionate recipes will be shown in this page (3-8). Hide them by set this to 9. 新的分馏配方将出现在这些页（3-8）。设置为9则不再显示。");
            if (DefaultPage.Value < 3 || DefaultPage.Value > 8)
            {
                DefaultPage.Value = 3;
            }
            pagePlus = (DefaultPage.Value - 3) * 1000;
            Config.Save();

            //ab = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("FractionateEverything.fracicons"));

            LDBTool.PreAddDataAction += PreAddDataAction;
            LDBTool.PostAddDataAction += PostAddDataAction;
            Harmony.CreateAndPatchAll(typeof(FractionateEverything), GUID);
        }

        private void PreAddDataAction()
        {
            LocalizationModule.RegisterTranslation("分馏f", " Fractionation", "分馏", " Fractionation");
            LocalizationModule.RegisterTranslation("从f", "Fractionate ", "从", "Fractionate ");
            LocalizationModule.RegisterTranslation("中分馏出f", " to ", "中分馏出", " to ");
            LocalizationModule.RegisterTranslation("。f", ".", "。", ".");

            //分馏只能单产物，不能多产物！
            AddFracChain(new List<Item> { Item.采矿机, Item.大型采矿机, });
            AddFracChain(new List<Item> { Item.小型储物仓, Item.大型储物仓, });
            AddFracChain(new List<Item> { Item.传送带, Item.高速传送带, Item.极速传送带, });
            AddFracChain(new List<Item> { Item.分拣器, Item.高速分拣器, Item.极速分拣器, });
            AddFracChain(new List<Item> { Item.电弧熔炉, Item.位面熔炉, Item.负熵熔炉, });
            AddFracChain(new List<Item> { Item.制造台MkI, Item.制造台MkII, Item.制造台MkIII, Item.重组式制造台, });
            AddFracChain(new List<Item> { Item.化工厂, Item.量子化工厂, });
            AddFracChain(new List<Item> { Item.矩阵研究站, Item.自演化研究站, });
            AddFracChain(new List<Item> { Item.动力引擎, Item.推进器, Item.加力推进器, });
            AddFracChain(new List<Item> { Item.配送运输机, Item.物流运输机, Item.星际物流运输船, });
            AddFracChain(new List<Item> { Item.物流配送器, Item.行星内物流运输站, Item.星际物流运输站, Item.轨道采集器, });
            AddFracChain(new List<Item> { Item.增产剂MkI, Item.增产剂MkII, Item.增产剂MkIII, });
            AddFracChain(new List<Item> { Item.液氢燃料棒, Item.氘核燃料棒, Item.反物质燃料棒, Item.奇异湮灭燃料棒, });
            AddFracChain(new List<Item> { Item.电力感应塔, Item.无线输电塔, Item.卫星配电站, });
            AddFracChain(new List<Item> { Item.风力涡轮机, Item.太阳能板, Item.能量枢纽, });
            AddFracChain(new List<Item> { Item.火力发电厂, Item.地热发电站, Item.微型聚变发电站, Item.人造恒星, });
            AddFracChain(new List<Item> { Item.燃烧单元, Item.爆破单元, Item.晶石爆破单元, });
            AddFracChain(new List<Item> { Item.机枪弹箱, Item.钛化弹箱, Item.超合金弹箱, });
            AddFracChain(new List<Item> { Item.炮弹组, Item.高爆炮弹组, Item.晶石炮弹组, });
            AddFracChain(new List<Item> { Item.等离子胶囊, Item.反物质胶囊, });
            AddFracChain(new List<Item> { Item.导弹组, Item.超音速导弹组, Item.引力导弹组, });
            AddFracChain(new List<Item> { Item.原型机, Item.精准无人机, Item.攻击无人机, Item.护卫舰, Item.驱逐舰, });
            AddFracChain(new List<Item> { Item.能量碎片, Item.硅基神经元, Item.物质重组器, Item.负熵奇点, Item.核心素, Item.黑雾矩阵, Item.电磁矩阵, Item.能量矩阵, Item.结构矩阵, Item.信息矩阵, Item.引力矩阵, Item.宇宙矩阵, });
            AddFracChain(new List<Item> { Item.战场分析基站, Item.干扰塔, Item.信号塔, Item.行星护盾发生器, Item.高斯机枪塔, Item.高频激光塔, Item.聚爆加农炮, Item.磁化电浆炮, Item.导弹防御塔, });
            AddFracChain(new List<Item> { Item.水, Item.氢, });
            AddFracChain(new List<Item> { Item.临界光子, Item.反物质, });
            AddFracChain(new List<Item> { Item.电磁轨道弹射器, Item.垂直发射井, Item.射线接收站, });
            AddFracChain(new List<Item> { Item.铁矿, Item.铁块, Item.钢材, });
            AddFracChain(new List<Item> { Item.磁铁, Item.磁线圈, Item.电动机, Item.电磁涡轮, Item.超级磁场环, });
            AddFracChain(new List<Item> { Item.铜矿, Item.铜块, Item.粒子容器, Item.奇异物质, Item.引力透镜, });
            AddFracChain(new List<Item> { Item.电路板, Item.处理器, Item.量子芯片, });
            AddFracChain(new List<Item> { Item.硅石, Item.高纯硅块, Item.微晶元件, });
            AddFracChain(new List<Item> { Item.钛石, Item.钛块, Item.钛合金, Item.框架材料, Item.戴森球组件, Item.小型运载火箭, });
            AddFracChain(new List<Item> { Item.石矿, Item.石材, Item.地基, });
            AddFracChain(new List<Item> { Item.玻璃, Item.钛化玻璃, Item.位面过滤器, });
            AddFracChain(new List<Item> { Item.棱镜, Item.光子合并器, Item.太阳帆 });
            AddFracChain(new List<Item> { Item.煤矿, Item.高能石墨, Item.石墨烯, Item.碳纳米管, Item.粒子宽带, });
            AddFracChain(new List<Item> { Item.原油, Item.精炼油, Item.塑料, Item.有机晶体, Item.钛晶石, Item.卡西米尔晶体, });
        }

        /// <summary>
        /// 添加一个分馏链。
        /// itemChain的第i个物品可以分馏出第i+1个物品，最后一个物品会分馏为自身。
        /// </summary>
        /// <param name="itemChain">分馏产物链</param>
        private void AddFracChain(List<Item> itemChain)
        {
            //如果有x个产品，则有x-1个分馏配方
            for (int i = 0; i < itemChain.Count - 1; i++)
            {
                int inputItemID = (int)itemChain[i];
                int outputItemID = (int)itemChain[i + 1];
                int recipeID = nextRecipeID;
                nextRecipeID++;
                RecipeProto r = ProtoRegistry.RegisterRecipe(recipeID, ERecipeType.Fractionate, 60, new[] { inputItemID }, new[] { ratio }, new[] { outputItemID }, new[] { 1 }, "FR" + recipeID + "描述");
                LocalizationModule.RegisterTranslation("FR" + recipeID + "描述", "从f".Translate() + LDB.ItemName(inputItemID).Translate() + "中分馏出f".Translate() + LDB.ItemName(outputItemID).Translate() + "。f".Translate());
            }
        }

        private void PostAddDataAction()
        {
            RecipeProto.InitFractionatorNeeds();
            logger.LogInfo("当前分馏配方个数为" + RecipeProto.fractionatorRecipes.Length);
        }
    }
}
