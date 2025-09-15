using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Building;
using FE.Logic.Recipe;
using HarmonyLib;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class BuildingManager {
    public static void AddTranslations() {
        InteractionTower.AddTranslations();
        MineralCopyTower.AddTranslations();
        PointAggregateTower.AddTranslations();
        QuantumCopyTower.AddTranslations();
        AlchemyTower.AddTranslations();
        DeconstructionTower.AddTranslations();
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
        MineralCopyTower.Create();
        PointAggregateTower.Create();
        QuantumCopyTower.Create();
        AlchemyTower.Create();
        DeconstructionTower.Create();
        ConversionTower.Create();

        PlanetaryInteractionStation.Create();
        InterstellarInteractionStation.Create();
    }

    public static void SetFractionatorMaterial() {
        InteractionTower.SetMaterial();
        MineralCopyTower.SetMaterial();
        PointAggregateTower.SetMaterial();
        QuantumCopyTower.SetMaterial();
        AlchemyTower.SetMaterial();
        DeconstructionTower.SetMaterial();
        ConversionTower.SetMaterial();

        PlanetaryInteractionStation.SetMaterial();
        InterstellarInteractionStation.SetMaterial();
    }

    public static void UpdateHpAndEnergy() {
        InteractionTower.UpdateHpAndEnergy();
        MineralCopyTower.UpdateHpAndEnergy();
        PointAggregateTower.UpdateHpAndEnergy();
        QuantumCopyTower.UpdateHpAndEnergy();
        AlchemyTower.UpdateHpAndEnergy();
        DeconstructionTower.UpdateHpAndEnergy();
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
            IFE矿物复制塔 => BaseFracProductOutputMax * MineralCopyTower.MaxProductOutputStack,
            IFE点数聚集塔 => BaseFracProductOutputMax * PointAggregateTower.MaxProductOutputStack,
            IFE量子复制塔 => BaseFracProductOutputMax * QuantumCopyTower.MaxProductOutputStack,
            IFE点金塔 => BaseFracProductOutputMax * AlchemyTower.MaxProductOutputStack,
            IFE分解塔 => BaseFracProductOutputMax * DeconstructionTower.MaxProductOutputStack,
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
            IFE矿物复制塔 => MineralCopyTower.EnableFluidOutputStack,
            IFE点数聚集塔 => PointAggregateTower.EnableFluidOutputStack,
            IFE量子复制塔 => QuantumCopyTower.EnableFluidOutputStack,
            IFE点金塔 => AlchemyTower.EnableFluidOutputStack,
            IFE分解塔 => DeconstructionTower.EnableFluidOutputStack,
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
                MineralCopyTower.EnableFluidOutputStack = enable;
                break;
            case IFE点数聚集塔:
                PointAggregateTower.EnableFluidOutputStack = enable;
                break;
            case IFE量子复制塔:
                QuantumCopyTower.EnableFluidOutputStack = enable;
                break;
            case IFE点金塔:
                AlchemyTower.EnableFluidOutputStack = enable;
                break;
            case IFE分解塔:
                DeconstructionTower.EnableFluidOutputStack = enable;
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
            IFE矿物复制塔 => MineralCopyTower.MaxProductOutputStack,
            IFE点数聚集塔 => PointAggregateTower.MaxProductOutputStack,
            IFE量子复制塔 => QuantumCopyTower.MaxProductOutputStack,
            IFE点金塔 => AlchemyTower.MaxProductOutputStack,
            IFE分解塔 => DeconstructionTower.MaxProductOutputStack,
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
                MineralCopyTower.MaxProductOutputStack = stack;
                break;
            case IFE点数聚集塔:
                PointAggregateTower.MaxProductOutputStack = stack;
                break;
            case IFE量子复制塔:
                QuantumCopyTower.MaxProductOutputStack = stack;
                break;
            case IFE点金塔:
                AlchemyTower.MaxProductOutputStack = stack;
                break;
            case IFE分解塔:
                DeconstructionTower.MaxProductOutputStack = stack;
                break;
            case IFE转化塔:
                ConversionTower.MaxProductOutputStack = stack;
                break;
            case IFE行星内物流交互站:
                PlanetaryInteractionStation.MaxProductOutputStack = stack;
                break;
            default:
                return;
        }
    }

    public static bool EnableFracForever(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.EnableFracForever,
            IFE矿物复制塔 => MineralCopyTower.EnableFracForever,
            IFE点数聚集塔 => PointAggregateTower.EnableFracForever,
            IFE量子复制塔 => QuantumCopyTower.EnableFracForever,
            IFE点金塔 => AlchemyTower.EnableFracForever,
            IFE分解塔 => DeconstructionTower.EnableFracForever,
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
                MineralCopyTower.EnableFracForever = enable;
                break;
            case IFE点数聚集塔:
                PointAggregateTower.EnableFracForever = enable;
                break;
            case IFE量子复制塔:
                QuantumCopyTower.EnableFracForever = enable;
                break;
            case IFE点金塔:
                AlchemyTower.EnableFracForever = enable;
                break;
            case IFE分解塔:
                DeconstructionTower.EnableFracForever = enable;
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
            IFE矿物复制塔 => MineralCopyTower.workEnergyPerTick,
            IFE点数聚集塔 => PointAggregateTower.workEnergyPerTick,
            IFE量子复制塔 => QuantumCopyTower.workEnergyPerTick,
            IFE点金塔 => AlchemyTower.workEnergyPerTick,
            IFE分解塔 => DeconstructionTower.workEnergyPerTick,
            IFE转化塔 => ConversionTower.workEnergyPerTick,
            _ => LDB.models.Select(M分馏塔).prefabDesc.workEnergyPerTick
        };
    }

    public static long idleEnergyPerTick(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.idleEnergyPerTick;
            case IFE矿物复制塔:
                return MineralCopyTower.idleEnergyPerTick;
            case IFE点数聚集塔:
                return PointAggregateTower.idleEnergyPerTick;
            case IFE量子复制塔:
                return QuantumCopyTower.idleEnergyPerTick;
            case IFE点金塔:
                return AlchemyTower.idleEnergyPerTick;
            case IFE分解塔:
                return DeconstructionTower.idleEnergyPerTick;
            case IFE转化塔:
                return ConversionTower.idleEnergyPerTick;
            default:
                return LDB.models.Select(M分馏塔).prefabDesc.idleEnergyPerTick;
        }
    }

    public static int ReinforcementLevel(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementLevel,
            IFE矿物复制塔 => MineralCopyTower.ReinforcementLevel,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementLevel,
            IFE量子复制塔 => QuantumCopyTower.ReinforcementLevel,
            IFE点金塔 => AlchemyTower.ReinforcementLevel,
            IFE分解塔 => DeconstructionTower.ReinforcementLevel,
            IFE转化塔 => ConversionTower.ReinforcementLevel,
            IFE行星内物流交互站 => PlanetaryInteractionStation.ReinforcementLevel,
            _ => 0
        };
    }

    public static void ReinforcementLevel(this ItemProto building, int level) {
        switch (building.ID) {
            case IFE交互塔:
                InteractionTower.ReinforcementLevel = level;
                InteractionTower.UpdateHpAndEnergy();
                break;
            case IFE矿物复制塔:
                MineralCopyTower.ReinforcementLevel = level;
                MineralCopyTower.UpdateHpAndEnergy();
                break;
            case IFE点数聚集塔:
                PointAggregateTower.ReinforcementLevel = level;
                PointAggregateTower.UpdateHpAndEnergy();
                break;
            case IFE量子复制塔:
                QuantumCopyTower.ReinforcementLevel = level;
                QuantumCopyTower.UpdateHpAndEnergy();
                break;
            case IFE点金塔:
                AlchemyTower.ReinforcementLevel = level;
                AlchemyTower.UpdateHpAndEnergy();
                break;
            case IFE分解塔:
                DeconstructionTower.ReinforcementLevel = level;
                DeconstructionTower.UpdateHpAndEnergy();
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
    }

    public static float ReinforcementSuccessRate(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementSuccessRate,
            IFE矿物复制塔 => MineralCopyTower.ReinforcementSuccessRate,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementSuccessRate,
            IFE量子复制塔 => QuantumCopyTower.ReinforcementSuccessRate,
            IFE点金塔 => AlchemyTower.ReinforcementSuccessRate,
            IFE分解塔 => DeconstructionTower.ReinforcementSuccessRate,
            IFE转化塔 => ConversionTower.ReinforcementSuccessRate,
            IFE行星内物流交互站 => PlanetaryInteractionStation.ReinforcementSuccessRate,
            _ => 0
        };
    }

    public static float ReinforcementBonusDurability(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementBonusDurability,
            IFE矿物复制塔 => MineralCopyTower.ReinforcementBonusDurability,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusDurability,
            IFE量子复制塔 => QuantumCopyTower.ReinforcementBonusDurability,
            IFE点金塔 => AlchemyTower.ReinforcementBonusDurability,
            IFE分解塔 => DeconstructionTower.ReinforcementBonusDurability,
            IFE转化塔 => ConversionTower.ReinforcementBonusDurability,
            IFE行星内物流交互站 => PlanetaryInteractionStation.ReinforcementBonusDurability,
            _ => 0
        };
    }

    public static float ReinforcementBonusEnergy(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.ReinforcementBonusEnergy,
            IFE矿物复制塔 => MineralCopyTower.ReinforcementBonusEnergy,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusEnergy,
            IFE量子复制塔 => QuantumCopyTower.ReinforcementBonusEnergy,
            IFE点金塔 => AlchemyTower.ReinforcementBonusEnergy,
            IFE分解塔 => DeconstructionTower.ReinforcementBonusEnergy,
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
            IFE矿物复制塔 => MineralCopyTower.ReinforcementBonusFracSuccess,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusFracSuccess,
            IFE量子复制塔 => QuantumCopyTower.ReinforcementBonusFracSuccess,
            IFE点金塔 => AlchemyTower.ReinforcementBonusFracSuccess,
            IFE分解塔 => DeconstructionTower.ReinforcementBonusFracSuccess,
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
            IFE矿物复制塔 => MineralCopyTower.ReinforcementBonusMainOutputCount,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusMainOutputCount,
            IFE量子复制塔 => QuantumCopyTower.ReinforcementBonusMainOutputCount,
            IFE点金塔 => AlchemyTower.ReinforcementBonusMainOutputCount,
            IFE分解塔 => DeconstructionTower.ReinforcementBonusMainOutputCount,
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
            IFE矿物复制塔 => MineralCopyTower.ReinforcementBonusAppendOutputRate,
            IFE点数聚集塔 => PointAggregateTower.ReinforcementBonusAppendOutputRate,
            IFE量子复制塔 => QuantumCopyTower.ReinforcementBonusAppendOutputRate,
            IFE点金塔 => AlchemyTower.ReinforcementBonusAppendOutputRate,
            IFE分解塔 => DeconstructionTower.ReinforcementBonusAppendOutputRate,
            IFE转化塔 => ConversionTower.ReinforcementBonusAppendOutputRate,
            _ => 0
        };
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        OutputExtendImport(r);
        InteractionTower.Import(r);
        MineralCopyTower.Import(r);
        PointAggregateTower.Import(r);
        QuantumCopyTower.Import(r);
        AlchemyTower.Import(r);
        DeconstructionTower.Import(r);
        ConversionTower.Import(r);
        PlanetaryInteractionStation.Import(r);
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        OutputExtendExport(w);
        InteractionTower.Export(w);
        MineralCopyTower.Export(w);
        PointAggregateTower.Export(w);
        QuantumCopyTower.Export(w);
        AlchemyTower.Export(w);
        DeconstructionTower.Export(w);
        ConversionTower.Export(w);
        PlanetaryInteractionStation.Export(w);
    }

    public static void IntoOtherSave() {
        OutputExtendIntoOtherSave();
        InteractionTower.IntoOtherSave();
        MineralCopyTower.IntoOtherSave();
        PointAggregateTower.IntoOtherSave();
        QuantumCopyTower.IntoOtherSave();
        AlchemyTower.IntoOtherSave();
        DeconstructionTower.IntoOtherSave();
        ConversionTower.IntoOtherSave();
        PlanetaryInteractionStation.IntoOtherSave();
    }

    #endregion
}
