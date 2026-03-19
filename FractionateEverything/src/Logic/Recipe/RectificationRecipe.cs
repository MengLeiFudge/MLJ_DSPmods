using System.Collections.Generic;
using System.IO;
using FE.Logic.Manager;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

public class RectificationRecipe : BaseRecipe {
    public static void CreateAll() {
        foreach (ItemProto item in LDB.items.dataArray) {
            if (item.ID <= 0 || item.ID >= 12000) continue;
            int matrixID = itemToMatrix[item.ID];
            if (matrixID <= 0) continue;
            int ticketID = matrixID switch {
                var m when m == I电磁矩阵 => IFE电磁奖券,
                var m when m == I能量矩阵 => IFE能量奖券,
                var m when m == I结构矩阵 => IFE结构奖券,
                var m when m == I信息矩阵 => IFE信息奖券,
                var m when m == I引力矩阵 => IFE引力奖券,
                var m when m == I宇宙矩阵 => IFE宇宙奖券,
                var m when m == I黑雾矩阵 => IFE黑雾奖券,
                _ => 0
            };
            if (ticketID == 0) continue;
            AddRecipe(new RectificationRecipe(item.ID, 0.05f,
                [new(1.0f, ticketID, 1)],
                []));
        }
    }

    public override ERecipe RecipeType => ERecipe.Rectification;

    public RectificationRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }

    public override void GetOutputs(ref uint seed, float pointsBonus, float successBoost,
        int fluidInputIncAvg, ref int fluidInputInc, out int inputChange, out List<ProductOutputInfo> outputs) {
        base.GetOutputs(ref seed, pointsBonus, successBoost,
            fluidInputIncAvg, ref fluidInputInc, out inputChange, out outputs);

        if (outputs == null || outputs.Count == 0) return;

        int lvl = Level < 0 ? 0 : Level;

        float essenceChance = 0.05f + lvl * 0.01f;
        if (GetRandDouble(ref seed) < essenceChance) {
            int essenceID = (int)(GetRandDouble(ref seed) * 4) switch {
                0 => IFE速度精华,
                1 => IFE产能精华,
                2 => IFE节能精华,
                _ => IFE增产精华
            };
            outputs.Add(new(false, essenceID, 1));
        }

        if (lvl >= 7) {
            float coreChance = 0.005f + (lvl - 7) * 0.005f;
            if (GetRandDouble(ref seed) < coreChance) {
                outputs.Add(new(false, IFE分馏配方核心, 1));
            }
        }
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
