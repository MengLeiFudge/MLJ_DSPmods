using FE.Compatibility;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using static FE.Utils.ProtoID;
using static FE.FractionateEverything;
using static FE.Utils.TranslationUtils;
using static FE.Logic.FracProcess;

namespace FE.Logic;

public static class FracRecipeManager {
    public static readonly FracRecipe DeuteriumFracRecipe =
        new FracRecipe(FracRecipeType.Origin, I氢, I重氢, 0.01f, 1, 0).Unlock();
    private static readonly List<FracRecipe> naturalResourceRecipeList = [];
    private static readonly List<FracRecipe> upgradeRecipeList = [];
    private static readonly List<FracRecipe> downgradeRecipeList = [];
    private static readonly List<FracRecipe> pointsAggregateRecipeList = [];
    private static readonly List<FracRecipe> increaseRecipeList = [];

    public static FracRecipe GetNaturalResourceRecipe(int inputID) {
        foreach (FracRecipe r in naturalResourceRecipeList) {
            if (r.inputID == inputID) {
                return r;
            }
        }
        return null;
    }

    public static FracRecipe GetUpgradeRecipe(int inputID) {
        foreach (FracRecipe r in upgradeRecipeList) {
            if (r.inputID == inputID) {
                return r;
            }
        }
        return null;
    }

    public static FracRecipe GetDowngradeRecipe(int inputID) {
        foreach (FracRecipe r in downgradeRecipeList) {
            if (r.inputID == inputID) {
                return r;
            }
        }
        return null;
    }

    public static FracRecipe GetPointsAggregateRecipe(int inputID) {
        foreach (FracRecipe r in pointsAggregateRecipeList) {
            if (r.inputID == inputID) {
                return r;
            }
        }
        return null;
    }

    public static FracRecipe GetIncreaseRecipe(int inputID) {
        foreach (FracRecipe r in increaseRecipeList) {
            if (r.inputID == inputID) {
                return r;
            }
        }
        return null;
    }

    #region 创建分馏配方

#if DEBUG
        private const string SPRITE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
        private const string SPRITE_CSV_PATH = $@"{SPRITE_CSV_DIR}\fracIconPath.csv";
#endif

