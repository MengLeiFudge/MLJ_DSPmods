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
/// 转化塔
/// </summary>
public static class ConversionTower {
    public static void AddTranslations() {
        Register("转化塔", "Conversion Tower");
        Register("I转化塔",
            "Convert items into other items related to them. There is a certain probability of obtaining conversion essence. The corresponding recipes must be unlocked and upgraded at the fractionation data centre.",
            "将物品转化为与其相关的其他物品。有一定概率得到转化精华。需要在分馏数据中心解锁并升级对应配方。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.7f, 0.6f, 0.8f);

    public static bool EnableFluidOutputStack = false;
    public static int MaxProductOutputStack = 1;
    public static bool EnableFracForever = false;
    public static int ReinforcementLevel = 0;
    private static float ReinforcementBonus => ReinforcementBonusArr[ReinforcementLevel];
    public static float ReinforcementSuccessRate => ReinforcementSuccessRateArr[ReinforcementLevel];
    public static float ReinforcementBonusDurability => ReinforcementBonus * 4;
    public static float ReinforcementBonusEnergy => ReinforcementBonus;
    public static float ReinforcementBonusFracSuccess => 0;
    public static float ReinforcementBonusMainOutputCount => ReinforcementBonus * 0.5f;
    public static float ReinforcementBonusAppendOutputRate => ReinforcementBonus;
    private static readonly float propertyRatio = 1.0f;
    public static long workEnergyPerTick => model.prefabDesc.workEnergyPerTick;
    public static long idleEnergyPerTick => model.prefabDesc.idleEnergyPerTick;

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE转化塔, "转化塔", "I转化塔",
            "Assets/fe/conversion-tower", tab分馏 * 1000 + 304, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE转化塔,
            ERecipeType.Assemble, 60, [IFE分馏塔定向原胚], [2], [IFE转化塔], [5],
            "I转化塔", TFE物品转化, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE转化塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(OrbitalRing.Enable ? 6 : 5, item.GridIndex % 10, true);
    }

    public static void SetMaterial() {
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

    public static void UpdateHpAndEnergy() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        ModelProto fractionatorModel = LDB.models.Select(M分馏塔);
        model.HpMax = (int)(fractionatorModel.HpMax * propertyRatio * (1 + ReinforcementBonusDurability));
        double energyRatio = propertyRatio * (1 + ReinforcementBonusEnergy);
        model.prefabDesc.workEnergyPerTick = (long)(fractionatorModel.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(fractionatorModel.prefabDesc.idleEnergyPerTick * energyRatio);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        EnableFluidOutputStack = r.ReadBoolean();
        MaxProductOutputStack = r.ReadInt32();
        if (MaxProductOutputStack < 0) {
            MaxProductOutputStack = 0;
        } else if (MaxProductOutputStack > 4) {
            MaxProductOutputStack = 4;
        }
        EnableFracForever = r.ReadBoolean();
        ReinforcementLevel = r.ReadInt32();
        if (ReinforcementLevel < 0) {
            ReinforcementLevel = 0;
        } else if (ReinforcementLevel > MaxReinforcementLevel) {
            ReinforcementLevel = MaxReinforcementLevel;
        }
        UpdateHpAndEnergy();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(EnableFluidOutputStack);
        w.Write(MaxProductOutputStack);
        w.Write(EnableFracForever);
        w.Write(ReinforcementLevel);
    }

    public static void IntoOtherSave() {
        EnableFluidOutputStack = false;
        MaxProductOutputStack = 1;
        EnableFracForever = false;
        ReinforcementLevel = 0;
        UpdateHpAndEnergy();
    }

    #endregion
}
