using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Enhancements.Stacking;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 高级矿物塔原型。
/// </summary>
public static class AdvancedMineralTower {
    private static ItemProto item;
    private static ModelProto model;

    /// <summary>
    /// 获取该塔的主题颜色。
    /// </summary>
    public static Color color = new(0.95f, 0.62f, 0.18f);

    /// <summary>
    /// 获取当前全局分馏堆叠上限。
    /// </summary>
    public static int MaxStack => StackingManager.GetFractionatorMaxStack();

    /// <summary>
    /// 高级塔不接入电网。
    /// </summary>
    public const float EnergyRatio = 0.0f;

    /// <summary>
    /// 获取或设置工作能耗。
    /// </summary>
    public static long workEnergyPerTick {
        get => model.prefabDesc.workEnergyPerTick;
        set => model.prefabDesc.workEnergyPerTick = value;
    }

    /// <summary>
    /// 获取或设置待机能耗。
    /// </summary>
    public static long idleEnergyPerTick {
        get => model.prefabDesc.idleEnergyPerTick;
        set => model.prefabDesc.idleEnergyPerTick = value;
    }

    /// <summary>
    /// 获取固定增产点倍率。
    /// </summary>
    public const float PlrRatio = 1.0f;

    /// <summary>
    /// 注册物品与模型原型；高级塔没有制造配方，只能由原胚培育获得。
    /// </summary>
    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE高级矿物塔, "高级矿物塔", "I高级矿物塔",
            "Assets/fe/mineral-copy-tower", tab分馏 * 1000 + 103, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        // 高级塔由高级交互塔培育，制造来源沿用其物品名的本地化键。
        item.ProduceFrom = "培养塔";
        item.UnlockKey = -1;
        item.IconTag = "gjkwt";
        model = ProtoRegistry.RegisterModel(MFE高级矿物塔, item,
            "Entities/Prefabs/fractionator", null, [53, 1, 40], 0);
        item.SetBuildBar(OrbitalRing.Enable ? 6 : 5, item.GridIndex % 10, true);
    }

    /// <summary>
    /// 应用高级矿物塔材质。
    /// </summary>
    public static void SetMaterial() {
        Material main = new(model.prefabDesc.lodMaterials[0][0]) { color = color };
        Material black = model.prefabDesc.lodMaterials[0][1];
        Material glass = model.prefabDesc.lodMaterials[0][2];
        Material glassDetail = model.prefabDesc.lodMaterials[0][3];
        Material lod = new(model.prefabDesc.lodMaterials[1][0]) { color = color };
        Material lod2 = new(model.prefabDesc.lodMaterials[2][0]) { color = color };
        model.prefabDesc.materials = [main, black];
        model.prefabDesc.lodMaterials = [
            [main, black, glass, glassDetail],
            [lod, black, glass, glassDetail],
            [lod2, black, glass, glassDetail],
            null,
        ];
    }

    /// <summary>
    /// 同步生命值并关闭电网能耗。
    /// </summary>
    public static void UpdateHpAndEnergy() {
        ModelProto fractionatorModel = LDB.models.Select(M分馏塔);
        model.HpMax = fractionatorModel.HpMax;
        workEnergyPerTick = 0;
        idleEnergyPerTick = 0;
    }
}
