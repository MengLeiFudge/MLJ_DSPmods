using System.Collections.Generic;
using static GetDspData.GetDspData;

namespace GetDspData.Utils;

public static partial class Utils {
    public static void InitMiningData() {
        if (GenesisBookEnable) {
            miningFromVein = [
                I铁矿, I铜矿, I硅石, I钛石, I煤矿, I石矿,
                I可燃冰, I金伯利矿石, I分形硅石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
                IGB铝矿, IGB钨矿, IGB硫矿, IGB放射性矿物,
            ];
            //气巨[氢，重氢，氦] 冰巨[氢，可燃冰，氨]
            miningFromGasGiant = [I氢, I重氢, IGB氦, I可燃冰, IGB氨];
            miningFromSea = [IGB海水, I水, IGB盐酸, I硫酸, IGB硝酸, IGB氨];
            miningFromOilWell = [I原油];
        } else if (OrbitalRingEnable) {
            miningFromVein = [
                I铁矿, I铜矿, I硅石, I钛石, I石矿, I煤矿,
                I可燃冰, I金伯利矿石, IOR莫桑石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
                IOR黄铁矿, IOR铀矿, IOR石墨矿,
            ];
            //气巨[氢，重氢] 冰巨[氢，甲烷]
            miningFromGasGiant = [I氢, I重氢, IOR甲烷];
            miningFromSea = [I水, I硫酸, I原油, IOR甲烷];
            miningFromOilWell = [I原油, I水, IOR深层熔岩];
        } else {
            miningFromVein = [
                I铁矿, I铜矿, I硅石, I钛石, I石矿, I煤矿,
                I可燃冰, I金伯利矿石, I分形硅石, I光栅石, I刺笋结晶, I单极磁石, I有机晶体,
            ];
            //气巨[氢，重氢] 冰巨[重氢，可燃冰]
            miningFromGasGiant = [I氢, I重氢, I可燃冰];
            miningFromSea = [I水, I硫酸];
            miningFromOilWell = [I原油];
        }
        if (MoreMegaStructureEnable) {
            miningByRayReceiver = [I临界光子, I铁块, I铜块, I高纯硅块, I钛块, I单极磁石, I高能石墨, I卡西米尔晶体, I光栅石];
        } else {
            miningByRayReceiver = [I临界光子];
        }
    }

    //矿脉开采
    private static List<int> miningFromVein;
    public static bool canMiningFromVein(this ItemProto item) => miningFromVein.Contains(item.ID);

    //伊卡洛斯采集
    private static List<int> miningByIcarus = [I植物燃料, I木材];
    public static bool canMiningByIcarus(this ItemProto item) => miningByIcarus.Contains(item.ID);

    //气态巨行星采集
    private static List<int> miningFromGasGiant;
    public static bool canMiningFromGasGiant(this ItemProto item) => miningFromGasGiant.Contains(item.ID);

    //海洋抽取
    private static List<int> miningFromSea;
    public static bool canMiningFromSea(this ItemProto item) => miningFromSea.Contains(item.ID);

    //油井抽取
    private static List<int> miningFromOilWell;
    public static bool canMiningFromOilWell(this ItemProto item) => miningFromOilWell.Contains(item.ID);

    //射线接收器接收
    private static List<int> miningByRayReceiver;
    public static bool canMiningByRayReceiver(this ItemProto item) => miningByRayReceiver.Contains(item.ID);

    //星际组装厂传递
    static List<int> miningByMS = [IMS多功能集成组件];
    public static bool canMiningByMS(this ItemProto item) => miningByMS.Contains(item.ID);

    //黑雾掉落
    public static bool canDropFromEnemy(this ItemProto item) => item.EnemyDropCount > 0;

    //大气采集
    static List<int> miningFromAtmosphere = [IGB氮, IGB氧, IGB二氧化碳, IGB二氧化硫];
    public static bool canMiningFromAtmosphere(this ItemProto item) => miningFromAtmosphere.Contains(item.ID);

