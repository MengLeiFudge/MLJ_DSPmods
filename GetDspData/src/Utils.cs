using System.Collections.Generic;
using static GetDspData.ProtoID;
using static GetDspData.GetDspData;

namespace GetDspData;

public enum FactoryType {
    //异常类型
    None = -1,
    Custom = -2,
    Error = -3,
    //采集
    轻型工业机甲 = 0,
    采矿设备,
    抽水设备,
    抽油设备,
    巨星采集,
    //制作
    冶炼设备,
    制造台,
    化工设备,
    科研设备,
    精炼设备,
    粒子对撞机,
    射线接收站,
    分馏设备,
    充电设备,
    //战斗掉落
    黑雾残骸,
    //mod
    行星大气,
    矿物处理,
    标准制造,
    精密组装,
    高精度加工,
    高分子化工,
    垃圾回收,
}

public enum Utils_ERecipeType {
    None = 0,
    Smelt = 1,
    Chemical = 2,
    Refine = 3,
    Assemble = 4,
    Particle = 5,
    Exchange = 6,
    PhotonStore = 7,
    Fractionate = 8,
    标准制造 = 9,
    高精度加工 = 10,
    矿物处理 = 11,
    所有制造 = 12,// 4 + 9 + 10
    垃圾回收 = 14,
    Research = 15,
    高分子化工 = 16,
    所有化工 = 17,// 2 + 3 + 16
    复合制造 = 18,// 4 + 9
    所有熔炉 = 19,// 1 + 11
    Custom = 20,
    巨构星际组装厂 = 21,
}

public static class Utils {
    //可通过矿脉开采
    static List<int> miningFromVein = GenesisBookEnable
        ? [
            I铁矿, I铜矿, IGB铝矿, I硅石, I钛石, IGB钨矿, I煤矿, I石矿, IGB硫矿, IGB放射性矿物,
            I可燃冰, I金伯利矿石, I分形硅石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
        ]
        : [
            I铁矿, I铜矿, I硅石, I钛石, I石矿, I煤矿,
            I可燃冰, I金伯利矿石, I分形硅石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
        ];
    public static bool canMiningFromVein(this ItemProto item) => miningFromVein.Contains(item.ID);

    // //可通过石头获取
    // static List<int> miningFromStone = [
    //     I石矿, I硅石, I钛石,
    // ];
    // static bool canMiningFromStone(this ItemProto item) => miningFromStone.Contains(item.ID);
    //
    // //可通过植物获取
    // static List<int> miningFromPlant = [
    //     I植物燃料,
    // ];
    // static bool canMiningFromPlant(this ItemProto item) => miningFromPlant.Contains(item.ID);
    //
    // //可通过树木获取
    // static List<int> miningFromTree = [
    //     I木材,
    // ];
    // static bool canMiningFromTree(this ItemProto item) => miningFromTree.Contains(item.ID);

    //由伊卡洛斯采集
    static List<int> miningByIcarus = [
        I植物燃料, I木材,
    ];
    public static bool canMiningByIcarus(this ItemProto item) => miningByIcarus.Contains(item.ID);

    //可通过气态巨行星获取
    static List<int> miningFromGasGiant = GenesisBookEnable
        ? [
            I氢, I重氢, IGB氦, I可燃冰, IGB氨,//气巨[氢，重氢，氦] 冰巨[氢，可燃冰，氨]
        ]
        : [
            I氢, I重氢, I可燃冰,//气巨[氢，重氢] 冰巨[重氢，可燃冰]
        ];
    public static bool canMiningFromGasGiant(this ItemProto item) => miningFromGasGiant.Contains(item.ID);

    //可通过海洋获取
    static List<int> miningFromSea = GenesisBookEnable
        ? [
            IGB海水, I水, IGB盐酸, I硫酸, IGB硝酸, IGB氨,
        ]
        : [
            I水, I硫酸,
        ];
    public static bool canMiningFromSea(this ItemProto item) => miningFromSea.Contains(item.ID);

    //可通过油井获取
    static List<int> miningFromOilWell = [
        I原油,
    ];
    public static bool canMiningFromOilWell(this ItemProto item) => miningFromOilWell.Contains(item.ID);

    //可通过射线接收器获取
    static List<int> miningByRayReceiver = MoreMegaStructureEnable
        ? [
            I临界光子, I铁块, I铜块, I高纯硅块, I钛块, I单极磁石, I高能石墨, I卡西米尔晶体, I光栅石,
        ]
        : [
            I临界光子,
        ];
    public static bool canMiningByRayReceiver(this ItemProto item) => miningByRayReceiver.Contains(item.ID);

    //可通过星际组装厂获取
    static List<int> miningByMS = [
        IMS多功能集成组件,
    ];
    public static bool canMiningByMS(this ItemProto item) => miningByMS.Contains(item.ID);

    //可通过黑雾获取
    public static bool canDropFromEnemy(this ItemProto item) => item.EnemyDropCount > 0;

