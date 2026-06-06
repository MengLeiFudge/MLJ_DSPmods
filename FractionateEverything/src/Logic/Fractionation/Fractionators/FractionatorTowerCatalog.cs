using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 当前版本有效的 FE 分馏塔目录。历史 ID 仍保留在 ProtoID 中，但不再视为可用塔型。
/// </summary>
public static class FractionatorTowerCatalog {
    public static readonly int[] ActiveFractionatorBuildingIds = [
        IFE交互塔,
        IFE矿物复制塔,
        IFE转化塔,
        IFE精馏塔,
    ];

    public static readonly int[] ActiveFractionatorProtoIds = [
        IFE交互塔原胚,
        IFE矿物复制塔原胚,
        IFE转化塔原胚,
        IFE精馏塔原胚,
    ];

    public static int ActiveFractionatorTypeCount => ActiveFractionatorBuildingIds.Length;

    public static bool IsActiveFractionator(int itemId) => GetActiveFractionatorIndex(itemId) >= 0;

    public static bool IsActiveFractionatorProto(int itemId) => GetActiveFractionatorProtoIndex(itemId) >= 0;

    public static bool IsActiveFractionatorProtoOrDirectional(int itemId) {
        return IsActiveFractionatorProto(itemId) || itemId == IFE分馏塔定向原胚;
    }

    public static int GetActiveFractionatorIndex(int itemId) {
        return itemId switch {
            IFE交互塔 => 0,
            IFE矿物复制塔 => 1,
            IFE转化塔 => 2,
            IFE精馏塔 => 3,
            _ => -1,
        };
    }

    public static int GetActiveFractionatorProtoIndex(int itemId) {
        return itemId switch {
            IFE交互塔原胚 => 0,
            IFE矿物复制塔原胚 => 1,
            IFE转化塔原胚 => 2,
            IFE精馏塔原胚 => 3,
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
