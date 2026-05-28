using System;
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
/// 点数聚集塔
/// </summary>
public static class PointAggregateTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    /// <summary>
    /// 保存该分馏塔原型使用的主题颜色。
    /// </summary>
    public static Color color = new(1.0f, 0.72f, 0.18f);

    /// <summary>
    /// 读取或设置该分馏塔建筑的成长等级。
    /// </summary>
    public static int Level = 0;
    /// <summary>
    /// 判断该建筑是否已启用流动输入增产加成。
    /// </summary>
    public static bool EnableFluidEnhancement => Level >= LevelThresholdFluidEnhancement;
    /// <summary>
    /// 判断点数聚集塔是否已解锁虚空喷涂特质。
    /// </summary>
    public static bool EnableVoidSpray => Level >= LevelThresholdTrait1;
    /// <summary>
    /// 判断点数聚集塔是否已解锁虚空聚集特质。
    /// </summary>
    public static bool EnableVoidAggregation => Level >= LevelThresholdTrait2;
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
    public static float PlrRatio => 1.0f;
    /// <summary>
    /// 获取点数聚集塔当前可输出的最高增产点数。
    /// </summary>
    public static int MaxInc => Math.Min(Level + 4, 10);
    /// <summary>
    /// 保存该分馏塔类型当前获得的全局成功率加成。
    /// </summary>
    public static float SuccessBoost = 0;

    /// <summary>
    /// 注册该分馏域对象需要的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("点数聚集塔", "Points Aggregate Tower");
        Register("I点数聚集塔",
            "Concentrate proliferator points onto specific items to produce goods carrying greater proliferator points. Requires upgrading the proliferator point aggregation efficiency tier at the fractionation data centre.",
            "将增产点数集中到部分物品上，从而产出携带更多的增产点数的物品。需要在分馏数据中心升级点数聚集效率层次。");
    }

    /// <summary>
    /// 创建并注册该分馏塔的物品、配方和模型原型。
    /// </summary>
    public static void Create() {
        item = ProtoRegistry.RegisterItem(IFE点数聚集塔, "点数聚集塔", "I点数聚集塔",
            "Assets/fe/point-aggregate-tower", tab分馏 * 1000 + 303, 30, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        recipe = ProtoRegistry.RegisterRecipe(RFE点数聚集塔,
            ERecipeType.Assemble, 60, [IFE分馏塔定向原胚], [2], [IFE点数聚集塔], [3],
            "I点数聚集塔", TFE增产点数聚集, item.GridIndex, item.Name, item.IconPath);
        recipe.IconPath = "";
        recipe.NonProductive = true;
        item.IconTag = "dsjjt";
        recipe.IconTag = "dsjjt";
        model = ProtoRegistry.RegisterModel(MFE点数聚集塔, item,
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
