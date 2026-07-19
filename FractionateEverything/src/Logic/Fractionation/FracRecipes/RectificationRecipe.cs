using System.Collections.Generic;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 矩阵萃取与矩阵精华重整配方的产出分布逻辑。
/// </summary>
public class RectificationRecipe : BaseRecipe {
    /// <summary>
    /// 区分精馏配方的矩阵萃取和精华重整类型。
    /// </summary>
    public enum RectificationRecipeKind {
        MatrixExtraction,
        EssenceTuning,
    }

    /// <summary>
    /// 定义精华重整时向高阶压缩的基础权重。
    /// </summary>
    public const float BaseCompressionRatio = 0.45f;
    /// <summary>
    /// 定义当前规划的最高压缩权重。
    /// </summary>
    public const float MaxPlannedCompressionRatio = 0.60f;
    private const float TicketSplitRatio = 0.10f;
    public static int CurrentTuningTargetId;
    private const float CompressionOutputCount = 0.45f;
    private const float RefluxOutputCount = 1.80f;

    private static readonly int[] MatrixInputs = [
        I电磁矩阵,
        I能量矩阵,
        I结构矩阵,
        I信息矩阵,
        I引力矩阵,
        I宇宙矩阵,
    ];

    /// <summary>
    /// 创建并注册该类型下的全部分馏配方。
    /// </summary>
    public static void CreateAll() {
        foreach (int matrixId in MatrixInputs) {
            AddRecipe(CreateMatrixExtraction(matrixId));
        }

    }

    private static RectificationRecipe CreateMatrixExtraction(int matrixId) {
        return new RectificationRecipe(matrixId, RectificationRecipeKind.MatrixExtraction, 0.05f,
            BuildMatrixExtractionOutputs(matrixId), []);
    }

    private static RectificationRecipe CreateEssenceTuning(int inputId) {
        return new RectificationRecipe(inputId, RectificationRecipeKind.EssenceTuning, 0.05f,
            BuildEssenceTuningOutputs(inputId, BaseCompressionRatio), []);
    }

    private static List<OutputInfo> BuildMatrixExtractionOutputs(int matrixId) {
        int essenceId = matrixId switch {
            I电磁矩阵 => IFE电磁精华,
            I能量矩阵 => IFE能量精华,
            I结构矩阵 => IFE结构精华,
            I信息矩阵 => IFE信息精华,
            I引力矩阵 => IFE引力精华,
            I宇宙矩阵 => IFE宇宙精华,
            _ => IFE电磁精华,
        };
        return BuildOutputInfos([(1.0f, essenceId, 1.0f)]);
    }

    private static List<OutputInfo> BuildEssenceTuningOutputs(int inputId, float compressionRatio) {
        int inputLevel = GetMatrixEssenceLevel(inputId);
        if (inputLevel < 0) {
            return [];
        }

        List<(float ratio, int itemId, float count)> outputs = [];
        float remainingRatio = 1.0f - TicketSplitRatio;
        if (inputLevel < MatrixEssenceItemIds.Length - 1) {
            float ratio = inputLevel == 0 ? remainingRatio : remainingRatio * compressionRatio;
            AddOrMergeOutput(outputs, ratio, GetMatrixEssenceItemId(inputLevel + 1), 0.5f);
        }
        if (inputLevel > 0) {
            float ratio = inputLevel == MatrixEssenceItemIds.Length - 1
                ? remainingRatio
                : remainingRatio * (1.0f - compressionRatio);
            AddOrMergeOutput(outputs, ratio, GetMatrixEssenceItemId(inputLevel - 1), 2.0f);
        }
        if (inputLevel == 0 || inputLevel == MatrixEssenceItemIds.Length - 1) {
            AddOrMergeOutput(outputs, 1.0f - remainingRatio, IFE残片, GetMatrixEssenceFaceValue(inputId));
        } else {
            AddOrMergeOutput(outputs, TicketSplitRatio, IFE残片, GetMatrixEssenceFaceValue(inputId));
        }
        return BuildOutputInfos(outputs);
    }

