using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Recipe;
using HarmonyLib;
using NebulaAPI;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class BuildingManager {
    public static void AddTranslations() {
        InteractionTower.AddTranslations();
        MineralReplicationTower.AddTranslations();
        PointAggregateTower.AddTranslations();
        ConversionTower.AddTranslations();
        RecycleTower.AddTranslations();

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
        RecycleTower.Create();

        PlanetaryInteractionStation.Create();
        InterstellarInteractionStation.Create();
    }

    public static void SetFractionatorMaterial() {
        InteractionTower.SetMaterial();
        MineralReplicationTower.SetMaterial();
        PointAggregateTower.SetMaterial();
        ConversionTower.SetMaterial();
        RecycleTower.SetMaterial();

        PlanetaryInteractionStation.SetMaterial();
        InterstellarInteractionStation.SetMaterial();
    }

    public static void UpdateHpAndEnergy() {
        InteractionTower.UpdateHpAndEnergy();
        MineralReplicationTower.UpdateHpAndEnergy();
        PointAggregateTower.UpdateHpAndEnergy();
        ConversionTower.UpdateHpAndEnergy();
        RecycleTower.UpdateHpAndEnergy();

        PlanetaryInteractionStation.UpdateHpAndEnergy();
        InterstellarInteractionStation.UpdateHpAndEnergy();
    }

    /// <summary>
    /// 调整分馏塔缓存区大小（实际运行不使用此值，该方法只对原版分馏塔生效）
    /// </summary>
    public static void SetFractionatorCacheSize() {
        foreach (ModelProto modelProto in LDB.models.dataArray) {
            if (modelProto.prefabDesc.isFractionator) {
                modelProto.prefabDesc.fracFluidInputMax = BaseFracFluidInputMax;
                modelProto.prefabDesc.fracProductOutputMax = BaseFracProductOutputMax;
                modelProto.prefabDesc.fracFluidOutputMax = BaseFracFluidOutputMax;
            }
        }
    }

    /// <summary>
    /// 调整已放置的分馏塔缓存区大小（实际运行不使用此值，该方法只对原版分馏塔生效）
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
    public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
        __instance.fluidInputMax = BaseFracFluidInputMax;
        __instance.productOutputMax = BaseFracProductOutputMax;
        __instance.fluidOutputMax = BaseFracFluidOutputMax;
    }

    /// <summary>
    /// 返回分馏塔流动输入缓存大小
    /// </summary>
    public static int FluidInputMax(this ItemProto fractionator) {
        return BaseFracFluidInputMax;
    }

    /// <summary>
    /// 返回分馏塔产物输出缓存大小
    /// </summary>
    public static int ProductOutputMax(this ItemProto fractionator) {
        return fractionator.ID switch {
            IFE交互塔 => BaseFracProductOutputMax * InteractionTower.MaxProductOutputStack,
            IFE矿物复制塔 => BaseFracProductOutputMax * MineralReplicationTower.MaxProductOutputStack,
            IFE点数聚集塔 => BaseFracProductOutputMax * PointAggregateTower.MaxProductOutputStack,
            IFE转化塔 => BaseFracProductOutputMax * ConversionTower.MaxProductOutputStack,
            IFE回收塔 => BaseFracProductOutputMax * RecycleTower.MaxProductOutputStack,
            _ => BaseFracProductOutputMax
        };
    }

    /// <summary>
    /// 返回分馏塔流动输出缓存大小
    /// </summary>
    public static int FluidOutputMax(this ItemProto fractionator) {
        return BaseFracFluidOutputMax;
    }

    #region 分馏塔产物输出拓展

    /// <summary>
    /// 存储分馏塔所有产物。结构：
    /// (planetId, entityId) => List&lt;ProductOutputInfo&gt;
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), List<ProductOutputInfo>> outputDic = [];

    public static void OutputExtendImport(BinaryReader r) {
        int version = r.ReadInt32();
        outputDic.Clear();
        int fractionatorNum = r.ReadInt32();
        for (int i = 0; i < fractionatorNum; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            List<ProductOutputInfo> outputList = [];
            int outputKinds = r.ReadInt32();
            for (int j = 0; j < outputKinds; j++) {
                bool isMainOutput = r.ReadBoolean();
                int outputId = r.ReadInt32();
                int outputCount = r.ReadInt32();
                if (LDB.items.Exist(outputId)) {
                    continue;
                }
                outputList.Add(new(isMainOutput, outputId, outputCount));
            }
            outputDic.TryAdd((planetId, entityId), outputList);
        }
    }

    public static void OutputExtendExport(BinaryWriter w) {
        w.Write(1);
        w.Write(outputDic.Count);
        foreach (var p in outputDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            List<ProductOutputInfo> outputList = outputDic[p.Key];
            w.Write(outputList.Count);
            foreach (ProductOutputInfo outputItem in outputList) {
                w.Write(outputItem.isMainOutput);
                w.Write(outputItem.itemId);
                w.Write(outputItem.count);
            }
        }
    }

    public static void OutputExtendIntoOtherSave() {
        outputDic.Clear();
    }

    public static List<ProductOutputInfo> products(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (!outputDic.ContainsKey((planetId, entityId))) {
            outputDic.TryAdd((planetId, entityId), []);
        }
        return outputDic[(planetId, entityId)];
    }

    #endregion

    public static int Level(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.Level,
            IFE矿物复制塔 => MineralReplicationTower.Level,
            IFE点数聚集塔 => PointAggregateTower.Level,
            IFE转化塔 => ConversionTower.Level,
            IFE回收塔 => RecycleTower.Level,
            IFE行星内物流交互站 => PlanetaryInteractionStation.Level,
            IFE星际物流交互站 => InterstellarInteractionStation.Level,
            _ => 0
        };
    }

    public static void Level(this ItemProto building, int level, bool manual = false) {
        switch (building.ID) {
            case IFE交互塔:
                InteractionTower.Level = level;
                InteractionTower.UpdateHpAndEnergy();
                break;
            case IFE矿物复制塔:
                MineralReplicationTower.Level = level;
                MineralReplicationTower.UpdateHpAndEnergy();
                break;
            case IFE点数聚集塔:
                PointAggregateTower.Level = level;
                PointAggregateTower.UpdateHpAndEnergy();
                break;
            case IFE转化塔:
                ConversionTower.Level = level;
                ConversionTower.UpdateHpAndEnergy();
                break;
            case IFE回收塔:
                RecycleTower.Level = level;
                RecycleTower.UpdateHpAndEnergy();
                break;
            case IFE行星内物流交互站:
            case IFE星际物流交互站:
                PlanetaryInteractionStation.Level = level;
                PlanetaryInteractionStation.UpdateHpAndEnergy();
                InterstellarInteractionStation.UpdateHpAndEnergy();
                break;
            default:
                return;
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new BuildingChangePacket(building.ID, 1, level));
        }
    }

    public static bool EnableFluidEnhancement(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnableFluidEnhancement,
            IFE矿物复制塔 => MineralReplicationTower.EnableFluidEnhancement,
            IFE点数聚集塔 => PointAggregateTower.EnableFluidEnhancement,
            IFE转化塔 => ConversionTower.EnableFluidEnhancement,
            IFE回收塔 => RecycleTower.EnableFluidEnhancement,
            _ => false
        };
    }

    public static int MaxProductOutputStack(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.MaxProductOutputStack,
            IFE矿物复制塔 => MineralReplicationTower.MaxProductOutputStack,
            IFE点数聚集塔 => PointAggregateTower.MaxProductOutputStack,
            IFE转化塔 => ConversionTower.MaxProductOutputStack,
            IFE回收塔 => RecycleTower.MaxProductOutputStack,
            IFE行星内物流交互站 => PlanetaryInteractionStation.MaxProductOutputStack,
            IFE星际物流交互站 => InterstellarInteractionStation.MaxProductOutputStack,
            _ => 1
        };
    }

    public static long workEnergyPerTick(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.workEnergyPerTick,
            IFE矿物复制塔 => MineralReplicationTower.workEnergyPerTick,
            IFE点数聚集塔 => PointAggregateTower.workEnergyPerTick,
            IFE转化塔 => ConversionTower.workEnergyPerTick,
            IFE回收塔 => RecycleTower.workEnergyPerTick,
            IFE行星内物流交互站 => PlanetaryInteractionStation.workEnergyPerTick,
            IFE星际物流交互站 => InterstellarInteractionStation.workEnergyPerTick,
            _ => LDB.models.Select(M分馏塔).prefabDesc.workEnergyPerTick
        };
    }

    public static long idleEnergyPerTick(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.idleEnergyPerTick;
            case IFE矿物复制塔:
                return MineralReplicationTower.idleEnergyPerTick;
            case IFE点数聚集塔:
                return PointAggregateTower.idleEnergyPerTick;
            case IFE转化塔:
                return ConversionTower.idleEnergyPerTick;
            case IFE回收塔:
                return RecycleTower.idleEnergyPerTick;
            case IFE行星内物流交互站:
                return PlanetaryInteractionStation.idleEnergyPerTick;
            case IFE星际物流交互站:
                return InterstellarInteractionStation.idleEnergyPerTick;
            default:
                return LDB.models.Select(M分馏塔).prefabDesc.idleEnergyPerTick;
        }
    }

    public static float EnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnergyRatio,
            IFE矿物复制塔 => MineralReplicationTower.EnergyRatio,
            IFE点数聚集塔 => PointAggregateTower.EnergyRatio,
            IFE转化塔 => ConversionTower.EnergyRatio,
            IFE回收塔 => RecycleTower.EnergyRatio,
            IFE行星内物流交互站 => PlanetaryInteractionStation.EnergyRatio,
            IFE星际物流交互站 => InterstellarInteractionStation.EnergyRatio,
            _ => 1.0f
        };
    }

    public static float InteractEnergyRatio(this ItemProto building) {
        return building.ID switch {
            IFE行星内物流交互站 => PlanetaryInteractionStation.InteractEnergyRatio,
            IFE星际物流交互站 => InterstellarInteractionStation.InteractEnergyRatio,
            _ => 1.0f
        };
    }

    public static float PlrRatio(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.PlrRatio,
            IFE矿物复制塔 => MineralReplicationTower.PlrRatio,
            IFE点数聚集塔 => PointAggregateTower.PlrRatio,
            IFE转化塔 => ConversionTower.PlrRatio,
            IFE回收塔 => RecycleTower.PlrRatio,
            _ => 1.0f
        };
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        OutputExtendImport(r);
        InteractionTower.Import(r);
        MineralReplicationTower.Import(r);
        PointAggregateTower.Import(r);
        ConversionTower.Import(r);
        PlanetaryInteractionStation.Import(r);
        RecycleTower.Import(r);
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        OutputExtendExport(w);
        InteractionTower.Export(w);
        MineralReplicationTower.Export(w);
        PointAggregateTower.Export(w);
        ConversionTower.Export(w);
        PlanetaryInteractionStation.Export(w);
        RecycleTower.Export(w);
    }

    public static void IntoOtherSave() {
        OutputExtendIntoOtherSave();
        InteractionTower.IntoOtherSave();
        MineralReplicationTower.IntoOtherSave();
        PointAggregateTower.IntoOtherSave();
        ConversionTower.IntoOtherSave();
        PlanetaryInteractionStation.IntoOtherSave();
        RecycleTower.IntoOtherSave();
    }

    #endregion

    /// <summary>
    /// 将已建造的建筑转为新的ID
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityData), nameof(EntityData.Import))]
    public static void EntityData_Import_Postfix(ref EntityData __instance) {
        if (__instance.modelIndex == 606) {
            __instance.protoId = IFE回收塔;
            __instance.modelIndex = MFE回收塔;
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