    //可通过大气获取（创世独有）
    static List<int> miningFromAtmosphere = [
        IGB氮, IGB氧, IGB二氧化碳, IGB二氧化硫,
    ];
    public static bool canMiningFromAtmosphere(this ItemProto item) => miningFromAtmosphere.Contains(item.ID);

    //可以凭空获取
    public static bool canMining(this ItemProto item) =>
        item.canMiningFromVein()
        || item.canMiningByIcarus()
        || item.canMiningFromGasGiant()
        || item.canMiningFromSea()
        || item.canMiningFromOilWell()
        || item.canMiningByRayReceiver()
        || item.canMiningByMS()
        || item.canDropFromEnemy()
        || item.canMiningFromAtmosphere();

    //根据配方类型获取对应的工厂类型
    public static int[] getAcceptFactories(this RecipeProto recipe) {
        return (int)recipe.Type switch {
            (int)Utils_ERecipeType.Smelt => GenesisBookEnable
                ? [I电弧熔炉, I位面熔炉, I负熵熔炉, IGB物质裂解塔]
                : [I电弧熔炉, I位面熔炉, I负熵熔炉],
            (int)Utils_ERecipeType.Chemical => GenesisBookEnable
                ? [I化工厂, IGB埃克森美孚化工厂]
                : [I化工厂, I量子化工厂_GB先进化学反应釜],
            (int)Utils_ERecipeType.Refine => GenesisBookEnable
                ? [I原油精炼厂, IGB埃克森美孚化工厂]
                : [I原油精炼厂],
            (int)Utils_ERecipeType.Assemble => GenesisBookEnable
                ? [I制造台MkI_GB基础制造台, I重组式制造台_GB物质重组工厂, IGB天穹装配厂]
                : [I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂],
            (int)Utils_ERecipeType.Particle => GenesisBookEnable
                ? [I微型粒子对撞机, IGB苍穹粒子加速器]
                : [I微型粒子对撞机],
            (int)Utils_ERecipeType.PhotonStore => [I射线接收站_MS射线重构站],
            (int)Utils_ERecipeType.Fractionate => [I分馏塔],//万物分馏配方在代码中处理，不在此处
            (int)Utils_ERecipeType.标准制造 => [I制造台MkII_GB标准制造单元, I重组式制造台_GB物质重组工厂, IGB天穹装配厂],
            (int)Utils_ERecipeType.高精度加工 => [I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂, IGB工业先锋精密加工中心],
            (int)Utils_ERecipeType.矿物处理 => [IGB矿物处理厂, I负熵熔炉, IGB物质裂解塔],
            // (int)Utils_ERecipeType.所有制造 =>
            //     [I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂, IGB天穹装配厂, IGB工业先锋精密加工中心],
            (int)Utils_ERecipeType.垃圾回收 => [IGB物质分解设施],
            (int)Utils_ERecipeType.Research => [I矩阵研究站, I自演化研究站],
            (int)Utils_ERecipeType.高分子化工 => [I量子化工厂_GB先进化学反应釜, IGB埃克森美孚化工厂],
            _ => throw new($"配方类型异常，配方名称{recipe.name}，配方类型{recipe.Type}")
        };
    }

    public static float GetSpace(this ItemProto item) {
        return item.ID switch {
            I电弧熔炉 or I位面熔炉 or I负熵熔炉 or IGB矿物处理厂 => 5.76f,
            I制造台MkI_GB基础制造台 or I制造台MkII_GB标准制造单元 or I制造台MkIII_GB高精度装配线 or I重组式制造台_GB物质重组工厂 => 10.24f,
            I化工厂 or I量子化工厂_GB先进化学反应釜 => 23.76f,
            I矩阵研究站 or I自演化研究站 => 20.25f,
            I采矿机 => 15f,
            I大型采矿机 => 25f,
            I抽水站 => 12f,
            I原油萃取站 or IGB天穹装配厂 or IGB物质裂解塔 or IGB埃克森美孚化工厂 or IGB工业先锋精密加工中心
                or IGB物质分解设施 or IGB苍穹粒子加速器 or IGB大气采集站 => 50f,
            I原油精炼厂 => 18f,
            I射线接收站_MS射线重构站 => 54.82f,
            I分馏塔 or IGB聚束液体汲取设施 or IFE自然资源分馏塔 or IFE升级分馏塔 or IFE降级分馏塔
                or IFE垃圾回收分馏塔 or IFE点数聚集分馏塔 or IFE增产分馏塔 => 12.96f,
            I微型粒子对撞机 => 45.12f,
            I能量枢纽 => 64f,
            I蓄电器 or I蓄电器满 or IGB同位素温差发电机 => 4f,
            I轨道采集器 or I伊卡洛斯 or I行星基地 or I巨构星际组装厂 => 0f,
            _ => -1f,
        };
    }
}
