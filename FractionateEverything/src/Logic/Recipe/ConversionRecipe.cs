using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 转化塔配方类（1A -> XA + YB + ZC）
/// </summary>
public class ConversionRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有转化配方
    /// </summary>
    public static void CreateAll() {
        //添加特有转化配方
        //物品页面
        // if (GenesisBook.Enable) {
        //     CreateChain([[I铁块], [I钢材], [I钛合金], [IGB钨合金], [IGB三元精金]]);
        // } else if (OrbitalRing.Enable) {
        //     CreateChain([[I铁块], [I钢材]]);
        // } else {
        //     CreateChain(I铁块, [], [I铁块], [I钢材]);
        //     CreateChain(I钢材, [I铁块], [I钢材], [I钛合金]);
        //     CreateChain(I钛合金, [I钛块, I钢材], [I钛合金], []);
        // }
        // CreateChain([[I框架材料], [I戴森球组件], [I小型运载火箭]]);
        // CreateChain([[I高纯硅块], [I晶格硅]]);
        // CreateChain([[I棱镜], [I电浆激发器], [I光子合并器], [I太阳帆]]);
        // CreateChain([[I高能石墨], [I金刚石, I石墨烯], [I碳纳米管], [I粒子宽带], [IGB光学信息传输纤维]]);
        // CreateChain([[I粒子容器], [I奇异物质], [I引力透镜], [I空间翘曲器]]);
        // CreateChain([[I钛晶石], [I卡西米尔晶体]]);
        // if (GenesisBook.Enable) {
        //     CreateChain([[IGB基础机械组件], [IGB先进机械组件], [IGB尖端机械组件], [IGB超级机械组件]]);
        //     CreateChain([[IGB塑料基板], [IGB光学基板]]);
        //     CreateChain([[IGB量子计算主机], [IGB超越X1型光学主机]]);
        // }
        // CreateChain([[I玻璃], [I钛化玻璃], [IGB钨强化玻璃]]);
        // CreateChain([[I氢], [I重氢]]);
        // CreateChain([[IGB氦], [IGB氦三]]);
        // CreateChain([[I磁线圈], [I电动机], [I电磁涡轮], [I超级磁场环]]);
        // CreateChain([[I电路板, I微晶元件], [I处理器, I位面过滤器], [I量子芯片], [IGB光学处理器]]);
        // CreateChain([[I临界光子], [I反物质]]);
        // CreateChain([[I动力引擎], [I推进器], [I加力推进器]]);
        CreateChain([[I配送运输机], [I物流运输机], [I星际物流运输船]]);
        CreateChain([[I能量碎片], [I黑雾矩阵], [I物质重组器], [I硅基神经元], [I负熵奇点], [I核心素]]);
        CreateChain([[I电磁矩阵], [I能量矩阵], [I结构矩阵], [I信息矩阵], [I引力矩阵]]);

        //建筑页面
        CreateChain([[I电力感应塔], [I无线输电塔], [I卫星配电站]]);
        if (!SmelterMiner.Enable && CustomCreateBirthStar.Enable) {
            CreateChain([
                [I风力涡轮机, I太阳能板, IGB同位素温差发电机],
                [I蓄电器, ICCBS能量核心],
                [I能量枢纽, ICCBS星际能量枢纽],
                [ICCBS星际能量枢纽MK2]
            ]);
        } else {
            CreateChain([
                [I风力涡轮机, I太阳能板, IGB同位素温差发电机],
                [I蓄电器, ICCBS_xxldm_能量核心],
                [I能量枢纽, ICCBS_xxldm_星际能量枢纽],
                [ICCBS_xxldm_星际能量枢纽MK2]
            ]);
        }

        if (GenesisBook.Enable) {
            CreateChain([[IGB燃料电池发电厂], [I地热发电站], [IGB裂变能源发电站], [IGB朱曦K型人造恒星], [IGB湛曦O型人造恒星]]);
        } else {
            CreateChain([[I火力发电厂], [I地热发电站], [I微型聚变发电站], [I人造恒星]]);
        }
        CreateChain([[I传送带], [I高速传送带], [I极速传送带]]);
        CreateChain([[I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机]]);
        CreateChain([[I小型储物仓], [I大型储物仓], [IGB量子储物仓]]);
        CreateChain([[I储液罐], [IGB量子储液罐]]);
        CreateChain([[I物流配送器], [I行星内物流运输站], [I星际物流运输站, IMS物资交换物流站], [I轨道采集器]]);
        CreateChain([[I分拣器], [I高速分拣器], [I极速分拣器], [I集装分拣器]]);
        if (SmelterMiner.Enable && !CustomCreateBirthStar.Enable) {
            CreateChain([
                [I采矿机],
                [
                    ISM熔炉采矿机A型,
                    ISM熔炉采矿机B型,
                    ISM化工采矿机C型
                ],
                [I大型采矿机],
                [
                    ISM大型熔炉采矿机A型,
                    ISM大型熔炉采矿机B型,
                    ISM大型化工采矿机C型
                ]
            ]);
            CreateChain([[I原油萃取站, I原油精炼厂], [ISM等离子精炼油井]]);
        } else {
            CreateChain([[I采矿机], [I大型采矿机]]);
            CreateChain([[I原油萃取站, I原油精炼厂]]);
        }

        CreateChain([[I抽水站], [IGB聚束液体汲取设施]]);
        if (GenesisBook.Enable) {
            CreateChain([[I化工厂], [IGB先进化学反应釜]]);
        } else {
            CreateChain([[I化工厂], [I量子化工厂]]);
        }
        CreateChain([[I电弧熔炉], [IGB等离子熔炉], [I位面熔炉], [I负熵熔炉]]);
        if (GenesisBook.Enable) {
            CreateChain([[IGB基础制造台], [IGB标准制造单元], [IGB高精度装配线], [IGB物质重组工厂]]);
        } else {
            CreateChain([[I制造台MkI], [I制造台MkII], [I制造台MkIII], [I重组式制造台]]);
        }

        CreateChain([[I矩阵研究站], [I自演化研究站]]);
        if (MoreMegaStructure.Enable) {
            CreateChain([[I电磁轨道弹射器, IMS射线重构站, I垂直发射井, I微型粒子对撞机]]);
        } else {
            CreateChain([[I电磁轨道弹射器, I射线接收站, I垂直发射井, I微型粒子对撞机]]);
        }
        if (GenesisBook.Enable) {
            CreateChain([[IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器]]);
        }

        //精炼页面
        if (GenesisBook.Enable) {
            CreateChain([
                [IGB空燃料棒],
                [I液氢燃料棒], [IGB焦油燃料棒], [IGB四氢双环戊二烯燃料棒, IGB铀燃料棒],
                [IGB钚燃料棒], [I氘核燃料棒, IGBMOX燃料棒], [IGB氦三燃料棒],
                [I反物质燃料棒, IGB氘氦混合燃料棒], [I奇异湮灭燃料棒]
            ]);
        } else if (OrbitalRing.Enable) {
            CreateChain([
                [IOR化学燃料棒], [IOR铀燃料棒], [I氘核燃料棒], [I反物质燃料棒], [I奇异湮灭燃料棒]
            ]);
        } else {
            CreateChain([
                [I液氢燃料棒], [I氘核燃料棒], [I反物质燃料棒], [I奇异湮灭燃料棒]
            ]);
        }

        //化工页面
        if (GenesisBook.Enable) {
            CreateChain([[IGB聚丙烯], [IGB聚苯硫醚PPS], [IGB聚酰亚胺PI]]);
        } else if (OrbitalRing.Enable) {
            CreateChain([[I原油], [IOR重油], [IOR轻油]]);
        } else {
            CreateChain([[I增产剂MkI], [I增产剂MkII], [I增产剂MkIII]]);
        }

        //防御页面
        CreateChain([[I原型机], [I精准无人机, I攻击无人机], [I护卫舰], [I驱逐舰], [IMS水滴]]);
        CreateChain([[I高频激光塔, IGB紫外激光塔, I近程电浆塔, I磁化电浆炮]]);
        CreateChain([[I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]]);
        CreateChain([[I高斯机枪塔, I聚爆加农炮, IGB电磁加农炮, I导弹防御塔]]);
        if (GenesisBook.Enable) {
            CreateChain([[I机枪弹箱], [IGB钢芯弹箱], [I超合金弹箱], [IGB钨芯弹箱], [IGB三元弹箱], [IGB湮灭弹箱]]);
            CreateChain([[I燃烧单元], [I爆破单元], [IGB核子爆破单元], [IGB反物质湮灭单元]]);
            CreateChain([[I炮弹组], [I高爆炮弹组], [IGB微型核弹组], [IGB反物质炮弹组]]);
            CreateChain([[I导弹组], [I超音速导弹组], [I引力导弹组], [IGB反物质导弹组]]);
            CreateChain([[I干扰胶囊], [I压制胶囊]]);
            CreateChain([[I等离子胶囊], [I反物质胶囊]]);
        } else if (OrbitalRing.Enable) {
            CreateChain([[I机枪弹箱], [IOR钢芯弹箱], [IOR贫铀弹箱], [IOR零素矢]]);
            CreateChain([[IOR炸药单元], [IOR金属氢单元]]);
            CreateChain([[IOR杀爆榴弹组], [IOR金属氢炮弹组]]);
            CreateChain([[I导弹组], [I超音速导弹组], [IOR战术核导弹], [IOR启示录聚变弹], [IOR重力鱼雷]]);
            CreateChain([[I干扰胶囊], [I压制胶囊]]);
            CreateChain([[IOR氘核轨道弹], [IOR反物质轨道弹]]);
        } else {
            CreateChain([[I机枪弹箱], [I钛化弹箱], [I超合金弹箱]]);
            CreateChain([[I燃烧单元], [I爆破单元], [I晶石爆破单元]]);
            CreateChain([[I炮弹组], [I高爆炮弹组], [I晶石炮弹组]]);
            CreateChain([[I导弹组], [I超音速导弹组], [I引力导弹组]]);
            CreateChain([[I干扰胶囊], [I压制胶囊]]);
            CreateChain([[I等离子胶囊], [I反物质胶囊]]);
        }

        //分馏页面
        CreateChain([[IFE电磁奖券], [IFE能量奖券], [IFE结构奖券], [IFE信息奖券], [IFE引力奖券]]);
        // 屏蔽原胚、分馏塔的转化，以提升定向原胚价值
        // CreateChain([[IFE交互塔原胚, IFE矿物复制塔原胚, IFE点数聚集塔原胚, IFE转化塔原胚, IFE回收塔原胚]]);
        // CreateChain([[IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔, IFE回收塔]]);
        CreateChain([[IFE复制精华, IFE点金精华, IFE分解精华, IFE转化精华]]);
        CreateChain([[IBC插件效果分享塔], [IBC插件效果分享站]]);
        CreateChain([
            [IBC速度插件MK1, IBC产能插件MK1, IBC节能插件MK1, IBC品质插件MK1],
            [IBC速度插件MK2, IBC产能插件MK2, IBC节能插件MK2, IBC品质插件MK2],
            [IBC速度插件MK3, IBC产能插件MK3, IBC节能插件MK3, IBC品质插件MK3]
        ]);
    }

    /// <summary>
    /// 构建多个物品混合的转化配方链
    /// </summary>
    private static void CreateChain(List<List<int>> itemLists) {
        //移除不存在的物品
        foreach (List<int> itemList in itemLists) {
            itemList.RemoveAll(itemID => itemValue[itemID] >= maxValue);
        }
        //移除空的物品层次
        itemLists.RemoveAll(itemList => itemList.Count == 0);
        //如果移除之后没有任何物品层次，或者只有一个层次且层次内只有一个物品，直接返回
        if (itemLists.Count == 0 || (itemLists.Count == 1 && itemLists[0].Count == 1)) {
            return;
        }
        //每个物品只能转化成低1层次任何物品、同层次其他物品或高1层次任何物品
        //转化时，需要保证物品整体价值不变。也就是说，必须先确定所有物品的概率，再确定数目
        for (int i = 0; i < itemLists.Count; i++) {
            for (int j = 0; j < itemLists[i].Count; j++) {
                int inputID = itemLists[i][j];
                //构建候选物品ID、候选物品出现概率列表
                List<int> itemIDs = [];
                List<float> itemValuePercents = [];
                for (int k = i - 1; k <= i + 1; k++) {
                    if (k < 0 || k >= itemLists.Count) {
                        continue;
                    }
                    for (int l = 0; l < itemLists[k].Count; l++) {
                        int targetItemID = itemLists[k][l];
                        //排除自身
                        if (targetItemID == inputID) {
                            continue;
                        }
                        itemIDs.Add(targetItemID);
                        float basePercent = itemValue[targetItemID] * LDB.items.Select(targetItemID).StackSize;
                        if (k == i - 1) {
                            itemValuePercents.Add(basePercent * 0.8f);
                        } else if (k == i) {
                            itemValuePercents.Add(basePercent * 1.0f);
                        } else {
                            itemValuePercents.Add(basePercent * 1.25f);
                        }
                    }
                }
                //构建转化列表
                float successRate = 1.0f / itemIDs.Count;
                float totalValuePercent = itemValuePercents.Sum();
                List<OutputInfo> outputMain = [];
                for (int k = 0; k < itemIDs.Count; k++) {
                    int outputID = itemIDs[k];
                    //计算分配给这个输出的价值
                    float allocatedValue = itemValue[inputID] * (itemValuePercents[k] / totalValuePercent);
                    //根据输出物品的价值计算数量
                    float outputCount = allocatedValue / (successRate * itemValue[outputID]);
                    outputMain.Add(new(successRate, outputID, outputCount));
                }
                AddRecipe(new ConversionRecipe(inputID, 0.02f,
                    outputMain,
                    [
                        new OutputInfo(0.01f, IFE转化精华, 1),
                    ]));
            }
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Conversion;

    /// <summary>
    /// 创建转化塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public ConversionRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRate, outputMain, outputAppend) { }

    #region IModCanSave

    public override void Import(BinaryReader r) {
        base.Import(r);
        int version = r.ReadInt32();
    }

    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.Write(1);
    }

    public override void IntoOtherSave() {
        base.IntoOtherSave();
    }

    #endregion
}
