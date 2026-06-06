using System.Collections.Generic;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 建筑培养配方（分馏塔原胚 -> 分馏塔）
/// </summary>
public class BuildingTrainRecipe : BaseRecipe {
    /// <summary>
    /// 添加所有建筑培养配方
    /// </summary>
    public static void CreateAll() {
        foreach (int fracProtoID in FractionatorTowerCatalog.ActiveFractionatorProtoIds) {
            if (!FractionatorTowerCatalog.TryGetBuildingIdForProto(fracProtoID, out int buildingID)) {
                continue;
            }
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
    /// 获取该配方在成长系统中的角色。
    /// </summary>
    public override ERecipeGrowthRole GrowthRole => ERecipeGrowthRole.ToolUnlock;

    /// <summary>
    /// 创建建筑培养配方实例
    /// </summary>
    /// <param name="inputID">输入物品ID</param>
    /// <param name="baseSuccessRatio">最大成功率</param>
    /// <param name="outputMain">主输出物品</param>
    /// <param name="outputAppend">附加输出物品</param>
    /// <summary>
    /// 初始化 BuildingTrainRecipe 的新实例。
    /// </summary>
    public BuildingTrainRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }

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
