using System.Collections.Generic;
using FE.Logic.Fractionation.Process;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 定义解析塔的矩阵解析和通用原胚谱系分化配方。
/// </summary>
public sealed class RectificationRecipe : BaseRecipe {
    /// <summary>
    /// 区分矩阵解析与原胚谱系分化行为。
    /// </summary>
    public enum RectificationRecipeKind {
        MatrixAnalysis,
        LineageDifferentiation,
    }

    /// <summary>
    /// 保存热路径为当前解析塔实例设置的谱系目标；主路锁定接入前保持为零。
    /// </summary>
    public static int CurrentLineageTargetId;

    private static readonly int[] MatrixInputs = [
        I电磁矩阵,
        I能量矩阵,
        I结构矩阵,
        I信息矩阵,
        I引力矩阵,
        I宇宙矩阵,
    ];

    /// <summary>
    /// 创建六阶段矩阵解析配方和通用原胚谱系分化配方。
    /// </summary>
    public static void CreateAll() {
        foreach (int matrixId in MatrixInputs) {
            AddRecipe(new RectificationRecipe(matrixId, RectificationRecipeKind.MatrixAnalysis, 0.05f,
                [new(1.0f, GetAnalysisDataItemId(matrixId), 1)], []));
        }

        AddRecipe(new RectificationRecipe(IFE通用原胚, RectificationRecipeKind.LineageDifferentiation, 0.05f, [
            new(0.25f, IFE交互塔原胚, 1),
            new(0.25f, IFE解析塔原胚, 1),
            new(0.25f, IFE资源塔原胚, 1),
            new(0.25f, IFE转化塔原胚, 1),
        ], []));
    }

    /// <summary>
    /// 获取文明解析配方类型。
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Rectification;

    /// <summary>
    /// 谱系分化需要先恢复文明协议，矩阵解析属于基础设施配方。
    /// </summary>
    public override bool RequiresProtocolRecovery => Kind == RectificationRecipeKind.LineageDifferentiation;

    /// <summary>
    /// 谱系分化协议固定归入电磁阶段。
    /// </summary>
    public override int ProtocolStageOrder => Kind == RectificationRecipeKind.LineageDifferentiation ? 0 : -1;

    /// <summary>
    /// 获取该实例对应的解析塔行为类型。
    /// </summary>
    public RectificationRecipeKind Kind { get; }

    /// <summary>
    /// 判断物品是否属于谱系分化配方的可选主产物。
    /// </summary>
    public bool SupportsLineageTarget(int itemId) {
        return Kind == RectificationRecipeKind.LineageDifferentiation
               && OutputMain.Exists(output => output.OutputID == itemId);
    }

    /// <summary>
    /// 初始化一项矩阵解析或谱系分化配方。
    /// </summary>
    public RectificationRecipe(int inputId, RectificationRecipeKind kind, float baseSuccessRatio,
        List<OutputInfo> outputMain, List<OutputInfo> outputAppend)
        : base(inputId, baseSuccessRatio, outputMain, outputAppend) {
        Kind = kind;
    }

