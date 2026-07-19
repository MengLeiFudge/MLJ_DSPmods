using System.Collections.Generic;
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
                new(1.0f, buildingID, 1),
            ], []));
            AddRecipe(new BuildingTrainRecipe(buildingID, 0.05f, [
                new(1.0f, fracProtoID, 1),
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
    /// <summary>
    /// 初始化 BuildingTrainRecipe 的新实例。
    /// </summary>
    public BuildingTrainRecipe(int inputID, float baseSuccessRatio, List<OutputInfo> outputMain,
        List<OutputInfo> outputAppend)
        : base(inputID, baseSuccessRatio, outputMain, outputAppend) { }
}
