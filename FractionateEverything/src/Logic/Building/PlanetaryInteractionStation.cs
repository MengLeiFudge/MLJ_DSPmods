using BuildBarTool;
using CommonAPI.Systems;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Building;

/// <summary>
/// 行星内物流交互站
/// </summary>
public static class PlanetaryInteractionStation {
    public static void AddTranslations() {
        Register("行星内物流交互站", "Planetary Interaction Station");
        Register("I行星内物流交互站",
            """
            An planetary logistics station that allows for automatic item interaction with fractionation data centres.
            Supply = Items will be downloaded up to the set limit as much as possible 
            Demand = Items will be uploaded in full 
            Storage = Items will be kept at half the set limit as much as possible
            """,
            """
            可以与分馏数据中心自动进行物品交互操作的行星内物流运输站。
            供应 = 物品会尽可能下载至设定上限
            需求 = 物品会全部上传
            仓储 = 物品会尽可能维持数目为设定上限的一半
            """);
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.8f, 0.3f, 0.6f);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE行星内物流交互站, "行星内物流交互站", "I行星内物流交互站",
            LDB.items.Select(I行星内物流运输站).IconPath, tab分馏 * 1000 + 308, 10, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE行星内物流交互站,
            ERecipeType.Assemble, 1200, [I行星内物流运输站, IFE交互塔], [1, 12], [IFE行星内物流交互站], [1],
            "I行星内物流交互站", T行星物流系统, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE行星内物流交互站, item,
            "Entities/Prefabs/logistic-station", null, [53, 24, 38, 12, 10, 1, 40], 0);
        item.SetBuildBar(5, item.GridIndex % 10, true);
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
        model.prefabDesc.workEnergyPerTick = stationModel.prefabDesc.workEnergyPerTick;
        model.prefabDesc.idleEnergyPerTick = stationModel.prefabDesc.idleEnergyPerTick;
    }
}
