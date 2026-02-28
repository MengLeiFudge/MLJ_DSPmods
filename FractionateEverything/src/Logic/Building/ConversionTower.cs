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
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.7f, 0.6f, 0.8f);
    
    public static int Level = 0;
    public static bool EnableFluidEnhancement => Level >= 3;
    public static int MaxProductOutputStack => Level switch {
        < 9 => 1,
        _ => 4,
    };
    public static float EnergyRatio => Level switch {
        < 1 => 1.0f,
        < 4 => 0.95f,
        < 7 => 0.85f,
        < 10 => 0.7f,
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
    public static float PlrRatio => Level switch {
        < 2 => 1.0f,
        < 5 => 1.1f,
        < 8 => 1.3f,
        < 11 => 1.6f,
        _ => 2.0f,
    };

    public static void AddTranslations() {
        Register("转化塔", "Conversion Tower");
        Register("I转化塔",
            "Convert items into other items related to them. The corresponding recipes must be unlocked and upgraded at the fractionation data centre.",
            "将物品转化为与其相关的其他物品。需要在分馏数据中心解锁并升级对应配方。");
    }

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE转化塔, "转化塔", "I转化塔",
            "Assets/fe/conversion-tower", tab分馏 * 1000 + 304, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE转化塔,
            ERecipeType.Assemble, 60, [IFE分馏塔定向原胚], [2], [IFE转化塔], [5],
            "I转化塔", TFE物品转化, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        item.IconTag = "zht";
        recipe.IconTag = "zht";
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
        workEnergyPerTick = (long)(fractionatorModel.prefabDesc.workEnergyPerTick * EnergyRatio);
        idleEnergyPerTick = (long)(fractionatorModel.prefabDesc.idleEnergyPerTick * EnergyRatio);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        if (version < 2) {
            r.ReadBoolean();
            r.ReadInt32();
            r.ReadBoolean();
        }
        Level = r.ReadInt32();
        if (Level < 0) {
            Level = 0;
        } else if (Level > MaxLevel) {
            Level = MaxLevel;
        }
        UpdateHpAndEnergy();
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);
        w.Write(Level);
    }

    public static void IntoOtherSave() {
        Level = 0;
        UpdateHpAndEnergy();
    }

    #endregion
}
