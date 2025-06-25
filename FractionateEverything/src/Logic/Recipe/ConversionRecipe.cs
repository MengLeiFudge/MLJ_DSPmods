using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.ProtoID;

namespace FE.Logic.Recipe;

/// <summary>
/// 转化塔配方类（1A -> XA + YB + ZC）
/// </summary>
public class ConversionRecipe : BaseRecipe {
    private static List<int> CreatedID = [];

    /// <summary>
    /// 添加所有转化配方
    /// </summary>
    public static void CreateAll() {
        CreatedID.Clear();
        //添加特有转化配方
        //物品页面
        CreateFracChain([I钢材, I钛合金, IGB钨合金, IGB三元精金]);
        CreateFracChain([I框架材料, I戴森球组件, I小型运载火箭]);
        CreateFracChain([I高纯硅块, I晶格硅]);
        CreateFracChain([I石材, IGB混凝土]);
        CreateFracChain([I棱镜, I电浆激发器, I光子合并器, I太阳帆]);
        CreateFracChain([I高能石墨, I金刚石]);
        CreateFracChain([I石墨烯, I碳纳米管, I粒子宽带, IGB光学信息传输纤维]);
        CreateFracChain([I粒子容器, I奇异物质, I引力透镜, I空间翘曲器]);
        CreateFracChain([I钛晶石, I卡西米尔晶体]);
        CreateFracChain([IGB基础机械组件, IGB先进机械组件, IGB尖端机械组件, IGB超级机械组件]);//创世独有配方
        CreateFracChain([IGB塑料基板, IGB光学基板]);//创世独有配方
        CreateFracChain([IGB量子计算主机, IGB超越X1型光学主机]);//创世独有配方
        CreateFracChain([I玻璃, I钛化玻璃, IGB钨强化玻璃]);
        CreateFracChain([I氢, I重氢]);
        CreateFracChain([IGB氦, IGB氦三]);
        CreateFracChain([I磁线圈_GB铜线圈, I电动机, I电磁涡轮, I超级磁场环]);
        CreateFracChain([I电路板, I处理器, I量子芯片, IGB光学处理器]);
        CreateFracChain([I临界光子, I反物质]);
        CreateFracChain([I动力引擎, I推进器, I加力推进器]);
        CreateFracChain([I配送运输机, I物流运输机, I星际物流运输船]);
        CreateFracChain([I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素]);
        CreateFracChain([I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵]);

        //建筑页面
        CreateFracChain([I电力感应塔, I无线输电塔, I卫星配电站]);
        CreateFracChain([I风力涡轮机, I太阳能板, IGB同位素温差发电机, I蓄电器, I蓄电器满, I能量枢纽]);
        CreateFracChain([I火力发电厂_GB燃料电池发电厂, I地热发电站, I微型聚变发电站_GB裂变能源发电站, I人造恒星_GB朱曦K型人造恒星, IGB湛曦O型人造恒星]);
        CreateFracChain([I传送带, I高速传送带, I极速传送带]);
        CreateFracChain([I四向分流器, I流速监测器, IGB大气采集站, I喷涂机, I自动集装机]);//注意科技解锁顺序
        CreateFracChain([I小型储物仓, I大型储物仓, IGB量子储物仓]);
        CreateFracChain([I储液罐, IGB量子储液罐]);
        CreateFracChain([I物流配送器, I行星内物流运输站, I星际物流运输站, IMS物资交换物流站, I轨道采集器]);
        CreateFracChain([I分拣器, I高速分拣器, I极速分拣器, I集装分拣器]);
        CreateFracChain([I采矿机, I大型采矿机]);
        CreateFracChain([I抽水站, IGB聚束液体汲取设施]);
        CreateFracChain([I原油萃取站, I原油精炼厂]);
        CreateFracChain([I化工厂, I量子化工厂_GB先进化学反应釜]);
        CreateFracChain([I电弧熔炉, IGB矿物处理厂, I位面熔炉, I负熵熔炉]);
        CreateFracChain([I制造台MkI_GB基础制造台, I制造台MkII_GB标准制造单元, I制造台MkIII_GB高精度装配线, I重组式制造台_GB物质重组工厂]);
        CreateFracChain([I矩阵研究站, I自演化研究站]);
        CreateFracChain([I电磁轨道弹射器, I射线接收站_MS射线重构站, I垂直发射井]);
        CreateFracChain([I微型粒子对撞机]);
        CreateFracChain([IGB物质裂解塔, IGB天穹装配厂, IGB埃克森美孚化工厂, IGB物质分解设施, IGB工业先锋精密加工中心, IGB苍穹粒子加速器]);
        CreateFracChain([IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔]);

        //精炼页面
        CreateFracChain([
            IGB空燃料棒,
            I液氢燃料棒, IGB煤油燃料棒, IGB四氢双环戊二烯燃料棒,
            IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
            I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
            I反物质燃料棒, I奇异湮灭燃料棒,
        ]);

        //化工页面
        CreateFracChain([I塑料_GB聚丙烯, IGB聚苯硫醚PPS, IGB聚酰亚胺PI]);
        CreateFracChain([I增产剂MkI, I增产剂MkII, I增产剂MkIII_GB增产剂]);

        //防御页面
        CreateFracChain([I原型机, I精准无人机, I攻击无人机]);
        CreateFracChain([I护卫舰, I驱逐舰, IVD水滴]);
        CreateFracChain([I高频激光塔, IGB紫外激光塔, I近程电浆塔, I磁化电浆炮]);
        CreateFracChain([I战场分析基站, I信号塔, I干扰塔, I行星护盾发生器]);
        CreateFracChain([I高斯机枪塔, I聚爆加农炮, IGB电磁加农炮, I导弹防御塔]);
        CreateFracChain([I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱]);
        CreateFracChain([I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元]);
        CreateFracChain([I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组]);
        CreateFracChain([I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组]);
        CreateFracChain([I干扰胶囊, I压制胶囊]);
        CreateFracChain([I等离子胶囊, I反物质胶囊]);

        //剩余物品统一转化为自身
        foreach (var item in LDB.items.dataArray) {
            if (!CreatedID.Contains(item.ID)) {
                Create(item.ID, itemRatioDic[item.ID], [
                    new OutputInfo(1f, item.ID, 1),
                ]);
            }
        }
    }

