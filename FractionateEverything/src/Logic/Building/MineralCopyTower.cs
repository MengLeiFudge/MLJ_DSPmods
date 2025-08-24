using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Building;

/// <summary>
/// 矿物复制塔
/// </summary>
public static class MineralCopyTower {
    public static void AddTranslations() {
        Register("矿物复制塔", "Mineral Copy Tower");
        Register("I矿物复制塔",
            "Duplicate most minerals, including those unique to dark fog.",
            "复制大多数矿物，包括黑雾特有掉落。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.4f, 1.0f, 0.949f);

    public static bool EnableFluidOutputStack = false;
    public static int MaxProductOutputStack = 1;
    public static bool EnableFracForever = false;

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE矿物复制塔, "矿物复制塔", "I矿物复制塔",
            "Assets/fe/mineral-copy-tower", tab分馏 * 1000 + 302, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE矿物复制塔,
            ERecipeType.Assemble, 60, [IFE分馏塔原胚定向], [1], [IFE矿物复制塔], [3],
            "I矿物复制塔", TFE矿物复制, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE矿物复制塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(5, item.GridIndex % 10, true);
    }

    public static void SetMaterials() {
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
        SetHpAndEnergy(1);
    }

    public static void SetHpAndEnergy(int level) {
        model.HpMax = LDB.models.Select(M分馏塔).HpMax + level * 50;
        double energyRatio = 0.4 * (1 - level * 0.1);
        model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
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
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(EnableFluidOutputStack);
        w.Write(MaxProductOutputStack);
        w.Write(EnableFracForever);
    }

    public static void IntoOtherSave() {
        EnableFluidOutputStack = false;
        MaxProductOutputStack = 1;
        EnableFracForever = false;
    }

    #endregion
}
