using BuildBarTool;
using CommonAPI.Systems;
using UnityEngine;
using static FE.Utils.ProtoID;
using static FE.FractionateEverything;
using static FE.Logic.Manager.BuildingManager;

namespace FE.Logic.Building;

/// <summary>
/// 点金塔
/// </summary>
public static class AlchemyTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(1.0f, 0.7019f, 0.4f);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE点金塔, "点金塔", "点金塔描述",
            "Assets/fracicons/alchemy-tower", tab分馏 * 1000 + 106, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE点金塔,
            ERecipeType.Assemble, 60, [IFE分馏原胚定向], [3], [IFE点金塔], [1],
            "点金塔描述", TFE点金塔);
        model = ProtoRegistry.RegisterModel(MFE点金塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(5, item.GridIndex % 10, true);
    }

    public static void PostFix() {
        model.HpMax += 0;
        double energyRatio = 0.75;
        model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
        Material m_main = new(model.prefabDesc.lodMaterials[0][0]) { color = color };
        Material m_black = model.prefabDesc.lodMaterials[0][1];
        Material m_glass = model.prefabDesc.lodMaterials[0][2];
        Material m_glass1 = model.prefabDesc.lodMaterials[0][3];
        Material m_lod = new(model.prefabDesc.lodMaterials[1][0]) { color = color };
        Material m_lod2 = new(model.prefabDesc.lodMaterials[2][0]) { color = color };
        model.prefabDesc.materials = [m_main, m_black];
        model.prefabDesc.lodMaterials = [
            [m_main, m_black, m_glass, m_glass1],
            [m_lod, m_black, m_glass, m_glass1],
            [m_lod2, m_black, m_glass, m_glass1],
            null,
        ];
    }
}
