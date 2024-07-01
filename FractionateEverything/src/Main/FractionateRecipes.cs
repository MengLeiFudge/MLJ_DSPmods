using FractionateEverything.Compatibility;
using FractionateEverything.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.FractionateEverything;
using static FractionateEverything.Compatibility.GenesisBook;
using static FractionateEverything.Compatibility.MoreMegaStructure;
using static FractionateEverything.Utils.AddProtoUtils;
using static FractionateEverything.Utils.TranslationUtils;

namespace FractionateEverything.Main {
    //LDB.ItemName 等价于 itemproto.name，itemproto.name 等价于 itemproto.Name.Translate()
    //name: 推进器  name.Translate: <0xa0>-<0xa0>推进器  Name: 推进器2  Name.Translate: 推进器
    //name: Thruster  name.Translate: Thruster  Name: 推进器2  Name.Translate: Thruster
    //name: 制造台<0xa0>Mk.I  name.Translate: 制造台<0xa0>Mk.I  Name: 制造台 Mk.I  Name.Translate: 制造台<0xa0>Mk.I
    //name: Assembling Machine Mk.I  name.Translate: Assembling Machine Mk.I  Name: 制造台 Mk.I  Name.Translate: Assembling Machine Mk.I

    /// <summary>
    /// 向游戏中添加所有新增的分馏配方，并生成分馏关系表（包括原料-产物，以及对应概率表）
    /// 注意，添加的分馏配方仅用于显示，分馏塔实际使用生成的关系表进行处理
    /// </summary>
    public static class FractionateRecipes {
        #region 默认概率表，仅用于配方显示

        private static readonly Dictionary<int, float> defaultNumRatioCommonVein = new() { { 2, 0.05f } };
        private static readonly Dictionary<int, float> defaultNumRatioRealVein = new() { { 2, 0.025f } };
        private static readonly Dictionary<int, float> defaultNumRatioUpgrade = new() { { 1, 0.04f }, { -1, 0.01f } };
        private static readonly Dictionary<int, float> defaultNumRatioDowngrade = new() { { 2, 0.02f } };

        #endregion

        #region 物品分馏对应关系以及概率

        private static readonly Dictionary<int, float> noResultDic = new() { { 1, 0.00f } };
        private static readonly List<int> itemNaturalResourceList = [];
        private static readonly Dictionary<int, Dictionary<int, float>> numRatioNaturalResourceDic = [];
        private static readonly Dictionary<int, int> itemUpgradeDic = [];
        private static readonly Dictionary<int, Dictionary<int, float>> numRatioUpgradeDic = [];
        private static readonly Dictionary<int, int> itemDowngradeDic = [];
        private static readonly Dictionary<int, Dictionary<int, float>> numRatioDowngradeDic = [];

        public static int GetItemNaturalResource(int itemID) =>
            itemNaturalResourceList.Contains(itemID) ? itemID : 0;

        public static Dictionary<int, float> GetNumRatioNaturalResource(int itemID) =>
            numRatioNaturalResourceDic.TryGetValue(itemID, out Dictionary<int, float> dic) ? dic : noResultDic;

        public static int GetItemUpgrade(int itemID) =>
            itemUpgradeDic.TryGetValue(itemID, out int value) ? value : 0;

        public static Dictionary<int, float> GetNumRatioUpgrade(int itemID) =>
            numRatioUpgradeDic.TryGetValue(itemID, out Dictionary<int, float> dic) ? dic : noResultDic;

        public static int GetItemDowngrade(int itemID) =>
            itemDowngradeDic.TryGetValue(itemID, out int value) ? value : 0;

        public static Dictionary<int, float> GetNumRatioDowngrade(int itemID) =>
            numRatioDowngradeDic.TryGetValue(itemID, out Dictionary<int, float> dic) ? dic : noResultDic;

        #endregion

#if DEBUG
        /// <summary>
        /// sprite名称将被记录在该文件中。
        /// </summary>
        private const string SPRITE_CSV_PATH =
            @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata\fracIconPath.csv";
#endif

        private static RecipeHelper helper;
        private static readonly List<RecipeProto> recipeList = [];
        private static List<RecipeProto> fuelRodRecipeList = [];
        private static List<RecipeProto> matrixRecipeList = [];
        private static List<int> fuelRodGridIndexList = [];
        private static List<int> matrixGridIndexList = [];

