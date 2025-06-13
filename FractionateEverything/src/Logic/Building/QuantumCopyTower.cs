using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 量子复制塔
/// </summary>
public static class QuantumCopyTower {
    /// <summary>
    /// 创建量子复制塔
    /// </summary>
    /// <returns>创建的量子复制塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateFractionator(
            "量子复制塔", RFE量子复制塔, IFE量子复制塔, MFE量子复制塔,
            [IFE分馏原胚定向], [1], [10],
            3105, new(0.6235f, 0.6941f, 0.8f), 0, 1.0f, TFE量子复制塔
        );
    }
}