    private static void CreateFracChain(List<int> itemList) {
        itemList = itemList.Where(itemID => LDB.items.Exist(itemID)).ToList();
        float rateSelf = itemList.Count == 1 ? 1.0f : 0.4f;
        float rateOther = (1 - rateSelf) / itemList.Count;
        foreach (int item in itemList) {
            List<OutputInfo> outputMain = [];
            foreach (int item0 in itemList) {
                if (item0 == item) {
                    outputMain.Add(new(rateSelf, item0, 1));
                } else {
                    outputMain.Add(new(rateOther, item0, 1));
                }
            }
            Create(item, 0.04f, outputMain);
        }
    }

    private static void Create(int inputID, float baseSuccessRate, List<OutputInfo> outputMain) {
        if (!LDB.items.Exist(inputID)) {
            return;
        }
        AddRecipe(new ConversionRecipe(inputID, baseSuccessRate,
            outputMain,
            [
                new OutputInfo(0.010f, IFE分馏原胚普通, 1),
                new OutputInfo(0.007f, IFE分馏原胚精良, 1),
                new OutputInfo(0.004f, IFE分馏原胚稀有, 1),
                new OutputInfo(0.002f, IFE分馏原胚史诗, 1),
                new OutputInfo(0.001f, IFE分馏原胚传说, 1),
                new OutputInfo(0.050f, IFE转化精华, 1),
            ]));
        CreatedID.Add(inputID);
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Conversion;

    /// <summary>
    /// 创建转化塔配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">基础成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public ConversionRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRate, outputMain, outputAppend) { }

    /// <summary>
    /// 是否不消耗材料（突破特殊属性）
    /// </summary>
    public bool NoMaterialConsumption { get; set; }

    /// <summary>
    /// 是否输出翻倍（突破特殊属性）
    /// </summary>
    public bool DoubleOutput { get; set; }

    /// <summary>
    /// 专精产物ID（突破特殊属性）
    /// </summary>
    public int SpecializedOutputId { get; set; }

    /// <summary>
    /// 专精产物加成系数（突破特殊属性）
    /// </summary>
    public float SpecializedBonus { get; set; } = 1.0f;

    #region IModCanSave

    public virtual void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public virtual void Export(BinaryWriter w) {
        w.Write(1);
    }

    public virtual void IntoOtherSave() { }

    #endregion
}
