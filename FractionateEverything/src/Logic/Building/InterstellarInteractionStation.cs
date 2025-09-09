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
            """
            An interstellar logistics station that allows for automatic item interaction with fractionation data centres.
            Local Supply = Items will be downloaded up to the set limit as much as possible 
            Local Demand = Items will be uploaded in full 
            Local Storage = Items will be kept at half the set limit as much as possible
            """,
            """
            可以与分馏数据中心自动进行物品交互操作的星际物流运输站。
            本地供应 = 物品会尽可能下载至设定上限
            本地需求 = 物品会全部上传
            本地仓储 = 物品会尽可能维持数目为设定上限的一半
            """);
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
        model = ProtoRegistry.RegisterModel(MFE星际物流交互站, item,
            "Entities/Prefabs/interstellar-logistic-station", null, [53, 24, 38, 12, 10, 1, 40], 0);
        item.SetBuildBar(OrbitalRing.Enable ? 6 : 5, item.GridIndex % 10, true);
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
        model.HpMax = stationModel.HpMax;
        model.prefabDesc.workEnergyPerTick = stationModel.prefabDesc.workEnergyPerTick;
        model.prefabDesc.idleEnergyPerTick = stationModel.prefabDesc.idleEnergyPerTick;
    }
}