        public static void AddFracRecipes() {
#if DEBUG
            if (File.Exists(SPRITE_CSV_PATH)) {
                File.Delete(SPRITE_CSV_PATH);
            }
#endif

            LogInfo("Begin to add fractionate recipes...");

            helper = new(tab分馏1);
            List<RecipeProto> list;
            if (!GenesisBook.Enable) {
                //自然资源自增值
                AddFracRecipe(I铁矿, defaultNumRatioCommonVein).Modify(tab分馏1, 101);
                AddFracRecipe(I铜矿, defaultNumRatioCommonVein).Modify(tab分馏1, 102);
                AddFracRecipe(I硅石, defaultNumRatioCommonVein).Modify(tab分馏1, 103, T冶炼提纯);
                AddFracRecipe(I钛石, defaultNumRatioCommonVein).Modify(tab分馏1, 104, T钛矿冶炼);
                AddFracRecipe(I石矿, defaultNumRatioCommonVein).Modify(tab分馏1, 105);
                AddFracRecipe(I煤矿, defaultNumRatioCommonVein).Modify(tab分馏1, 106);
                AddFracRecipe(I水, defaultNumRatioCommonVein).Modify(tab分馏1, 201, T流体储存封装);
                AddFracRecipe(I原油, defaultNumRatioCommonVein).Modify(tab分馏1, 202, T等离子萃取精炼);
                AddFracRecipe(I硫酸, defaultNumRatioCommonVein).Modify(tab分馏1, 203);
                AddFracRecipe(I氢, defaultNumRatioCommonVein).Modify(tab分馏1, 204);
                AddFracRecipe(I重氢, defaultNumRatioCommonVein).Modify(tab分馏1, 205);
                AddFracRecipe(I可燃冰, defaultNumRatioRealVein).Modify(tab分馏1, 208, T应用型超导体);
                AddFracRecipe(I金伯利矿石, defaultNumRatioRealVein).Modify(tab分馏1, 306, T晶体冶炼);
                AddFracRecipe(I分形硅石, defaultNumRatioRealVein).Modify(tab分馏1, 303, T粒子可控);
                AddFracRecipe(I光栅石, defaultNumRatioRealVein).Modify(tab分馏1, 605, T卡西米尔晶体);
                AddFracRecipe(I刺笋结晶, defaultNumRatioRealVein).Modify(tab分馏1, 508, T高强度材料);
                AddFracRecipe(I单极磁石, defaultNumRatioRealVein).Modify(tab分馏1, 606, T粒子磁力阱);
                AddFracRecipe(I有机晶体, defaultNumRatioRealVein).Modify(tab分馏1, 309, T高分子化工);
                //物品循环链
                AddFracChain([I原型机, I精准无人机, I攻击无人机]);
                if (!TheyComeFromVoid.Enable) {
                    AddFracChain([I护卫舰, I驱逐舰]);
                }
                else {
                    list = AddFracChain([I护卫舰, I驱逐舰, IVD水滴]);
                    list[1].Modify(tab巨构, 112);
                }
                list = AddFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素]);
                list[0].Modify(tab分馏1, 807);
                list[1].Modify(tab分馏1, 808);
                list[2].Modify(tab分馏1, 809);
                list[3].Modify(tab分馏1, 810);
                list[4].Modify(tab分馏1, 811);
                matrixRecipeList = AddFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵]);
                matrixGridIndexList = matrixRecipeList.Select(r => r.GridIndex).ToList();
                foreach (RecipeProto r in matrixRecipeList) {
                    r.ModifyIconAndDesc();
                }
                AddFracChain([I增产剂MkI, I增产剂MkII, I增产剂MkIII_GB增产剂]);
                if (!MoreMegaStructure.Enable) {
                    AddFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器]);
                }
                else {
                    list = AddFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器]);
                    list[2].Modify(tab巨构, 410);
                }
                AddFracChain([I配送运输机, I物流运输机, I星际物流运输船]);
                fuelRodRecipeList = AddFracChain([I液氢燃料棒, I氘核燃料棒, I反物质燃料棒, I奇异湮灭燃料棒]);
                fuelRodGridIndexList = fuelRodRecipeList.Select(r => r.GridIndex).ToList();
                foreach (RecipeProto r in fuelRodRecipeList) {
                    r.ModifyIconAndDesc();
                }
                AddFracChain([I机枪弹箱, I钛化弹箱, I超合金弹箱]);
                AddFracChain([I炮弹组, I高爆炮弹组, I晶石炮弹组]);
                AddFracChain([I导弹组, I超音速导弹组, I引力导弹组]);
                AddFracChain([I等离子胶囊, I反物质胶囊]);
                AddFracChain([I干扰胶囊, I压制胶囊]);

                //建筑I
                AddFracChain([I电力感应塔, I无线输电塔, I卫星配电站]);
                list = AddFracChain([I风力涡轮机, I太阳能板, I蓄电器, I蓄电器满, I能量枢纽]);
                list[2].Modify(tab分馏2, 113);
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
                AddFracChain([I分馏塔, I微型粒子对撞机]);
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
                AddFracChain([IFE自然资源分馏塔, IFE升级分馏塔, IFE降级分馏塔, IFE垃圾回收分馏塔, IFE点数聚集分馏塔, IFE增产分馏塔]);
            }
            else {
                //创世改动过大，单独处理
                RegisterOrEditAsync("左键点击：更换生产设备",
                    "Left click: Change machine\nRight click: Assembler or Fractionator",
                    "左键点击：更换生产设备\n右键点击：常规设备或分馏塔");
                //物品页面
                //自然资源自增值
                AddFracRecipe(I铁矿, defaultNumRatioCommonVein).Modify(tab分馏1, 101);
                AddFracRecipe(I铜矿, defaultNumRatioCommonVein).Modify(tab分馏1, 102);
                AddFracRecipe(IGB铝矿, defaultNumRatioCommonVein).Modify(tab分馏1, 103);
                AddFracRecipe(I硅石, defaultNumRatioCommonVein).Modify(tab分馏1, 104, T冶炼提纯);
                AddFracRecipe(I钛石, defaultNumRatioCommonVein).Modify(tab分馏1, 105, T钛矿冶炼);
                AddFracRecipe(IGB钨矿, defaultNumRatioCommonVein).Modify(tab分馏1, 106, T钛矿冶炼);
                AddFracRecipe(I煤矿, defaultNumRatioCommonVein).Modify(tab分馏1, 107, T冶炼提纯);
                AddFracRecipe(I石矿, defaultNumRatioCommonVein).Modify(tab分馏1, 108);
                AddFracRecipe(IGB硫矿, defaultNumRatioCommonVein).Modify(tab分馏1, 109, TGB矿物处理);
                AddFracRecipe(I水, defaultNumRatioCommonVein).Modify(tab分馏1, 201);
                AddFracRecipe(I原油, defaultNumRatioCommonVein).Modify(tab分馏1, 202, TGB焦油精炼);
                AddFracRecipe(I硫酸, defaultNumRatioCommonVein).Modify(tab分馏1, 203);
                AddFracRecipe(IGB氨, defaultNumRatioCommonVein).Modify(tab分馏1, 204);
                AddFracRecipe(I氢, defaultNumRatioCommonVein).Modify(tab分馏1, 205);
                AddFracRecipe(I重氢, defaultNumRatioCommonVein).Modify(tab分馏1, 206);
                AddFracRecipe(IGB氦, defaultNumRatioCommonVein).Modify(tab分馏1, 207);
                AddFracRecipe(IGB氮, defaultNumRatioCommonVein).Modify(tab分馏1, 208, T氢燃料棒_GB气体冷凝);
                AddFracRecipe(IGB氧, defaultNumRatioCommonVein).Modify(tab分馏1, 209);
                AddFracRecipe(IGB放射性矿物, defaultNumRatioCommonVein).Modify(tab精炼, 302, TGB放射性矿物提炼);
                AddFracRecipe(IGB铀矿, defaultNumRatioCommonVein).Modify(tab精炼, 305, TGB放射性矿物提炼);
                AddFracRecipe(IGB钚矿, defaultNumRatioCommonVein).Modify(tab精炼, 306, TGB放射性矿物提炼);
                AddFracRecipe(I金伯利矿石, defaultNumRatioRealVein).Modify(tab分馏1, 504, T晶体冶炼);
                AddFracRecipe(I分形硅石, defaultNumRatioRealVein).Modify(tab分馏1, 505, T粒子可控);
                AddFracRecipe(I可燃冰, defaultNumRatioRealVein).Modify(tab分馏1, 506, T应用型超导体);
                AddFracRecipe(I刺笋结晶, defaultNumRatioRealVein).Modify(tab分馏1, 507, T高强度材料);
                AddFracRecipe(I有机晶体, defaultNumRatioRealVein).Modify(tab分馏1, 508, T高分子化工);
                AddFracRecipe(I光栅石, defaultNumRatioRealVein).Modify(tab分馏1, 510, T卡西米尔晶体);
                AddFracRecipe(I单极磁石, defaultNumRatioRealVein).Modify(tab分馏1, 511, T粒子磁力阱);
                //物品循环链
                AddFracChain([I配送运输机, I物流运输机, I星际物流运输船]);
                AddFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素]).Modify(tab防御, 317, false);
                matrixRecipeList = AddFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵]);
                matrixGridIndexList = matrixRecipeList.Select(r => r.GridIndex).ToList();
                foreach (RecipeProto r in matrixRecipeList) {
                    r.ModifyIconAndDesc();
                }

                //建筑页面
                AddFracChain([I电力感应塔, I无线输电塔, I卫星配电站]);
                list = AddFracChain([I风力涡轮机, I太阳能板, IGB同位素温差发电机, I蓄电器, I蓄电器满, I能量枢纽]);
                list[3].Modify(tab分馏2, 114);
                AddFracChain([I火力发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB人造恒星MKI, IGB人造恒星MKII]);
                AddFracChain([I传送带, I高速传送带, I极速传送带]);
                AddFracChain([I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机]);//注意科技解锁顺序
                AddFracChain([I小型储物仓, I大型储物仓, IGB量子储物仓]);
                AddFracChain([I储液罐, IGB量子储液罐]);
                if (!MoreMegaStructure.Enable) {
                    AddFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, I轨道采集器]);
                }
                else {
                    list = AddFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器]);
                    list[2].Modify(tab巨构, 410);
                }
                AddFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器]);
                AddFracChain([I采矿机, I大型采矿机]);
                AddFracChain([I抽水站, IGB聚束液体汲取设施]);
                AddFracChain([I原油萃取站, I原油精炼厂]);
                AddFracChain([I化工厂, I量子化工厂_GB先进化学反应釜]);
                AddFracChain([I电弧熔炉, IGB矿物处理厂, I位面熔炉, I负熵熔炉]);
                AddFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂]);
                AddFracChain([I矩阵研究站, I自演化研究站]);
                AddFracChain([I电磁轨道弹射器, I射线接收站, I垂直发射井]);
                AddFracChain([I微型粒子对撞机]);
                AddFracChain([IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器]);
                AddFracChain([IFE自然资源分馏塔, IFE升级分馏塔, IFE降级分馏塔, IFE垃圾回收分馏塔, IFE点数聚集分馏塔, IFE增产分馏塔]);

                //精炼页面
                fuelRodRecipeList = AddFracChain([
                    IGB空燃料棒,
                    I液氢燃料棒, IGB煤油燃料棒, IGB四氢双环戊二烯燃料棒,
                    IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
                    I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
                    I反物质燃料棒, I奇异湮灭燃料棒,
                ]).Modify(tab精炼, 601);
                fuelRodGridIndexList = fuelRodRecipeList.Select(r => r.GridIndex).ToList();
                foreach (RecipeProto r in fuelRodRecipeList) {
                    r.ModifyIconAndDesc();
                }

                //化工页面
                AddFracChain([I增产剂MkIII_GB增产剂]).Modify(tab化工, 506);

                //防御页面
                AddFracChain([I原型机, I精准无人机, I攻击无人机]).Modify(tab防御, 202);
                if (!TheyComeFromVoid.Enable) {
                    AddFracChain([I护卫舰, I驱逐舰]).Modify(tab防御, 205);
                }
                else {
                    list = AddFracChain([I护卫舰, I驱逐舰, IVD水滴]);
                    list[0].Modify(tab防御, 205);
                    list[1].Modify(tab巨构, 112);
                }
                AddFracChain([I高频激光塔_GB高频激光塔MKI, IGB高频激光塔MKII, I近程电浆塔, I磁化电浆炮]).Modify(tab防御, 207);
                AddFracChain([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]).Modify(tab防御, 211);
                AddFracChain([I高斯机枪塔]).Modify(tab防御, 302);
                AddFracChain([I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱]).Modify(tab防御, 309);
                AddFracChain([I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元]).Modify(tab防御, 409);
                AddFracChain([I聚爆加农炮_GB聚爆加农炮MKI, IGB聚爆加农炮MKII]).Modify(tab防御, 502, false);
                AddFracChain([I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组]).Modify(tab防御, 509);
                AddFracChain([I导弹防御塔]).Modify(tab防御, 602);
                AddFracChain([I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组]).Modify(tab防御, 609);
                AddFracChain([I干扰胶囊, I压制胶囊]).Modify(tab防御, 709);
                AddFracChain([I等离子胶囊, I反物质胶囊]).Modify(tab防御, 711);
            }

            //添加所有翻译
            LoadLanguagePostfixAfterCommonApi();
            //立即添加所有配方
            AddRecipe(recipeList);
            //Preload所有分馏配方
            for (int i = 0; i < LDB.recipes.dataArray.Length; ++i) {
                RecipeProto r = LDB.recipes.dataArray[i];
                if (r.Type == ERecipeType.Fractionate) {
                    LDB.recipes.dataArray[i].Preload(i);
                }
            }

            LogInfo("Finish to add fractionate recipes.");
        }

        /// <summary>
        /// 添加某种自然资源的自分馏配方。
        /// </summary>
        private static RecipeProto AddFracRecipe(int itemID, Dictionary<int, float> fracNumRatioDic) {
            return AddFracRecipe(itemID, itemID, fracNumRatioDic, null);
        }

        /// <summary>
        /// 添加一些物品构成的升降级分馏链对应的配方。
        /// </summary>
        private static List<RecipeProto> AddFracChain(IReadOnlyList<int> itemChain,
            Dictionary<int, float> numRatioUpgrade = null, Dictionary<int, float> numRatioDowngrade = null) {
            numRatioUpgrade ??= defaultNumRatioUpgrade;
            numRatioDowngrade ??= defaultNumRatioDowngrade;
            if (itemChain.Count == 1) {
                List<RecipeProto> list = [
                    AddFracRecipe(itemChain[0], itemChain[0], numRatioUpgrade, numRatioDowngrade),
                ];
                return list;
            }
            else {
                List<RecipeProto> list = [];
                for (int i = 0; i < itemChain.Count - 1; i++) {
                    list.Add(AddFracRecipe(itemChain[i], itemChain[i + 1], numRatioUpgrade, numRatioDowngrade));
                }
                return list;
            }
        }

        /// <summary>
        /// 添加一个分馏配方。
        /// </summary>
        private static RecipeProto AddFracRecipe(int inputItemID, int outputItemID,
            Dictionary<int, float> ratioUpgrade, Dictionary<int, float> ratioDowngrade) {
            try {
                int recipeID = helper.GetUnusedRecipeID();
                ItemProto inputItem = LDB.items.Select(inputItemID);
                ItemProto outputItem = LDB.items.Select(outputItemID);
                //名称与翻译、描述与翻译
                string Name = $"{inputItem.Name}-{outputItem.Name}分馏";
                string DescriptionNoDestroy = $"R{inputItem.Name}-{outputItem.Name}分馏";
                string DescriptionDestroy = $"R{inputItem.Name}-{outputItem.Name}损毁分馏";
                if (ratioDowngrade == null) {
                    RegisterOrEditAsync(Name, $"{outputItem.name} Fractionation", $"{outputItem.name}分馏");
                    string description_en = $"Fractionate {inputItem.name} into multiple.";
                    string description_cn = $"将{inputItem.name}分馏为多个。";
                    foreach (KeyValuePair<int, float> p in ratioUpgrade.Where(p => p.Key > 0)) {
                        description_en +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)} fractionate {p.Key.ToString().AddOrangeLabel(true)} product{(p.Key > 1 ? "s" : "")}";
                        description_cn +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)}分馏出{p.Key.ToString().AddOrangeLabel(true)}个产物";
                    }
                    RegisterOrEditAsync(DescriptionNoDestroy, description_en, description_cn);
                    RegisterOrEditAsync(DescriptionDestroy, description_en, description_cn);
                }
                else {
                    RegisterOrEditAsync(Name, $"{inputItem.name}-{outputItem.name} Fractionation",
                        $"{inputItem.name}-{outputItem.name}分馏");
                    string noDestroy_en = $"Fractionate {inputItem.name} upgrade to {outputItem.name}.";
                    string noDestroy_cn = $"将{inputItem.name}升级分馏为{outputItem.name}。";
                    foreach (KeyValuePair<int, float> p in ratioUpgrade.Where(p => p.Key > 0)) {
                        noDestroy_en +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)} fractionate {p.Key.ToString().AddOrangeLabel(true)} product{(p.Key > 1 ? "s" : "")}";
                        noDestroy_cn +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)}分馏出{p.Key.ToString().AddOrangeLabel(true)}个产物";
                    }
                    string destroy_en = noDestroy_en;
                    string destroy_cn = noDestroy_cn;
                    if (ratioUpgrade.TryGetValue(-1, out float destroyRatio)) {
                        destroy_en +=
                            $"\n{"WARNING:".AddOrangeLabel()} There is a probability of {destroyRatio.ToString("0.###%").AddRedLabel(true)} that an input item will be destroyed at each fractionation!";
                        destroy_cn +=
                            $"\n{"警告：".AddOrangeLabel()}每次分馏时，有{destroyRatio.ToString("0.###%").AddRedLabel(true)}概率导致原料损毁！";
                    }
                    noDestroy_en += $"\n————————————————\nFractionate {outputItem.name} downgrade to {inputItem.name}.";
                    noDestroy_cn += $"\n————————————————\n将{outputItem.name}降级分馏为{inputItem.name}。";
                    destroy_en += $"\n————————————————\nFractionate {outputItem.name} downgrade to {inputItem.name}.";
                    destroy_cn += $"\n————————————————\n将{outputItem.name}降级分馏为{inputItem.name}。";
                    foreach (KeyValuePair<int, float> p in ratioDowngrade.Where(p => p.Key > 0)) {
                        noDestroy_en +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)} fractionate {p.Key.ToString().AddOrangeLabel(true)} product{(p.Key > 1 ? "s" : "")}";
                        noDestroy_cn +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)}分馏出{p.Key.ToString().AddOrangeLabel(true)}个产物";
                        destroy_en +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)} fractionate {p.Key.ToString().AddOrangeLabel(true)} product{(p.Key > 1 ? "s" : "")}";
                        destroy_cn +=
                            $"\n{p.Value.ToString("0.###%").AddOrangeLabel(true)}分馏出{p.Key.ToString().AddOrangeLabel(true)}个产物";
                    }
                    RegisterOrEditAsync(DescriptionNoDestroy, noDestroy_en, noDestroy_cn);
                    RegisterOrEditAsync(DescriptionDestroy, destroy_en, destroy_cn);
                }
                //根据产物对应的配方位置，确定分馏配方的位置
                //方法外可再次调整部分配方的显示位置，例如无配方可生成、显示位置重合、分馏循环链影响、超出分馏页等情况
                int gridIndex = outputItem.recipes.Count == 0
                    ? 0
                    : outputItem.recipes[0].GridIndex + (tab分馏1 - 1) * 1000;
                //前置科技如果为null，【必须】修改为戴森球计划，才能确保某些配方能正常解锁、显示
                TechProto preTech = outputItem.preTech;
                preTech ??= LDB.techs.Select(T戴森球计划);
                //对于原版分馏配方而言，ItemCounts[0]和ResultCounts[0]只影响分馏成功率和配方显示，不会分出多个产物
                //万物分馏为了解决这个问题，采用如下方案：
                //1.抛掉ItemCounts[0]和ResultCounts[0]，它们不再造成任何影响
                //2.重写分馏塔的运行逻辑，通过fracRecipeNumRatioDic获取指定物品的概率集合，然后处理
                //3.如果UIItemTip最下面的某一个UIRecipeEntry（就是物品、配方详情弹窗最下面的制作方式）是分馏配方，修改输入、输出、概率。输入恒定为1，输出为配方产物数，概率显示无增产情况下的概率以及损毁概率。
                //值得一提的是，在不考虑损毁的情况下，目前万物分馏所有的显式分馏配方均为单概率配方，这使得第3点更容易计算。
                RecipeProto r = new() {
                    Type = ERecipeType.Fractionate,
                    ID = recipeID,
                    SID = "",
                    Handcraft = false,
                    Explicit = true,
                    TimeSpend = 60,
                    Items = [inputItemID],
                    ItemCounts = [100],
                    Results = [outputItemID],
                    ResultCounts = [1],
                    Name = Name,
                    GridIndex = gridIndex,
                    preTech = preTech,
                };
                //添加配方的图标路径和描述
                r.ModifyIconAndDesc();
                //如果gridIndex已被使用，则使用分馏页面末端的位置
                helper.ModifyGridIndex(r, gridIndex);
                //添加到list里面，因为有可能需要再次修改gridIndex。所有配方处理完成后才会添加他们
                recipeList.Add(r);
                //为对应产物添加这个公式的显示
                outputItem.recipes.Add(r);
                //存储分馏配方概率信息，为逻辑处理提供数据支持，这样才能实现分馏出多个的逻辑
                if (ratioDowngrade == null) {
                    itemNaturalResourceList.Add(inputItemID);
                    numRatioNaturalResourceDic.Add(inputItemID, ratioUpgrade);
                }
                else {
                    itemUpgradeDic.Add(inputItemID, outputItemID);
                    numRatioUpgradeDic.Add(inputItemID, ratioUpgrade);
                    itemDowngradeDic.Add(outputItemID, inputItemID);
                    numRatioDowngradeDic.Add(outputItemID, ratioDowngrade);
                }
                return r;
            }
            catch (Exception ex) {
                LogError(ex.ToString());
                return null;
            }
        }

        private static List<RecipeProto> Modify(this List<RecipeProto> recipes, int tab, int rowColumn,
            bool addColumn = true) {
            //先获取原有物品的位置，根据原有物品位置进行排序
            List<ItemProto> items = recipes.Select(r => LDB.items.Select(r.Results[0])).ToList();
            items.Sort((i1, i2) => i1.GridIndex.CompareTo(i2.GridIndex));
            //根据原有物品位置进行排序
            foreach (RecipeProto r in recipes) {
                //找到这个recipes对应的产物在items里面的索引
                int index = items.IndexOf(LDB.items.Select(r.Results[0]));
                helper.ModifyGridIndex(r, tab, rowColumn + (addColumn ? index : index * 100));
            }
            return recipes;
        }

        private static RecipeProto Modify(this RecipeProto r, int tab, int rowColumn, int techID = 0) {
            helper.ModifyGridIndex(r, tab, rowColumn);
            TechProto t;
            if ((t = LDB.techs.Select(techID)) != null) {
                r.preTech = t;
            }
            return r;
        }

        public static RecipeProto ModifyIconAndDesc(this RecipeProto r) {
            try {
                if (r.Type != ERecipeType.Fractionate) {
                    return r;
                }
                ItemProto inputItem = LDB.items.Select(r.Items[0]);
                string inputIconName = null;
                if (inputItem.iconSprite != null) {
                    inputIconName = inputItem.iconSprite.name;
                }
                else if (inputItem.IconPath != null && inputItem.IconPath.Contains("/")) {
                    inputIconName = inputItem.IconPath.Substring(inputItem.IconPath.LastIndexOf("/") + 1);
                }
                ItemProto outputItem = LDB.items.Select(r.Results[0]);
                string outputIconName = null;
                if (outputItem.iconSprite != null) {
                    outputIconName = outputItem.iconSprite.name;
                }
                else if (outputItem.IconPath != null && outputItem.IconPath.Contains("/")) {
                    outputIconName = outputItem.IconPath.Substring(outputItem.IconPath.LastIndexOf("/") + 1);
                }
                r.Description = $"R{inputItem.Name}-{outputItem.Name}{(enableDestroy ? "损毁分馏" : "分馏")}";
                if (inputIconName != null && outputIconName != null) {
                    //由于不同原料可能分馏出同一种产物，配方名字应以原料名称命名
                    //考虑到重氢可能分离为其他物品（虽然现在没有），为了不冲突，名称改为“原料-产物-formula-版本”
                    string iconPath = $"Assets/fracicons/{inputIconName}-{outputIconName}-formula-v{iconVersion}";
                    r.IconPath = Resources.Load<Sprite>(iconPath) != null ? iconPath : outputItem.IconPath;
#if DEBUG
                    //输出分馏配方需要的图标的路径，以便于制作图标
                    if (Directory.Exists(SPRITE_CSV_PATH.Substring(0, SPRITE_CSV_PATH.LastIndexOf('\\')))) {
                        using StreamWriter sw = new(SPRITE_CSV_PATH, true, Encoding.UTF8);
                        sw.WriteLine(inputIconName + "," + outputIconName);
                    }
#endif
                }
                else {
                    //如果其他mod图标资源包未加载成功，将会造成图标为null的情况
                    r.IconPath = "";
                }
                if (fuelRodRecipeList.Contains(r)) {
                    if (enableFuelRodFrac) {
                        r.GridIndex = fuelRodGridIndexList[fuelRodRecipeList.IndexOf(r)];
                        if (!itemUpgradeDic.ContainsKey(r.Items[0])) {
                            itemUpgradeDic.Add(r.Items[0], r.Results[0]);
                        }
                        if (!numRatioUpgradeDic.ContainsKey(r.Items[0])) {
                            numRatioUpgradeDic.Add(r.Items[0], defaultNumRatioUpgrade);
                        }
                        if (!itemDowngradeDic.ContainsKey(r.Results[0])) {
                            itemDowngradeDic.Add(r.Results[0], r.Items[0]);
                        }
                        if (!numRatioDowngradeDic.ContainsKey(r.Results[0])) {
                            numRatioDowngradeDic.Add(r.Results[0], defaultNumRatioDowngrade);
                        }
                    }
                    else {
                        r.GridIndex = 0;
                        if (itemUpgradeDic.ContainsKey(r.Items[0])) {
                            itemUpgradeDic.Remove(r.Items[0]);
                        }
                        if (numRatioUpgradeDic.ContainsKey(r.Items[0])) {
                            numRatioUpgradeDic.Remove(r.Items[0]);
                        }
                        if (itemDowngradeDic.ContainsKey(r.Results[0])) {
                            itemDowngradeDic.Remove(r.Results[0]);
                        }
                        if (numRatioDowngradeDic.ContainsKey(r.Results[0])) {
                            numRatioDowngradeDic.Remove(r.Results[0]);
                        }
                    }
                }
                if (matrixRecipeList.Contains(r)) {
                    if (enableMatrixFrac) {
                        r.GridIndex = matrixGridIndexList[matrixRecipeList.IndexOf(r)];
                        if (!itemUpgradeDic.ContainsKey(r.Items[0])) {
                            itemUpgradeDic.Add(r.Items[0], r.Results[0]);
                        }
                        if (!numRatioUpgradeDic.ContainsKey(r.Items[0])) {
                            numRatioUpgradeDic.Add(r.Items[0], defaultNumRatioUpgrade);
                        }
                        if (!itemDowngradeDic.ContainsKey(r.Results[0])) {
                            itemDowngradeDic.Add(r.Results[0], r.Items[0]);
                        }
                        if (!numRatioDowngradeDic.ContainsKey(r.Results[0])) {
                            numRatioDowngradeDic.Add(r.Results[0], defaultNumRatioDowngrade);
                        }
                    }
                    else {
                        r.GridIndex = 0;
                        if (itemUpgradeDic.ContainsKey(r.Items[0])) {
                            itemUpgradeDic.Remove(r.Items[0]);
                        }
                        if (numRatioUpgradeDic.ContainsKey(r.Items[0])) {
                            numRatioUpgradeDic.Remove(r.Items[0]);
                        }
                        if (itemDowngradeDic.ContainsKey(r.Results[0])) {
                            itemDowngradeDic.Remove(r.Results[0]);
                        }
                        if (numRatioDowngradeDic.ContainsKey(r.Results[0])) {
                            numRatioDowngradeDic.Remove(r.Results[0]);
                        }
                    }
                }
                return r;
            }
            catch (Exception ex) {
                LogError($"ModifyIconAndDesc {r.Name} error: " + ex);
                return r;
            }
        }
    }
}
