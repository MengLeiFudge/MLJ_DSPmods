using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 点数聚集塔
/// </summary>
public static class PointAggregatorTower {
    /// <summary>
    /// 创建点数聚集塔
    /// </summary>
    /// <returns>创建的点数聚集塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateAndPreAddNewFractionator(
            "点数聚集塔",
            IFE点数聚集塔,
            MFE点数聚集塔,
            2710,
            new Color(0.2509f, 0.8392f, 1.0f),
            0,
            1.0f
        );
    }

    public static void InternalUpdate(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result) { }
}