    public static void AddFracRecipes() {
#if DEBUG
            if (File.Exists(SPRITE_CSV_PATH)) {
                File.Delete(SPRITE_CSV_PATH);
            }
#endif

        LogInfo("Begin to add fractionate recipes...");

        if (!GenesisBook.Enable) {
            //自然资源复制
            //采集10个对应物品，或者向老虎机投入10个对应物品，即可解锁自然资源复制配方
            //使用矿物复制塔成功分馏出指定数目的物品之后，配方可以消耗资源来升级
            //todo：能不能判定采集了多少个资源
            CreateNaturalResourceRecipe(I铁矿, 0.05f);
            CreateNaturalResourceRecipe(I铜矿, 0.05f);
            CreateNaturalResourceRecipe(I硅石, 0.05f).AddProduct(I分形硅石, 0.01f, 1);
            CreateNaturalResourceRecipe(I钛石, 0.05f);
            CreateNaturalResourceRecipe(I石矿, 0.05f).AddProduct(I硅石, 0.01f, 1).AddProduct(I钛石, 0.01f, 1);
            CreateNaturalResourceRecipe(I煤矿, 0.05f);
            CreateNaturalResourceRecipe(I水, 0.05f);
            CreateNaturalResourceRecipe(I原油, 0.05f);
            CreateNaturalResourceRecipe(I硫酸, 0.05f);
            CreateNaturalResourceRecipe(I氢, 0.05f).AddProduct(I重氢, 0.01f, 1);
            CreateNaturalResourceRecipe(I重氢, 0.05f);
            CreateNaturalResourceRecipe(I可燃冰, 0.025f);
            CreateNaturalResourceRecipe(I金伯利矿石, 0.025f);
            CreateNaturalResourceRecipe(I分形硅石, 0.025f);
            CreateNaturalResourceRecipe(I光栅石, 0.025f);
            CreateNaturalResourceRecipe(I刺笋结晶, 0.025f);
            CreateNaturalResourceRecipe(I单极磁石, 0.01f);
            CreateNaturalResourceRecipe(I有机晶体, 0.025f);
            CreateNaturalResourceRecipe(I临界光子, 0.01f);
            //升转化链
            //分为消耗品、材料、建筑三种
            //消耗品指燃料棒、弹药、增产剂
            //材料指电路板、处理器等
            //建筑指能放置的物品
            CreateFracChain([I铁块, I钢材, I钛合金], true);
            CreateFracChain([I框架材料, I戴森球组件, I小型运载火箭], true);
            CreateFracChain([I高纯硅块, I晶格硅], true);
            CreateFracChain([I石材, I地基], true);
            CreateFracChain([I玻璃, I钛化玻璃, I位面过滤器], true);
            CreateFracChain([I棱镜, I电浆激发器, I光子合并器, I太阳帆], true);
            CreateFracChain([I高能石墨, I金刚石], true);
            CreateFracChain([I石墨烯, I碳纳米管, I粒子宽带], true);
            CreateFracChain([I粒子容器, I奇异物质, I引力透镜, I空间翘曲器], true);
            CreateFracChain([I精炼油_GB焦油, I塑料_GB聚丙烯, I有机晶体], true);
            CreateFracChain([I钛晶石, I卡西米尔晶体], true);
            CreateFracChain3([I水, I硫酸]);
            CreateFracChain([I磁铁, I磁线圈_GB铜线圈, I电动机], true);
            CreateFracChain([I电动机, I电磁涡轮, I超级磁场环], true);
            CreateFracChain([I电路板, I处理器, I量子芯片], true);
            CreateFracChain([I原型机, I精准无人机, I攻击无人机], false);
            if (!TheyComeFromVoid.Enable) {
                CreateFracChain([I护卫舰, I驱逐舰], false);
            } else {
                CreateFracChain([I护卫舰, I驱逐舰, IVD水滴], false);
            }
            CreateFracChain3([I临界光子, I反物质]);
            CreateFracChain3([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素]);
            CreateFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], true);
            CreateFracChain([I增产剂MkI, I增产剂MkII, I增产剂MkIII_GB增产剂], false);
            CreateFracChain([I燃烧单元, I爆破单元, I晶石爆破单元], true);
            CreateFracChain([I动力引擎, I推进器, I加力推进器], true);
            CreateFracChain([I配送运输机, I物流运输机, I星际物流运输船], false);
            CreateFracChain([I液氢燃料棒, I氘核燃料棒, I反物质燃料棒, I奇异湮灭燃料棒], true);
            CreateFracChain([I机枪弹箱, I钛化弹箱, I超合金弹箱], false);
            CreateFracChain([I炮弹组, I高爆炮弹组, I晶石炮弹组], false);
            CreateFracChain([I导弹组, I超音速导弹组, I引力导弹组], false);
            CreateFracChain([I等离子胶囊, I反物质胶囊], false);
            CreateFracChain([I干扰胶囊, I压制胶囊], false);