    /// <summary>
    /// 执行单次解析结算，并在主路锁定生效时固定谱系分化结果。
    /// </summary>
    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        FractionationOutcome outcome = RollOutputs(ref seed, pointsBonus, successBoost, fluidInputIncAvg,
            ref fluidInputInc, out inputChange, out ProductOutputInfo product);
        outputs = outcome == FractionationOutcome.Destroyed
            ? null
            : product == null
                ? ProcessManager.emptyOutputs
                : [product];
    }

    /// <summary>
    /// 执行单次轻量解析结算。
    /// </summary>
    public override FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        outputs.Clear();
        FractionationOutcome outcome = RollOutputs(ref seed, pointsBonus, successBoost, fluidInputIncAvg,
            ref fluidInputInc, out inputChange, out ProductOutputInfo product);
        if (product != null) {
            outputs.Add(product.isMainOutput, product.itemId, product.count);
        }
        return outcome;
    }

    /// <summary>
    /// 执行批量轻量解析结算。
    /// </summary>
    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        outputs.Clear();
        int destroyedCount = RollBinomialApprox(ref seed, batchCount, DestroyRatio);
        int aliveCount = batchCount - destroyedCount;
        int successCount = RollBinomialApprox(ref seed, aliveCount,
            SuccessRatio * (1 + pointsBonus) * (1 + successBoost));
        int passThroughCount = aliveCount - successCount;
        OutputInfo directedOutput = GetDirectedOutputInfo();
        if (directedOutput != null) {
            AddRolledOutput(ref seed, outputs, directedOutput, true, successCount);
        } else {
            RollMainOutputs(ref seed, successCount, outputs);
        }

        int inputRemoveCount = destroyedCount + successCount + passThroughCount;
        fluidInputInc = System.Math.Max(0, fluidInputInc - fluidInputIncAvg * inputRemoveCount);
        return new FractionationBatchResult {
            InputRemoveCount = inputRemoveCount,
            ConsumedRegisterCount = destroyedCount + successCount,
            SuccessCount = successCount,
            DestroyedCount = destroyedCount,
            PassThroughCount = passThroughCount,
            PassThroughInc = fluidInputIncAvg * passThroughCount,
        };
    }

    private FractionationOutcome RollOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out ProductOutputInfo product) {
        inputChange = -1;
        fluidInputInc = System.Math.Max(0, fluidInputInc - fluidInputIncAvg);
        product = null;
        if (GetRandDouble(ref seed) < DestroyRatio) {
            return FractionationOutcome.Destroyed;
        }
        if (GetRandDouble(ref seed) >= SuccessRatio * (1 + pointsBonus) * (1 + successBoost)) {
            return FractionationOutcome.PassThrough;
        }

        OutputInfo output = GetDirectedOutputInfo() ?? RollMainOutput(ref seed);
        int count = RollOutputCount(ref seed, output.OutputCount);
        if (count <= 0) {
            return FractionationOutcome.Destroyed;
        }
        output.OutputTotalCount += count;
        product = new ProductOutputInfo(true, output.OutputID, count);
        return FractionationOutcome.Produced;
    }

    private OutputInfo GetDirectedOutputInfo() {
        if (Kind != RectificationRecipeKind.LineageDifferentiation || CurrentLineageTargetId == 0) {
            return null;
        }
        return OutputMain.Find(output => output.OutputID == CurrentLineageTargetId);
    }

    private OutputInfo RollMainOutput(ref uint seed) {
        double ratio = GetRandDouble(ref seed);
        float accumulated = 0f;
        foreach (OutputInfo output in OutputMain) {
            accumulated += output.SuccessRatio;
            if (ratio <= accumulated) {
                return output;
            }
        }
        return OutputMain[OutputMain.Count - 1];
    }

    private void RollMainOutputs(ref uint seed, int successCount, ProductOutputBuffer outputs) {
        int remaining = successCount;
        float remainingRatio = 1f;
        for (int i = 0; i < OutputMain.Count && remaining > 0; i++) {
            OutputInfo output = OutputMain[i];
            int hits = i == OutputMain.Count - 1
                ? remaining
                : RollBinomialApprox(ref seed, remaining, output.SuccessRatio / remainingRatio);
            remaining -= hits;
            remainingRatio -= output.SuccessRatio;
            AddRolledOutput(ref seed, outputs, output, true, hits);
        }
    }

    private static int GetAnalysisDataItemId(int matrixId) {
        return matrixId switch {
            I电磁矩阵 => IFE电磁解析数据,
            I能量矩阵 => IFE能量解析数据,
            I结构矩阵 => IFE结构解析数据,
            I信息矩阵 => IFE信息解析数据,
            I引力矩阵 => IFE引力解析数据,
            I宇宙矩阵 => IFE宇宙解析数据,
            _ => IFE电磁解析数据,
        };
    }
}
