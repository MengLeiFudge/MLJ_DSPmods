using System.Collections.Generic;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 矩阵精馏配方的产出分布逻辑。
/// </summary>
public class RectificationRecipe : BaseRecipe {
    private static readonly int[] MatrixInputs = [
        I电磁矩阵,
        I能量矩阵,
        I结构矩阵,
        I信息矩阵,
        I引力矩阵,
        I宇宙矩阵,
        I黑雾矩阵,
    ];

    public static void CreateAll() {
        foreach (int matrixId in MatrixInputs) {
            int fragmentCount = GetRectificationBaseFragmentYield(matrixId);
            AddRecipe(new RectificationRecipe(matrixId, 1.0f,
                [new(1.0f, IFE残片, fragmentCount)],
                []));
        }
    }

    public override ERecipe RecipeType => ERecipe.Rectification;
    public override ERecipeGrowthRole GrowthRole => ERecipeGrowthRole.SpecialGrowth;

    public RectificationRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }

    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        inputChange = -1;
        fluidInputInc -= fluidInputIncAvg;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        int fragmentCount = GetRectificationFragmentYield(InputID, RectificationTower.PlrRatio);
        outputs = [new(true, IFE残片, fragmentCount)];
    }

    public override FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        outputs.Clear();
        inputChange = -1;
        fluidInputInc -= fluidInputIncAvg;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        int fragmentCount = GetRectificationFragmentYield(InputID, RectificationTower.PlrRatio);
        outputs.Add(true, IFE残片, fragmentCount);
        return FractionationOutcome.Produced;
    }

    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        outputs.Clear();
        fluidInputInc -= fluidInputIncAvg * batchCount;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }

        int fragmentCount = GetRectificationFragmentYield(InputID, RectificationTower.PlrRatio);
        outputs.Add(true, IFE残片, fragmentCount * batchCount);
        int memoryCount = RollMemoryOutputs(ref seed, batchCount, fluidInputIncAvg);
        outputs.Add(false, IFE记忆源点, memoryCount);

        return new FractionationBatchResult {
            InputRemoveCount = batchCount,
            ConsumedRegisterCount = batchCount,
            SuccessCount = batchCount,
            DestroyedCount = 0,
            PassThroughCount = 0,
        };
    }

    private int RollMemoryOutputs(ref uint seed, int batchCount, int fluidInputIncAvg) {
        if (!RectificationTower.EnableHyperphaseCompression
            || batchCount <= 0
            || InputID != GetCurrentProgressMatrixId() && InputID != I黑雾矩阵) {
            return 0;
        }

        float probability = GetMemoryYieldChance(fluidInputIncAvg);
        return RollBinomialApprox(ref seed, batchCount, probability);
    }

    private static float GetMemoryYieldChance(int fluidInputIncAvg) {
        // Level 12 后才允许极低频萃取 Memory。Level 6 特质不再额外增加残片件数，
        // 而是要求使用增产点数来提高萃取进度，避免继续放大残片输出带宽。
        float chance = 0.0025f;
        if (RectificationTower.EnableAfterglowExtraction && fluidInputIncAvg >= 4) {
            chance += 0.0015f;
        }
        if (fluidInputIncAvg >= 8) {
            chance += 0.001f;
        }
        return chance;
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
