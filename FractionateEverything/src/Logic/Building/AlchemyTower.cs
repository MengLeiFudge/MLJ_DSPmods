using FE.Logic.Manager;
using UnityEngine;
using static FE.Utils.ProtoID;

namespace FE.Logic.Building;

/// <summary>
/// 点金塔
/// </summary>
public static class AlchemyTower {
    /// <summary>
    /// 创建点金塔
    /// </summary>
    /// <returns>创建的点金塔原型元组</returns>
    public static (RecipeProto, ModelProto, ItemProto) Create() {
        return BuildingManager.CreateAndPreAddNewFractionator(
            "点金塔",
            IFE点金塔,
            MFE点金塔,
            2310,
            new Color(1.0f, 0.85f, 0.2f),
            0,
            0.75f
        );
    }

    public static void InternalUpdate(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result) {
        // 点金塔的特殊逻辑将在这里实现
    }
}
