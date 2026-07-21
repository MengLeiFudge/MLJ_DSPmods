using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Progression;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 转化塔
/// </summary>
public static class ConversionTower {
    private static ItemProto item;
    private static ModelProto model;
    /// <summary>
    /// 保存该分馏塔原型使用的主题颜色。
    /// </summary>
    public static Color color = new(0.58f, 0.38f, 0.95f);

    /// <summary>
    /// 读取该建筑当前允许的分馏处理堆叠上限。
    /// </summary>
    public static int MaxStack => StackingManager.GetFractionatorMaxStack();
    /// <summary>
    /// 读取当前固定能耗倍率；后续增幅只由文明科技运行投影提供。
    /// </summary>
    public const float EnergyRatio = 1.0f;
    /// <summary>
    /// 读取该建筑当前每 tick 工作能耗。
    /// </summary>
    public static long workEnergyPerTick {
        get => model.prefabDesc.workEnergyPerTick;
        set => model.prefabDesc.workEnergyPerTick = value;
    }
    /// <summary>
    /// 读取该建筑当前每 tick 待机能耗。
    /// </summary>
    public static long idleEnergyPerTick {
        get => model.prefabDesc.idleEnergyPerTick;
        set => model.prefabDesc.idleEnergyPerTick = value;
    }
    /// <summary>
    /// 读取当前固定增产点倍率；不再由旧建筑等级改变。
    /// </summary>
    public const float PlrRatio = 1.0f;
    /// <summary>
    /// 注册该分馏域对象需要的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("转化塔", "Conversion Tower");
        Register("I转化塔",
            "Convert items into other related items after their civilization protocols have been fully recovered.",
            "将物品转化为与其相关的其他物品。对应文明协议完整恢复后即可使用。");
    }

    /// <summary>
    /// 创建并注册该分馏塔的物品、配方和模型原型。
    /// </summary>
    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE转化塔, "转化塔", "I转化塔",
            "Assets/fe/conversion-tower", tab分馏 * 1000 + 304, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        item.UnlockKey = -1;
        item.IconTag = "zht";
        model = ProtoRegistry.RegisterModel(MFE转化塔, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        item.SetBuildBar(OrbitalRing.Enable ? 6 : 5, item.GridIndex % 10, true);
    }

    /// <summary>
    /// 应用该分馏塔的模型材质和颜色配置。
    /// </summary>
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

    /// <summary>
    /// 按当前固定原型参数刷新该分馏塔的生命值和能耗。
    /// </summary>
    public static void UpdateHpAndEnergy() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        ModelProto fractionatorModel = LDB.models.Select(M分馏塔);
        model.HpMax = fractionatorModel.HpMax;
        workEnergyPerTick = (long)(fractionatorModel.prefabDesc.workEnergyPerTick * EnergyRatio);
        idleEnergyPerTick = (long)(fractionatorModel.prefabDesc.idleEnergyPerTick * EnergyRatio);
    }

}
