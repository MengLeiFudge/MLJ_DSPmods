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
            $"Take full advantage of the proliferator points' proliferator feature to reorganize and duplicate the input items. It can fractionate everything and truly create something from nothing.\nSuccess rate is related to the input item proliferator points, and maximum rate is related to the input item value.",
            $"充分利用增产点数的增产特性，将输入的物品进行重组复制。它可以分馏万物，真正达到无中生有的效果。\n成功率与输入物品的增产点数有关，最大值与输入物品的价值有关。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.6235f, 0.6941f, 0.8f);
    public static ConfigEntry<bool> EnableFluidOutputStackEntry;
    public static ConfigEntry<int> MaxProductOutputStackEntry;
    public static ConfigEntry<bool> EnableFracForeverEntry;

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

    public static void PostFix() {
        // model.HpMax += 0;
        double energyRatio = 1.0;
        model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
        model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
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
}
