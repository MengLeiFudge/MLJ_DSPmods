using System.Collections.Generic;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 矩阵入口与精馏链纯化配方的产出分布逻辑。
/// </summary>
public class RectificationRecipe : BaseRecipe {
    /// <summary>
    /// 区分精馏配方的入口类型和链式纯化类型。
    /// </summary>
    public enum RectificationRecipeKind {
        MatrixEntry,
        ChainPurification,
    }

    /// <summary>
    /// 定义精馏链纯化的基础相位压缩比例。
    /// </summary>
    public const float BaseHyperphaseRatio = 0.25f;
    /// <summary>
    /// 定义当前规划的最高相位压缩比例。
    /// </summary>
    public const float MaxPlannedHyperphaseRatio = 0.60f;
    private const float BranchDecay = 0.10f;
    private const float ChainPurificationOutputCount = 0.50f;

    private static readonly int[] MatrixInputs = [
        I电磁矩阵,
        I能量矩阵,
        I结构矩阵,
        I信息矩阵,
        I引力矩阵,
        I宇宙矩阵,
        I黑雾矩阵,
    ];

    /// <summary>
    /// 创建并注册该类型下的全部分馏配方。
    /// </summary>
    public static void CreateAll() {
        foreach (int matrixId in MatrixInputs) {
            AddRecipe(CreateMatrixEntry(matrixId));
        }

        foreach (int itemId in RectificationChainItemIds) {
            AddRecipe(CreateChainPurification(itemId));
        }
    }

    private static RectificationRecipe CreateMatrixEntry(int matrixId) {
        return new RectificationRecipe(matrixId, RectificationRecipeKind.MatrixEntry, 1.0f,
            BuildMatrixEntryOutputs(matrixId), []);
    }

    private static RectificationRecipe CreateChainPurification(int inputId) {
        return new RectificationRecipe(inputId, RectificationRecipeKind.ChainPurification, 1.0f,
            BuildChainPurificationOutputs(inputId, BaseHyperphaseRatio), []);
    }

    private static List<OutputInfo> BuildMatrixEntryOutputs(int matrixId) {
        (int mainLevel, int secondLevel, int thirdLevel, float mainRatio, float secondRatio) = matrixId switch {
            I电磁矩阵 => (0, 1, 8, 0.94f, 0.055f),
            I能量矩阵 => (2, 3, 8, 0.92f, 0.075f),
            I结构矩阵 => (4, 5, 8, 0.90f, 0.090f),
            I信息矩阵 => (6, 7, 8, 0.88f, 0.110f),
            I引力矩阵 => (8, 9, 10, 0.86f, 0.125f),
            I宇宙矩阵 => (8, 9, 10, 0.82f, 0.155f),
            I黑雾矩阵 => (5, 8, 9, 0.88f, 0.105f),
            _ => (0, 1, 8, 0.95f, 0.045f),
        };

        return BuildOutputInfos([
            (mainRatio, GetRectificationChainItemId(mainLevel), GetMatrixEntryOutputBaseCount(matrixId, mainLevel)),
            (secondRatio, GetRectificationChainItemId(secondLevel),
                GetMatrixEntryOutputBaseCount(matrixId, secondLevel)),
            (1.0f - mainRatio - secondRatio, GetRectificationChainItemId(thirdLevel),
                GetMatrixEntryOutputBaseCount(matrixId, thirdLevel)),
        ]);
    }

    private static float GetMatrixEntryOutputBaseCount(int matrixId, int outputLevel) {
        float baseYield = GetRectificationBaseFragmentYield(matrixId);
        float chainValue = itemValue[GetRectificationChainItemId(outputLevel)];
        if (chainValue <= 0f || chainValue >= maxValue) {
            return 1.0f;
        }

        float count = baseYield / chainValue;
        return count < 0.0001f ? 0.0001f : count;
    }

    private static List<OutputInfo> BuildChainPurificationOutputs(int inputId, float hyperphaseRatio) {
        int inputLevel = GetRectificationChainLevel(inputId);
        if (inputLevel < 0) {
            return [];
        }

        List<(float ratio, int itemId, float count)> outputs = [];
        AddDirectionalChainOutputs(outputs, inputLevel, hyperphaseRatio, +1);
        AddDirectionalChainOutputs(outputs, inputLevel, 1.0f - hyperphaseRatio, -1);
        return BuildOutputInfos(outputs);
    }

    private static void AddDirectionalChainOutputs(List<(float ratio, int itemId, float count)> outputs,
        int inputLevel, float directionRatio, int direction) {
        if (directionRatio <= 0f) {
            return;
        }

        float remaining = directionRatio;
        float branchRatio = directionRatio * (1.0f - BranchDecay);
        for (int delta = 1; delta < RectificationChainItemIds.Length && remaining > 0.000001f; delta++) {
            int targetLevel = inputLevel + direction * delta;
            bool isTail = targetLevel <= 0 || targetLevel >= RectificationChainItemIds.Length - 1;
            if (isTail) {
                AddOrMergeOutput(outputs, remaining, GetRectificationChainItemId(targetLevel),
                    ChainPurificationOutputCount);
                break;
            }

            AddOrMergeOutput(outputs, branchRatio, GetRectificationChainItemId(targetLevel),
                ChainPurificationOutputCount);
            remaining -= branchRatio;
            branchRatio *= BranchDecay;
        }
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
    /// 获取该配方在成长系统中的角色。
    /// </summary>
    public override ERecipeGrowthRole GrowthRole => ERecipeGrowthRole.SpecialGrowth;
    /// <summary>
    /// 获取精馏配方的具体类型。
    /// </summary>
    public RectificationRecipeKind Kind { get; }

    /// <summary>
    /// 获取该配方失败时输入物品损毁概率。
    /// </summary>
    public override float DestroyRatio => Kind == RectificationRecipeKind.ChainPurification
        ? 0.0f
        : base.DestroyRatio;

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
        int successCount = batchCount - destroyedCount;
        int inputRemoveCount = batchCount;
        RollMainOutputs(ref seed, successCount, outputs);

        fluidInputInc -= fluidInputIncAvg * inputRemoveCount;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        return new FractionationBatchResult {
            InputRemoveCount = inputRemoveCount,
            ConsumedRegisterCount = batchCount,
            SuccessCount = successCount,
            DestroyedCount = destroyedCount,
            PassThroughCount = 0,
            PassThroughInc = 0,
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

        OutputInfo outputInfo = RollMainOutputInfo(ref seed);
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
        if (Kind != RectificationRecipeKind.MatrixEntry) {
            return outputInfo.OutputCount;
        }
        float count = outputInfo.OutputCount * GetStageDecayFactor(InputID) * RectificationTower.PlrRatio;
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

    #region IModCanSave

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public override void Import(BinaryReader r) {
        base.Import(r);
        r.ReadBlocks();
    }

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.WriteBlocks();
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public override void IntoOtherSave() {
        base.IntoOtherSave();
    }

    #endregion
}