    //根据配方类型获取对应的工厂类型
    public static int[] getAcceptFactories(this RecipeProto recipe) {
        if (GenesisBookEnable) {
            return (int)recipe.Type switch {
                (int)Utils_ERecipeType.Smelt => [I电弧熔炉, I位面熔炉, I负熵熔炉, IGB物质裂解塔],
                (int)Utils_ERecipeType.Chemical => [I化工厂, IGB埃克森美孚化工厂],
                (int)Utils_ERecipeType.Refine => [I原油精炼厂, IGB埃克森美孚化工厂],
                (int)Utils_ERecipeType.Assemble => [IGB基础制造台, IGB物质重组工厂, IGB天穹装配厂],
                (int)Utils_ERecipeType.Particle => [I微型粒子对撞机, IGB苍穹粒子加速器],
                (int)Utils_ERecipeType.PhotonStore => [I射线接收站],
                (int)Utils_ERecipeType.Fractionate => [I分馏塔],
                (int)Utils_ERecipeType.GB标准制造 => [IGB标准制造单元, IGB物质重组工厂, IGB天穹装配厂],
                (int)Utils_ERecipeType.GB高精度加工 => [IGB高精度装配线, IGB物质重组工厂, IGB工业先锋精密加工中心],
                (int)Utils_ERecipeType.GB矿物处理 => [IGB矿物处理厂, I负熵熔炉, IGB物质裂解塔],
                (int)Utils_ERecipeType.GB垃圾回收 => [IGB物质分解设施],
                (int)Utils_ERecipeType.Research => [I矩阵研究站, I自演化研究站],
                (int)Utils_ERecipeType.GB高分子化工 => [IGB先进化学反应釜, IGB埃克森美孚化工厂],
                _ => throw new($"配方类型异常，配方名称{recipe.name}，配方类型{recipe.Type}")
            };
        } else if (OrbitalRingEnable) {
            return (int)recipe.Type switch {
                (int)Utils_ERecipeType.Smelt => [I电弧熔炉, IOR等离子熔炉, I负熵熔炉, IOR轨道熔炼站],
                (int)Utils_ERecipeType.Chemical => [I化工厂, I量子化工厂],
                (int)Utils_ERecipeType.Refine => [I原油精炼厂],
                (int)Utils_ERecipeType.Assemble => [IOR基础制造台, IOR高速装配线, IOR粒子打印车间, IOR物质重组工厂],
                (int)Utils_ERecipeType.Particle => [I微型粒子对撞机, IOR星环对撞机总控站],
                (int)Utils_ERecipeType.PhotonStore => [I射线接收站],
                (int)Utils_ERecipeType.Fractionate => [I分馏塔],
                (int)Utils_ERecipeType.OR太空船坞 => [IOR太空船坞],
                (int)Utils_ERecipeType.OR粒子打印 => [IOR粒子打印车间, IOR物质重组工厂],
                (int)Utils_ERecipeType.OR等离子熔炼 => [IOR等离子熔炉, I负熵熔炉, IOR轨道熔炼站],
                (int)Utils_ERecipeType.OR物质重组 => [IOR物质重组工厂],
                (int)Utils_ERecipeType.OR生物化工 => [IOR生态穹顶],
                (int)Utils_ERecipeType.Research => [I矩阵研究站, I自演化研究站],
                _ => throw new($"配方类型异常，配方名称{recipe.name}，配方类型{recipe.Type}")
            };
        } else {
            return (int)recipe.Type switch {
                (int)Utils_ERecipeType.Smelt => [I电弧熔炉, I位面熔炉, I负熵熔炉],
                (int)Utils_ERecipeType.Chemical => [I化工厂, I量子化工厂],
                (int)Utils_ERecipeType.Refine => [I原油精炼厂],
                (int)Utils_ERecipeType.Assemble => [I制造台MkI, I制造台MkII, I制造台MkIII, I重组式制造台],
                (int)Utils_ERecipeType.Particle => [I微型粒子对撞机],
                (int)Utils_ERecipeType.PhotonStore => [I射线接收站],
                (int)Utils_ERecipeType.Fractionate => [I分馏塔],
                (int)Utils_ERecipeType.Research => [I矩阵研究站, I自演化研究站],
                _ => throw new($"配方类型异常，配方名称{recipe.name}，配方类型{recipe.Type}")
            };
        }
    }

    public static float GetSpace(this ItemProto item) {
        return item.ID switch {
            I电弧熔炉 or I位面熔炉 or IOR等离子熔炉 or I负熵熔炉 or IGB矿物处理厂 => 5.76f,
            I制造台MkI or IGB基础制造台 or IOR基础制造台
                or I制造台MkII or IGB标准制造单元 or IOR高速装配线
                or I制造台MkIII or IGB高精度装配线
                or I重组式制造台 or IGB物质重组工厂 or IOR物质重组工厂 => 10.24f,
            I化工厂 or I量子化工厂 or IGB先进化学反应釜 => 23.76f,
            I矩阵研究站 or I自演化研究站 => 20.25f,
            I采矿机 => 15f,
            I大型采矿机 => 25f,
            I抽水站 => 12f,
            I原油萃取站 or IGB天穹装配厂 or IGB物质裂解塔 or IGB埃克森美孚化工厂 or IGB工业先锋精密加工中心
                or IGB物质分解设施 or IGB苍穹粒子加速器 or IGB大气采集站 => 50f,
            I原油精炼厂 => 18f,
            I射线接收站 or IMS射线重构站 => 54.82f,
            I分馏塔 or IGB聚束液体汲取设施 or IFE交互塔 or IFE矿物复制塔 or IFE点数聚集塔 or IFE量子复制塔
                or IFE点金塔 or IFE分解塔 or IFE转化塔 => 12.96f,
            I微型粒子对撞机 => 45.12f,
            I能量枢纽 => 64f,
            I蓄电器 or I蓄电器满 or IGB同位素温差发电机 => 4f,
            I轨道采集器 or I伊卡洛斯 or I行星基地 or I巨构星际组装厂 => 0f,
            IOR星环对撞机总控站 or IOR太空船坞 or IOR轨道熔炼站 or IOR生态穹顶 => 25f,
            _ => -1f,
        };
    }
}
