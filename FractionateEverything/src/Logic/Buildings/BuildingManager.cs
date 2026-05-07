using System;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;
using FE.Logic.Station.Definitions;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Buildings;

/// <summary>
/// FE 建筑等级阈值、原型注册和建筑聚合入口。
/// </summary>
public static partial class BuildingManager {
    public static void AddTranslations() {
        InteractionTower.AddTranslations();
        MineralReplicationTower.AddTranslations();
        PointAggregateTower.AddTranslations();
        ConversionTower.AddTranslations();
        RectificationTower.AddTranslations();

        PlanetaryInteractionStation.AddTranslations();
        InterstellarInteractionStation.AddTranslations();
    }

    public static void AddFractionators() {
        //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
        //new(1.0f, 0.6596f, 0.3066f)
        //new(0.0f, 1.0f, 0.9112f)
        //new(0.3726f, 0.8f, 1.0f)
        //new(0.549f, 0.5922f, 0.6235f)

        InteractionTower.Create();
        MineralReplicationTower.Create();
        PointAggregateTower.Create();
        ConversionTower.Create();
        RectificationTower.Create();

        PlanetaryInteractionStation.Create();
        InterstellarInteractionStation.Create();
    }

    public static void SetFractionatorMaterial() {
        InteractionTower.SetMaterial();
        MineralReplicationTower.SetMaterial();
        PointAggregateTower.SetMaterial();
        ConversionTower.SetMaterial();
        RectificationTower.SetMaterial();

        PlanetaryInteractionStation.SetMaterial();
        InterstellarInteractionStation.SetMaterial();
    }

