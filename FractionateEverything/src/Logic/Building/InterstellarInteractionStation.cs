using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;
using static FE.Logic.Manager.ProcessManager;

namespace FE.Logic.Building;

/// <summary>
/// 星际物流交互站
/// </summary>
public static class InterstellarInteractionStation {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.8f, 0.3f, 0.6f);

    public static int Level => PlanetaryInteractionStation.Level;
    public static int MaxProductOutputStack => Level switch {
        < 6 => 1,
        < 9 => 4,
        < 12 => 8,
        _ => 12,
    };
    public static float EnergyRatio => Level switch {
        < 1 => 1.0f,
        < 4 => 0.95f,
        < 7 => 0.85f,
        < 10 => 0.7f,
        _ => 0.5f,
    };
    public static float InteractEnergyRatio => Level switch {
        < 2 => 1.0f,
        < 5 => 0.95f,
        < 8 => 0.85f,
        < 11 => 0.7f,
        _ => 0.5f,
    };
    public static long workEnergyPerTick {
        get => model.prefabDesc.workEnergyPerTick;
        set => model.prefabDesc.workEnergyPerTick = value;
    }
    public static long idleEnergyPerTick {
        get => model.prefabDesc.idleEnergyPerTick;
        set => model.prefabDesc.idleEnergyPerTick = value;
    }

    public static void AddTranslations() {
        Register("星际物流交互站", "Interstellar Interaction Station");
        Register("I星际物流交互站",
            "Interstellar logistics station capable of interacting with the fractionation data centre regarding goods.\nWhen supply is unlocked or demand is locked, items may be downloaded from the data centre; When supply is locked or demand is unlocked, items may be uploaded to the data centre; When storage is unlocked, the item count remains at half; When storage is locked, the item count remains consistent with the data centre.",
            "可以与分馏数据中心进行物品交互的星际物流运输站。\n供应无锁或需求锁定时，可从数据中心下载物品；供应锁定或需求无锁时，可上传物品至数据中心；仓储无锁时，物品数目维持在一半；仓储锁定时，物品数目与数据中心保持一致。");
    }

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE星际物流交互站, "星际物流交互站", "I星际物流交互站",
            "Assets/fe/interstellar-interaction-station", tab分馏 * 1000 + 307, 10, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE星际物流交互站,
            ERecipeType.Assemble, 1800, [I星际物流运输站, IFE交互塔], [1, 12], [IFE星际物流交互站], [1],
            "I星际物流交互站", TFE星际物流交互, item.GridIndex, item.Name, item.IconPath);
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
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        ModelProto stationModel = LDB.models.Select(M星际物流运输站);
        workEnergyPerTick = (long)(stationModel.prefabDesc.workEnergyPerTick * EnergyRatio);
        idleEnergyPerTick = (long)(stationModel.prefabDesc.idleEnergyPerTick * EnergyRatio);
    }
}
