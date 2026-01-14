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

        PlanetaryInteractionStation.Create();
        InterstellarInteractionStation.Create();
    }

    public static void SetFractionatorMaterial() {
        InteractionTower.SetMaterial();
        MineralReplicationTower.SetMaterial();
        PointAggregateTower.SetMaterial();
        ConversionTower.SetMaterial();

        PlanetaryInteractionStation.SetMaterial();
        InterstellarInteractionStation.SetMaterial();
    }

    public static void UpdateHpAndEnergy() {
        InteractionTower.UpdateHpAndEnergy();
        MineralReplicationTower.UpdateHpAndEnergy();
        PointAggregateTower.UpdateHpAndEnergy();
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

    public static bool EnableFluidOutputStack(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnableFluidOutputStack,
            IFE矿物复制塔 => MineralReplicationTower.EnableFluidOutputStack,
            IFE点数聚集塔 => PointAggregateTower.EnableFluidOutputStack,
            IFE转化塔 => ConversionTower.EnableFluidOutputStack,
            _ => false
        };
    }

    public static void EnableFluidOutputStack(this ItemProto building, bool enable) {
        switch (building.ID) {
            case IFE交互塔:
                InteractionTower.EnableFluidOutputStack = enable;
                break;
            case IFE矿物复制塔:
                MineralReplicationTower.EnableFluidOutputStack = enable;
                break;
            case IFE点数聚集塔:
                PointAggregateTower.EnableFluidOutputStack = enable;
                break;
            case IFE转化塔:
                ConversionTower.EnableFluidOutputStack = enable;
                break;
            default:
                return;
        }
    }

    public static int MaxProductOutputStack(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.MaxProductOutputStack,
            IFE矿物复制塔 => MineralReplicationTower.MaxProductOutputStack,
            IFE点数聚集塔 => PointAggregateTower.MaxProductOutputStack,
            IFE转化塔 => ConversionTower.MaxProductOutputStack,
            IFE行星内物流交互站 => PlanetaryInteractionStation.MaxProductOutputStack,
            _ => 1
        };
    }

    public static void MaxProductOutputStack(this ItemProto building, int stack) {
        switch (building.ID) {
            case IFE交互塔:
                InteractionTower.MaxProductOutputStack = stack;
                break;
            case IFE矿物复制塔:
                MineralReplicationTower.MaxProductOutputStack = stack;
                break;
            case IFE点数聚集塔:
                PointAggregateTower.MaxProductOutputStack = stack;
                break;
            case IFE转化塔:
                ConversionTower.MaxProductOutputStack = stack;
                break;
            case IFE行星内物流交互站:
                PlanetaryInteractionStation.MaxProductOutputStack = stack;
                StationManager.SetMaxCount();
                break;
            default:
                return;
        }
    }

    public static bool EnableFracForever(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnableFracForever,
            IFE矿物复制塔 => MineralReplicationTower.EnableFracForever,
            IFE点数聚集塔 => PointAggregateTower.EnableFracForever,
            IFE转化塔 => ConversionTower.EnableFracForever,
            _ => false
        };
    }

    public static void EnableFracForever(this ItemProto building, bool enable) {
        switch (building.ID) {
            case IFE交互塔:
                InteractionTower.EnableFracForever = enable;
                break;
            case IFE矿物复制塔:
                MineralReplicationTower.EnableFracForever = enable;
                break;
            case IFE点数聚集塔:
                PointAggregateTower.EnableFracForever = enable;
                break;
            case IFE转化塔:
                ConversionTower.EnableFracForever = enable;
                break;
            default:
                return;
        }
    }

    public static long workEnergyPerTick(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.workEnergyPerTick,
            IFE矿物复制塔 => MineralReplicationTower.workEnergyPerTick,
            IFE点数聚集塔 => PointAggregateTower.workEnergyPerTick,
            IFE转化塔 => ConversionTower.workEnergyPerTick,
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
            default:
                return LDB.models.Select(M分馏塔).prefabDesc.idleEnergyPerTick;
        }
    }

    public static int ReinforcementLevel(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementLevel,
            IFE矿物复制塔 => MineralReplicationTower.ReinforcementLevel,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementLevel,
            IFE转化塔 => ConversionTower.ReinforcementLevel,
            IFE行星内物流交互站 => PlanetaryInteractionStation.ReinforcementLevel,
            _ => 0
        };
    }

    public static void ReinforcementLevel(this ItemProto building, int level, bool manual = false) {
        switch (building.ID) {
            case IFE交互塔:
                InteractionTower.ReinforcementLevel = level;
                InteractionTower.UpdateHpAndEnergy();
                break;
            case IFE矿物复制塔:
                MineralReplicationTower.ReinforcementLevel = level;
                MineralReplicationTower.UpdateHpAndEnergy();
                break;
            case IFE点数聚集塔:
                PointAggregateTower.ReinforcementLevel = level;
                PointAggregateTower.UpdateHpAndEnergy();
                break;
            case IFE转化塔:
                ConversionTower.ReinforcementLevel = level;
                ConversionTower.UpdateHpAndEnergy();
                break;
            case IFE行星内物流交互站:
                PlanetaryInteractionStation.ReinforcementLevel = level;
                PlanetaryInteractionStation.UpdateHpAndEnergy();
                break;
            default:
                return;
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new BuildingChangePacket(building.ID, 5, level));
        }
    }

    public static float ReinforcementSuccessRate(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementSuccessRate,
            IFE矿物复制塔 => MineralReplicationTower.ReinforcementSuccessRate,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementSuccessRate,
            IFE转化塔 => ConversionTower.ReinforcementSuccessRate,
            IFE行星内物流交互站 => PlanetaryInteractionStation.ReinforcementSuccessRate,
            _ => 0
        };
    }

    public static float ReinforcementBonusDurability(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementBonusDurability,
            IFE矿物复制塔 => MineralReplicationTower.ReinforcementBonusDurability,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusDurability,
            IFE转化塔 => ConversionTower.ReinforcementBonusDurability,
            IFE行星内物流交互站 => PlanetaryInteractionStation.ReinforcementBonusDurability,
            _ => 0
        };
    }

    public static float ReinforcementBonusEnergy(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementBonusEnergy,
            IFE矿物复制塔 => MineralReplicationTower.ReinforcementBonusEnergy,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusEnergy,
            IFE转化塔 => ConversionTower.ReinforcementBonusEnergy,
            IFE行星内物流交互站 => PlanetaryInteractionStation.ReinforcementBonusEnergy,
            _ => 0
        };
    }

    /// <summary>
    /// 强化对配方的基础成功率加成，与其他增幅累乘
    /// </summary>
    public static float ReinforcementBonusFracSuccess(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementBonusFracSuccess,
            IFE矿物复制塔 => MineralReplicationTower.ReinforcementBonusFracSuccess,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusFracSuccess,
            IFE转化塔 => ConversionTower.ReinforcementBonusFracSuccess,
            _ => 0
        };
    }

    /// <summary>
    /// 强化对配方的主产物数目的加成，与其他增幅累加
    /// </summary>
    public static float ReinforcementBonusMainOutputCount(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementBonusMainOutputCount,
            IFE矿物复制塔 => MineralReplicationTower.ReinforcementBonusMainOutputCount,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusMainOutputCount,
            IFE转化塔 => ConversionTower.ReinforcementBonusMainOutputCount,
            _ => 0
        };
    }

    /// <summary>
    /// 强化对配方的副产物概率的加成，与其他增幅累乘
    /// </summary>
    public static float ReinforcementBonusAppendOutputRate(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementBonusAppendOutputRate,
            IFE矿物复制塔 => MineralReplicationTower.ReinforcementBonusAppendOutputRate,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusAppendOutputRate,
            IFE转化塔 => ConversionTower.ReinforcementBonusAppendOutputRate,
            _ => 0
        };
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        OutputExtendImport(r);
        InteractionTower.Import(r);
        MineralReplicationTower.Import(r);
        PointAggregateTower.Import(r);
        if (version < 3) {
            for (int i = 0; i < 3; i++) {
                r.ReadInt32();
                r.ReadBoolean();
                r.ReadInt32();
                r.ReadBoolean();
                r.ReadInt32();
            }
        }
        ConversionTower.Import(r);
        if (version >= 2) {
            PlanetaryInteractionStation.Import(r);
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(3);
        OutputExtendExport(w);
        InteractionTower.Export(w);
        MineralReplicationTower.Export(w);
        PointAggregateTower.Export(w);
        ConversionTower.Export(w);
        PlanetaryInteractionStation.Export(w);
    }

    public static void IntoOtherSave() {
        OutputExtendIntoOtherSave();
        InteractionTower.IntoOtherSave();
        MineralReplicationTower.IntoOtherSave();
        PointAggregateTower.IntoOtherSave();
        ConversionTower.IntoOtherSave();
        PlanetaryInteractionStation.IntoOtherSave();
    }

    #endregion
}
