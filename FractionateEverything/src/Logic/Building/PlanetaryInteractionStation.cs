using System;
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
            """
            An planetary logistics station that allows for automatic item interaction with fractionation data centres.
            Supply = Items will be downloaded up to the set limit as much as possible 
            Demand = Items will be uploaded in full 
            Storage = Items will be kept at half the set limit as much as possible (In lock mode, the data centre will not automatically upload the corresponding items when the number of items is greater than the set limit)
            """,
            """
            可以与分馏数据中心自动进行物品交互操作的行星内物流运输站。
            供应 = 物品会尽可能下载至设定上限
            需求 = 物品会全部上传
            仓储 = 物品会尽可能维持数目为设定上限的一半（锁定模式下，数据中心对应物品数目大于设定上限时不会自动上传）
            """);
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
    public static float ReinforcementBonusEnergy => (float)((100 - 90 * Math.Pow(ReinforcementLevel / 20f, 1.4f)) / 100);
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
