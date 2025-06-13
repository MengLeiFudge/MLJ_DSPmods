using FE.Logic.Recipe;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.FractionateEverything;
using static FE.Utils.ProtoID;

namespace FE.Logic.Manager;

public static class RecipeManager {
    #region 配方列表

    /// <summary>
    /// 配方列表，[(int)ERecipe][(int)ItemID]
    /// </summary>
    private static BaseRecipe[][] BaseRecipes = null;

    /// <summary>
    /// 临时存储所有配方，生成baseRecipes之后就不需要了
    /// </summary>
    private static readonly Dictionary<ERecipe, List<BaseRecipe>> RecipesWithType = [];

    /// <summary>
    /// 存储所有配方
    /// </summary>
    private static readonly List<BaseRecipe> RecipeList = [];

    /// <summary>
    /// 添加一个配方
    /// </summary>
    public static void AddRecipe<T>(T recipe) where T : BaseRecipe {
        ERecipe recipeType = recipe.RecipeType;
        if (!RecipesWithType.TryGetValue(recipeType, out var recipeList)) {
            recipeList = [];
            RecipesWithType[recipeType] = recipeList;
        }
        if (recipeList.Any(r => r.InputID == recipe.InputID)) {
            LogError($"Recipe with ID {recipe.InputID} already exists for type {recipeType}");
            return;
        }
        recipeList.Add(recipe);
        RecipeList.Add(recipe);
        LogInfo($"Add {recipe.InputID} {LDB.items.Select(recipe.InputID).Name} to {recipeType.ToString()} Recipe.");
    }

    /// <summary>
    /// 获取指定类型的指定输入ID的配方
    /// </summary>
    /// <param name="recipeType">要获取的配方类型</param>
    /// <param name="inputId">要获取的配方的输入ID</param>
    /// <typeparam name="T">BaseRecipe的子类</typeparam>
    /// <returns>类型为recipeType，输入物品ID为inputId的配方。找不到返回null</returns>
    public static T GetRecipe<T>(ERecipe recipeType, int inputId) where T : BaseRecipe {
        return BaseRecipes[(int)recipeType][inputId] as T;
    }

    #endregion

    public static void UnlockAll() {
        foreach (var recipe in RecipeList) {
            if (!recipe.IsUnlocked) {
                recipe.Level = 1;
                recipe.Quality = 1;
                LogInfo($"Unlocked {recipe.RecipeType} recipe - {LDB.items.Select(recipe.InputID).Name}");
            }
        }
    }

    #region 创建配方

#if DEBUG
    private const string SPRITE_CSV_DIR = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
    private const string SPRITE_CSV_PATH = $@"{SPRITE_CSV_DIR}\fracIconPath.csv";
#endif