            //建筑I
            CreateFracChain([I电力感应塔, I无线输电塔, I卫星配电站], false);
            CreateFracChain([I风力涡轮机, I太阳能板, I蓄电器, I蓄电器满, I能量枢纽], false);
            CreateFracChain([I火力发电厂_GB燃料电池发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB朱曦K型人造恒星], false);
            //建筑II
            CreateFracChain([I传送带, I高速传送带, I极速传送带], false);
            CreateFracChain([I流速监测器, I四向分流器, I喷涂机, I自动集装机], false);//注意科技解锁顺序
            CreateFracChain([I小型储物仓, I储液罐, I大型储物仓], false);//注意科技解锁顺序
            if (!MoreMegaStructure.Enable) {
                CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器], false);
            } else {
                CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器], false);
            }
            //建筑III
            CreateFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器], false);
            CreateFracChain([I采矿机, I大型采矿机], false);
            CreateFracChain([I抽水站, I原油萃取站, I原油精炼厂], false);
            CreateFracChain([I化工厂, I量子化工厂_GB先进化学反应釜], false);
            //CreateFracChain([I分馏塔, I微型粒子对撞机], false);
            //建筑IV
            CreateFracChain([I电弧熔炉, I位面熔炉, I负熵熔炉], false);
            CreateFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂], false);
            CreateFracChain([I矩阵研究站, I自演化研究站], false);
            CreateFracChain([I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井], false);
            //建筑V
            CreateFracChain3([I高斯机枪塔, I导弹防御塔, I聚爆加农炮]);//注意科技解锁顺序
            CreateFracChain3([I高频激光塔, I磁化电浆炮, I近程电浆塔]);//注意科技解锁顺序
            CreateFracChain3([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);//注意科技解锁顺序
            //建筑VI
            // List<FracRecipe>[] lists =
            //     CreateFracChain([IFE矿物复制塔, IFE转化塔MK1, IFE转化塔MK1, IFE垃圾回收分馏塔, IFE点数聚集塔, IFE量子复制塔], false);
            // foreach (FracRecipe recipe in lists[0]) {
            //     recipe.AddProduct(IFE老虎机分馏塔, 0.001f, 1);
            // }
        } else {
            //创世改动过大，单独处理
            RegisterOrEditAsync("左键点击：更换生产设备",
                "Left click: Change machine\nRight click: Assembler or Fractionator",
                "左键点击：更换生产设备\n右键点击：常规设备或分馏塔");
            //物品页面
            //自然资源自增值

            CreateNaturalResourceRecipe(I铁矿, 0.05f);
            CreateNaturalResourceRecipe(I铜矿, 0.05f);
            CreateNaturalResourceRecipe(IGB铝矿, 0.05f);
            CreateNaturalResourceRecipe(I硅石, 0.05f).AddProduct(I分形硅石, 0.01f, 1);
            CreateNaturalResourceRecipe(I钛石, 0.05f);
            CreateNaturalResourceRecipe(IGB钨矿, 0.05f);
            CreateNaturalResourceRecipe(I煤矿, 0.05f);
            CreateNaturalResourceRecipe(I石矿, 0.05f).AddProduct(I硅石, 0.01f, 1).AddProduct(I钛石, 0.01f, 1);
            CreateNaturalResourceRecipe(IGB硫矿, 0.05f);
            CreateNaturalResourceRecipe(IGB放射性矿物, 0.05f);

            CreateNaturalResourceRecipe(I原油, 0.05f);
            CreateNaturalResourceRecipe(IGB海水, 0.05f);
            CreateNaturalResourceRecipe(I水, 0.05f);
            CreateNaturalResourceRecipe(IGB盐酸, 0.05f);
            CreateNaturalResourceRecipe(I硫酸, 0.05f);
            CreateNaturalResourceRecipe(IGB硝酸, 0.05f);
            CreateNaturalResourceRecipe(IGB氨, 0.05f);

            CreateNaturalResourceRecipe(I氢, 0.05f).AddProduct(I重氢, 0.01f, 1);
            CreateNaturalResourceRecipe(I重氢, 0.05f);
            CreateNaturalResourceRecipe(IGB氦, 0.05f).AddProduct(IGB氦三, 0.01f, 1);
            CreateNaturalResourceRecipe(IGB氮, 0.05f);
            CreateNaturalResourceRecipe(IGB氧, 0.05f);
            CreateNaturalResourceRecipe(IGB二氧化碳, 0.05f);
            CreateNaturalResourceRecipe(IGB二氧化硫, 0.05f);

            CreateNaturalResourceRecipe(I金伯利矿石, 0.025f);
            CreateNaturalResourceRecipe(I分形硅石, 0.025f);
            CreateNaturalResourceRecipe(I可燃冰, 0.025f);
            CreateNaturalResourceRecipe(I刺笋结晶, 0.025f);
            CreateNaturalResourceRecipe(I有机晶体, 0.025f);
            CreateNaturalResourceRecipe(I光栅石, 0.025f);
            CreateNaturalResourceRecipe(I单极磁石, 0.025f);

            //物品页面
            CreateFracChain([I钢材, I钛合金, IGB钨合金, IGB三元精金], true);
            CreateFracChain([I框架材料, I戴森球组件, I小型运载火箭], true);
            CreateFracChain([I高纯硅块, I晶格硅], true);
            //CreateFracChain([I石材, IGB混凝土], true);
            CreateFracChain([I棱镜, I电浆激发器, I光子合并器, I太阳帆], true);
            CreateFracChain([I高能石墨, I金刚石], true);
            CreateFracChain([I石墨烯, I碳纳米管, I粒子宽带], true);
            CreateFracChain([I粒子宽带, IGB光学信息传输纤维], true);
            CreateFracChain([I粒子容器, I奇异物质, I引力透镜, I空间翘曲器], true);
            CreateFracChain([I钛晶石, I卡西米尔晶体], true);
            CreateFracChain([IGB基础机械组件, IGB先进机械组件, IGB尖端机械组件, IGB超级机械组件], true);//创世独有配方
            CreateFracChain([IGB塑料基板, IGB光学基板], true);//创世独有配方
            CreateFracChain([IGB量子计算主机, IGB超越X1型光学主机], true);//创世独有配方
            CreateFracChain([I玻璃, I钛化玻璃, IGB钨强化玻璃], true);
            CreateFracChain([I氢, I重氢], true);
            CreateFracChain([IGB氦, IGB氦三], true);
            CreateFracChain3([I水, I硫酸]);
            CreateFracChain([I磁线圈_GB铜线圈, I电动机, I电磁涡轮, I超级磁场环], true);
            CreateFracChain([I电路板, I处理器, I量子芯片, IGB光学处理器], true);
            CreateFracChain3([I临界光子, I反物质]);
            CreateFracChain([I动力引擎, I推进器, I加力推进器], true);
            CreateFracChain([I配送运输机, I物流运输机, I星际物流运输船], false);
            CreateFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素], false);
            CreateFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], true);

            //建筑页面
            CreateFracChain([I电力感应塔, I无线输电塔, I卫星配电站], false);
            CreateFracChain([I风力涡轮机, I太阳能板, IGB同位素温差发电机, I蓄电器, I蓄电器满, I能量枢纽], false);
            CreateFracChain([I火力发电厂_GB燃料电池发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB朱曦K型人造恒星, IGB湛曦O型人造恒星], false);
            CreateFracChain([I传送带, I高速传送带, I极速传送带], false);
            CreateFracChain([I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机], false);//注意科技解锁顺序
            CreateFracChain([I小型储物仓, I大型储物仓, IGB量子储物仓], false);
            CreateFracChain([I储液罐, IGB量子储液罐], false);
            if (!MoreMegaStructure.Enable) {
                CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器], false);
            } else {
                CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器], false);
            }
            CreateFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器], false);
            CreateFracChain([I采矿机, I大型采矿机], false);
            CreateFracChain([I抽水站, IGB聚束液体汲取设施], false);
            CreateFracChain([I原油萃取站, I原油精炼厂], false);
            CreateFracChain([I化工厂, I量子化工厂_GB先进化学反应釜], false);
            CreateFracChain([I电弧熔炉, IGB矿物处理厂, I位面熔炉, I负熵熔炉], false);
            CreateFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂], false);
            CreateFracChain([I矩阵研究站, I自演化研究站], false);
            CreateFracChain([I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井], false);
            //CreateFracChain([I微型粒子对撞机], false);
            CreateFracChain3([IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器]);
            // List<FracRecipe>[] lists =
            //     CreateFracChain([IFE矿物复制塔, IFE转化塔MK1, IFE转化塔MK1, IFE垃圾回收分馏塔, IFE点数聚集塔, IFE量子复制塔], false);
            // foreach (FracRecipe recipe in lists[0]) {
            //     recipe.AddProduct(IFE老虎机分馏塔, 0.001f, 1);
            // }

            //精炼页面
            CreateFracChain([
                IGB空燃料棒,
                I液氢燃料棒, IGB煤油燃料棒, IGB四氢双环戊二烯燃料棒,
                IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
                I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
                I反物质燃料棒, I奇异湮灭燃料棒,
            ], true);

            //化工页面
            CreateFracChain([I塑料_GB聚丙烯, IGB聚苯硫醚PPS, IGB聚酰亚胺PI], true);
            //CreateFracChain([I增产剂MkIII_GB增产剂], false);

            //防御页面
            CreateFracChain([I原型机, I精准无人机, I攻击无人机], false);
            if (!TheyComeFromVoid.Enable) {
                CreateFracChain([I护卫舰, I驱逐舰], false);
            } else {
                CreateFracChain([I护卫舰, I驱逐舰, IVD水滴], false);
            }
            CreateFracChain([I高频激光塔, IGB紫外激光塔, I近程电浆塔, I磁化电浆炮], false);
            CreateFracChain3([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);
            CreateFracChain3([I高斯机枪塔, I聚爆加农炮, IGB电磁加农炮, I导弹防御塔]);
            CreateFracChain([I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱], false);
            CreateFracChain([I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元], false);
            CreateFracChain([I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组], false);
            CreateFracChain([I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组], false);
            CreateFracChain([I干扰胶囊, I压制胶囊], false);
            CreateFracChain([I等离子胶囊, I反物质胶囊], false);
        }

        //为所有物品添加点数聚集配方以及量子复制配方
        for (int i = 0; i < LDB.items.Length; i++) {
            int itemID = LDB.items[i].ID;
            CreatePointsAggregateRecipe(itemID);
            CreateIncreaseRecipe(itemID, IPFDic[itemID]);
        }

        //添加所有翻译
        LoadLanguagePostfixAfterCommonApi();

        LogInfo("Finish to add fractionate recipes.");
    }

    /// <summary>
    /// 创建一个自然资源分馏配方。
    /// 添加配方解锁条件：10个物品。
    /// </summary>
    private static FracRecipe CreateNaturalResourceRecipe(int itemID, float ratio) {
        FracRecipe r = new(FracRecipeType.NaturalResource, itemID, itemID, ratio, 2, 0);
        naturalResourceRecipeList.Add(r);
        r.unlockItemDic.Add(itemID, 10);
        return r;
    }

    /// <summary>
    /// 创建一个升级分馏配方。
    /// 添加配方解锁条件：如果属于特殊类，必须使用矩阵；否则使用10个物品。
    /// </summary>
    private static FracRecipe CreateUpgradeRecipe(int inputID, int outputID, float outputRatio, float destroy,
        bool special) {
        FracRecipe r = new(FracRecipeType.Upgrade, inputID, outputID, outputRatio, 1, destroy);
        upgradeRecipeList.Add(r);
        if (special) {
            ItemProto item = LDB.items.Select(outputID);
            if (item.missingTech || item.preTech == null) {
                r.unlockItemDic.Add(I电磁矩阵, 1);
            } else {
                TechProto pretech = item.preTech;
                int maxMatrixID = I电磁矩阵;
                foreach (int matrixID in pretech.Items) {
                    if (matrixID > 6000 && matrixID < 6100) {
                        maxMatrixID = Math.Max(maxMatrixID, matrixID);
                    }
                }
                r.unlockItemDic.Add(maxMatrixID, 1);
                //todo: 如果仅有黑雾材料 或者没有矩阵 如何处理？
            }
        } else {
            r.unlockItemDic.Add(outputID, 10);
        }
        return r;
    }

    /// <summary>
    /// 创建一个转化配方。
    /// </summary>
    private static FracRecipe CreateDownGradeRecipe(int inputID, int outputID, float outputRatio, float destroy,
        bool special) {
        FracRecipe r = new(FracRecipeType.DownGrade, inputID, outputID, outputRatio, 1, destroy);
        downgradeRecipeList.Add(r);
        if (special) {
            ItemProto item = LDB.items.Select(outputID);
            if (item.missingTech || item.preTech == null) {
                r.unlockItemDic.Add(I电磁矩阵, 1);
            } else {
                TechProto pretech = item.preTech;
                int maxMatrixID = I电磁矩阵;
                foreach (int matrixID in pretech.Items) {
                    if (matrixID > 6000 && matrixID < 6100) {
                        maxMatrixID = Math.Max(maxMatrixID, matrixID);
                    }
                }
                r.unlockItemDic.Add(maxMatrixID, 1);
                //todo: 如果仅有黑雾材料 或者没有矩阵 如何处理？
            }
        } else {
            r.unlockItemDic.Add(outputID, 10);
        }
        return r;
    }

    /// <summary>
    /// 创建一个点数聚集配方。
    /// </summary>
    private static FracRecipe CreatePointsAggregateRecipe(int itemID) {
        FracRecipe r = new FracRecipe(FracRecipeType.PointsAggregate, itemID, itemID, 0.01f, 1, 0).Unlock();
        pointsAggregateRecipeList.Add(r);
        r.unlockItemDic.Add(itemID, 10);
        return r;
    }

    /// <summary>
    /// 创建一个量子复制配方。
    /// </summary>
    private static FracRecipe CreateIncreaseRecipe(int itemID, float ratio) {
        FracRecipe r = new(FracRecipeType.Increase, itemID, itemID, ratio, 2, 0);
        increaseRecipeList.Add(r);
        r.unlockItemDic.Add(itemID, 10);
        return r;
    }

    /// <summary>
    /// 添加一些具有上下级关系的物品构成的升转化链对应的配方。
    /// </summary>
    private static List<FracRecipe>[] CreateFracChain(IReadOnlyList<int> itemChain, bool special) {
        List<FracRecipe> list1 = [];
        List<FracRecipe> list2 = [];
        for (int i = 0; i < itemChain.Count - 1; i++) {
            //upgrade: itemChain[i] -> itemChain[i + 1]
            float success1 = IPFDic[itemChain[i + 1]] * 2.0f;
            FracRecipe r1 = CreateUpgradeRecipe(itemChain[i], itemChain[i + 1], success1, 0.02f, special);
            //升级分馏时，有小概率分馏出下两级产物
            if (i < itemChain.Count - 2) {
                float success11 = IPFDic[itemChain[i + 2]] / 5.0f;
                r1.AddProduct(itemChain[i + 2], success11, 1);
            }
            list1.Add(r1);
            //downgrade: itemChain[i + 1] -> itemChain[i]
            float success2 = IPFDic[itemChain[i + 1]] * 3.0f;
            FracRecipe r2 = CreateDownGradeRecipe(itemChain[i + 1], itemChain[i], success2, 0.05f, special);
            list2.Add(r2);
        }
        return [list1, list2];
    }

    /// <summary>
    /// 添加一些同等级物品构成的升转化链对应的配方。
    /// </summary>
    private static List<FracRecipe>[] CreateFracChain3(IReadOnlyList<int> itemChain) {
        //todo: 任何物品分馏时，都有概率分馏出分馏链内任意物品
        List<FracRecipe> list1 = [];
        List<FracRecipe> list2 = [];
        for (int i = 0; i < itemChain.Count - 1; i++) {
            //upgrade: itemChain[i] -> itemChain[i + 1]
            float success1 = IPFDic[itemChain[i + 1]];
            FracRecipe r1 = CreateUpgradeRecipe(itemChain[i], itemChain[i + 1], success1, 0.01f, false);
            //升级分馏时，有小概率分馏出下两级产物
            if (i < itemChain.Count - 2) {
                float success11 = IPFDic[itemChain[i + 2]] / 5.0f;
                r1.AddProduct(itemChain[i + 2], success11, 1);
            }
            list1.Add(r1);
            //downgrade: itemChain[i + 1] -> itemChain[i]
            float success2 = IPFDic[itemChain[i + 1]];
            FracRecipe r2 = CreateDownGradeRecipe(itemChain[i + 1], itemChain[i], success2, 0.01f, false);
            list2.Add(r2);
        }
        return [list1, list2];
    }

    #endregion

    #region 从存档读取分馏配方数据

    public static void Import(BinaryReader r) {
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int inputID = r.ReadInt32();
            FracRecipe recipe = GetNaturalResourceRecipe(inputID);
            if (recipe == null) {
                //规定每一个配方导出信息时，最后必须为 int.MaxValue
                while (r.ReadInt32() != int.MaxValue) { }
            } else {
                recipe.Import(r);
            }
        }
        count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int inputID = r.ReadInt32();
            FracRecipe recipe = GetUpgradeRecipe(inputID);
            if (recipe == null) {
                while (r.ReadInt32() != int.MaxValue) { }
            } else {
                recipe.Import(r);
            }
        }
        count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int inputID = r.ReadInt32();
            FracRecipe recipe = GetDowngradeRecipe(inputID);
            if (recipe == null) {
                while (r.ReadInt32() != int.MaxValue) { }
            } else {
                recipe.Import(r);
            }
        }
        count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int inputID = r.ReadInt32();
            FracRecipe recipe = GetPointsAggregateRecipe(inputID);
            if (recipe == null) {
                while (r.ReadInt32() != int.MaxValue) { }
            } else {
                recipe.Import(r);
            }
        }
        count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int inputID = r.ReadInt32();
            FracRecipe recipe = GetIncreaseRecipe(inputID);
            if (recipe == null) {
                while (r.ReadInt32() != int.MaxValue) { }
            } else {
                recipe.Import(r);
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(naturalResourceRecipeList.Count);
        foreach (FracRecipe recipe in naturalResourceRecipeList) {
            w.Write(recipe.inputID);
            recipe.Export(w);
        }
        w.Write(upgradeRecipeList.Count);
        foreach (FracRecipe recipe in upgradeRecipeList) {
            w.Write(recipe.inputID);
            recipe.Export(w);
        }
        w.Write(downgradeRecipeList.Count);
        foreach (FracRecipe recipe in downgradeRecipeList) {
            w.Write(recipe.inputID);
            recipe.Export(w);
        }
        w.Write(pointsAggregateRecipeList.Count);
        foreach (FracRecipe recipe in pointsAggregateRecipeList) {
            w.Write(recipe.inputID);
            recipe.Export(w);
        }
        w.Write(increaseRecipeList.Count);
        foreach (FracRecipe recipe in increaseRecipeList) {
            w.Write(recipe.inputID);
            recipe.Export(w);
        }
    }

    public static void IntoOtherSave() { }

    #endregion

    #region 解锁分馏配方

    /// <summary>
    /// 如果科技已解锁但是配方未解锁，则解锁配方。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UITechTree), nameof(UITechTree.Do1KeyUnlock))]
    public static void UITechTree_Do1KeyUnlock_Postfix() {
        int unlockCount = 0;
        foreach (FracRecipe r in naturalResourceRecipeList) {
            if (!r.IsUnlocked) {
                unlockCount++;
                r.Unlock();
            }
        }
        if (unlockCount > 0) {
            LogInfo($"Unlocked {naturalResourceRecipeList.Count} natural resource recipes.");
        }
        unlockCount = 0;
        foreach (FracRecipe r in upgradeRecipeList) {
            if (!r.IsUnlocked) {
                unlockCount++;
                r.Unlock();
            }
        }
        if (unlockCount > 0) {
            LogInfo($"Unlocked {upgradeRecipeList.Count} upgrade recipes.");
        }
        unlockCount = 0;
        foreach (FracRecipe r in downgradeRecipeList) {
            if (!r.IsUnlocked) {
                unlockCount++;
                r.Unlock();
            }
        }
        if (unlockCount > 0) {
            LogInfo($"Unlocked {downgradeRecipeList.Count} downgrade recipes.");
        }
        unlockCount = 0;
        foreach (FracRecipe r in pointsAggregateRecipeList) {
            if (!r.IsUnlocked) {
                unlockCount++;
                r.Unlock();
            }
        }
        if (unlockCount > 0) {
            LogInfo($"Unlocked {pointsAggregateRecipeList.Count} points aggregate recipes.");
        }
        unlockCount = 0;
        foreach (FracRecipe r in increaseRecipeList) {
            if (!r.IsUnlocked) {
                unlockCount++;
                r.Unlock();
            }
        }
        if (unlockCount > 0) {
            LogInfo($"Unlocked {increaseRecipeList.Count} increase recipes.");
        }
    }

    #endregion
}
