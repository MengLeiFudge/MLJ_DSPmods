using System.Collections.Generic;
using FE.Logic.Fractionation.State;
using System.IO;
using System.Linq;
using FE.Compatibility.Mods;
using FE.Logic.Buildings.Definitions;
using FE.Logic.Fractionation.Process;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Growth;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 转化塔配方类（1A -> XA + YB + ZC）
/// </summary>
public class ConversionRecipe : BaseRecipe {
    /// <summary>
    /// 单路锁定时使用的固定目标方案。构造期预计算，运行期只查表。
    /// </summary>
    public readonly struct LockedOutputPlan(OutputInfo sourceOutput, bool isMainOutput, float outputCount) {
        public OutputInfo SourceOutput => sourceOutput;
        public int OutputID => sourceOutput.OutputID;
        public bool IsMainOutput => isMainOutput;
        public float OutputCount => outputCount;
        public float ExtraOutputCount => OutputCount - SourceOutput.SuccessRatio * SourceOutput.OutputCount;
    }

    /// <summary>
    /// 添加所有转化配方
    /// </summary>
    public static void CreateAll() {
        //添加特有转化配方
        //物品页面
        CreateChain([[I配送运输机], [I物流运输机], [I星际物流运输船]]);
        CreateChain([[I能量碎片], [I黑雾矩阵], [I物质重组器], [I硅基神经元], [I负熵奇点], [I核心素]]);

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
                float successRatio = 1.0f / itemIDs.Count;
                float totalValuePercent = itemValuePercents.Sum();
                List<OutputInfo> outputMain = [];
                for (int k = 0; k < itemIDs.Count; k++) {
                    int outputID = itemIDs[k];
                    //计算分配给这个输出的价值
                    float allocatedValue = itemValue[inputID] * (itemValuePercents[k] / totalValuePercent);
                    //根据输出物品的价值计算数量
                    float outputCount = allocatedValue / (successRatio * itemValue[outputID]);
                    outputMain.Add(new(successRatio, outputID, outputCount));
                }
                AddRecipe(new ConversionRecipe(inputID, 0.05f,
                    outputMain,
                    []));
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
    /// <param name="baseSuccessRatio">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public ConversionRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) {
        lockedOutputPlansByItemId = BuildLockedOutputPlans(inputID, outputMain, outputAppend);
    }

    private readonly Dictionary<int, LockedOutputPlan> lockedOutputPlansByItemId;
    public bool SupportsLockedOutput => lockedOutputPlansByItemId.Count > 0;

    /// <summary>
    /// 当前分馏塔锁定的输出物品ID（由 ProcessManager 在调用 GetOutputs 前设置）
    /// </summary>
    public static int CurrentLockedOutputId = 0;

    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        if (ConversionTower.EnableSingleLock
            && CurrentLockedOutputId != 0
            && TryGetLockedOutputPlan(CurrentLockedOutputId, out LockedOutputPlan lockedPlan)) {
            GetLockedOutput(ref seed, pointsBonus, successBoost, fluidInputIncAvg, ref fluidInputInc,
                lockedPlan, out inputChange, out outputs);
            return;
        }

