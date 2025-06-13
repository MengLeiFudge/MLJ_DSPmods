using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 矿物复制塔
/// </summary>
public static class MineralCopyTower {
    /// <summary>
    /// 创建矿物复制塔
    /// </summary>
    /// <returns>创建的矿物复制塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateFractionator(
            "矿物复制塔", RFE矿物复制塔, IFE矿物复制塔, MFE矿物复制塔,
            [IFE分馏原胚定向], [1], [10],
            3102, new(0.4f, 1.0f, 0.949f), -20, 0.4f, TFE矿物复制塔
        );
    }
}
