using System.Collections.Generic;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 定义通用原胚随机孵化、专属原胚稳定培养和实体塔返祖配方。
/// </summary>
public sealed class BuildingTrainRecipe : BaseRecipe {
    /// <summary>
    /// 区分建筑恢复链中的三类配方行为。
    /// </summary>
    public enum BuildingTrainRecipeKind {
        CommonIncubation,
        DirectedCultivation,
        Atavism,
    }

    /// <summary>
    /// 返祖协议进入检索池前需要完成的对应塔型培养次数基线。
    /// </summary>
    public const int AtavismEligibilityCultivationCount = 20;

    /// <summary>
    /// 创建通用孵化、四种专属培养和四种返祖配方。
    /// </summary>
    public static void CreateAll() {
        AddRecipe(new BuildingTrainRecipe(IFE通用原胚, BuildingTrainRecipeKind.CommonIncubation, 0.05f, [
            new(0.25f, IFE交互塔, 1),
            new(0.25f, IFE解析塔, 1),
            new(0.25f, IFE资源塔, 1),
            new(0.25f, IFE转化塔, 1),
        ], []));

        foreach (int protoId in FractionatorTowerCatalog.ActiveFractionatorProtoIds) {
            if (!FractionatorTowerCatalog.TryGetBuildingIdForProto(protoId, out int buildingId)) {
                continue;
            }

            AddRecipe(new BuildingTrainRecipe(protoId, BuildingTrainRecipeKind.DirectedCultivation, 0.05f, [
                new(1.0f, buildingId, 1),
            ], []));
            AddRecipe(new BuildingTrainRecipe(buildingId, BuildingTrainRecipeKind.Atavism, 0.05f, [
                new(1.0f, protoId, 1),
            ], []));
        }
    }

    /// <summary>
    /// 获取建筑培养配方类型。
    /// </summary>
    public override ERecipe RecipeType => ERecipe.BuildingTrain;

    /// <summary>
    /// 只有实体塔返祖需要先恢复文明协议。
    /// </summary>
    public override bool RequiresProtocolRecovery => Kind == BuildingTrainRecipeKind.Atavism;

    /// <summary>
    /// 建筑恢复协议固定归入电磁阶段。
    /// </summary>
    public override int ProtocolStageOrder => 0;

    /// <summary>
    /// 返祖是后期可选协议，不阻塞电磁阶段主线完成度。
    /// </summary>
    public override bool CountsTowardStageCompletion => Kind != BuildingTrainRecipeKind.Atavism;

    /// <summary>
    /// 返祖协议只有在对应专属原胚完成足够培养后才具备检索资格。
    /// </summary>
    public override bool IsProtocolEligible {
        get {
            if (Kind != BuildingTrainRecipeKind.Atavism || OutputMain.Count == 0) {
                return true;
            }

            BuildingTrainRecipe cultivation = GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain,
                OutputMain[0].OutputID);
            return cultivation?.Kind == BuildingTrainRecipeKind.DirectedCultivation
                   && cultivation.TotalSuccessCount >= AtavismEligibilityCultivationCount;
        }
    }

    /// <summary>
    /// 获取该实例在建筑恢复链中的行为类型。
    /// </summary>
    public BuildingTrainRecipeKind Kind { get; }

    /// <summary>
    /// 初始化一项建筑恢复配方。
    /// </summary>
    public BuildingTrainRecipe(int inputId, BuildingTrainRecipeKind kind, float baseSuccessRatio,
        List<OutputInfo> outputMain, List<OutputInfo> outputAppend)
        : base(inputId, baseSuccessRatio, outputMain, outputAppend) {
        Kind = kind;
    }
}
