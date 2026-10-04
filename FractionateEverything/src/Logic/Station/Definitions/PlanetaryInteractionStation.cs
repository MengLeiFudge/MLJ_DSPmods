using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Enhancements.Stacking;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Station.Definitions;

/// <summary>
/// 行星内物流交互站
/// </summary>
public static class PlanetaryInteractionStation {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.8f, 0.3f, 0.6f);

    public static int MaxStack => StackingManager.GetLogisticStationMaxStack();
    public const float InteractEnergyRatio = 1.0f;
    public static long workEnergyPerTick {
        get => model.prefabDesc.workEnergyPerTick;
        set => model.prefabDesc.workEnergyPerTick = value;
    }
    public static long idleEnergyPerTick {
        get => model.prefabDesc.idleEnergyPerTick;
        set => model.prefabDesc.idleEnergyPerTick = value;
    }

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE行星内物流交互站, "行星内物流交互站", "I行星内物流交互站",
            "Assets/fe/interaction-station", tab分馏 * 1000 + 306, 10, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE行星内物流交互站,
            ERecipeType.Assemble, 1200, [I行星内物流运输站, IFE基础矿物塔, IFE基础解析塔], [1, 4, 4],
            [IFE行星内物流交互站], [1],
            "I行星内物流交互站", TFE行星内物流交互, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        item.IconTag = "xxnjhz";
        recipe.IconTag = "xxnjhz";
        model = ProtoRegistry.RegisterModel(MFE行星内物流交互站, item,
            "Entities/Prefabs/logistic-station", null, [53, 24, 38, 12, 10, 1, 40], 0);
        item.SetBuildBar(OrbitalRing.Enable ? 6 : 5, item.GridIndex % 10, true);
        //设定升降级关系
        ItemProto oriItem = LDB.items.Select(I行星内物流运输站);
        int[] upgrades = [I行星内物流运输站, IFE行星内物流交互站];
        oriItem.Upgrades = upgrades;
        oriItem.Grade = 1;
        item.Upgrades = upgrades;
        item.Grade = 2;
    }

    public static void SetMaterial() {
        Material m_main = new(model.prefabDesc.lodMaterials[0][0]) { color = color };
        Material m_black = model.prefabDesc.lodMaterials[0][1];
        model.prefabDesc.materials = [m_main];
        model.prefabDesc.lodMaterials = [
            [m_main, m_black],
            null,
            null,
            null,
        ];
    }

    public static void UpdateHpAndEnergy() {
        ModelProto stationModel = LDB.models.Select(M行星内物流运输站);
        model.HpMax = stationModel.HpMax;
        model.prefabDesc.stationMaxItemKinds = stationModel.prefabDesc.stationMaxItemKinds;
        model.prefabDesc.stationMaxItemCount = stationModel.prefabDesc.stationMaxItemCount;
        model.prefabDesc.stationMaxEnergyAcc = stationModel.prefabDesc.stationMaxEnergyAcc;
        workEnergyPerTick = stationModel.prefabDesc.workEnergyPerTick;
        idleEnergyPerTick = stationModel.prefabDesc.idleEnergyPerTick;
    }
}
