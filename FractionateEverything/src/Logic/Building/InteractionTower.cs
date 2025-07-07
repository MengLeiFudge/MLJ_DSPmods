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
/// 交互塔
/// </summary>
public static class InteractionTower {
    public static void AddTranslations() {
        Register("交互塔", "Interaction Tower");
        Register("I交互塔",
            "-",
            $"将分馏原胚转换为各种分馏建筑。{"正面连接口作为输入时".WithColor(Orange)}，物品将以数据形式传递到主脑，这些物品可以进行兑换、抽奖等操作。");
    }

    public static ConfigEntry<bool> EnableFluidOutputStackEntry;
    public static ConfigEntry<int> MaxProductOutputStackEntry;
    public static ConfigEntry<bool> EnableFracForeverEntry;

    public static void LoadConfig(ConfigFile configFile) {
        string className = "InteractionTower";
        EnableFluidOutputStackEntry = configFile.Bind(className, "Enable Fluid Output Stack", false);
        MaxProductOutputStackEntry = configFile.Bind(className, "Max Product Output Stack", 1);
        EnableFracForeverEntry = configFile.Bind(className, "Enable Frac Forever", false);
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.8f, 0.3f, 0.6f);

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE交互塔, "交互塔", "I交互塔",
            "Assets/fe/interaction-tower", tab分馏 * 1000 + 101, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE交互塔,
            ERecipeType.Assemble, 60, [IFE分馏原胚定向], [1], [IFE交互塔], [1],
            "I交互塔", TFE物品交互);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE交互塔, item,
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
        double energyRatio = 3.0 * (1 - level * 0.1);
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
