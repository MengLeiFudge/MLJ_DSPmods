using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        CreateChain([[I铁块], [I钢材], [I钛合金], [IGB钨合金], [IGB三元精金]]);
        CreateChain([[I框架材料], [I戴森球组件], [I小型运载火箭]]);
        CreateChain([[I高纯硅块], [I晶格硅]]);
        CreateChain([[I棱镜], [I电浆激发器], [I光子合并器], [I太阳帆]]);
        CreateChain([[I高能石墨], [I金刚石, I石墨烯], [I碳纳米管], [I粒子宽带], [IGB光学信息传输纤维]]);
        CreateChain([[I粒子容器], [I奇异物质], [I引力透镜], [I空间翘曲器]]);
        CreateChain([[I钛晶石], [I卡西米尔晶体]]);
        CreateChain([[IGB基础机械组件], [IGB先进机械组件], [IGB尖端机械组件], [IGB超级机械组件]]);//创世独有配方
        CreateChain([[IGB塑料基板], [IGB光学基板]]);//创世独有配方
        CreateChain([[IGB量子计算主机], [IGB超越X1型光学主机]]);//创世独有配方
        CreateChain([[I玻璃], [I钛化玻璃], [IGB钨强化玻璃]]);
        CreateChain([[I氢], [I重氢]]);
        CreateChain([[IGB氦], [IGB氦三]]);
        CreateChain([[I磁线圈], [I电动机], [I电磁涡轮], [I超级磁场环]]);
        CreateChain([[I电路板, I微晶元件], [I处理器, I位面过滤器], [I量子芯片], [IGB光学处理器]]);
        CreateChain([[I临界光子], [I反物质]]);
        CreateChain([[I动力引擎], [I推进器], [I加力推进器]]);
        CreateChain([[I配送运输机], [I物流运输机], [I星际物流运输船]]);
        CreateChain([[I能量碎片], [I黑雾矩阵], [I物质重组器], [I硅基神经元], [I负熵奇点], [I核心素]]);
        CreateChain([[I电磁矩阵], [I能量矩阵], [I结构矩阵], [I信息矩阵], [I引力矩阵]]);

        //建筑页面
        CreateChain([[I电力感应塔], [I无线输电塔], [I卫星配电站]]);
        CreateChain([[I风力涡轮机, I太阳能板, IGB同位素温差发电机], [I蓄电器], [I能量枢纽]]);
        CreateChain([[I火力发电厂_GB燃料电池发电厂], [I地热发电站], [I微型聚变发电站_GB裂变能源发电站], [I人造恒星_GB朱曦K型人造恒星], [IGB湛曦O型人造恒星]]);
        CreateChain([[I传送带], [I高速传送带], [I极速传送带]]);
        CreateChain([[I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机]]);//注意科技解锁顺序
        CreateChain([[I小型储物仓], [I大型储物仓], [IGB量子储物仓]]);
        CreateChain([[I储液罐], [IGB量子储液罐]]);
        CreateChain([[I物流配送器], [I行星内物流运输站], [I星际物流运输站, IMS物资交换物流站], [I轨道采集器]]);
        CreateChain([[I分拣器], [I高速分拣器], [I极速分拣器], [I集装分拣器]]);
        CreateChain([[I采矿机], [I大型采矿机]]);
        CreateChain([[I抽水站], [IGB聚束液体汲取设施]]);
        CreateChain([[I原油萃取站, I原油精炼厂]]);
        CreateChain([[I化工厂], [I量子化工厂_GB先进化学反应釜]]);
        CreateChain([[I电弧熔炉, IGB矿物处理厂], [I位面熔炉], [I负熵熔炉]]);
        CreateChain([[I制造台MkI_GB基础制造台], [I制造台MkII_GB标准制造单元], [I制造台MkIII_GB高精度装配线], [I重组式制造台_GB物质重组工厂]]);
        CreateChain([[I矩阵研究站], [I自演化研究站]]);
        CreateChain([[I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井, I微型粒子对撞机]]);
        CreateChain([[IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器]]);

        //精炼页面
        CreateChain([
            [IGB空燃料棒],
            [I液氢燃料棒], [IGB焦油燃料棒], [IGB四氢双环戊二烯燃料棒, IGB铀燃料棒],
            [IGB钚燃料棒], [I氘核燃料棒, IGBMOX燃料棒], [IGB氦三燃料棒],
            [I反物质燃料棒, IGB氘氦混合燃料棒], [I奇异湮灭燃料棒]
        ]);

        //化工页面
        CreateChain([[I塑料_GB聚丙烯], [IGB聚苯硫醚PPS], [IGB聚酰亚胺PI]]);
        CreateChain([[I增产剂MkI], [I增产剂MkII], [I增产剂MkIII_GB增产剂]]);

        //防御页面
        CreateChain([[I原型机], [I精准无人机, I攻击无人机]]);
        CreateChain([[I护卫舰], [I驱逐舰], [IMS水滴]]);
        CreateChain([[I高频激光塔, IGB紫外激光塔, I近程电浆塔, I磁化电浆炮]]);
        CreateChain([[I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]]);
        CreateChain([[I高斯机枪塔, I聚爆加农炮, IGB电磁加农炮, I导弹防御塔]]);
        CreateChain([[I机枪弹箱], [I钛化弹箱, IGB钢芯弹箱], [I超合金弹箱], [IGB钨芯弹箱], [IGB三元弹箱], [IGB湮灭弹箱]]);
        CreateChain([[I燃烧单元], [I爆破单元], [I晶石爆破单元, IGB核子爆破单元], [IGB反物质湮灭单元]]);
        CreateChain([[I炮弹组], [I高爆炮弹组], [I晶石炮弹组, IGB微型核弹组], [IGB反物质炮弹组]]);
        CreateChain([[I导弹组], [I超音速导弹组], [I引力导弹组], [IGB反物质导弹组]]);
        CreateChain([[I干扰胶囊, I等离子胶囊], [I压制胶囊, I反物质胶囊]]);

        //分馏页面
        CreateChain([[IFE电磁奖券], [IFE能量奖券], [IFE结构奖券], [IFE信息奖券], [IFE引力奖券]]);
        CreateChain([[IFE分馏塔原胚普通], [IFE分馏塔原胚精良], [IFE分馏塔原胚稀有], [IFE分馏塔原胚史诗], [IFE分馏塔原胚传说]]);
        //CreateChain([[IFE分馏配方通用核心, IFE分馏塔增幅芯片]]);
        CreateChain([[IFE矿物复制塔], [IFE交互塔, IFE点金塔, IFE分解塔, IFE转化塔], [IFE点数聚集塔], [IFE量子复制塔]]);
        CreateChain([[IFE行星矿物复制塔], [IFE行星交互塔, IFE行星点金塔, IFE行星分解塔, IFE行星转化塔], [IFE行星点数聚集塔], [IFE行星量子复制塔]]);
        CreateChain([[IFE复制精华, IFE点金精华, IFE分解精华, IFE转化精华]]);
        CreateChain([[IBC插件效果分享塔], [IBC插件效果分享站]]);
        CreateChain([
            [IBC速度插件MK1, IBC产能插件MK1, IBC节能插件MK1, IBC品质插件MK1],
            [IBC速度插件MK2, IBC产能插件MK2, IBC节能插件MK2, IBC品质插件MK2],
            [IBC速度插件MK3, IBC产能插件MK3, IBC节能插件MK3, IBC品质插件MK3]
        ]);
    }

    /// <summary>
    /// 构建一个物品逐步升级的配方链
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
                        float basePercent = itemValue[targetItemID] / LDB.items.Select(targetItemID).StackSize;
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
                Create(inputID, 0.05f, outputMain);
            }
        }
    }

    private static void Create(int inputID, float baseSuccessRate, List<OutputInfo> outputMain) {
        //由于上层已经做过价值的相关判断（也就是某个物品是否存在的判断），此处不需要再判断
        AddRecipe(new ConversionRecipe(inputID, baseSuccessRate,
            outputMain,
            [
                new OutputInfo(0.01f, IFE转化精华, 1),
            ]));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Conversion;

    /// <summary>
    /// 创建转化塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="maxSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public ConversionRecipe(int inputID, float maxSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, maxSuccessRate, outputMain, outputAppend) { }

    /// <summary>
    /// 主产物数目增幅
    /// </summary>
    public override float MainOutputCountInc => (Progress - 0.56f) / 0.88f;

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
