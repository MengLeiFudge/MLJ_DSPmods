using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Mods;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 转化配方类（1A -> XA + YB + ZC）
/// </summary>
public class ConversionRecipe : BaseRecipe {
    /// <summary>
    /// 单路锁定时使用的固定目标方案。构造期预计算，运行期只查表。
    /// </summary>
    /// <param name="sourceOutput">锁定目标对应的原始输出配置与统计所有者。</param>
    /// <param name="isMainOutput">目标是否来自主产物列表。</param>
    /// <param name="outputCount">按输入/输出价值换算的每次成功基础数量。</param>
    public readonly struct LockedOutputPlan(OutputInfo sourceOutput, bool isMainOutput, float outputCount) {
        /// <summary>
        /// 获取锁定输出对应的原始输出配置。
        /// </summary>
        public OutputInfo SourceOutput => sourceOutput;
        /// <summary>
        /// 获取输出物品 ID。
        /// </summary>
        public int OutputID => sourceOutput.OutputID;
        /// <summary>
        /// 判断该锁定输出是否来自主产物列表。
        /// </summary>
        public bool IsMainOutput => isMainOutput;
        /// <summary>
        /// 获取每次成功输出的基础数量。
        /// </summary>
        public float OutputCount => outputCount;
        /// <summary>
        /// 获取锁定输出相对原始期望产量增加的数量。
        /// </summary>
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

        //化工页面仅保留增产剂这类消耗品链，普通化工材料不能进入转化线。
        CreateChain([[I增产剂MkI], [I增产剂MkII], [I增产剂MkIII]]);

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
    /// 初始化一项转化配方。
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRatio">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public ConversionRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) {
        lockedOutputPlansByItemId = BuildLockedOutputPlans(inputID, outputMain);
    }

    /// <summary>
    /// 按输出物品 ID 索引的构造期预计算单路锁定方案；唯一可锁候选不足两项时为空。
    /// </summary>
    private readonly Dictionary<int, LockedOutputPlan> lockedOutputPlansByItemId;
    /// <summary>
    /// 判断该转化配方是否支持单路锁定输出。
    /// </summary>
    public bool SupportsLockedOutput => lockedOutputPlansByItemId.Count > 0;

    /// <inheritdoc/>
    /// <summary>
    /// 判断指定主产物是否可作为当前转化配方的主路目标。
    /// </summary>
    public override bool SupportsMainOutputLock(int itemId) => TryGetLockedOutputPlan(itemId, out _);

    /// <inheritdoc/>
    /// <summary>
    /// 执行单次完整分馏结算并写回主产物、副产物和输入保留结果。
    /// </summary>
    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        if (TryGetActiveLockedSettlement(pointsBonus, successBoost, fluidInputIncAvg, out LockedSettlementArgs args)) {
            FractionationOutcome outcome = SettleLockedOnce(ref seed, in args, ref fluidInputInc,
                out inputChange, out int producedCount);
            outputs = outcome switch {
                FractionationOutcome.Destroyed => null,
                FractionationOutcome.Produced => [
                    new ProductOutputInfo(args.Plan.IsMainOutput, args.Plan.OutputID, producedCount)
                ],
                _ => ProcessManager.emptyOutputs,
            };
            return;
        }

