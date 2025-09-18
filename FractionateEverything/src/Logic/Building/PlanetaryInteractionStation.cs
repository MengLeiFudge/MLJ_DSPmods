using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Building;

/// <summary>
/// 行星内物流交互站
/// </summary>
public static class PlanetaryInteractionStation {
    public static void AddTranslations() {
        Register("物流交互站", "Interaction Station");
        Register("行星内物流交互站", "Planetary Interaction Station");
        Register("I行星内物流交互站",
            "Planetary logistics station capable of interacting with the fractionation data centre regarding goods. In supply mode, goods are uploaded to the data centre when surplus exists; in demand mode, goods are downloaded from the data centre when shortages occur; in storage unlocked mode, stock levels are maintained at half capacity; in storage locked mode, stock levels are kept identical to those in the data centre.",
            "可以与分馏数据中心进行物品交互的行星内物流运输站。\n供应模式下，物品过多时上传到数据中心；需求模式下，物品过少时从数据中心下载；仓储无锁定模式下，物品数目维持在一半；仓储锁定模式下，物品数目与数据中心保持一致。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.8f, 0.3f, 0.6f);

    public static int MaxProductOutputStack = 1;
    public static int ReinforcementLevel = 0;
    private static float ReinforcementBonus => ReinforcementBonusArr[ReinforcementLevel];
    public static float ReinforcementSuccessRate => ReinforcementSuccessRateArr[ReinforcementLevel];
    public static float ReinforcementBonusDurability => ReinforcementBonus * 4;
    public static float ReinforcementBonusEnergy => 1 / (1 + ReinforcementBonus * 9);
    public static readonly float propertyRatio = 1.0f;

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE行星内物流交互站, "行星内物流交互站", "I行星内物流交互站",
            "Assets/fe/interaction-station", tab分馏 * 1000 + 308, 10, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE行星内物流交互站,
            ERecipeType.Assemble, 1200, [I行星内物流运输站, IFE交互塔], [1, 12], [IFE行星内物流交互站], [1],
            "I行星内物流交互站", T行星物流系统, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE行星内物流交互站, item,
            "Entities/Prefabs/logistic-station", null, [53, 24, 38, 12, 10, 1, 40], 0);
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
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        ModelProto stationModel = LDB.models.Select(M行星内物流运输站);
        model.HpMax = (int)(stationModel.HpMax * propertyRatio * (1 + ReinforcementBonusDurability));
        model.prefabDesc.workEnergyPerTick = stationModel.prefabDesc.workEnergyPerTick;
        model.prefabDesc.idleEnergyPerTick = stationModel.prefabDesc.idleEnergyPerTick;
    }


    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        MaxProductOutputStack = r.ReadInt32();
        if (MaxProductOutputStack < 0) {
            MaxProductOutputStack = 0;
        } else if (MaxProductOutputStack > 4) {
            MaxProductOutputStack = 4;
        }
        if (version < 2) {
            ReinforcementLevel = 0;
        } else {
            ReinforcementLevel = r.ReadInt32();
            if (ReinforcementLevel < 0) {
                ReinforcementLevel = 0;
            } else if (ReinforcementLevel > MaxReinforcementLevel) {
                ReinforcementLevel = MaxReinforcementLevel;
            }
        }
        UpdateHpAndEnergy();
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(MaxProductOutputStack);
        w.Write(ReinforcementLevel);
    }

    public static void IntoOtherSave() {
        MaxProductOutputStack = 1;
        ReinforcementLevel = 0;
        UpdateHpAndEnergy();
    }

    #endregion
}
