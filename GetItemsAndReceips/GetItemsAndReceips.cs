using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using xiaoye97;

namespace GetItemsAndReceips
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry))]
    public class GetItemsAndReceips : BaseUnityPlugin
    {
        public const string GUID = "com.menglei.dsp.GetItemsAndReceips";
        public const string NAME = "Get Items and Receips";
        public const string VERSION = "1.0.0";
        public static ManualLogSource logger;

        private static string dir;

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

            ConfigEntry<string> BaseDir = Config.Bind("config", "BaseDir", "", "在哪个目录生成文件，为空表示使用桌面");
            dir = string.IsNullOrEmpty(BaseDir.Value)
               ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
               : BaseDir.Value;

            LDBTool.PreAddDataAction += PreAddDataAction;
            LDBTool.PostAddDataAction += PostAddDataAction;
            Harmony.CreateAndPatchAll(typeof(GetItemsAndReceips), GUID);
        }

        private void PreAddDataAction() { }

        private void PostAddDataAction()
        {
            //注意：不能使用$拼接字符串
            try
            {
                using (StreamWriter sw = new StreamWriter(dir + "\\DSP_ItemEnum.txt"))
                {
                    //代码里面的enum
                    sw.WriteLine("public enum Item");
                    sw.WriteLine("{");
                    foreach (var item in LDB.items.dataArray)
                    {
                        int id = item.ID;
                        string name = item.name.Replace(" ", "").Replace(".", "").Replace("（", "").Replace("）", "");
                        //铁矿=1001,
                        sw.WriteLine(name + "=" + id + ",");
                    }
                    sw.WriteLine("}");
                }

                using (StreamWriter sw = new StreamWriter(dir + "\\DSP_DataInfo.csv"))
                {
                    Dictionary<int, string> itemDic = new Dictionary<int, string>();

                    //csv物品数据
                    sw.WriteLine("物品ID,物品名称");
                    foreach (var item in LDB.items.dataArray)
                    {
                        int id = item.ID;
                        string name = item.name;
                        itemDic.Add(id, name);
                        //1001,铁矿
                        sw.WriteLine(id + "," + name);
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    //csv配方数据
                    sw.WriteLine("配方ID,配方名称,原料,产物,时间");
                    foreach (var recipe in LDB.recipes.dataArray)
                    {
                        int id = recipe.ID;
                        string name = recipe.name;
                        int[] itemIDs = recipe.Items;
                        int[] itemCounts = recipe.ItemCounts;
                        int[] resultIDs = recipe.Results;
                        int[] resultCounts = recipe.ResultCounts;
                        double timeSpeed = recipe.TimeSpend / 60.0;
                        string s = id + "," + name + ",";
                        for (int i = 0; i < itemIDs.Length; i++)
                        {
                            s += itemIDs[i] + "(" + itemDic[itemIDs[i]] + ")*" + itemCounts[i] + " + ";
                        }
                        s = s.Substring(0, s.Length - 3) + " -> ";
                        for (int i = 0; i < resultIDs.Length; i++)
                        {
                            s += resultIDs[i] + "(" + itemDic[resultIDs[i]] + ")*" + resultCounts[i] + " + ";
                        }
                        s = s.Substring(0, s.Length - 3) + ",";
                        s += recipe.TimeSpend + "(" + timeSpeed.ToString("F1") + "s)";
                        sw.WriteLine(s);
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    ////csv科技数据
                    //sw.WriteLine("科技ID,科技名称,科技描述");
                    //foreach (var tech in LDB.techs.dataArray)
                    //{
                    //    int id = tech.ID;
                    //    string name = tech.name;
                    //    string desc = tech.description;
                    //    int[] itemIDs = tech.Items;
                    //    sw.WriteLine(id + "," + name + "," + desc);
                    //}

                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }
    }
}
