using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Building;
using FE.Logic.Manager;
using static FE.Utils.Utils;
using static FE.Logic.Manager.RecipeManager;

namespace FE.Logic.Recipe;

/// <summary>
/// 点数聚集配方
/// </summary>
public class PointAggregateRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有点数聚集配方
    /// </summary>
    public static void CreateAll() {
        foreach (ItemProto item in LDB.items.dataArray) {
            PointAggregateRecipe recipe = new(item.ID, 0.2f, [new(1.0f, item.ID, 1)], []) {
                Level = 10,
            };
            AddRecipe(recipe);
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.PointAggregate;
    public override ERecipeGrowthRole GrowthRole => ERecipeGrowthRole.SpecialGrowth;

    /// <summary>
    /// 创建点数聚集配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRatio">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public PointAggregateRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }

    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {

        // 点数聚集逻辑：如果平均增产等级足够，则有概率聚集成功
        float ratio = fluidInputIncAvg / 10.0f * SuccessRatio * (1 + successBoost);

        if (GetRandDouble(ref seed) < ratio) {
            // 成功聚集：双重点数仍然更强，但不再直接把点数消耗砍半，避免 12 级形成压倒性最优。
            inputChange = -1;
            outputs = [new(true, InputID, 1)];
            fluidInputInc -= PointAggregateTower.EnableDoublePoints
                ? Math.Max(1, (PointAggregateTower.MaxInc * 7 + 9) / 10)
                : PointAggregateTower.MaxInc;
            return;
        }

        // 失败：直通
        inputChange = -1;
        outputs = ProcessManager.emptyOutputs;
        fluidInputInc -= fluidInputIncAvg;
    }

    public override byte GetOutputInc(int itemId) => (byte)PointAggregateTower.MaxInc;

    #region IModCanSave

    public override void Import(BinaryReader r) {
        base.Import(r);
        r.ReadBlocks();
        Level = 10;
    }

    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.WriteBlocks();
    }

    public override void IntoOtherSave() {
        base.IntoOtherSave();
        Level = 10;
    }

    #endregion
}
