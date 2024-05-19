using FractionateEverything.Compatibility;
using FractionateEverything.Utils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.FractionateEverything;
using static FractionateEverything.Main.FractionatorLogic;
using static FractionateEverything.Compatibility.GenesisBook;
using static FractionateEverything.Compatibility.MoreMegaStructure;
using static FractionateEverything.Utils.AddProtoUtils;

namespace FractionateEverything.Main {
    //LDB.ItemName 等价于 itemproto.name，itemproto.name 等价于 itemproto.Name.Translate()
    //name: 推进器  name.Translate: <0xa0>-<0xa0>推进器  Name: 推进器2  Name.Translate: 推进器
    //name: Thruster  name.Translate: Thruster  Name: 推进器2  Name.Translate: Thruster
    //name: 制造台<0xa0>Mk.I  name.Translate: 制造台<0xa0>Mk.I  Name: 制造台 Mk.I  Name.Translate: 制造台<0xa0>Mk.I
    //name: Assembling Machine Mk.I  name.Translate: Assembling Machine Mk.I  Name: 制造台 Mk.I  Name.Translate: Assembling Machine Mk.I

    public static class FractionateRecipes {
        private static RecipeHelper helper;
        private static readonly List<Proto> recipeList = [];
        private static bool _finished;

        public static void AddFracRecipesAfterLDBToolPostAddData() {
            if (_finished) return;

            PreloadAndInitAll();

            //获取一二三级传送带速度，并生成精准分馏塔系数
            beltSpeed = [
                LDB.items.Select(I传送带).prefabDesc.beltSpeed * 6,
                LDB.items.Select(I高速传送带).prefabDesc.beltSpeed * 6,
                LDB.items.Select(I极速传送带).prefabDesc.beltSpeed * 6,
            ];
            k1 = (2.0 - 3.0) / (beltSpeed[1] - beltSpeed[0]);
            b1 = 3.0 - k1 * beltSpeed[0];
            k2 = (1.0 - 2.0) / (beltSpeed[2] - beltSpeed[1]);
            b2 = 2.0 - k2 * beltSpeed[1];

            //获取传送带的最大速度，以此决定循环的最大次数
            int maxSpeed = (from item in LDB.items.dataArray
                where item.Type == EItemType.Logistics && item.prefabDesc.isBelt
                select item.prefabDesc.beltSpeed * 6).Prepend(0).Max();
            MaxInputTimes = (int)Math.Ceiling(maxSpeed / 60.0);
            MaxOutputTimes = (int)Math.Ceiling(maxSpeed / 15.0);

            LogInfo("Begin to add fractionate recipes...");

            helper = new(tab分馏1);
            List<RecipeProto> list;
            if (!MoreMegaStructure.Enable) {
                AddFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器]);
            }
            else {
                list = AddFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器]);
                list[2].ModifyGridIndex(tab巨构, 410);
                AddFracChain([
                        IMS物质解压器运载火箭, IMS谐振发射器运载火箭, IMS星际组装厂运载火箭,
                        IMS晶体重构器运载火箭, IMS恒星炮运载火箭, IMS科学枢纽运载火箭, IMS物质解压器运载火箭
                    ], false)
                    .ModifyGridIndex(tab巨构, 601);
                AddFracChain([
                        IMS铁金属重构装置, IMS铜金属重构装置, IMS高纯硅重构装置, IMS钛金属重构装置,
                        IMS单极磁石重构装置, IMS石墨提炼装置, IMS晶体接收器, IMS光栅晶体接收器, IMS铁金属重构装置
                    ], false)
                    .ModifyGridIndex(tab巨构, 701);
            }
            if (!GenesisBook.Enable) {
                //添加重氢分馏配方的信息
                fracRecipeNumRatioDic.Add(I氢, new() { { 1, 0.01 } });
                //矿物自增值
                AddFracRecipe(I铁矿, I铁矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 101);
                AddFracRecipe(I铜矿, I铜矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 102);
                AddFracRecipe(I硅石, I硅石, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 103)
                    .preTech = LDB.techs.Select(T冶炼提纯);
                AddFracRecipe(I钛石, I钛石, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 104)
                    .preTech = LDB.techs.Select(T钛矿冶炼);
                AddFracRecipe(I石矿, I石矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 105);
                AddFracRecipe(I煤矿, I煤矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 106);
                AddFracRecipe(I可燃冰, I可燃冰, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 208)
                    .preTech = LDB.techs.Select(T应用型超导体);
                AddFracRecipe(I金伯利矿石, I金伯利矿石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 306)
                    .preTech = LDB.techs.Select(T晶体冶炼);
                AddFracRecipe(I分形硅石, I分形硅石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 303)
                    .preTech = LDB.techs.Select(T粒子可控);
                AddFracRecipe(I光栅石, I光栅石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 605)
                    .preTech = LDB.techs.Select(T卡西米尔晶体);
                AddFracRecipe(I刺笋结晶, I刺笋结晶, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 508)
                    .preTech = LDB.techs.Select(T高强度材料);
                AddFracRecipe(I单极磁石, I单极磁石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 606)
                    .preTech = LDB.techs.Select(T粒子磁力阱);
                AddFracRecipe(I有机晶体, I有机晶体, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 309)
                    .preTech = LDB.techs.Select(T高分子化工);
                //物品左侧部分非循环链
                AddFracChain([I铁块, I钢材, I钛合金], false);
                AddFracChain([I框架材料, I戴森球组件, I小型运载火箭], false);
                AddFracChain([I高纯硅块, I晶格硅], false);
                AddFracChain([I石材, I地基], false);
                AddFracChain([I玻璃, I钛化玻璃, I位面过滤器], false);
                AddFracChain([I棱镜, I电浆激发器, I光子合并器, I太阳帆], false);
                AddFracChain([I高能石墨, I金刚石], false);
                AddFracChain([I石墨烯, I碳纳米管, I粒子宽带], false);
                AddFracChain([I粒子容器, I奇异物质, I引力透镜, I空间翘曲器], false);
                AddFracChain([I精炼油_GB焦油, I塑料_GB聚丙烯, I有机晶体], false);
                AddFracChain([I钛晶石, I卡西米尔晶体], false);
                //物品左侧部分循环链
                list = AddFracChain([I水, I硫酸]);
                list[1].ModifyGridIndex(tab分馏1, 407)
                    .preTech = LDB.techs.Select(T基础化工);
                list = AddFracChain([I磁铁, I磁线圈_GB铜线圈, I电动机], false);
                list[0].ModifyGridIndex(tab分馏1, 201);
                list[1].ModifyGridIndex(tab分馏1, 202);
                AddFracChain([I电动机, I电磁涡轮, I超级磁场环]);
                AddFracChain([I电路板, I处理器, I量子芯片]);
                AddFracChain([I原型机, I精准无人机, I攻击无人机]);
                AddFracChain([I护卫舰, I驱逐舰]);
                list = AddFracChain([I临界光子, I反物质]);
                list[1].ModifyGridIndex(tab分馏1, 608);
                AddFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素])
                    .ModifyGridIndex(tab分馏1, 806);
                AddFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], true, new() { { 1, 0.01 }, { -1, 0.03 } });
                //物品右侧区域
                AddFracChain([I增产剂MkI, I增产剂MkII, I增产剂MkIII_GB增产剂], true, new() { { 2, 0.01 } });
                AddFracChain([I燃烧单元, I爆破单元, I晶石爆破单元]);
                AddFracChain([I动力引擎, I推进器, I加力推进器]);
                AddFracChain([I配送运输机, I物流运输机, I星际物流运输船]);
                AddFracChain([I液氢燃料棒, I氘核燃料棒, I反物质燃料棒, I奇异湮灭燃料棒]);
                AddFracChain([I机枪弹箱, I钛化弹箱, I超合金弹箱]);
                AddFracChain([I炮弹组, I高爆炮弹组, I晶石炮弹组]);
                AddFracChain([I导弹组, I超音速导弹组, I引力导弹组]);
                AddFracChain([I等离子胶囊, I反物质胶囊]);
                AddFracChain([I干扰胶囊, I压制胶囊]);

                //建筑I
                AddFracChain([I电力感应塔, I无线输电塔, I卫星配电站]);
                list = AddFracChain([I风力涡轮机, I太阳能板, I蓄电器, I蓄电器满, I能量枢纽]);
                list[2].ModifyGridIndex(tab分馏2, 113);
                AddFracChain([I火力发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB人造恒星MKI]);
                //建筑II
                AddFracChain([I传送带, I高速传送带, I极速传送带]);
                AddFracChain([I流速监测器, I四向分流器, I喷涂机, I自动集装机]);//注意科技解锁顺序
                AddFracChain([I小型储物仓, I储液罐, I大型储物仓]);//注意科技解锁顺序
                //建筑III
                AddFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器]);
                AddFracChain([I采矿机, I大型采矿机]);
                AddFracChain([I抽水站, I原油萃取站, I原油精炼厂]);
                AddFracChain([I化工厂, I量子化工厂_GB先进化学反应釜]);
                AddFracRecipe(I微型粒子对撞机, I微型粒子对撞机, false, new() { { 2, 0.01 } });
                //建筑IV
                AddFracChain([I电弧熔炉, I位面熔炉, I负熵熔炉]);
                AddFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂]);
                AddFracChain([I矩阵研究站, I自演化研究站]);
                AddFracChain([I电磁轨道弹射器, I射线接收站, I垂直发射井]);
                //建筑V
                AddFracChain([I高斯机枪塔, I导弹防御塔, I聚爆加农炮_GB聚爆加农炮MKI]);//注意科技解锁顺序
                AddFracChain([I高频激光塔_GB高频激光塔MKI, I磁化电浆炮, I近程电浆塔]);//注意科技解锁顺序
                AddFracChain([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);//注意科技解锁顺序
                //建筑VI
                AddFracChain([IFE精准分馏塔, IFE建筑极速分馏塔, I分馏塔_FE通用分馏塔, IFE点数聚集分馏塔, IFE增产分馏塔]);
            }
            else {
                //创世改动过大，单独处理
                //物品页面
                //矿物自增值
                AddFracRecipe(I铁矿, I铁矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 101);
                AddFracRecipe(I铜矿, I铜矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 102);
                AddFracRecipe(IGB铝矿, IGB铝矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 103);
                AddFracRecipe(I硅石, I硅石, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 104)
                    .preTech = LDB.techs.Select(T冶炼提纯);
                AddFracRecipe(I钛石, I钛石, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 105)
                    .preTech = LDB.techs.Select(T钛矿冶炼);
                AddFracRecipe(IGB钨矿, IGB钨矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 106)
                    .preTech = LDB.techs.Select(T钛矿冶炼);
                AddFracRecipe(I煤矿, I煤矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 107)
                    .preTech = LDB.techs.Select(T冶炼提纯);
                AddFracRecipe(I石矿, I石矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 108);
                AddFracRecipe(IGB硫矿, IGB硫矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab分馏1, 109)
                    .preTech = LDB.techs.Select(TGB矿物处理);
                AddFracRecipe(IGB放射性矿物, IGB放射性矿物, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab精炼, 302)
                    .preTech = LDB.techs.Select(TGB放射性矿物提炼);
                AddFracRecipe(IGB铀矿, IGB铀矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab精炼, 305)
                    .preTech = LDB.techs.Select(TGB放射性矿物提炼);
                AddFracRecipe(IGB钚矿, IGB钚矿, false, new() { { 2, 0.04 } })
                    .ModifyGridIndex(tab精炼, 306)
                    .preTech = LDB.techs.Select(TGB放射性矿物提炼);
                AddFracRecipe(I金伯利矿石, I金伯利矿石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 504)
                    .preTech = LDB.techs.Select(T晶体冶炼);
                AddFracRecipe(I分形硅石, I分形硅石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 505)
                    .preTech = LDB.techs.Select(T粒子可控);
                AddFracRecipe(I可燃冰, I可燃冰, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 506)
                    .preTech = LDB.techs.Select(T应用型超导体);
                AddFracRecipe(I刺笋结晶, I刺笋结晶, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 507)
                    .preTech = LDB.techs.Select(T高强度材料);
                AddFracRecipe(I有机晶体, I有机晶体, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 508)
                    .preTech = LDB.techs.Select(T高强度晶体);
                AddFracRecipe(I光栅石, I光栅石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 510)
                    .preTech = LDB.techs.Select(T光子变频);
                AddFracRecipe(I单极磁石, I单极磁石, false, new() { { 2, 0.02 } })
                    .ModifyGridIndex(tab分馏1, 511)
                    .preTech = LDB.techs.Select(T粒子磁力阱);
                list = AddFracChain([I氢, I重氢]);
                list[1].ModifyGridIndex(tab分馏1, 113);
                AddFracChain([IGB氦, IGB氦三]);
                //部分非循环链
                AddFracChain([I钢材, I钛合金, IGB钨合金, IGB三元精金], false);
                AddFracChain([I框架材料, I戴森球组件, I小型运载火箭], false);
                AddFracChain([I高纯硅块, I晶格硅], false);
                AddFracChain([I石材, IGB混凝土], false);
                AddFracChain([I棱镜, I电浆激发器, I光子合并器, I太阳帆], false);
                AddFracChain([I高能石墨, I金刚石], false);
                AddFracChain([I石墨烯, I碳纳米管, I粒子宽带], false);
                AddFracChain([I粒子宽带, IGB光学信息传输纤维], false);
                AddFracChain([I粒子容器, I奇异物质, I引力透镜, I空间翘曲器], false);
                AddFracChain([I钛晶石, I卡西米尔晶体], false);
                //部分循环链
                AddFracChain([IGB基础机械组件, IGB先进机械组件, IGB尖端机械组件]);//创世独有配方
                AddFracChain([IGB塑料基板, IGB光学基板]);//创世独有配方
                AddFracChain([IGB量子计算主机, IGB超越X1型光学主机]);//创世独有配方
                AddFracChain([I玻璃, I钛化玻璃, IGB钨强化玻璃]);
                AddFracChain([I水, I硫酸])
                    .ModifyGridIndex(tab分馏1, 313);
                AddFracRecipe(I磁线圈_GB铜线圈, I电动机)
                    .ModifyGridIndex(tab分馏1, 302);
                AddFracChain([I电动机, I电磁涡轮, I超级磁场环]);
                AddFracChain([I电路板, I处理器, I量子芯片, IGB光学处理器]);
                list = AddFracChain([I临界光子, I反物质]);
                list[1].ModifyGridIndex(tab分馏1, 514);
                AddFracChain([I动力引擎, I推进器, I加力推进器]);
                AddFracChain([I配送运输机, I物流运输机, I星际物流运输船]);
                AddFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素])
                    .ModifyGridIndex(tab防御, 217, false);
                AddFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], true, new() { { 1, 0.01 }, { -1, 0.03 } });

                //建筑页面
                AddFracChain([I电力感应塔, I无线输电塔, I卫星配电站]);
                list = AddFracChain([I风力涡轮机, I太阳能板, IGB同位素温差发电机, I蓄电器, I蓄电器满, I能量枢纽]);
                list[3].ModifyGridIndex(tab分馏2, 114);
                AddFracChain([I火力发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB人造恒星MKI, IGB人造恒星MKII]);
                AddFracChain([I传送带, I高速传送带, I极速传送带]);
                AddFracChain([I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机]);//注意科技解锁顺序
                AddFracChain([I小型储物仓, I大型储物仓, IGB量子储物仓]);
                AddFracChain([I储液罐, IGB量子储液罐]);
                AddFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器]);
                AddFracChain([I采矿机, I大型采矿机]);
                AddFracChain([I抽水站, IGB聚束液体汲取设施]);
                AddFracChain([I原油萃取站, I原油精炼厂]);
                AddFracChain([I化工厂, I量子化工厂_GB先进化学反应釜]);
                AddFracChain([I电弧熔炉, IGB矿物处理厂, I位面熔炉, I负熵熔炉]);
                AddFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂]);
                AddFracChain([I矩阵研究站, I自演化研究站]);
                AddFracChain([I电磁轨道弹射器, I射线接收站, I垂直发射井]);
                AddFracRecipe(I微型粒子对撞机, I微型粒子对撞机, false, new() { { 2, 0.01 } });
                AddFracChain([
                    IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施,
                    IGB工业先锋精密加工中心, IGB苍穹粒子加速器, IGB物质裂解塔
                ], false);
                AddFracChain([IFE精准分馏塔, IFE建筑极速分馏塔, I分馏塔_FE通用分馏塔, IFE点数聚集分馏塔, IFE增产分馏塔]);

                //精炼页面
                AddFracChain([I液氢燃料棒, IGB煤油燃料棒, IGB四氢双环戊二烯燃料棒])
                    .ModifyGridIndex(tab精炼, 601);
                AddFracChain([IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒])
                    .ModifyGridIndex(tab精炼, 604);
                AddFracChain([I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒])
                    .ModifyGridIndex(tab精炼, 607);
                AddFracChain([I反物质燃料棒, I奇异湮灭燃料棒])
                    .ModifyGridIndex(tab精炼, 610);

                //化工页面
                AddFracChain([I塑料_GB聚丙烯, IGB聚苯硫醚PPS, IGB聚酰亚胺PI])
                    .ModifyGridIndex(tab化工, 105);
                AddFracRecipe(I增产剂MkIII_GB增产剂, I增产剂MkIII_GB增产剂, false, new() { { 2, 0.01 } })
                    .ModifyGridIndex(tab化工, 506);

                //防御页面
                AddFracChain([I原型机, I精准无人机, I攻击无人机])
                    .ModifyGridIndex(tab防御, 109);
                AddFracChain([I护卫舰, I驱逐舰])
                    .ModifyGridIndex(tab防御, 112);
                AddFracChain([I高频激光塔_GB高频激光塔MKI, IGB高频激光塔MKII, I磁化电浆炮, I近程电浆塔])
                    .ModifyGridIndex(tab防御, 209);
                AddFracChain([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器])
                    .ModifyGridIndex(tab防御, 213);
                AddFracRecipe(I高斯机枪塔, I高斯机枪塔, false, new() { { 2, 0.01 } })
                    .ModifyGridIndex(tab防御, 309);
                AddFracChain([I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱])
                    .ModifyGridIndex(tab防御, 311);
                AddFracChain([I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元])
                    .ModifyGridIndex(tab防御, 411);
                AddFracChain([I聚爆加农炮_GB聚爆加农炮MKI, IGB聚爆加农炮MKII])
                    .ModifyGridIndex(tab防御, 509);
                AddFracChain([I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组])
                    .ModifyGridIndex(tab防御, 511);
                AddFracRecipe(I导弹防御塔, I导弹防御塔, false, new() { { 2, 0.01 } })
                    .ModifyGridIndex(tab防御, 609);
                AddFracChain([I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组])
                    .ModifyGridIndex(tab防御, 611);
                AddFracChain([I干扰胶囊, I压制胶囊])
                    .ModifyGridIndex(tab防御, 711);
                AddFracChain([I等离子胶囊, I反物质胶囊])
                    .ModifyGridIndex(tab防御, 713);
            }

            //立即添加所有配方
            AddRecipe(recipeList);

            LogInfo("Finish to add fractionate recipes.");
            _finished = true;
        }

        /// <summary>
        /// 添加一个分馏链。
        /// 如果cycle为true，会多添加结尾物品到起始物品的分馏配方。
        /// </summary>
        private static List<RecipeProto> AddFracChain(IReadOnlyList<int> itemChain,
            bool cycle = true, Dictionary<int, double> fracNumRatioDic = null) {
            fracNumRatioDic ??= new() { { 1, 0.01 } };
            List<RecipeProto> list = [];
            for (int i = 0; i < itemChain.Count - 1; i++) {
                list.Add(AddFracRecipe(itemChain[i], itemChain[i + 1], false, fracNumRatioDic));
            }
            if (cycle) {
                //循环链的末尾转初级，产物数目根据链的长短而翻倍
                Dictionary<int, double> tempDic = new();
                foreach (var p in fracNumRatioDic) {
                    tempDic.Add(p.Key > 0 ? p.Key * itemChain.Count : p.Key, p.Value);
                }
                list.Add(AddFracRecipe(itemChain[itemChain.Count - 1], itemChain[0], true, tempDic));
            }
            return list;
        }

        /// <summary>
        /// 添加一个分馏配方。
        /// </summary>
        private static RecipeProto AddFracRecipe(int inputItemID, int outputItemID,
            bool useInputTech = false, Dictionary<int, double> fracNumRatioDic = null) {
            TechProto preTech = useInputTech
                ? LDB.items.Select(inputItemID).preTech
                : LDB.items.Select(outputItemID).preTech;
            fracNumRatioDic ??= new() { { 1, 0.01 } };
            //如果不启用损毁概率，去除对应键值对
            if (!enableDestroy && fracNumRatioDic.ContainsKey(-1)) {
                fracNumRatioDic.Remove(-1);
            }
            try {
                int recipeID = helper.GetUnusedRecipeID();
                ItemProto inputItem = LDB.items.Select(inputItemID);
                ItemProto outputItem = LDB.items.Select(outputItemID);
                string recipeName = outputItem.name + "分馏".Translate();
                //前置科技如果为null，【必须】修改为戴森球计划，才能确保某些配方能正常解锁、显示
                preTech ??= LDB.techs.Select(T戴森球计划);
                //根据产物对应的配方位置，确定分馏配方的位置
                //方法外可再次调整部分配方的显示位置，例如无配方可生成、显示位置重合、分馏循环链影响、超出分馏页等情况
                int gridIndex = outputItem.recipes.Count == 0
                    ? 0
                    : outputItem.recipes[0].GridIndex + (tab分馏1 - 1) * 1000;
                //获取配方图标。图标由python拼接，由unity打包
                string inputIconName = inputItem.iconSprite.name;
                string outputIconName = outputItem.iconSprite.name;
                //由于不同原料可能分馏出同一种产物，配方名字应以原料名称命名
                //考虑到重氢可能分离为其他物品（虽然现在没有），为了不冲突，名称改为“原料-产物-formula-版本”
                string iconName = $"{inputIconName}-{outputIconName}-formula-v{iconVersion}";
                string iconPath = $"Assets/fracicons/{iconName}";
                Sprite sprite = fracicons.bundle.LoadAsset<Sprite>(iconName);
                if (sprite == null) {
                    LogWarning($"缺失{recipeName}配方图标{iconPath}，"
                               + $"配方图标改为使用产物{outputItem.Name}图标！");
                    iconPath = outputItem.IconPath;
                    sprite = outputItem.iconSprite;
                }
                //配方中的ResultCounts[0]大于1时，仅影响分馏成功率与显示上的分馏产物数目，实际并不能分出多个；
                //实际分馏出多个是通过FractionatorInternalUpdatePatch方法达成的
                //根据fracNumRatioDic的内容，构建配方的description
                string description =
                    $"{"从".Translate()}{inputItem.name}{"中分馏出".Translate()}{outputItem.name}{"。".Translate()}";
                foreach (var p in fracNumRatioDic.Where(p => p.Key > 0)) {
                    description += $"\n{p.Value:0.###%}{"分馏出".Translate()}{p.Key}{"个产物".Translate()}";
                }
                if (fracNumRatioDic.TryGetValue(-1, out double destroyRatio)) {
                    description += $"\n{"损毁分馏警告1".Translate()}{destroyRatio:0.###%}{"损毁分馏警告2".Translate()}";
                }
                //ProtoRegistry.RegisterRecipe用起来有很多问题，自己创建不容易出bug
                RecipeProto r = new() {
                    Type = ERecipeType.Fractionate,
                    Handcraft = false,
                    Explicit = true,
                    TimeSpend = 60,
                    Items = [inputItemID],
                    ItemCounts = [100],
                    Results = [outputItemID],
                    ResultCounts = [1],
                    Description = "R" + inputItem.Name + "分馏",
                    description = description,
                    GridIndex = gridIndex,
                    Name = inputItem.Name + "分馏",
                    name = recipeName,
                    preTech = preTech,
                    IconPath = iconPath,
                    ID = recipeID,
                };
                if (sprite != null) {
                    Traverse.Create(r).Field("_iconSprite").SetValue(sprite);
                }

                r.ModifyGridIndex(gridIndex);

                //LDBTool.PostAddProto(r);
                //通过反射调用LDBTool的AddProtosToSet方法
                recipeList.Add(r);

                //add之后要再次设定ID，不然id会莫名其妙变化。不知道这个bug怎么回事，反正这样就正常了。
                r.ID = recipeID;
                //为基础配方添加这个公式的显示
                outputItem.recipes.Add(r);
                //存储部分信息，为更新显示UI（FractionatorInternalUpdatePatch）提供数据支持
                fracRecipeNumRatioDic.Add(inputItemID, fracNumRatioDic);
                if (inputItemID == outputItemID) {
                    fracSelfRecipeList.Add(inputItemID);
                }
#if DEBUG
                //输出分馏配方需要的图标的路径，以便于制作图标
                if (Directory.Exists(SPRITE_CSV_PATH.Substring(0, SPRITE_CSV_PATH.LastIndexOf('\\')))) {
                    using StreamWriter sw = new(SPRITE_CSV_PATH, true, Encoding.UTF8);
                    sw.WriteLine(inputIconName + "," + outputIconName);
                }
#endif
                return r;
            }
            catch (Exception ex) {
                LogError(ex.ToString());
                return null;
            }
        }

        private static void ModifyGridIndex(this List<RecipeProto> recipes, int tab, int rowColumn,
            bool addColumn = true) {
            for (int i = 0; i < recipes.Count - 1; i++) {
                helper.ModifyGridIndex(recipes[i], tab, rowColumn + (addColumn ? i + 1 : (i + 1) * 100));
            }
            helper.ModifyGridIndex(recipes[recipes.Count - 1], tab, rowColumn);
        }

        private static RecipeProto ModifyGridIndex(this RecipeProto r, int tab, int rowColumn) {
            helper.ModifyGridIndex(r, tab, rowColumn);
            return r;
        }

        private static RecipeProto ModifyGridIndex(this RecipeProto r, int gridIndex) {
            helper.ModifyGridIndex(r, gridIndex);
            return r;
        }
    }
}
