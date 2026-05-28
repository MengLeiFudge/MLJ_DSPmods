using System.IO;
using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility.Mods;
using FE.Logic.Progression;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Logic.Fractionation.Fractionators.BuildingGrowthService;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 矿物复制塔
/// </summary>
public static class MineralReplicationTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    /// <summary>
    /// 保存该分馏塔原型使用的主题颜色。
    /// </summary>
    public static Color color = new(0.22f, 0.9f, 0.38f);

    /// <summary>
    /// 读取或设置该分馏塔建筑的成长等级。
    /// </summary>
    public static int Level = 0;
    /// <summary>
    /// 判断该建筑是否已启用流动输入增产加成。
    /// </summary>
    public static bool EnableFluidEnhancement => Level >= LevelThresholdFluidEnhancement;
    /// <summary>
    /// 判断矿物复制塔是否已解锁质能裂变特质。
    /// </summary>
    public static bool EnableMassEnergyFission => Level >= LevelThresholdTrait1;
    /// <summary>
    /// 判断矿物复制塔是否已解锁零压循环特质。
    /// </summary>
    public static bool EnableZeroPressureCycle => Level >= LevelThresholdTrait2;
    /// <summary>
    /// 读取该建筑当前允许的分馏处理堆叠上限。
    /// </summary>
    public static int MaxStack => StackingManager.GetFractionatorMaxStack();
    /// <summary>
    /// 读取该建筑当前能耗倍率。
    /// </summary>
    public static float EnergyRatio => GetDefaultEnergyRatioByLevel(Level);
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
    /// 读取该建筑当前增产点倍率。
    /// </summary>
    public static float PlrRatio => GetDefaultPlrRatioByLevel(Level);
    /// <summary>
    /// 保存该分馏塔类型当前获得的全局成功率加成。
    /// </summary>
    public static float SuccessBoost = 0;

    /// <summary>
    /// 注册该分馏域对象需要的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("矿物复制塔", "Mineral Replication Tower");
        Register("I矿物复制塔",
            "Replicate various minerals, including dark fog-specific drops. The corresponding recipes must be unlocked and upgraded at the fractionation data centre.",
            "复制各种矿物，包括黑雾特有掉落。需要在分馏数据中心解锁并升级对应配方。");
    }

    /// <summary>
    /// 创建并注册该分馏塔的物品、配方和模型原型。
    /// </summary>
    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE矿物复制塔, "矿物复制塔", "I矿物复制塔",
            "Assets/fe/mineral-copy-tower", tab分馏 * 1000 + 302, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE矿物复制塔,
            ERecipeType.Assemble, 60, [IFE分馏塔定向原胚], [2], [IFE矿物复制塔], [10],
            "I矿物复制塔", TFE矿物复制, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        item.IconTag = "kwfzt";
        recipe.IconTag = "kwfzt";
        model = ProtoRegistry.RegisterModel(MFE矿物复制塔, item,
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
    /// 按当前等级刷新该分馏塔原型的生命值和能耗。
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

    #region IModCanSave

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Level", br => { Level = Mathf.Max(0, Mathf.Min(MaxLevel, br.ReadInt32())); })
        );
        UpdateHpAndEnergy();
    }

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Level", bw => bw.Write(Level))
        );
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public static void IntoOtherSave() {
        Level = 0;
        UpdateHpAndEnergy();
    }

    #endregion
}
