using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Building;

/// <summary>
/// 星际物流交互站
/// </summary>
public static class InterstellarInteractionStation {
    public static void AddTranslations() {
        Register("星际物流交互站", "Interstellar Interaction Station");
        Register("I星际物流交互站",
            "Interstellar logistics station that interacts with the fractionation data centre.\nTransfer Mode: Upload - uploads items exceeding the threshold to data centre; Download - downloads items from data centre when below threshold; Sync - both upload and download.\nCapacity Mode: Limited - upload limited by data centre target count; Infinite - unlimited upload.",
            "可以与分馏数据中心进行物品交互的星际物流运输站。\n传输模式：仅上传-超过阈值时上传超出部分；仅下载-低于阈值时下载至阈值；双向同步-同时支持上传和下载。\n容量模式：有限上传-受数据中心目标数量限制；无限上传-不限制上传数量。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.8f, 0.3f, 0.6f);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE星际物流交互站, "星际物流交互站", "I星际物流交互站",
            "Assets/fe/interstellar-interaction-station", tab分馏 * 1000 + 309, 10, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE星际物流交互站,
            ERecipeType.Assemble, 1800, [I星际物流运输站, IFE交互塔], [1, 12], [IFE星际物流交互站], [1],
            "I星际物流交互站", T星际物流系统, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        item.IconTag = "xjjhz";
        recipe.IconTag = "xjjhz";
        model = ProtoRegistry.RegisterModel(MFE星际物流交互站, item,
            "Entities/Prefabs/interstellar-logistic-station", null, [53, 24, 38, 12, 10, 1, 40], 0);
        item.SetBuildBar(OrbitalRing.Enable ? 6 : 5, item.GridIndex % 10, true);
        //设定升降级关系
        ItemProto oriItem = LDB.items.Select(I星际物流运输站);
        int[] upgrades = [I星际物流运输站, IFE星际物流交互站];
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
        ModelProto stationModel = LDB.models.Select(M星际物流运输站);
        // 强化与小塔共用1个
        model.HpMax = (int)(stationModel.HpMax
                            * PlanetaryInteractionStation.propertyRatio
                            * (1 + PlanetaryInteractionStation.ReinforcementBonusDurability));
        model.prefabDesc.workEnergyPerTick = stationModel.prefabDesc.workEnergyPerTick;
        model.prefabDesc.idleEnergyPerTick = stationModel.prefabDesc.idleEnergyPerTick;
    }
}
