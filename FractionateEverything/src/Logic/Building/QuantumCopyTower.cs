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
/// 量子复制塔
/// </summary>
public static class QuantumCopyTower {
    public static void AddTranslations() {
        Register("量子复制塔", "Quantum Copy Tower");
        Register("I量子复制塔",
            "Rearrange the item at the microscopic level and add distilled essence as a catalyst to replicate the item in bulk. Proliferator points no longer increase processing speed, but they can reduce the consumption of distilled essence. Consumed distillation essence will be automatically deducted from the fractionation data centre.",
            "将物品在微观层面进行重组，并添加分馏精华作为催化剂，从而批量复制这个物品。增产点数不再增加处理速度，但可以减少分馏精华的损耗。消耗的分馏精华会自动从分馏数据中心扣除。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.6235f, 0.6941f, 0.8f);

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
    public static float ReinforcementBonusAppendOutputRate => 0;
    private static readonly float propertyRatio = 2.0f;
    public static long workEnergyPerTick => model.prefabDesc.workEnergyPerTick;
    public static long idleEnergyPerTick => model.prefabDesc.idleEnergyPerTick;

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE量子复制塔, "量子复制塔", "I量子复制塔",
            "Assets/fe/quantum-copy-tower", tab分馏 * 1000 + 304, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE量子复制塔,
            ERecipeType.Assemble, 60, [IFE分馏塔原胚定向], [5], [IFE量子复制塔], [1],
            "I量子复制塔", TFE量子复制, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE量子复制塔, item,
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