    private static void AddOrMergeOutput(List<(float ratio, int itemId, float count)> outputs, float ratio, int itemId,
        float count) {
        if (ratio <= 0f || itemId <= 0) {
            return;
        }

        for (int i = 0; i < outputs.Count; i++) {
            if (outputs[i].itemId == itemId) {
                (float oldRatio, int oldItemId, float oldCount) = outputs[i];
                outputs[i] = (oldRatio + ratio, oldItemId, oldCount);
                return;
            }
        }
        outputs.Add((ratio, itemId, count));
    }

    private static List<OutputInfo> BuildOutputInfos(List<(float ratio, int itemId, float count)> specs) {
        List<OutputInfo> outputs = [];
        float totalRatio = 0f;
        foreach ((float ratio, int itemId, float count) in specs) {
            if (ratio <= 0f || itemId <= 0 || count <= 0f) {
                continue;
            }
            outputs.Add(new(ratio, itemId, count));
            totalRatio += ratio;
        }

        if (outputs.Count == 0 || totalRatio <= 0f) {
            outputs.Add(new(1.0f, IFE残片, 1.0f));
            return outputs;
        }

        if (totalRatio > 0f && totalRatio < 0.999f || totalRatio > 1.001f) {
            float scale = 1.0f / totalRatio;
            for (int i = 0; i < outputs.Count; i++) {
                OutputInfo info = outputs[i];
                outputs[i] = new OutputInfo(info.SuccessRatio * scale, info.OutputID, info.OutputCount);
            }
        }
        return outputs;
    }

    /// <summary>
    /// 获取该配方所属的分馏配方类型。
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Rectification;
    /// <summary>
    /// 获取精馏配方的具体类型。
    /// </summary>
    public RectificationRecipeKind Kind { get; }

    public bool SupportsTuningTarget(int itemId) {
        if (Kind != RectificationRecipeKind.EssenceTuning || itemId <= 0) {
            return false;
        }
        foreach (OutputInfo output in OutputMain) {
            if (output.OutputID == itemId) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 执行 RectificationRecipe 对应的分馏域操作。
    /// </summary>
    public RectificationRecipe(int inputID, RectificationRecipeKind kind, float baseSuccessRatio,
        List<OutputInfo> outputMain, List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) {
        Kind = kind;
    }

    /// <summary>
    /// 执行单次完整分馏结算并写回主产物、副产物和输入保留结果。
    /// </summary>
    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        FractionationOutcome outcome = RollRectificationOutputs(ref seed, pointsBonus, successBoost, fluidInputIncAvg,
            ref fluidInputInc, out inputChange, out ProductOutputInfo product);
        outputs = outcome == FractionationOutcome.Destroyed
            ? null
            : product == null
                ? ProcessManager.emptyOutputs
                : [product];
    }

    /// <summary>
    /// 执行单次轻量分馏结算，供运行热路径减少分配使用。
    /// </summary>
    public override FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        outputs.Clear();
        FractionationOutcome outcome = RollRectificationOutputs(ref seed, pointsBonus, successBoost, fluidInputIncAvg,
            ref fluidInputInc, out inputChange, out ProductOutputInfo product);
        if (product != null) {
            outputs.Add(product.isMainOutput, product.itemId, product.count);
        }
        return outcome;
    }

    /// <summary>
    /// 执行批量轻量分馏结算，供运行热路径合并多次处理。
    /// </summary>
    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        outputs.Clear();

        int destroyedCount = RollBinomialApprox(ref seed, batchCount, DestroyRatio);
        int aliveCount = batchCount - destroyedCount;
        float successRatio = SuccessRatio * (1 + pointsBonus) * (1 + successBoost);
        int successCount = RollBinomialApprox(ref seed, aliveCount, successRatio);
        int passThroughCount = aliveCount - successCount;
        int inputRemoveCount = destroyedCount + successCount + passThroughCount;
        OutputInfo directedOutput = GetDirectedOutputInfo();
        if (directedOutput != null) {
            AddRolledRectificationOutput(ref seed, outputs, directedOutput, successCount);
        } else {
            RollMainOutputs(ref seed, successCount, outputs);
        }

