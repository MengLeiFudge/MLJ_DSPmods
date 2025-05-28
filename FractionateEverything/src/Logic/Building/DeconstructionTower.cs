using FE.Logic.Manager;
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
        return BuildingManager.CreateAndPreAddNewFractionator(
            "分解塔",
            IFE分解塔,
            MFE分解塔,
            2311,
            new Color(0.6f, 0.3f, 0.9f),
            0,
            0.9f
        );
    }

    public static void InternalUpdate(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result) {
        // 分解塔的特殊逻辑将在这里实现
    }
}
