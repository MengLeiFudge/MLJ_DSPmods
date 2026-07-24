using System.IO;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;
using FE.Logic.Progression.Stacking;
using FE.Logic.Station.Definitions;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Buildings;

/// <summary>
/// FE 建筑原型注册、固定运行属性和建筑实例状态聚合入口。
/// </summary>
public static partial class BuildingManager {
    public static void AddTranslations() {
        CultivationTower.AddTranslations();
        AnalysisTower.AddTranslations();
        ReplicationTower.AddTranslations();
        ConversionTower.AddTranslations();

        PlanetaryInteractionStation.AddTranslations();
        InterstellarInteractionStation.AddTranslations();
    }

    public static void AddFractionators() {
        //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
        //new(1.0f, 0.6596f, 0.3066f)
        //new(0.0f, 1.0f, 0.9112f)
        //new(0.3726f, 0.8f, 1.0f)
        //new(0.549f, 0.5922f, 0.6235f)

        CultivationTower.Create();
        AnalysisTower.Create();
        ReplicationTower.Create();
        ConversionTower.Create();

        PlanetaryInteractionStation.Create();
        InterstellarInteractionStation.Create();
    }

    public static void SetFractionatorMaterial() {
        CultivationTower.SetMaterial();
        AnalysisTower.SetMaterial();
        ReplicationTower.SetMaterial();
        ConversionTower.SetMaterial();

        PlanetaryInteractionStation.SetMaterial();
        InterstellarInteractionStation.SetMaterial();
    }

    public static void UpdateHpAndEnergy() {
        CultivationTower.UpdateHpAndEnergy();
        AnalysisTower.UpdateHpAndEnergy();
        ReplicationTower.UpdateHpAndEnergy();
        ConversionTower.UpdateHpAndEnergy();

        PlanetaryInteractionStation.UpdateHpAndEnergy();
        InterstellarInteractionStation.UpdateHpAndEnergy();
    }

    /// <summary>
    /// 调整分馏塔缓存区大小（实际运行不使用此值，该方法只对原版分馏塔生效）
    /// </summary>
    public static void SetFractionatorCacheSize() {
        foreach (ModelProto modelProto in LDB.models.dataArray) {
            if (modelProto.prefabDesc.isFractionator) {
                modelProto.prefabDesc.fracFluidInputMax = BaseFracFluidInputCargoMax;
                modelProto.prefabDesc.fracProductOutputMax = BaseFracProductOutputMax * StackingManager.CurrentMaxStack / 4;
                modelProto.prefabDesc.fracFluidOutputMax = BaseFracFluidOutputMax * StackingManager.CurrentMaxStack / 4;
            }
        }
    }

    /// <summary>
    /// 调整已放置的分馏塔缓存区大小（实际运行不使用此值，该方法只对原版分馏塔生效）
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
    public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
        __instance.fluidInputMax = BaseFracFluidInputCargoMax;
        __instance.productOutputMax = BaseFracProductOutputMax * StackingManager.CurrentMaxStack / 4;
        __instance.fluidOutputMax = BaseFracFluidOutputMax * StackingManager.CurrentMaxStack / 4;
    }

    /// <summary>
    /// 返回分馏塔流动输入缓存最大组数，固定为40
    /// </summary>
    public static int FluidInputCargoMax(this ItemProto fractionator) {
        return BaseFracFluidInputCargoMax;
    }

    /// <summary>
    /// 返回分馏塔产物输出缓存最大数目，由于输入的物品堆叠数可能超过塔的MaxStack，所以直接按照最高的来
    /// </summary>
    public static int ProductOutputMax(this ItemProto fractionator) {
        return fractionator.ID switch {
            IFE培养塔 => BaseFracProductOutputMax * CultivationTower.MaxStack,
            IFE解析塔 => BaseFracProductOutputMax * AnalysisTower.MaxStack,
            IFE复制塔 => BaseFracProductOutputMax * ReplicationTower.MaxStack,
            IFE转化塔 => BaseFracProductOutputMax * ConversionTower.MaxStack,
            _ => BaseFracProductOutputMax * StackingManager.CurrentMaxStack / 4
        };
    }

    /// <summary>
    /// 返回分馏塔流动输出缓存最大数目，由于输出仅由塔的MaxStack决定，所以根据当前MaxStack动态变化
    /// </summary>
    public static int FluidOutputMax(this ItemProto fractionator) {
        return fractionator.ID switch {
            IFE培养塔 => BaseFracFluidOutputMax * Mathf.Max(1, CultivationTower.MaxStack / 4),
            IFE解析塔 => BaseFracFluidOutputMax * Mathf.Max(1, AnalysisTower.MaxStack / 4),
            IFE复制塔 => BaseFracFluidOutputMax * Mathf.Max(1, ReplicationTower.MaxStack / 4),
            IFE转化塔 => BaseFracFluidOutputMax * Mathf.Max(1, ConversionTower.MaxStack / 4),
            _ => BaseFracFluidOutputMax * StackingManager.CurrentMaxStack / 4
        };
    }

    /// <summary>
    /// 读取建筑当前允许的处理堆叠上限；该值来自全局堆叠科技，不属于建筑等级。
    /// </summary>
    public static int MaxStack(this ItemProto building) {
        return building.ID switch {
            IFE培养塔 => CultivationTower.MaxStack,
            IFE解析塔 => AnalysisTower.MaxStack,
            IFE复制塔 => ReplicationTower.MaxStack,
            IFE转化塔 => ConversionTower.MaxStack,
            IFE行星内物流交互站 => PlanetaryInteractionStation.MaxStack,
            IFE星际物流交互站 => InterstellarInteractionStation.MaxStack,
            _ => 1
        };
    }

    /// <summary>
    /// 读取物流交互站的固定交互能耗倍率。
    /// </summary>
    public static float InteractEnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE行星内物流交互站 => PlanetaryInteractionStation.InteractEnergyRatio,
            IFE星际物流交互站 => InterstellarInteractionStation.InteractEnergyRatio,
            _ => 1.0f
        };
    }

    /// <summary>
    /// 读取分馏塔的固定增产点倍率。
    /// </summary>
    public static float PlrRatio(this ItemProto building) {
        return building.ID switch {
            IFE培养塔 => CultivationTower.PlrRatio,
            IFE解析塔 => AnalysisTower.PlrRatio,
            IFE复制塔 => ReplicationTower.PlrRatio,
            IFE转化塔 => ConversionTower.PlrRatio,
            _ => 1.0f
        };
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("OutputExtend", FractionatorOutputState.OutputExtendImport),
            ("LockedOutput", FractionatorMainOutputLock.LockedOutputImport),
            ("FractionatorByproductDiscard", FractionatorByproductDiscard.Import)
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("OutputExtend", FractionatorOutputState.OutputExtendExport),
            ("LockedOutput", FractionatorMainOutputLock.LockedOutputExport),
            ("FractionatorByproductDiscard", FractionatorByproductDiscard.Export)
        );
    }

    public static void IntoOtherSave() {
        FractionatorOutputState.OutputExtendIntoOtherSave();
        FractionatorMainOutputLock.LockedOutputIntoOtherSave();
        FractionatorByproductDiscard.IntoOtherSave();
    }

    #endregion
}
