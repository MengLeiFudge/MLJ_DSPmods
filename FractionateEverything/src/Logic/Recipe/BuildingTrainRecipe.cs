using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 建筑培养配方（分馏塔原胚 -> 分馏塔）
/// </summary>
public class BuildingTrainRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有建筑培养配方
    /// </summary>
    public static void CreateAll() {
        Create(IFE分馏塔原胚I型, 0.01f);
        Create(IFE分馏塔原胚II型, 0.01f);
        Create(IFE分馏塔原胚III型, 0.01f);
        Create(IFE分馏塔原胚IV型, 0.01f);
    }

    /// <summary>
    /// 添加一个建筑培养配方
    /// </summary>
    private static void Create(int inputID, float maxSuccessRate) {
        float[] ratioArr = inputID switch {
            IFE分馏塔原胚I型 => [0.96f, 0.00f, 0.00f, 0.00f, 0.04f],
            IFE分馏塔原胚II型 => [0.00f, 0.96f, 0.00f, 0.00f, 0.04f],
            IFE分馏塔原胚III型 => [0.00f, 0.00f, 0.96f, 0.00f, 0.04f],
            IFE分馏塔原胚IV型 => [0.00f, 0.00f, 0.00f, 0.96f, 0.04f],
            _ => null
        };
        if (ratioArr == null) {
            return;
        }
        float sum = ratioArr.Sum();
        List<OutputInfo> OutputMain = [
            new(ratioArr[0] / sum, IFE交互塔, 1),
            new(ratioArr[1] / sum, IFE矿物复制塔, 1),
            new(ratioArr[2] / sum, IFE点数聚集塔, 1),
            new(ratioArr[3] / sum, IFE转化塔, 1),
            new(ratioArr[4] / sum, IFE分馏塔定向原胚, 1),
        ];
        OutputMain.RemoveAll(info => info.SuccessRate <= 0);
        AddRecipe(new BuildingTrainRecipe(inputID, maxSuccessRate, OutputMain, []));
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.BuildingTrain;

    /// <summary>
    /// 创建建筑培养配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRate">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public BuildingTrainRecipe(int inputID, float baseSuccessRate, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRate, outputMain, outputAppend) { }

    #region IModCanSave

    public override void Import(BinaryReader r) {
        base.Import(r);
        int version = r.ReadInt32();
    }

    public override void Export(BinaryWriter w) {
        base.Export(w);
        w.Write(1);
    }

    public override void IntoOtherSave() {
        base.IntoOtherSave();
    }

    #endregion
}
