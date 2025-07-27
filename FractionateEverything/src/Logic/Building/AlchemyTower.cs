using System;
using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Building;

/// <summary>
/// 点金塔
/// </summary>
public static class AlchemyTower {
    public static void AddTranslations() {
        Register("点金塔", "Alchemy Tower");
        Register("I点金塔",
            "-",
            "将物品转换为各种矩阵。输入的原材料越珍贵，转化出的矩阵品质越高。有一定概率得到点金精华。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(1.0f, 0.7019f, 0.4f);

    public static bool EnableFluidOutputStack = false;
    public static int MaxProductOutputStack = 1;
    public static bool EnableFracForever = false;

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE点金塔, "点金塔", "I点金塔",
            "Assets/fe/alchemy-tower", tab分馏 * 1000 + 305, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE点金塔,
            ERecipeType.Assemble, 60, [IFE分馏塔原胚定向], [1], [IFE点金塔], [1],
            "I点金塔", TFE物品点金);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE点金塔, item,
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
        double energyRatio = 0.8 * (1 - level * 0.1);
        model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        EnableFluidOutputStack = r.ReadBoolean();
        MaxProductOutputStack = Math.Min(r.ReadInt32(), 4);
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
