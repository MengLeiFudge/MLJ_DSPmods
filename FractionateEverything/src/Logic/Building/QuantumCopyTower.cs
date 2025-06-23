using System.IO;
using BepInEx.Configuration;
using BuildBarTool;
using CommonAPI.Systems;
using UnityEngine;
using static FE.Utils.ProtoID;
using static FE.FractionateEverything;
using static FE.Utils.I18NUtils;

namespace FE.Logic.Building;

/// <summary>
/// 量子复制塔
/// </summary>
public static class QuantumCopyTower {
    public static void AddTranslations() {
        Register("量子复制塔", "Quantum Copy Tower");
        Register("I量子复制塔",
            $"-",
            $"充分利用增产点数的增产特性，将输入的物品进行重组复制。它可以分馏万物，真正达到无中生有的效果。");
    }

    public static ConfigEntry<bool> EnableFluidOutputStackEntry;
    public static ConfigEntry<int> MaxProductOutputStackEntry;
    public static ConfigEntry<bool> EnableFracForeverEntry;

    public static void LoadConfig(ConfigFile configFile) {
        string className = "QuantumCopyTower";
        EnableFluidOutputStackEntry = configFile.Bind(className, "Enable Fluid Output Stack", false);
        MaxProductOutputStackEntry = configFile.Bind(className, "Max Product Output Stack", 1);
        EnableFracForeverEntry = configFile.Bind(className, "Enable Frac Forever", false);
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.6235f, 0.6941f, 0.8f);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE量子复制塔, "量子复制塔", "I量子复制塔",
            "Assets/fe/quantum-copy-tower", tab分馏 * 1000 + 105, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE量子复制塔,
            ERecipeType.Assemble, 60, [IFE分馏原胚定向], [1], [IFE量子复制塔], [10],
            "I量子复制塔", TFE量子复制);
        model = ProtoRegistry.RegisterModel(MFE量子复制塔, item,
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
        double energyRatio = 1.0 * (1 - level * 0.1);
        model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        EnableFluidOutputStackEntry.Value = r.ReadBoolean();
        MaxProductOutputStackEntry.Value = r.ReadInt32();
        EnableFracForeverEntry.Value = r.ReadBoolean();
    }

    public static void Export(BinaryWriter w) {
        w.Write(EnableFluidOutputStackEntry.Value);
        w.Write(MaxProductOutputStackEntry.Value);
        w.Write(EnableFracForeverEntry.Value);
    }

    public static void IntoOtherSave() { }

    #endregion
}
