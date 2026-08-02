using System;
using System.Collections.Generic;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 定义解析塔的矩阵/样本 Frag-Memory 结算与通用原胚谱系分化配方。
/// </summary>
public sealed class AnalysisRecipe : BaseRecipe {
    /// <summary>
    /// 区分矩阵解析、黑雾样本解析与原胚谱系分化行为。
    /// </summary>
    public enum AnalysisRecipeKind {
        MatrixAnalysis,
        SampleAnalysis,
        LineageDifferentiation,
    }

    /// <summary>
    /// 创建全部有效矩阵、黑雾样本 A/B/C 与通用原胚谱系分化配方。
    /// </summary>
    public static void CreateAll() {
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item == null || item.Type != EItemType.Matrix) {
                continue;
            }
            if (!TryGetMatrixFragYield(item.ID, out int fragYield)) {
                continue;
            }
            AddRecipe(new AnalysisRecipe(item.ID, AnalysisRecipeKind.MatrixAnalysis, 0.05f,
                [new(1.0f, IFE残片, fragYield)], [], fragYield, 0));
        }

        AddSampleRecipe(IFE黑雾样本A, 4);
        AddSampleRecipe(IFE黑雾样本B, 8);
        AddSampleRecipe(IFE黑雾样本C, 16);

        AddRecipe(new AnalysisRecipe(IFE通用原胚, AnalysisRecipeKind.LineageDifferentiation, 0.05f, [
            new(0.25f, IFE培养塔原胚, 1),
            new(0.25f, IFE解析塔原胚, 1),
            new(0.25f, IFE复制塔原胚, 1),
            new(0.25f, IFE转化塔原胚, 1),
        ], [], 0, 0));
    }

    private static void AddSampleRecipe(int sampleItemId, int sampleBaseFrag) {
        AddRecipe(new AnalysisRecipe(sampleItemId, AnalysisRecipeKind.SampleAnalysis, 0.05f,
            [new(1.0f, IFE残片, sampleBaseFrag)], [], 0, sampleBaseFrag));
    }

    /// <summary>
    /// 按 itemValue 四舍五入计算矩阵单次成功 Frag；不可估值输入返回 false。
    /// </summary>
    public static bool TryGetMatrixFragYield(int matrixId, out int fragYield) {
        fragYield = 0;
        if (matrixId <= 0 || matrixId >= itemValue.Length) {
            return false;
        }

        float value = itemValue[matrixId];
        if (float.IsNaN(value) || float.IsInfinity(value) || value >= maxValue || value < 0f) {
            return false;
        }

        double rounded = Math.Floor(value + 0.5d);
        // 超过 int 可表示范围时跳过，不得错误降为 1。
        if (double.IsNaN(rounded) || double.IsInfinity(rounded) || rounded > int.MaxValue) {
            fragYield = 0;
            return false;
        }

        fragYield = Math.Max(1, (int)rounded);
        return true;
    }

    /// <summary>
    /// 按样本基础值与双认知等级计算 Frag：base + base/4*G + base/4*S。
    /// </summary>
    public static int GetSampleFragYield(int sampleBaseFrag, int groundRank, int spaceRank) {
        int g = Math.Max(0, Math.Min(4, groundRank));
        int s = Math.Max(0, Math.Min(4, spaceRank));
        int unit = sampleBaseFrag / 4;
        return sampleBaseFrag + unit * g + unit * s;
    }

    /// <summary>
    /// 获取文明解析配方类型。
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Analysis;

    /// <summary>
    /// 矩阵/样本解析与原胚谱系分化均为基础设施，不进入付费协议目录。
    /// </summary>
    public override bool RequiresProtocolRecovery => false;

    /// <summary>
    /// 获取该实例对应的解析塔行为类型。
    /// </summary>
    public AnalysisRecipeKind Kind { get; }

    /// <summary>
    /// 矩阵解析的固定单次 Frag 产量。
    /// </summary>
    public int MatrixFragYield { get; }

    /// <summary>
    /// 黑雾样本的基础 Frag（4/8/16）。
    /// </summary>
    public int SampleBaseFrag { get; }

    /// <summary>
    /// 初始化一项矩阵解析、样本解析或谱系分化配方。
    /// </summary>
    public AnalysisRecipe(int inputId, AnalysisRecipeKind kind, float baseSuccessRatio,
        List<OutputInfo> outputMain, List<OutputInfo> outputAppend, int matrixFragYield, int sampleBaseFrag)
        : base(inputId, baseSuccessRatio, outputMain, outputAppend) {
        Kind = kind;
        MatrixFragYield = matrixFragYield;
        SampleBaseFrag = sampleBaseFrag;
    }

    /// <summary>
    /// 执行单次解析结算；货币配方输出十进制打包后的 Frag/Memory。
    /// </summary>
    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        if (Kind == AnalysisRecipeKind.LineageDifferentiation) {
            FractionationOutcome outcome = RollLineageOutputs(ref seed, pointsBonus, successBoost, fluidInputIncAvg,
                ref fluidInputInc, out inputChange, out ProductOutputInfo product);
            outputs = outcome == FractionationOutcome.Destroyed
                ? null
                : product == null
                    ? ProcessManager.emptyOutputs
                    : [product];
            return;
        }

        FractionationOutcome currencyOutcome = RollCurrencySuccess(ref seed, pointsBonus, successBoost,
            fluidInputIncAvg, ref fluidInputInc, out inputChange);
        if (currencyOutcome == FractionationOutcome.Destroyed) {
            outputs = null;
            return;
        }
        if (currencyOutcome == FractionationOutcome.PassThrough) {
            outputs = ProcessManager.emptyOutputs;
            return;
        }

        List<ProductOutputInfo> list = [];
        EmitCurrencyOutputs(1, list);
        outputs = list.Count > 0 ? list : null;
    }

    /// <summary>
    /// 执行单次轻量解析结算。
    /// </summary>
    public override FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        outputs.Clear();
        if (Kind == AnalysisRecipeKind.LineageDifferentiation) {
            FractionationOutcome outcome = RollLineageOutputs(ref seed, pointsBonus, successBoost, fluidInputIncAvg,
                ref fluidInputInc, out inputChange, out ProductOutputInfo product);
            if (product != null) {
                outputs.Add(product.isMainOutput, product.itemId, product.count);
            }
            return outcome;
        }

        FractionationOutcome currencyOutcome = RollCurrencySuccess(ref seed, pointsBonus, successBoost,
            fluidInputIncAvg, ref fluidInputInc, out inputChange);
        if (currencyOutcome == FractionationOutcome.Produced) {
            EmitCurrencyOutputs(1, outputs);
            if (outputs.Count <= 0) {
                return FractionationOutcome.Destroyed;
            }
        }
        return currencyOutcome;
    }

    /// <summary>
    /// 执行批量轻量解析结算；货币总量与逐次路径等价。
    /// </summary>
    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        outputs.Clear();
        int destroyedCount = RollBinomialApprox(ref seed, batchCount, DestroyRatio);
        int aliveCount = batchCount - destroyedCount;
        int successCount = RollBinomialApprox(ref seed, aliveCount,
            SuccessRatio * (1 + pointsBonus) * (1 + successBoost));
        int passThroughCount = aliveCount - successCount;

        if (Kind == AnalysisRecipeKind.LineageDifferentiation) {
            OutputInfo directedOutput = GetDirectedOutputInfo();
            if (directedOutput != null) {
                AddRolledOutput(ref seed, outputs, directedOutput, true, successCount);
            } else {
                RollMainOutputs(ref seed, successCount, outputs);
            }
        } else if (successCount > 0) {
            EmitCurrencyOutputs(successCount, outputs);
            if (outputs.Count <= 0) {
                destroyedCount += successCount;
                successCount = 0;
            }
        }

        int inputRemoveCount = destroyedCount + successCount + passThroughCount;
        fluidInputInc = Math.Max(0, fluidInputInc - fluidInputIncAvg * inputRemoveCount);
        return new FractionationBatchResult {
            InputRemoveCount = inputRemoveCount,
            ConsumedRegisterCount = destroyedCount + successCount,
            SuccessCount = successCount,
            DestroyedCount = destroyedCount,
            PassThroughCount = passThroughCount,
            PassThroughInc = fluidInputIncAvg * passThroughCount,
        };
    }

    /// <summary>
    /// 读取当前配方一次成功应产出的基础 Frag。
    /// </summary>
    public int ResolveFragYield() {
        if (Kind == AnalysisRecipeKind.MatrixAnalysis) {
            return Math.Max(1, MatrixFragYield);
        }
        if (Kind == AnalysisRecipeKind.SampleAnalysis) {
            return GetSampleFragYield(SampleBaseFrag, DarkFogCognitionCache.GroundRank,
                DarkFogCognitionCache.SpaceRank);
        }
        return 0;
    }

    private void EmitCurrencyOutputs(int successCount, ProductOutputBuffer outputs) {
        if (successCount <= 0) {
            return;
        }

        long fragTotal = (long)ResolveFragYield() * successCount;
        if (fragTotal <= 0) {
            return;
        }

        TrackMainFrag(fragTotal);
        long memoryYield = MemoryResidueStore.AddFragAndTakeMemory(fragTotal);
        CivilizationCurrencyPacker.PackFrag(fragTotal, outputs, true);
        if (memoryYield > 0) {
            CivilizationCurrencyPacker.PackMemory(memoryYield, outputs, false);
        }
    }

    private void EmitCurrencyOutputs(int successCount, List<ProductOutputInfo> outputs) {
        if (successCount <= 0) {
            return;
        }

        long fragTotal = (long)ResolveFragYield() * successCount;
        if (fragTotal <= 0) {
            return;
        }

        TrackMainFrag(fragTotal);
        long memoryYield = MemoryResidueStore.AddFragAndTakeMemory(fragTotal);
        CivilizationCurrencyPacker.PackFrag(fragTotal, outputs, true);
        if (memoryYield > 0) {
            CivilizationCurrencyPacker.PackMemory(memoryYield, outputs, false);
        }
    }

    private void TrackMainFrag(long fragTotal) {
        if (OutputMain == null || OutputMain.Count == 0 || fragTotal <= 0) {
            return;
        }
        int tracked = fragTotal > int.MaxValue ? int.MaxValue : (int)fragTotal;
        OutputMain[0].AddOutputCount(tracked);
    }

    private FractionationOutcome RollCurrencySuccess(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange) {
        inputChange = -1;
        fluidInputInc = Math.Max(0, fluidInputInc - fluidInputIncAvg);
        if (GetRandDouble(ref seed) < DestroyRatio) {
            return FractionationOutcome.Destroyed;
        }
        if (GetRandDouble(ref seed) >= SuccessRatio * (1 + pointsBonus) * (1 + successBoost)) {
            return FractionationOutcome.PassThrough;
        }
        return FractionationOutcome.Produced;
    }

    private FractionationOutcome RollLineageOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out ProductOutputInfo product) {
        inputChange = -1;
        fluidInputInc = Math.Max(0, fluidInputInc - fluidInputIncAvg);
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
        output.AddOutputCount(count);
        product = new ProductOutputInfo(true, output.OutputID, count);
        return FractionationOutcome.Produced;
    }

    private OutputInfo GetDirectedOutputInfo() {
        if (Kind != AnalysisRecipeKind.LineageDifferentiation || CurrentMainOutputTargetId == 0) {
            return null;
        }
        return OutputMain.Find(output => output.OutputID == CurrentMainOutputTargetId);
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
}