    public static void UpdateHpAndEnergy() {
        InteractionTower.UpdateHpAndEnergy();
        MineralReplicationTower.UpdateHpAndEnergy();
        PointAggregateTower.UpdateHpAndEnergy();
        ConversionTower.UpdateHpAndEnergy();
        RectificationTower.UpdateHpAndEnergy();

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
                modelProto.prefabDesc.fracProductOutputMax = BaseFracProductOutputMax * 12 / 4;//todo: 改为全局
                modelProto.prefabDesc.fracFluidOutputMax = BaseFracFluidOutputMax * 12 / 4;//todo: 改为全局
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
        __instance.productOutputMax = BaseFracProductOutputMax * 12 / 4;//todo: 改为全局
        __instance.fluidOutputMax = BaseFracFluidOutputMax * 12 / 4;//todo: 改为全局
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
            IFE交互塔 => BaseFracProductOutputMax * InteractionTower.MaxStack,
            IFE矿物复制塔 => BaseFracProductOutputMax * MineralReplicationTower.MaxStack,
            IFE点数聚集塔 => BaseFracProductOutputMax * PointAggregateTower.MaxStack,
            IFE转化塔 => BaseFracProductOutputMax * ConversionTower.MaxStack,
            IFE精馏塔 => BaseFracProductOutputMax * RectificationTower.MaxStack,
            _ => BaseFracProductOutputMax * 12 / 4//todo: 改为全局
        };
    }

    /// <summary>
    /// 返回分馏塔流动输出缓存最大数目，由于输出仅由塔的MaxStack决定，所以根据当前MaxStack动态变化
    /// </summary>
    public static int FluidOutputMax(this ItemProto fractionator) {
        return fractionator.ID switch {
            IFE交互塔 => BaseFracFluidOutputMax * Mathf.Max(1, InteractionTower.MaxStack / 4),
            IFE矿物复制塔 => BaseFracFluidOutputMax * Mathf.Max(1, MineralReplicationTower.MaxStack / 4),
            IFE点数聚集塔 => BaseFracFluidOutputMax * Mathf.Max(1, PointAggregateTower.MaxStack / 4),
            IFE转化塔 => BaseFracFluidOutputMax * Mathf.Max(1, ConversionTower.MaxStack / 4),
            IFE精馏塔 => BaseFracFluidOutputMax * Mathf.Max(1, RectificationTower.MaxStack / 4),
            _ => BaseFracFluidOutputMax * 12 / 4//todo: 改为全局
        };
    }

    public static float SuccessBoost(this ItemProto fractionator) {
        return fractionator.ID switch {
            IFE交互塔 => InteractionTower.SuccessBoost,
            IFE矿物复制塔 => MineralReplicationTower.SuccessBoost,
            IFE点数聚集塔 => PointAggregateTower.SuccessBoost,
            IFE转化塔 => ConversionTower.SuccessBoost,
            IFE精馏塔 => RectificationTower.SuccessBoost,
            _ => 0
        };
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("InteractionTower", InteractionTower.Import),
            ("MineralReplicationTower", MineralReplicationTower.Import),
            ("PointAggregateTower", PointAggregateTower.Import),
            ("ConversionTower", ConversionTower.Import),
            ("RectificationTower", RectificationTower.Import),
            ("PlanetaryInteractionStation", PlanetaryInteractionStation.Import),
            ("InterstellarInteractionStation", InterstellarInteractionStation.Import),
            ("OutputExtend", FractionatorOutputState.OutputExtendImport),
            ("LockedOutput", FractionatorSingleLock.LockedOutputImport),
            ("FissionPointPool", FissionPointPool.FissionPointPoolImport),
            ("Resonance", ResonanceState.ResonanceImport),
            ("BuildingExp", BuildingGrowthService.Import)
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("InteractionTower", InteractionTower.Export),
            ("MineralReplicationTower", MineralReplicationTower.Export),
            ("PointAggregateTower", PointAggregateTower.Export),
            ("ConversionTower", ConversionTower.Export),
            ("RectificationTower", RectificationTower.Export),
            ("PlanetaryInteractionStation", PlanetaryInteractionStation.Export),
            ("InterstellarInteractionStation", InterstellarInteractionStation.Export),
            ("OutputExtend", FractionatorOutputState.OutputExtendExport),
            ("LockedOutput", FractionatorSingleLock.LockedOutputExport),
            ("FissionPointPool", FissionPointPool.FissionPointPoolExport),
            ("Resonance", ResonanceState.ResonanceExport),
            ("BuildingExp", BuildingGrowthService.Export)
        );
    }

    public static void IntoOtherSave() {
        InteractionTower.IntoOtherSave();
        MineralReplicationTower.IntoOtherSave();
        PointAggregateTower.IntoOtherSave();
        ConversionTower.IntoOtherSave();
        RectificationTower.IntoOtherSave();
        PlanetaryInteractionStation.IntoOtherSave();
        InterstellarInteractionStation.IntoOtherSave();
        FractionatorOutputState.OutputExtendIntoOtherSave();
        FractionatorSingleLock.LockedOutputIntoOtherSave();
        FissionPointPool.FissionPointPoolIntoOtherSave();
        ResonanceState.ResonanceIntoOtherSave();
        BuildingGrowthService.IntoOtherSave();
    }

    #endregion

    /// <summary>
    /// 将已建造的建筑转为新的ID
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityData), nameof(EntityData.Import))]
    public static void EntityData_Import_Postfix(ref EntityData __instance) {
        if (__instance.modelIndex == 606) {
            __instance.protoId = IFE精馏塔;
            __instance.modelIndex = MFE精馏塔;
        }
        if (__instance.modelIndex == 607) {
            __instance.protoId = IFE转化塔;
            __instance.modelIndex = MFE转化塔;
        }
        if (__instance.modelIndex == 608) {
            __instance.protoId = IFE行星内物流交互站;
            __instance.modelIndex = MFE行星内物流交互站;
        }
        if (__instance.modelIndex == 609) {
            __instance.protoId = IFE星际物流交互站;
            __instance.modelIndex = MFE星际物流交互站;
        }
    }
}