        // 调用基类获取原始结果
        base.GetOutputs(ref seed, pointsBonus, successBoost,
            fluidInputIncAvg, ref fluidInputInc, out inputChange, out outputs);
    }

    /// <inheritdoc/>
    /// <summary>
    /// 执行单次轻量分馏结算，供运行热路径减少分配使用。
    /// </summary>
    public override FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        if (TryGetActiveLockedSettlement(pointsBonus, successBoost, fluidInputIncAvg, out LockedSettlementArgs args)) {
            // 与 HEAD GetLockedOutputFast 一致：仅确认走锁定路径后才清空调用方缓冲。
            outputs.Clear();
            FractionationOutcome outcome = SettleLockedOnce(ref seed, in args, ref fluidInputInc,
                out inputChange, out int producedCount);
            if (outcome == FractionationOutcome.Produced) {
                outputs.Add(args.Plan.IsMainOutput, args.Plan.OutputID, producedCount);
            }
            return outcome;
        }

        return base.GetOutputsFast(ref seed, pointsBonus, successBoost,
            fluidInputIncAvg, ref fluidInputInc, out inputChange, outputs);
    }

    /// <inheritdoc/>
    /// <summary>
    /// 执行批量轻量分馏结算，供运行热路径合并多次处理。
    /// </summary>
    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        if (TryGetActiveLockedSettlement(pointsBonus, successBoost, fluidInputIncAvg, out LockedSettlementArgs args)) {
            return SettleLockedBatch(ref seed, in args, batchCount, ref fluidInputInc, outputs);
        }

        return base.GetOutputsBatchFast(ref seed, pointsBonus, successBoost, batchCount,
            fluidInputIncAvg, ref fluidInputInc, outputs);
    }

    /// <summary>
    /// 尝试取得指定物品对应的转化配方锁定输出方案。
    /// </summary>
    /// <param name="itemId">候选主产物物品 ID。</param>
    /// <param name="lockedPlan">命中时返回构造期预计算的锁定方案。</param>
    /// <returns>该物品存在可用锁定方案时为 <c>true</c>。</returns>
    public bool TryGetLockedOutputPlan(int itemId, out LockedOutputPlan lockedPlan) =>
        lockedOutputPlansByItemId.TryGetValue(itemId, out lockedPlan);

    /// <summary>
    /// 锁定主路单次/批量结算共享的运行参数。
    /// </summary>
    /// <param name="plan">预计算的固定目标方案。</param>
    /// <param name="pointsBonus">增产剂比例加成。</param>
    /// <param name="successBoost">配方成功率加成。</param>
    /// <param name="fluidInputIncAvg">单次输入对应的平均增产点。</param>
    private readonly struct LockedSettlementArgs(
        LockedOutputPlan plan,
        float pointsBonus,
        float successBoost,
        int fluidInputIncAvg) {
        /// <summary>固定目标方案。</summary>
        public LockedOutputPlan Plan => plan;
        /// <summary>单次输入对应的平均增产点。</summary>
        public int FluidInputIncAvg => fluidInputIncAvg;

        /// <summary>
        /// 计算锁定主路成功概率。
        /// </summary>
        /// <param name="successRatio">配方基础成功率。</param>
        /// <returns>含增产与成功率加成后的锁定成功概率。</returns>
        public float GetLockedSuccessRatio(float successRatio) =>
            successRatio * (1 + pointsBonus) * (1 + successBoost);
    }

    /// <summary>
    /// 判断当前是否启用有效主路锁定，并组装结算参数。
    /// </summary>
    /// <param name="pointsBonus">增产剂比例加成。</param>
    /// <param name="successBoost">配方成功率加成。</param>
    /// <param name="fluidInputIncAvg">单次输入对应的平均增产点。</param>
    /// <param name="args">组装后的锁定结算参数。</param>
    /// <returns>可走锁定路径时为 true。</returns>
    private bool TryGetActiveLockedSettlement(float pointsBonus, float successBoost, int fluidInputIncAvg,
        out LockedSettlementArgs args) {
        if (TowerRuntimeModifierCache.IsMainOutputLockEnabled(ERecipe.Conversion)
            && CurrentMainOutputTargetId != 0
            && TryGetLockedOutputPlan(CurrentMainOutputTargetId, out LockedOutputPlan lockedPlan)) {
            args = new LockedSettlementArgs(lockedPlan, pointsBonus, successBoost, fluidInputIncAvg);
            return true;
        }

        args = default;
        return false;
    }

    /// <summary>
    /// 执行单次锁定主路结算；先完成 RNG/产出统计，再写输入变化与增产点。
    /// </summary>
    /// <param name="seed">随机种子。</param>
    /// <param name="args">锁定结算共享参数。</param>
    /// <param name="fluidInputInc">塔内剩余增产点；仅在判定与统计完成后扣减。</param>
    /// <param name="inputChange">输入变化；锁定路径在状态写回时恒为 -1。</param>
    /// <param name="producedCount">成功时的产出件数；其余结果为 0。</param>
    /// <returns>损毁、产出或直通结果。</returns>
    private FractionationOutcome SettleLockedOnce(ref uint seed, in LockedSettlementArgs args,
        ref int fluidInputInc, out int inputChange, out int producedCount) {
        producedCount = 0;
        FractionationOutcome outcome;

        // 1. 损毁判定
        if (GetRandDouble(ref seed) < DestroyRatio) {
            outcome = FractionationOutcome.Destroyed;
        }
        // 2. 成功判定：单路锁定将成功后的随机路径替换为固定目标方案。
        else if (GetRandDouble(ref seed) < args.GetLockedSuccessRatio(SuccessRatio)) {
            int countReal = RollOutputCount(ref seed, args.Plan.OutputCount);
            if (countReal > 0) {
                args.Plan.SourceOutput.AddOutputCount(countReal);
                producedCount = countReal;
                outcome = FractionationOutcome.Produced;
            } else {
                // 与 BaseRecipe 保持一致：成功但产出数为 0，视为损毁。
                outcome = FractionationOutcome.Destroyed;
            }
        }
        // 3. 无变化 -> 直通输出
        else {
            outcome = FractionationOutcome.PassThrough;
        }

        // 判定与统计写入完成后再改输入状态，保持与原 GetLockedOutput* 异常顺序一致。
        inputChange = -1;
        fluidInputInc -= args.FluidInputIncAvg;
        return outcome;
    }

    /// <summary>
    /// 执行批量锁定主路结算。
    /// </summary>
    /// <param name="seed">随机种子。</param>
    /// <param name="args">锁定结算共享参数。</param>
    /// <param name="batchCount">本批处理次数。</param>
    /// <param name="fluidInputInc">塔内剩余增产点；在产出统计写入后按移除次数扣减。</param>
    /// <param name="outputs">批量产物缓冲。</param>
    /// <returns>批量结算结果。</returns>
    private FractionationBatchResult SettleLockedBatch(ref uint seed, in LockedSettlementArgs args,
        int batchCount, ref int fluidInputInc, ProductOutputBuffer outputs) {
        outputs.Clear();

        int destroyedCount = RollBinomialApprox(ref seed, batchCount, DestroyRatio);
        int aliveCount = batchCount - destroyedCount;
        int rolledSuccessCount = RollBinomialApprox(ref seed, aliveCount, args.GetLockedSuccessRatio(SuccessRatio));
        int passThroughCount = aliveCount - rolledSuccessCount;
        int producedSuccessCount = AddRolledLockedOutput(ref seed, outputs, args.Plan, rolledSuccessCount);

        destroyedCount += rolledSuccessCount - producedSuccessCount;
        int inputRemoveCount = destroyedCount + passThroughCount + producedSuccessCount;
        fluidInputInc -= args.FluidInputIncAvg * inputRemoveCount;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        return new FractionationBatchResult {
            InputRemoveCount = inputRemoveCount,
            ConsumedRegisterCount = destroyedCount + producedSuccessCount,
            SuccessCount = producedSuccessCount,
            DestroyedCount = destroyedCount,
            PassThroughCount = passThroughCount,
            PassThroughInc = args.FluidInputIncAvg * passThroughCount,
        };
    }

    /// <summary>
    /// 将批量命中的锁定输出写入缓冲，并累计配方产出统计。
    /// </summary>
    /// <param name="seed">随机种子。</param>
    /// <param name="outputs">产物缓冲。</param>
    /// <param name="lockedPlan">锁定目标方案。</param>
    /// <param name="outputHits">批量成功命中次数。</param>
    /// <returns>实际计入成功的次数；产出为 0 时返回 0。</returns>
    private static int AddRolledLockedOutput(ref uint seed, ProductOutputBuffer outputs,
        LockedOutputPlan lockedPlan, int outputHits) {
        if (outputHits <= 0) {
            return 0;
        }

        int baseCount = (int)lockedPlan.OutputCount;
        float fractionalCount = lockedPlan.OutputCount - baseCount;
        int totalCount = outputHits * baseCount + RollBinomialApprox(ref seed, outputHits, fractionalCount);
        if (totalCount <= 0) {
            return 0;
        }

        lockedPlan.SourceOutput.AddOutputCount(totalCount);
        outputs.Add(lockedPlan.IsMainOutput, lockedPlan.OutputID, totalCount);
        return baseCount > 0 ? outputHits : totalCount;
    }

    /// <summary>
    /// 为可锁定主产物预计算等价值固定输出方案。
    /// </summary>
    /// <param name="inputId">输入物品 ID。</param>
    /// <param name="outputMain">主产物列表。</param>
    /// <returns>可锁定方案表；唯一可锁定候选不足两项时为空。</returns>
    private static Dictionary<int, LockedOutputPlan> BuildLockedOutputPlans(int inputId,
        List<OutputInfo> outputMain) {
        // 先收集唯一可锁定 ID；不足两项时立即返回，避免无谓的等价值计算。
        HashSet<int> lockableOutputIds = [];
        foreach (OutputInfo output in outputMain) {
            if (IsLockableOutputValue(output.OutputID)) {
                lockableOutputIds.Add(output.OutputID);
            }
        }
        if (lockableOutputIds.Count <= 1) {
            return [];
        }

        Dictionary<int, LockedOutputPlan> plans = [];
        foreach (OutputInfo output in outputMain) {
            if (plans.ContainsKey(output.OutputID) || !lockableOutputIds.Contains(output.OutputID)) {
                continue;
            }

            float lockedOutputCount = itemValue[inputId] / itemValue[output.OutputID];
            // 主路锁定只处理 OutputMain，因此 isMainOutput 固定为 true。
            plans.Add(output.OutputID, new(output, true, lockedOutputCount));
        }
        return plans;
    }

    /// <summary>
    /// 判断输出物品价值是否落在可锁定区间。
    /// </summary>
    /// <param name="outputId">输出物品 ID。</param>
    /// <returns>价值有效且非 maxValue 占位时为 true。</returns>
    private static bool IsLockableOutputValue(int outputId) {
        float outputValue = itemValue[outputId];
        return outputValue > 0f && outputValue < maxValue;
    }
}
