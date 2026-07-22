using FE.Logic.Fractionation.FracRecipes;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 当前版本有效的 FE 分馏塔目录。历史 ID 仍保留在 ProtoID 中，但不再视为可用塔型。
/// </summary>
public static class FractionatorTowerCatalog {
    public static readonly int[] ActiveFractionatorBuildingIds = [
        IFE交互塔,
        IFE解析塔,
        IFE资源塔,
        IFE转化塔,
    ];

    public static readonly int[] ActiveFractionatorProtoIds = [
        IFE交互塔原胚,
        IFE解析塔原胚,
        IFE资源塔原胚,
        IFE转化塔原胚,
    ];

    public static int ActiveFractionatorTypeCount => ActiveFractionatorBuildingIds.Length;

    public static bool IsActiveFractionator(int itemId) => GetActiveFractionatorIndex(itemId) >= 0;

    public static bool IsActiveFractionatorProto(int itemId) => GetActiveFractionatorProtoIndex(itemId) >= 0;

    public static bool IsActiveFractionatorProtoOrCommon(int itemId) {
        return IsActiveFractionatorProto(itemId) || itemId == IFE通用原胚;
    }

    public static int GetActiveFractionatorIndex(int itemId) {
        return itemId switch {
            IFE交互塔 => 0,
            IFE解析塔 => 1,
            IFE资源塔 => 2,
            IFE转化塔 => 3,
            _ => -1,
        };
    }

    /// <summary>
    /// 获取活动分馏塔建筑对应的配方分支；未知建筑返回零值。
    /// </summary>
    public static ERecipe GetRecipeType(int buildingId) {
        return buildingId switch {
            IFE交互塔 => ERecipe.BuildingTrain,
            IFE解析塔 => ERecipe.Rectification,
            IFE资源塔 => ERecipe.MineralCopy,
            IFE转化塔 => ERecipe.Conversion,
            _ => (ERecipe)0,
        };
    }

    public static int GetActiveFractionatorProtoIndex(int itemId) {
        return itemId switch {
            IFE交互塔原胚 => 0,
            IFE解析塔原胚 => 1,
            IFE资源塔原胚 => 2,
            IFE转化塔原胚 => 3,
            _ => -1,
        };
    }

    public static bool TryGetBuildingIdForProto(int protoId, out int buildingId) {
        int index = GetActiveFractionatorProtoIndex(protoId);
        if (index < 0) {
            buildingId = 0;
            return false;
        }
        buildingId = ActiveFractionatorBuildingIds[index];
        return true;
    }

    public static bool TryGetProtoIdForBuilding(int buildingId, out int protoId) {
        int index = GetActiveFractionatorIndex(buildingId);
        if (index < 0) {
            protoId = 0;
            return false;
        }
        protoId = ActiveFractionatorProtoIds[index];
        return true;
    }
}
