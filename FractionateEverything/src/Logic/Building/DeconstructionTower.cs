using System;
using System.IO;
using BepInEx.Configuration;
using BuildBarTool;
using CommonAPI.Systems;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Building;

/// <summary>
/// 分解塔
/// </summary>
public static class DeconstructionTower {
    public static void AddTranslations() {
        Register("分解塔", "Deconstruction Tower");
        Register("I分解塔",
            "-",
            "将物品分解为原材料。无法分解的物品将转化为沙土。有一定概率得到分解精华。");
    }

    public static ConfigEntry<bool> EnableFluidOutputStackEntry;
    public static ConfigEntry<int> MaxProductOutputStackEntry;
    public static ConfigEntry<bool> EnableFracForeverEntry;

    public static void LoadConfig(ConfigFile configFile) {
        string className = "DeconstructionTower";
        EnableFluidOutputStackEntry = configFile.Bind(className, "Enable Fluid Output Stack", false);
        MaxProductOutputStackEntry = configFile.Bind(className, "Max Product Output Stack", 1);
        EnableFracForeverEntry = configFile.Bind(className, "Enable Frac Forever", false);
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.4f, 1.0f, 0.5f);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE分解塔, "分解塔", "I分解塔",
            "Assets/fe/deconstruction-tower", tab分馏 * 1000 + 107, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE分解塔,
            ERecipeType.Assemble, 60, [IFE分馏原胚定向], [3], [IFE分解塔], [1],
            "I分解塔", TFE物品分解);
        recipe.IconPath = "";
        model = ProtoRegistry.RegisterModel(MFE分解塔, item,
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
        EnableFluidOutputStackEntry.Value = r.ReadBoolean();
        MaxProductOutputStackEntry.Value = Math.Min(r.ReadInt32(), 4);
        EnableFracForeverEntry.Value = r.ReadBoolean();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(EnableFluidOutputStackEntry.Value);
        w.Write(MaxProductOutputStackEntry.Value);
        w.Write(EnableFracForeverEntry.Value);
    }

    public static void IntoOtherSave() {
        EnableFluidOutputStackEntry.Value = false;
        MaxProductOutputStackEntry.Value = 1;
        EnableFracForeverEntry.Value = false;
    }

    #endregion
}
