using System.Collections.Generic;
using System.IO;
using FE.Logic.Building;
using FE.Logic.Manager;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

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
            var recipe = new RectificationRecipe(matrixId, 1.0f,
                [new(1.0f, IFE残片, fragmentCount)],
                []);
            recipe.Level = 0;
            AddRecipe(recipe);
        }
    }

    public override ERecipe RecipeType => ERecipe.Rectification;

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