    public static void AddBaseRecipes() {
#if DEBUG
        if (File.Exists(SPRITE_CSV_PATH)) {
            File.Delete(SPRITE_CSV_PATH);
        }
#endif

        LogInfo("Begin to add fractionate recipes...");

        MineralCopyRecipe.CreateAll();
        QuantumCopyRecipe.CreateAll();
        AlchemyRecipe.CreateAll();
        DeconstructionRecipe.CreateAll();
        ConversionRecipe.CreateAll();

        BaseRecipes = new BaseRecipe[Enum.GetNames(typeof(ERecipe)).Length + 1][];
        for (int i = 0; i < BaseRecipes.Length; i++) {
            BaseRecipes[i] = new BaseRecipe[12000];
        }
        foreach (var p in RecipesWithType) {
            foreach (var recipe in p.Value) {
                BaseRecipes[(int)p.Key][recipe.InputID] = recipe;
            }
        }

        // if (!GenesisBook.Enable) {
        //     //自然资源复制
        //     CreateNaturalResourceRecipe(I铁矿, 0.05f);
        //     CreateNaturalResourceRecipe(I铜矿, 0.05f);
        //     CreateNaturalResourceRecipe(I硅石, 0.05f).AddProduct(I分形硅石, 0.01f, 1);
        //     CreateNaturalResourceRecipe(I钛石, 0.05f);
        //     CreateNaturalResourceRecipe(I石矿, 0.05f).AddProduct(I硅石, 0.01f, 1).AddProduct(I钛石, 0.01f, 1);
        //     CreateNaturalResourceRecipe(I煤矿, 0.05f);
        //     CreateNaturalResourceRecipe(I水, 0.05f);
        //     CreateNaturalResourceRecipe(I原油, 0.05f);
        //     CreateNaturalResourceRecipe(I硫酸, 0.05f);
        //     CreateNaturalResourceRecipe(I氢, 0.05f).AddProduct(I重氢, 0.01f, 1);
        //     CreateNaturalResourceRecipe(I重氢, 0.05f);
        //     CreateNaturalResourceRecipe(I可燃冰, 0.025f);
        //     CreateNaturalResourceRecipe(I金伯利矿石, 0.025f);
        //     CreateNaturalResourceRecipe(I分形硅石, 0.025f);
        //     CreateNaturalResourceRecipe(I光栅石, 0.025f);
        //     CreateNaturalResourceRecipe(I刺笋结晶, 0.025f);
        //     CreateNaturalResourceRecipe(I单极磁石, 0.01f);
        //     CreateNaturalResourceRecipe(I有机晶体, 0.025f);
        //     CreateNaturalResourceRecipe(I临界光子, 0.01f);
        //     //升转化链
        //     //分为消耗品、材料、建筑三种
        //     //消耗品指燃料棒、弹药、增产剂
        //     //材料指电路板、处理器等
        //     //建筑指能放置的物品
        //     CreateFracChain([I铁块, I钢材, I钛合金], true);
        //     CreateFracChain([I框架材料, I戴森球组件, I小型运载火箭], true);
        //     CreateFracChain([I高纯硅块, I晶格硅], true);
        //     CreateFracChain([I石材, I地基], true);
        //     CreateFracChain([I玻璃, I钛化玻璃, I位面过滤器], true);
        //     CreateFracChain([I棱镜, I电浆激发器, I光子合并器, I太阳帆], true);
        //     CreateFracChain([I高能石墨, I金刚石], true);
        //     CreateFracChain([I石墨烯, I碳纳米管, I粒子宽带], true);
        //     CreateFracChain([I粒子容器, I奇异物质, I引力透镜, I空间翘曲器], true);
        //     CreateFracChain([I精炼油_GB焦油, I塑料_GB聚丙烯, I有机晶体], true);
        //     CreateFracChain([I钛晶石, I卡西米尔晶体], true);
        //     CreateFracChain3([I水, I硫酸]);
        //     CreateFracChain([I磁铁, I磁线圈_GB铜线圈, I电动机], true);
        //     CreateFracChain([I电动机, I电磁涡轮, I超级磁场环], true);
        //     CreateFracChain([I电路板, I处理器, I量子芯片], true);
        //     CreateFracChain([I原型机, I精准无人机, I攻击无人机], false);
        //     if (!TheyComeFromVoid.Enable) {
        //         CreateFracChain([I护卫舰, I驱逐舰], false);
        //     } else {
        //         CreateFracChain([I护卫舰, I驱逐舰, IVD水滴], false);
        //     }
        //     CreateFracChain3([I临界光子, I反物质]);
        //     CreateFracChain3([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素]);
        //     CreateFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], true);
        //     CreateFracChain([I增产剂MkI, I增产剂MkII, I增产剂MkIII_GB增产剂], false);
        //     CreateFracChain([I燃烧单元, I爆破单元, I晶石爆破单元], true);
        //     CreateFracChain([I动力引擎, I推进器, I加力推进器], true);
        //     CreateFracChain([I配送运输机, I物流运输机, I星际物流运输船], false);
        //     CreateFracChain([I液氢燃料棒, I氘核燃料棒, I反物质燃料棒, I奇异湮灭燃料棒], true);
        //     CreateFracChain([I机枪弹箱, I钛化弹箱, I超合金弹箱], false);
        //     CreateFracChain([I炮弹组, I高爆炮弹组, I晶石炮弹组], false);
        //     CreateFracChain([I导弹组, I超音速导弹组, I引力导弹组], false);
        //     CreateFracChain([I等离子胶囊, I反物质胶囊], false);
        //     CreateFracChain([I干扰胶囊, I压制胶囊], false);
        //
        //
        //     //建筑I
        //     CreateFracChain([I电力感应塔, I无线输电塔, I卫星配电站], false);
        //     CreateFracChain([I风力涡轮机, I太阳能板, I蓄电器, I蓄电器满, I能量枢纽], false);
        //     CreateFracChain([I火力发电厂_GB燃料电池发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB朱曦K型人造恒星], false);
        //     //建筑II
        //     CreateFracChain([I传送带, I高速传送带, I极速传送带], false);
        //     CreateFracChain([I流速监测器, I四向分流器, I喷涂机, I自动集装机], false);//注意科技解锁顺序
        //     CreateFracChain([I小型储物仓, I储液罐, I大型储物仓], false);//注意科技解锁顺序
        //     if (!MoreMegaStructure.Enable) {
        //         CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器], false);
        //     } else {
        //         CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器], false);
        //     }
        //     //建筑III
        //     CreateFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器], false);
        //     CreateFracChain([I采矿机, I大型采矿机], false);
        //     CreateFracChain([I抽水站, I原油萃取站, I原油精炼厂], false);
        //     CreateFracChain([I化工厂, I量子化工厂_GB先进化学反应釜], false);
        //     //CreateFracChain([I分馏塔, I微型粒子对撞机], false);
        //     //建筑IV
        //     CreateFracChain([I电弧熔炉, I位面熔炉, I负熵熔炉], false);
        //     CreateFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂], false);
        //     CreateFracChain([I矩阵研究站, I自演化研究站], false);
        //     CreateFracChain([I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井], false);
        //     //建筑V
        //     CreateFracChain3([I高斯机枪塔, I导弹防御塔, I聚爆加农炮]);//注意科技解锁顺序
        //     CreateFracChain3([I高频激光塔, I磁化电浆炮, I近程电浆塔]);//注意科技解锁顺序
        //     CreateFracChain3([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);//注意科技解锁顺序
        //     //建筑VI
        //     // List<BaseRecipe>[] lists =
        //     //     CreateFracChain([IFE矿物复制塔, IFE转化塔, IFE转化塔, IFE垃圾回收分馏塔, IFE点数聚集塔, IFE量子复制塔], false);
        //     // foreach (BaseRecipe recipe in lists[0]) {
        //     //     recipe.AddProduct(IFE老虎机分馏塔, 0.001f, 1);
        //     // }
        // } else {
        //     //创世改动过大，单独处理
        //     RegisterOrEditAsync("左键点击：更换生产设备",
        //         "Left click: Change machine\nRight click: Assembler or Fractionator",
        //         "左键点击：更换生产设备\n右键点击：常规设备或分馏塔");
        //     //物品页面
        //     //自然资源自增值
        //
        //     CreateNaturalResourceRecipe(I铁矿, 0.05f);
        //     CreateNaturalResourceRecipe(I铜矿, 0.05f);
        //     CreateNaturalResourceRecipe(IGB铝矿, 0.05f);
        //     CreateNaturalResourceRecipe(I硅石, 0.05f).AddProduct(I分形硅石, 0.01f, 1);
        //     CreateNaturalResourceRecipe(I钛石, 0.05f);
        //     CreateNaturalResourceRecipe(IGB钨矿, 0.05f);
        //     CreateNaturalResourceRecipe(I煤矿, 0.05f);
        //     CreateNaturalResourceRecipe(I石矿, 0.05f).AddProduct(I硅石, 0.01f, 1).AddProduct(I钛石, 0.01f, 1);
        //     CreateNaturalResourceRecipe(IGB硫矿, 0.05f);
        //     CreateNaturalResourceRecipe(IGB放射性矿物, 0.05f);
        //
        //     CreateNaturalResourceRecipe(I原油, 0.05f);
        //     CreateNaturalResourceRecipe(IGB海水, 0.05f);
        //     CreateNaturalResourceRecipe(I水, 0.05f);
        //     CreateNaturalResourceRecipe(IGB盐酸, 0.05f);
        //     CreateNaturalResourceRecipe(I硫酸, 0.05f);
        //     CreateNaturalResourceRecipe(IGB硝酸, 0.05f);
        //     CreateNaturalResourceRecipe(IGB氨, 0.05f);
        //
        //     CreateNaturalResourceRecipe(I氢, 0.05f).AddProduct(I重氢, 0.01f, 1);
        //     CreateNaturalResourceRecipe(I重氢, 0.05f);
        //     CreateNaturalResourceRecipe(IGB氦, 0.05f).AddProduct(IGB氦三, 0.01f, 1);
        //     CreateNaturalResourceRecipe(IGB氮, 0.05f);
        //     CreateNaturalResourceRecipe(IGB氧, 0.05f);
        //     CreateNaturalResourceRecipe(IGB二氧化碳, 0.05f);
        //     CreateNaturalResourceRecipe(IGB二氧化硫, 0.05f);
        //
        //     CreateNaturalResourceRecipe(I金伯利矿石, 0.025f);
        //     CreateNaturalResourceRecipe(I分形硅石, 0.025f);
        //     CreateNaturalResourceRecipe(I可燃冰, 0.025f);
        //     CreateNaturalResourceRecipe(I刺笋结晶, 0.025f);
        //     CreateNaturalResourceRecipe(I有机晶体, 0.025f);
        //     CreateNaturalResourceRecipe(I光栅石, 0.025f);
        //     CreateNaturalResourceRecipe(I单极磁石, 0.025f);
        //
        //     //物品页面
        //     CreateFracChain([I钢材, I钛合金, IGB钨合金, IGB三元精金], true);
        //     CreateFracChain([I框架材料, I戴森球组件, I小型运载火箭], true);
        //     CreateFracChain([I高纯硅块, I晶格硅], true);
        //     //CreateFracChain([I石材, IGB混凝土], true);
        //     CreateFracChain([I棱镜, I电浆激发器, I光子合并器, I太阳帆], true);
        //     CreateFracChain([I高能石墨, I金刚石], true);
        //     CreateFracChain([I石墨烯, I碳纳米管, I粒子宽带], true);
        //     CreateFracChain([I粒子宽带, IGB光学信息传输纤维], true);
        //     CreateFracChain([I粒子容器, I奇异物质, I引力透镜, I空间翘曲器], true);
        //     CreateFracChain([I钛晶石, I卡西米尔晶体], true);
        //     CreateFracChain([IGB基础机械组件, IGB先进机械组件, IGB尖端机械组件, IGB超级机械组件], true);//创世独有配方
        //     CreateFracChain([IGB塑料基板, IGB光学基板], true);//创世独有配方
        //     CreateFracChain([IGB量子计算主机, IGB超越X1型光学主机], true);//创世独有配方
        //     CreateFracChain([I玻璃, I钛化玻璃, IGB钨强化玻璃], true);
        //     CreateFracChain([I氢, I重氢], true);
        //     CreateFracChain([IGB氦, IGB氦三], true);
        //     CreateFracChain3([I水, I硫酸]);
        //     CreateFracChain([I磁线圈_GB铜线圈, I电动机, I电磁涡轮, I超级磁场环], true);
        //     CreateFracChain([I电路板, I处理器, I量子芯片, IGB光学处理器], true);
        //     CreateFracChain3([I临界光子, I反物质]);
        //     CreateFracChain([I动力引擎, I推进器, I加力推进器], true);
        //     CreateFracChain([I配送运输机, I物流运输机, I星际物流运输船], false);
        //     CreateFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素], false);
        //     CreateFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵], true);
        //
        //     //建筑页面
        //     CreateFracChain([I电力感应塔, I无线输电塔, I卫星配电站], false);
        //     CreateFracChain([I风力涡轮机, I太阳能板, IGB同位素温差发电机, I蓄电器, I蓄电器满, I能量枢纽], false);
        //     CreateFracChain([I火力发电厂_GB燃料电池发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB朱曦K型人造恒星, IGB湛曦O型人造恒星], false);
        //     CreateFracChain([I传送带, I高速传送带, I极速传送带], false);
        //     CreateFracChain([I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机], false);//注意科技解锁顺序
        //     CreateFracChain([I小型储物仓, I大型储物仓, IGB量子储物仓], false);
        //     CreateFracChain([I储液罐, IGB量子储液罐], false);
        //     if (!MoreMegaStructure.Enable) {
        //         CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器], false);
        //     } else {
        //         CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器], false);
        //     }
        //     CreateFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器], false);
        //     CreateFracChain([I采矿机, I大型采矿机], false);
        //     CreateFracChain([I抽水站, IGB聚束液体汲取设施], false);
        //     CreateFracChain([I原油萃取站, I原油精炼厂], false);
        //     CreateFracChain([I化工厂, I量子化工厂_GB先进化学反应釜], false);
        //     CreateFracChain([I电弧熔炉, IGB矿物处理厂, I位面熔炉, I负熵熔炉], false);
        //     CreateFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂], false);
        //     CreateFracChain([I矩阵研究站, I自演化研究站], false);
        //     CreateFracChain([I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井], false);
        //     //CreateFracChain([I微型粒子对撞机], false);
        //     CreateFracChain3([IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器]);
        //     // List<BaseRecipe>[] lists =
        //     //     CreateFracChain([IFE矿物复制塔, IFE转化塔, IFE转化塔, IFE垃圾回收分馏塔, IFE点数聚集塔, IFE量子复制塔], false);
        //     // foreach (BaseRecipe recipe in lists[0]) {
        //     //     recipe.AddProduct(IFE老虎机分馏塔, 0.001f, 1);
        //     // }
        //
        //     //精炼页面
        //     CreateFracChain([
        //         IGB空燃料棒,
        //         I液氢燃料棒, IGB煤油燃料棒, IGB四氢双环戊二烯燃料棒,
        //         IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
        //         I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
        //         I反物质燃料棒, I奇异湮灭燃料棒,
        //     ], true);
        //
        //     //化工页面
        //     CreateFracChain([I塑料_GB聚丙烯, IGB聚苯硫醚PPS, IGB聚酰亚胺PI], true);
        //     //CreateFracChain([I增产剂MkIII_GB增产剂], false);
        //
        //     //防御页面
        //     CreateFracChain([I原型机, I精准无人机, I攻击无人机], false);
        //     if (!TheyComeFromVoid.Enable) {
        //         CreateFracChain([I护卫舰, I驱逐舰], false);
        //     } else {
        //         CreateFracChain([I护卫舰, I驱逐舰, IVD水滴], false);
        //     }
        //     CreateFracChain([I高频激光塔, IGB紫外激光塔, I近程电浆塔, I磁化电浆炮], false);
        //     CreateFracChain3([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);
        //     CreateFracChain3([I高斯机枪塔, I聚爆加农炮, IGB电磁加农炮, I导弹防御塔]);
        //     CreateFracChain([I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱], false);
        //     CreateFracChain([I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元], false);
        //     CreateFracChain([I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组], false);
        //     CreateFracChain([I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组], false);
        //     CreateFracChain([I干扰胶囊, I压制胶囊], false);
        //     CreateFracChain([I等离子胶囊, I反物质胶囊], false);
        // }
        //
        // //为所有物品添加点数聚集配方以及量子复制配方
        // for (int i = 0; i < LDB.items.Length; i++) {
        //     int itemID = LDB.items[i].ID;
        //     CreatePointsAggregateRecipe(itemID);
        //     CreateIncreaseRecipe(itemID, itemRatioDic[itemID]);
        // }

        //添加所有翻译
        //LoadLanguagePostfixAfterCommonApi();

        LogInfo("Finish to add fractionate recipes.");
    }

    #endregion

    #region 从存档读取配方数据

    public static void Import(BinaryReader r) {
        int typeCount = r.ReadInt32();
        for (int typeIndex = 0; typeIndex < typeCount; typeIndex++) {
            ERecipe recipeType = (ERecipe)r.ReadInt32();
            int recipeCount = r.ReadInt32();
            for (int i = 0; i < recipeCount; i++) {
                int inputID = r.ReadInt32();
                BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, inputID);
                if (recipe == null) {
                    int byteCount = r.ReadInt32();
                    for (int j = 0; j < byteCount; j++) {
                        r.ReadByte();
                    }
                } else {
                    recipe.Import(r);
                }
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(RecipesWithType.Count);
        foreach (var p in RecipesWithType) {
            w.Write((int)p.Key);
            w.Write(p.Value.Count);
            foreach (BaseRecipe recipe in p.Value) {
                w.Write(recipe.InputID);
                recipe.Export(w);
            }
        }
    }

    public static void IntoOtherSave() { }

    #endregion
}