        fluidInputInc -= fluidInputIncAvg * inputRemoveCount;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        return new FractionationBatchResult {
            InputRemoveCount = inputRemoveCount,
            ConsumedRegisterCount = destroyedCount + successCount,
            SuccessCount = successCount,
            DestroyedCount = destroyedCount,
            PassThroughCount = passThroughCount,
            PassThroughInc = fluidInputIncAvg * passThroughCount,
        };
    }

    private FractionationOutcome RollRectificationOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out ProductOutputInfo product) {
        inputChange = -1;
        fluidInputInc -= fluidInputIncAvg;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }
        product = null;

        if (GetRandDouble(ref seed) < DestroyRatio) {
            return FractionationOutcome.Destroyed;
        }

        if (GetRandDouble(ref seed) >= SuccessRatio * (1 + pointsBonus) * (1 + successBoost)) {
            return FractionationOutcome.PassThrough;
        }

        OutputInfo outputInfo = GetDirectedOutputInfo() ?? RollMainOutputInfo(ref seed);
        int count = RollOutputCount(ref seed, GetRuntimeOutputCount(outputInfo));
        if (count <= 0) {
            return FractionationOutcome.Destroyed;
        }

        product = new ProductOutputInfo(true, outputInfo.OutputID, count);
        outputInfo.OutputTotalCount += count;
        return FractionationOutcome.Produced;
    }

    private OutputInfo RollMainOutputInfo(ref uint seed) {
        double ratio = GetRandDouble(ref seed);
        float ratioMain = 0.0f;
        foreach (OutputInfo outputInfo in OutputMain) {
            ratioMain += outputInfo.SuccessRatio;
            if (ratio <= ratioMain) {
                return outputInfo;
            }
        }
        return OutputMain[OutputMain.Count - 1];
    }

    private OutputInfo GetDirectedOutputInfo() {
        if (Kind != RectificationRecipeKind.EssenceTuning || CurrentTuningTargetId == 0) {
            return null;
        }
        foreach (OutputInfo outputInfo in OutputMain) {
            if (outputInfo.OutputID == CurrentTuningTargetId) {
                return outputInfo;
            }
        }
        return null;
    }

    private void RollMainOutputs(ref uint seed, int successCount, ProductOutputBuffer outputs) {
        int remainingMainCount = successCount;
        float remainingMainRatio = 1.0f;
        for (int i = 0; i < OutputMain.Count && remainingMainCount > 0; i++) {
            OutputInfo outputInfo = OutputMain[i];
            int outputHits = i == OutputMain.Count - 1
                ? remainingMainCount
                : RollBinomialApprox(ref seed, remainingMainCount, outputInfo.SuccessRatio / remainingMainRatio);
            remainingMainCount -= outputHits;
            remainingMainRatio -= outputInfo.SuccessRatio;
            if (remainingMainRatio <= 0f) {
                remainingMainRatio = 1.0f;
            }
            AddRolledRectificationOutput(ref seed, outputs, outputInfo, outputHits);
        }
    }

    /// <summary>
    /// 获取 DisplayOutputCount 对应的分馏域数据。
    /// </summary>
    public float GetDisplayOutputCount(OutputInfo outputInfo) {
        return GetRuntimeOutputCount(outputInfo);
    }

    private float GetRuntimeOutputCount(OutputInfo outputInfo) {
        if (Kind == RectificationRecipeKind.MatrixExtraction) {
            float matrixCount = outputInfo.OutputCount * RectificationTower.PlrRatio;
            return matrixCount < 0.0001f ? 0.0001f : matrixCount;
        }

        int inputLevel = GetMatrixEssenceLevel(InputID);
        int outputLevel = GetMatrixEssenceLevel(outputInfo.OutputID);
        float count = outputInfo.OutputCount;
        if (inputLevel >= 0 && outputLevel == inputLevel + 1) {
            count = CompressionOutputCount;
        } else if (inputLevel >= 0 && outputLevel == inputLevel - 1) {
            count = RefluxOutputCount;
        }
        return count < 0.0001f ? 0.0001f : count;
    }

    private void AddRolledRectificationOutput(ref uint seed, ProductOutputBuffer outputs, OutputInfo outputInfo,
        int outputHits) {
        if (outputHits <= 0) {
            return;
        }

        float outputCount = GetRuntimeOutputCount(outputInfo);
        int baseCount = (int)outputCount;
        float fractionalCount = outputCount - baseCount;
        int totalCount = outputHits * baseCount + RollBinomialApprox(ref seed, outputHits, fractionalCount);
        if (totalCount <= 0) {
            return;
        }

        outputs.Add(true, outputInfo.OutputID, totalCount);
        outputInfo.OutputTotalCount += totalCount;
    }
}