        // 调用基类获取原始结果
        base.GetOutputs(ref seed, pointsBonus, successBoost,
            fluidInputIncAvg, ref fluidInputInc, out inputChange, out outputs);
    }

    public override FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        if (ConversionTower.EnableSingleLock
            && CurrentLockedOutputId != 0
            && TryGetLockedOutputPlan(CurrentLockedOutputId, out LockedOutputPlan lockedPlan)) {
            return GetLockedOutputFast(ref seed, pointsBonus, successBoost, fluidInputIncAvg,
                ref fluidInputInc, lockedPlan, out inputChange, outputs);
        }

        return base.GetOutputsFast(ref seed, pointsBonus, successBoost,
            fluidInputIncAvg, ref fluidInputInc, out inputChange, outputs);
    }

    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        if (ConversionTower.EnableSingleLock
            && CurrentLockedOutputId != 0
            && TryGetLockedOutputPlan(CurrentLockedOutputId, out LockedOutputPlan lockedPlan)) {
            return GetLockedOutputBatchFast(ref seed, pointsBonus, successBoost, batchCount,
                fluidInputIncAvg, ref fluidInputInc, lockedPlan, outputs);
        }

        return base.GetOutputsBatchFast(ref seed, pointsBonus, successBoost, batchCount,
            fluidInputIncAvg, ref fluidInputInc, outputs);
    }

    public bool TryGetLockedOutputPlan(int itemId, out LockedOutputPlan lockedPlan) =>
        lockedOutputPlansByItemId.TryGetValue(itemId, out lockedPlan);

    private void GetLockedOutput(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, LockedOutputPlan lockedPlan,
        out int inputChange, out List<ProductOutputInfo> outputs) {
        // 1. 损毁判定
        if (GetRandDouble(ref seed) < DestroyRatio) {
            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            outputs = null;
            return;
        }

        // 2. 成功判定：单路锁定将成功后的随机路径替换为固定目标方案。
        float lockedSuccessRatio = SuccessRatio * (1 + pointsBonus) * (1 + successBoost);
        if (GetRandDouble(ref seed) < lockedSuccessRatio) {
            RecipeGrowthQueries.GetProcessingRatios(this, out float remainInputRatio, out float doubleOutputRatio);
            int countReal = RollOutputCount(ref seed, lockedPlan.OutputCount);

            if (GetRandDouble(ref seed) < doubleOutputRatio) {
                countReal *= 2;
            }

            if (countReal > 0) {
                lockedPlan.SourceOutput.OutputTotalCount += countReal;
                inputChange = GetRandDouble(ref seed) < remainInputRatio ? 0 : -1;
                if (inputChange < 0) {
                    fluidInputInc -= fluidInputIncAvg;
                }

                outputs = [new ProductOutputInfo(lockedPlan.IsMainOutput, lockedPlan.OutputID, countReal)];
                return;
            }

            // 与 BaseRecipe 保持一致：成功但产出数为 0，视为损毁。
            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            outputs = null;
            return;
        }

        // 3. 无变化 -> 直通输出
        inputChange = -1;
        fluidInputInc -= fluidInputIncAvg;
        outputs = ProcessManager.emptyOutputs;
    }

    private FractionationOutcome GetLockedOutputFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, LockedOutputPlan lockedPlan,
        out int inputChange, ProductOutputBuffer outputs) {
        outputs.Clear();

        // 1. 损毁判定
        if (GetRandDouble(ref seed) < DestroyRatio) {
            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            return FractionationOutcome.Destroyed;
        }

        // 2. 成功判定：单路锁定将成功后的随机路径替换为固定目标方案。
        float lockedSuccessRatio = SuccessRatio * (1 + pointsBonus) * (1 + successBoost);
        if (GetRandDouble(ref seed) < lockedSuccessRatio) {
            RecipeGrowthQueries.GetProcessingRatios(this, out float remainInputRatio, out float doubleOutputRatio);
            int countReal = RollOutputCount(ref seed, lockedPlan.OutputCount);

            if (GetRandDouble(ref seed) < doubleOutputRatio) {
                countReal *= 2;
            }

            if (countReal > 0) {
                lockedPlan.SourceOutput.OutputTotalCount += countReal;
                inputChange = GetRandDouble(ref seed) < remainInputRatio ? 0 : -1;
                if (inputChange < 0) {
                    fluidInputInc -= fluidInputIncAvg;
                }

                outputs.Add(lockedPlan.IsMainOutput, lockedPlan.OutputID, countReal);
                return FractionationOutcome.Produced;
            }

            inputChange = -1;
            fluidInputInc -= fluidInputIncAvg;
            return FractionationOutcome.Destroyed;
        }

        // 3. 无变化 -> 直通输出
        inputChange = -1;
        fluidInputInc -= fluidInputIncAvg;
        return FractionationOutcome.PassThrough;
    }

    private FractionationBatchResult GetLockedOutputBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, LockedOutputPlan lockedPlan,
        ProductOutputBuffer outputs) {
        outputs.Clear();

        int destroyedCount = RollBinomialApprox(ref seed, batchCount, DestroyRatio);
        int aliveCount = batchCount - destroyedCount;
        float lockedSuccessRatio = SuccessRatio * (1 + pointsBonus) * (1 + successBoost);
        int successCount = RollBinomialApprox(ref seed, aliveCount, lockedSuccessRatio);
        int passThroughCount = aliveCount - successCount;

        RecipeGrowthQueries.GetProcessingRatios(this, out float remainInputRatio, out float doubleOutputRatio);
        int remainInputCount = RollBinomialApprox(ref seed, successCount, remainInputRatio);
        int successConsumedCount = successCount - remainInputCount;

        AddRolledLockedOutput(ref seed, outputs, lockedPlan, successCount, doubleOutputRatio);

        int inputRemoveCount = destroyedCount + passThroughCount + successConsumedCount;
        fluidInputInc -= fluidInputIncAvg * inputRemoveCount;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        FractionationBatchResult result = new() {
            InputRemoveCount = inputRemoveCount,
            ConsumedRegisterCount = destroyedCount + successCount,
            SuccessCount = successCount,
            DestroyedCount = destroyedCount,
            PassThroughCount = passThroughCount,
        };
        return result;
    }

    private static void AddRolledLockedOutput(ref uint seed, ProductOutputBuffer outputs,
        LockedOutputPlan lockedPlan, int outputHits, float doubleOutputRatio) {
        if (outputHits <= 0) {
            return;
        }

        int baseCount = (int)lockedPlan.OutputCount;
        float fractionalCount = lockedPlan.OutputCount - baseCount;
        int totalCount = outputHits * baseCount + RollBinomialApprox(ref seed, outputHits, fractionalCount);
        if (doubleOutputRatio > 0f) {
            int doubleHits = RollBinomialApprox(ref seed, outputHits, doubleOutputRatio);
            totalCount += doubleHits * baseCount + RollBinomialApprox(ref seed, doubleHits, fractionalCount);
        }
        if (totalCount <= 0) {
            return;
        }

        lockedPlan.SourceOutput.OutputTotalCount += totalCount;
        outputs.Add(lockedPlan.IsMainOutput, lockedPlan.OutputID, totalCount);
    }

    private static Dictionary<int, LockedOutputPlan> BuildLockedOutputPlans(int inputId,
        List<OutputInfo> outputMain, List<OutputInfo> outputAppend) {
        Dictionary<int, LockedOutputPlan> plans = [];
        HashSet<int> lockableOutputIds = [];
        CollectLockableOutputIds(lockableOutputIds, outputMain);
        CollectLockableOutputIds(lockableOutputIds, outputAppend);
        if (lockableOutputIds.Count <= 1) {
            return plans;
        }

        AddLockedOutputPlans(plans, inputId, outputMain, true);
        AddLockedOutputPlans(plans, inputId, outputAppend, false);
        return plans;
    }

    private static void CollectLockableOutputIds(HashSet<int> lockableOutputIds, List<OutputInfo> outputs) {
        foreach (OutputInfo output in outputs) {
            if (IsLockableOutputValue(output.OutputID)) {
                lockableOutputIds.Add(output.OutputID);
            }
        }
    }

    private static void AddLockedOutputPlans(Dictionary<int, LockedOutputPlan> plans, int inputId,
        List<OutputInfo> outputs, bool isMainOutput) {
        foreach (OutputInfo output in outputs) {
            if (plans.ContainsKey(output.OutputID)) {
                continue;
            }

            if (!IsLockableOutputValue(output.OutputID)) {
                continue;
            }

            float outputValue = itemValue[output.OutputID];
            float lockedOutputCount = itemValue[inputId] / outputValue;
            plans.Add(output.OutputID, new(output, isMainOutput, lockedOutputCount));
        }
    }

    private static bool IsLockableOutputValue(int outputId) {
        float outputValue = itemValue[outputId];
        return outputValue > 0f && outputValue < maxValue;
    }

    #region IModCanSave

    public override void Import(BinaryReader r) {
        base.Import(r);
        r.ReadBlocks();
    }

    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.WriteBlocks();
    }

    public override void IntoOtherSave() {
        base.IntoOtherSave();
    }

    #endregion
}
