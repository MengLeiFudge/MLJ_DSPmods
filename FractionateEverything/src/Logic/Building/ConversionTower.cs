using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
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
            "Convert items into other items related to them. There is a certain probability of obtaining conversion essence.",
            "将物品转化为与其相关的其他物品。有一定概率得到转化精华。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.7f, 0.6f, 0.8f);

    public static bool EnableFluidOutputStack = false;
    public static int MaxProductOutputStack = 1;
    public static bool EnableFracForever = false;
    public static int ReinforcementLevel = 0;
    public static float ReinforcementBonus => ReinforcementBonusArr[ReinforcementLevel];
    public static float ReinforcementSuccessRate => ReinforcementSuccessRateArr[ReinforcementLevel];
    public static readonly float propertyRatio = 1.0f;
    public static long workEnergyPerTick => model.prefabDesc.workEnergyPerTick;
    public static long idleEnergyPerTick => model.prefabDesc.idleEnergyPerTick;

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE转化塔, "转化塔", "I转化塔",
            "Assets/fe/conversion-tower", tab分馏 * 1000 + 307, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE转化塔,
            ERecipeType.Assemble, 60, [IFE分馏塔原胚定向], [1], [IFE转化塔], [1],
            "I转化塔", TFE物品转化, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE转化塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(5, item.GridIndex % 10, true);
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
        model.HpMax = (int)(fractionatorModel.HpMax * propertyRatio * (1 + ReinforcementBonus * 4));
        double energyRatio = propertyRatio * (1.0 + ReinforcementBonus);
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
