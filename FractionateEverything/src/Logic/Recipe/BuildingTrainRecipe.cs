using System.Collections.Generic;
using System.IO;
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
        for (int fracProtoID = IFE交互塔原胚; fracProtoID <= IFE回收塔原胚; fracProtoID++) {
            int buildingID = IFE交互塔 + fracProtoID - IFE交互塔原胚;
            AddRecipe(new BuildingTrainRecipe(fracProtoID, 0.05f, [
                new(0.96f, buildingID, 1),
                new(0.04f, IFE分馏塔定向原胚, 1),
            ], []));
            AddRecipe(new BuildingTrainRecipe(buildingID, 0.05f, [
                new(0.96f, fracProtoID, 1),
                new(0.04f, IFE分馏塔定向原胚, 1),
            ], []));
        }
    }

    /// <summary>
    /// 配方类型
    /// </summary>
    public override ERecipe RecipeType => ERecipe.BuildingTrain;

    /// <summary>
    /// 创建建筑培养配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRatio">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    public BuildingTrainRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }

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
