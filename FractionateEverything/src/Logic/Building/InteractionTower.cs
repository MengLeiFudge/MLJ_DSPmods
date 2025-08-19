using System;
using System.IO;
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
            $"Not only can it cultivate the original embryo of the fractionator into various fractionators, but it also bears the important responsibility of interacting with the fractionation data center. {"When the front connection port is used as input".WithColor(Orange)}, the item will be transmitted to the fractionation data center in the form of data and used for lottery and other operations.",
            $"不仅能将分馏塔原胚培养为各种分馏塔，还承担着与分馏数据中心进行物品交互的重要责任。{"正面连接口作为输入时".WithColor(Orange)}，物品将以数据形式传递到分馏数据中心，并用于抽奖等操作。");
    }

    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    private static Color color = new(0.8f, 0.3f, 0.6f);

    public static bool EnableFluidOutputStack = false;
    public static int MaxProductOutputStack = 1;
    public static bool EnableFracForever = false;

    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE交互塔, "交互塔", "I交互塔",
            "Assets/fe/interaction-tower", tab分馏 * 1000 + 301, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE交互塔,
            ERecipeType.Assemble, 60, [IFE分馏塔原胚定向], [1], [IFE交互塔], [1],
            "I交互塔", TFE物品交互, item.GridIndex, item.Name, item.IconPath);
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
        EnableFluidOutputStack = r.ReadBoolean();
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
