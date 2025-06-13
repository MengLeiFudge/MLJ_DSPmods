using FE.Logic.Manager;
using FE.Logic.Recipe;
using System.Collections.Generic;
using UnityEngine;
using static FE.Utils.ProtoID;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;

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
        return BuildingManager.CreateFractionator(
            "点金塔", RFE点金塔, IFE点金塔, MFE点金塔,
            [IFE分馏原胚定向], [3], [1],
            3106, new(1.0f, 0.7019f, 0.4f), 0, 0.75f, TFE点金塔
        );
    }
}
