using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 转化塔
/// </summary>
public static class ConversionTower {
    /// <summary>
    /// 创建转化塔
    /// </summary>
    /// <returns>创建的转化塔原型元组数组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateFractionator(
            "转化塔", RFE转化塔, IFE转化塔, MFE转化塔,
            [IFE分馏原胚定向], [3], [1],
            3108, new(0.7f, 0.6f, 0.8f), 0, 1.0f, TFE转化塔
        );
    }
}
