using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;
using FE.Logic.Station;
using static FE.Utils.Utils;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 点数聚集配方
/// </summary>
public class PointAggregateRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有点数聚集配方
    /// </summary>
    public static void CreateAll() {
        foreach (ItemProto item in LDB.items.dataArray) {
            PointAggregateRecipe recipe = new(item.ID, 0.25f, [new(1.0f, item.ID, 1)], []);
            AddRecipe(recipe);
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.PointAggregate;
    /// <summary>
    /// 获取该配方在成长系统中的角色。
    /// </summary>
    public override ERecipeGrowthRole GrowthRole => ERecipeGrowthRole.SpecialGrowth;

    /// <summary>
    /// 创建点数聚集配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRatio">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    /// <summary>
    /// 初始化 PointAggregateRecipe 的新实例。
    /// </summary>
    public PointAggregateRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }

    /// <summary>
    /// 执行单次完整分馏结算并写回主产物、副产物和输入保留结果。
    /// </summary>
    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {

        if (GetRandDouble(ref seed) < GetCandidateSuccessRatio(successBoost)
            && TryPaySuccessInc(ref fluidInputInc)) {
            inputChange = -1;
            outputs = [new(true, InputID, 1)];
            return;
        }

        TakePassThroughInc(fluidInputIncAvg, ref fluidInputInc);
        inputChange = -1;
        outputs = ProcessManager.emptyOutputs;
    }

    /// <summary>
    /// 执行单次轻量分馏结算，供运行热路径减少分配使用。
    /// </summary>
    public override FractionationOutcome GetOutputsFast(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, ProductOutputBuffer outputs) {
        outputs.Clear();

        if (GetRandDouble(ref seed) < GetCandidateSuccessRatio(successBoost)
            && TryPaySuccessInc(ref fluidInputInc)) {
            inputChange = -1;
            outputs.Add(true, InputID, 1);
            return FractionationOutcome.Produced;
        }

        TakePassThroughInc(fluidInputIncAvg, ref fluidInputInc);
        inputChange = -1;
        return FractionationOutcome.PassThrough;
    }

    /// <summary>
    /// 执行批量轻量分馏结算，供运行热路径合并多次处理。
    /// </summary>
    public override FractionationBatchResult GetOutputsBatchFast(ref uint seed, float pointsBonus, float successBoost,
        int batchCount, int fluidInputIncAvg, ref int fluidInputInc, ProductOutputBuffer outputs) {
        outputs.Clear();

        int candidateSuccessCount = RollBinomialApprox(ref seed, batchCount, GetCandidateSuccessRatio(successBoost));
        int batchInputInc = TakeBatchInputInc(batchCount, fluidInputIncAvg, ref fluidInputInc);
        int successCount = GetPayableSuccessCount(candidateSuccessCount, batchInputInc, out int usedInputInc,
            out int usedPoolInc);
        if (usedPoolInc > 0 && !ProliferatorPool.TryConsumeInc(usedPoolInc)) {
            successCount = GetPayableSuccessCount(candidateSuccessCount, batchInputInc, 0, out usedInputInc,
                out usedPoolInc);
        }
        if (successCount > 0) {
            outputs.Add(true, InputID, successCount);
        }

        int passThroughCount = batchCount - successCount;
        return new FractionationBatchResult {
            InputRemoveCount = batchCount,
            ConsumedRegisterCount = successCount,
            SuccessCount = successCount,
            DestroyedCount = 0,
            PassThroughCount = passThroughCount,
            PassThroughInc = batchInputInc - usedInputInc,
        };
    }

    /// <summary>
    /// 返回指定输出物品应携带的增产点数。
    /// </summary>
    public override byte GetOutputInc(int itemId) => (byte)PointAggregateTower.MaxInc;

    private float GetCandidateSuccessRatio(float successBoost) {
        float ratio = SuccessRatio * (1 + successBoost);
        return ratio > 0f ? ratio : 0f;
    }

    private static int TakeBatchInputInc(int batchCount, int fluidInputIncAvg, ref int fluidInputInc) {
        int batchInputInc = Math.Min(Math.Max(0, fluidInputIncAvg) * batchCount, Math.Max(0, fluidInputInc));
        fluidInputInc -= batchInputInc;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }
        return batchInputInc;
    }

    private static void TakePassThroughInc(int fluidInputIncAvg, ref int fluidInputInc) {
        int inputInc = Math.Min(Math.Max(0, fluidInputIncAvg), Math.Max(0, fluidInputInc));
        fluidInputInc -= inputInc;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }
    }

    private static bool TryPaySuccessInc(ref int fluidInputInc) {
        int targetInc = PointAggregateTower.MaxInc;
        int inputInc = Math.Min(Math.Max(0, fluidInputInc), targetInc);
        int poolInc = targetInc - inputInc;
        if (poolInc == 0) {
            fluidInputInc -= inputInc;
            return true;
        }
        if (!PointAggregateTower.EnableVoidAggregation) {
            return false;
        }
        if (!ProliferatorPool.TryConsumeInc(poolInc)) {
            return false;
        }
        fluidInputInc -= inputInc;
        if (fluidInputInc < 0) {
            fluidInputInc = 0;
        }
        return true;
    }

    private static int GetPayableSuccessCount(int candidateSuccessCount, int batchInputInc, out int usedInputInc,
        out int usedPoolInc) {
        int targetInc = PointAggregateTower.MaxInc;
        int requiredInc = candidateSuccessCount * targetInc;
        int poolNeed = Math.Max(0, requiredInc - batchInputInc);
        int poolAvailable = PointAggregateTower.EnableVoidAggregation
            ? Math.Min(ProliferatorPool.GetAvailableInc(), poolNeed)
            : 0;
        return GetPayableSuccessCount(candidateSuccessCount, batchInputInc, poolAvailable, out usedInputInc,
            out usedPoolInc);
    }

    private static int GetPayableSuccessCount(int candidateSuccessCount, int batchInputInc, int poolAvailable,
        out int usedInputInc, out int usedPoolInc) {
        int targetInc = PointAggregateTower.MaxInc;
        int successCount = Math.Min(candidateSuccessCount, (batchInputInc + poolAvailable) / targetInc);
        int outputInc = successCount * targetInc;
        usedInputInc = Math.Min(batchInputInc, outputInc);
        usedPoolInc = outputInc - usedInputInc;
        return successCount;
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
