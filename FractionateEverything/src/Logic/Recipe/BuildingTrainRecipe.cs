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
        Create(IFE交互塔原胚, 0.01f);
        Create(IFE矿物复制塔原胚, 0.01f);
        Create(IFE点数聚集塔原胚, 0.01f);
        Create(IFE转化塔原胚, 0.01f);
        Create(IFE回收塔原胚, 0.01f);
    }

    /// <summary>
    /// 添加一个建筑培养配方
    /// </summary>
    private static void Create(int inputID, float maxSuccessRatio) {
        AddRecipe(new BuildingTrainRecipe(inputID, maxSuccessRatio, [
            new(0.96f, inputID + (IFE交互塔 - IFE交互塔原胚), 1),
            new(0.04f, IFE分馏塔定向原胚, 1),
        ], []));
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
