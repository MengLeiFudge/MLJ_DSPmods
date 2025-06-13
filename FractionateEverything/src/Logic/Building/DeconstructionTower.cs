using FE.Logic.Manager;
using System.Linq;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 分解塔
/// </summary>
public static class DeconstructionTower {
    /// <summary>
    /// 创建分解塔
    /// </summary>
    /// <returns>创建的分解塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateFractionator(
            "分解塔", RFE分解塔, IFE分解塔, MFE分解塔,
            [IFE分馏原胚定向], [3], [1],
            3107, new(0.4f, 1.0f, 0.5f), 0, 0.9f, TFE分解塔
        );
    }
}
