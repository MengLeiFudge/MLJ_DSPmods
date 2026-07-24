using System.Collections.Generic;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 定义通用原胚随机培养、专属原胚定向培养和实体塔返祖配方。
/// </summary>
public sealed class CultivationRecipe : BaseRecipe {
    /// <summary>
    /// 区分建筑恢复链中的三类配方行为。
    /// </summary>
    public enum CultivationRecipeKind {
        CommonCultivation,
        DirectedCultivation,
        Atavism,
    }

    /// <summary>
    /// 返祖协议进入检索池前需要完成的对应塔型培养次数基线。
    /// </summary>
    public const int AtavismEligibilityCultivationCount = 20;

    /// <summary>
    /// 创建通用培养、四种专属培养和四种返祖配方。
    /// </summary>
    public static void CreateAll() {
        AddRecipe(new CultivationRecipe(IFE通用原胚, CultivationRecipeKind.CommonCultivation, 0.05f, [
            new(0.25f, IFE培养塔, 1),
            new(0.25f, IFE解析塔, 1),
            new(0.25f, IFE复制塔, 1),
            new(0.25f, IFE转化塔, 1),
        ], []));

        foreach (int protoId in FractionatorTowerCatalog.ActiveFractionatorProtoIds) {
            if (!FractionatorTowerCatalog.TryGetBuildingIdForProto(protoId, out int buildingId)) {
                continue;
            }

            AddRecipe(new CultivationRecipe(protoId, CultivationRecipeKind.DirectedCultivation, 0.05f, [
                new(1.0f, buildingId, 1),
            ], []));
            AddRecipe(new CultivationRecipe(buildingId, CultivationRecipeKind.Atavism, 0.05f, [
                new(1.0f, protoId, 1),
            ], []));
        }
    }

    /// <summary>
    /// 获取培养配方类型。
    /// </summary>
    public override ERecipe RecipeType => ERecipe.Cultivation;

    /// <summary>
    /// 只有实体塔返祖需要先恢复文明协议。
    /// </summary>
    public override bool RequiresProtocolRecovery => Kind == CultivationRecipeKind.Atavism;

    /// <summary>
    /// 建筑恢复协议固定归入电磁阶段。
    /// </summary>
    public override int ProtocolStageOrder => 0;

    /// <summary>
    /// 返祖是后期可选协议，不阻塞电磁阶段主线完成度。
    /// </summary>
    public override bool CountsTowardStageCompletion => Kind != CultivationRecipeKind.Atavism;

    /// <summary>
    /// 返祖协议只有在对应专属原胚完成足够培养后才具备检索资格。
    /// </summary>
    public override bool IsProtocolEligible {
        get {
            if (Kind != CultivationRecipeKind.Atavism || OutputMain.Count == 0) {
                return true;
            }

            CultivationRecipe cultivation = GetRecipe<CultivationRecipe>(ERecipe.Cultivation,
                OutputMain[0].OutputID);
            return cultivation?.Kind == CultivationRecipeKind.DirectedCultivation
                   && cultivation.TotalSuccessCount >= AtavismEligibilityCultivationCount;
        }
    }

    /// <summary>
    /// 获取该实例在建筑恢复链中的行为类型。
    /// </summary>
    public CultivationRecipeKind Kind { get; }

    /// <summary>
    /// 初始化一项建筑恢复配方。
    /// </summary>
    public CultivationRecipe(int inputId, CultivationRecipeKind kind, float baseSuccessRatio,
        List<OutputInfo> outputMain, List<OutputInfo> outputAppend)
        : base(inputId, baseSuccessRatio, outputMain, outputAppend) {
        Kind = kind;
    }
}
