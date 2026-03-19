using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Recipe;
using HarmonyLib;
using NebulaAPI;
using UnityEngine;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class BuildingManager {
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


    #region 分馏塔产物输出拓展

    /// <summary>
    /// 存储分馏塔所有产物。结构：
    /// (planetId, entityId) => List&lt;ProductOutputInfo&gt;
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), List<ProductOutputInfo>> outputDic = [];

    public static void OutputExtendImport(BinaryReader r) {
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

    #region 转化塔锁定

    /// <summary>
    /// 存储转化塔锁定的输出物品ID。结构：
    /// (planetId, entityId) => lockedOutputItemId (0 = 未锁定)
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), int> lockedOutputDic = [];

    public static void LockedOutputImport(BinaryReader r) {
        lockedOutputDic.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            int itemId = r.ReadInt32();
            lockedOutputDic.TryAdd((planetId, entityId), itemId);
        }
    }

    public static void LockedOutputExport(BinaryWriter w) {
        w.Write(lockedOutputDic.Count);
        foreach (var p in lockedOutputDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            w.Write(p.Value);
        }
    }

    public static void LockedOutputIntoOtherSave() {
        lockedOutputDic.Clear();
    }

    public static int GetLockedOutput(this FractionatorComponent fractionator, PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return lockedOutputDic.TryGetValue((planetId, entityId), out int itemId) ? itemId : 0;
    }

    public static void SetLockedOutput(this FractionatorComponent fractionator, PlanetFactory factory, int itemId) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (itemId == 0) {
            lockedOutputDic.TryRemove((planetId, entityId), out _);
        } else {
            lockedOutputDic[(planetId, entityId)] = itemId;
        }
    }

    public static int CountInteractionTowers() {
        int count = 0;
        if (GameMain.data == null || GameMain.data.factories == null) return 0;
        for (int i = 0; i < GameMain.data.factories.Length; i++) {
            PlanetFactory factory = GameMain.data.factories[i];
            if (factory == null || factory.factorySystem == null) continue;
            for (int j = 1; j < factory.factorySystem.fractionatorCursor; j++) {
                if (factory.factorySystem.fractionatorPool[j].id == j) {
                    int buildingID = factory.entityPool[factory.factorySystem.fractionatorPool[j].entityId].protoId;
                    if (buildingID == IFE交互塔) count++;
                }
            }
        }
        return count;
    }

    #endregion

    #region 分馏塔维度共鸣拓展

    /// <summary>
    /// 存储交互塔维度共鸣加成。结构：
    /// (planetId, entityId) => resonanceBoost
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), float> resonanceBoostDic = [];

    public static void ResonanceImport(BinaryReader r) {
        resonanceBoostDic.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            float boost = r.ReadSingle();
            resonanceBoostDic.TryAdd((planetId, entityId), boost);
        }
    }

    public static void ResonanceExport(BinaryWriter w) {
        w.Write(resonanceBoostDic.Count);
        foreach (var p in resonanceBoostDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            w.Write(p.Value);
        }
    }

    public static void ResonanceIntoOtherSave() {
        resonanceBoostDic.Clear();
    }

    public static float GetResonanceBoost(this FractionatorComponent fractionator, PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return resonanceBoostDic.TryGetValue((planetId, entityId), out float boost) ? boost : 0f;
    }

    public static void SetResonanceBoost(this FractionatorComponent fractionator, PlanetFactory factory, float boost) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (boost == 0f) {
            resonanceBoostDic.TryRemove((planetId, entityId), out _);
        } else {
            resonanceBoostDic[(planetId, entityId)] = boost;
        }
    }

    #endregion

    #region 质能裂变点数池

    /// <summary>
    /// 存储矿物复制塔质能裂变点数池。结构：
    /// (planetId, entityId) => fissionPointPool
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), int> fissionPointPoolDic = [];

    public static void FissionPointPoolImport(BinaryReader r) {
        fissionPointPoolDic.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            int points = r.ReadInt32();
            fissionPointPoolDic.TryAdd((planetId, entityId), points);
        }
    }

    public static void FissionPointPoolExport(BinaryWriter w) {
        w.Write(fissionPointPoolDic.Count);
        foreach (var p in fissionPointPoolDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            w.Write(p.Value);
        }
    }

    public static void FissionPointPoolIntoOtherSave() {
        fissionPointPoolDic.Clear();
    }

    public static int GetFissionPointPool(this FractionatorComponent fractionator, PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return fissionPointPoolDic.TryGetValue((planetId, entityId), out int points) ? points : 0;
    }

    public static void SetFissionPointPool(this FractionatorComponent fractionator, PlanetFactory factory, int points) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (points <= 0) {
            fissionPointPoolDic.TryRemove((planetId, entityId), out _);
        } else {
            fissionPointPoolDic[(planetId, entityId)] = points;
        }
    }

    #endregion

    public static int Level(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.Level,
            IFE矿物复制塔 => MineralReplicationTower.Level,
            IFE点数聚集塔 => PointAggregateTower.Level,
            IFE转化塔 => ConversionTower.Level,
            IFE精馏塔 => RectificationTower.Level,
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
            case IFE精馏塔:
                RectificationTower.Level = level;
                RectificationTower.UpdateHpAndEnergy();
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
            IFE精馏塔 => RectificationTower.EnableFluidEnhancement,
            _ => false
        };
    }

    public static int MaxStack(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.MaxStack,
            IFE矿物复制塔 => MineralReplicationTower.MaxStack,
            IFE点数聚集塔 => PointAggregateTower.MaxStack,
            IFE转化塔 => ConversionTower.MaxStack,
            IFE精馏塔 => RectificationTower.MaxStack,
            IFE行星内物流交互站 => PlanetaryInteractionStation.MaxStack,
            IFE星际物流交互站 => InterstellarInteractionStation.MaxStack,
            _ => 1
        };
    }

    public static long workEnergyPerTick(this ItemProto building) {
        return building.ID switch {
            IFE交互塔 => InteractionTower.workEnergyPerTick,
            IFE矿物复制塔 => MineralReplicationTower.workEnergyPerTick,
            IFE点数聚集塔 => PointAggregateTower.workEnergyPerTick,
            IFE转化塔 => ConversionTower.workEnergyPerTick,
            IFE精馏塔 => RectificationTower.workEnergyPerTick,
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
            case IFE精馏塔:
                return RectificationTower.idleEnergyPerTick;
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
            IFE精馏塔 => RectificationTower.EnergyRatio,
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
            IFE精馏塔 => RectificationTower.PlrRatio,
            _ => 1.0f
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
            ("OutputExtend", OutputExtendImport),
            ("LockedOutput", LockedOutputImport),
            ("FissionPointPool", FissionPointPoolImport),
            ("Resonance", ResonanceImport)
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
            ("OutputExtend", OutputExtendExport),
            ("LockedOutput", LockedOutputExport),
            ("FissionPointPool", FissionPointPoolExport),
            ("Resonance", ResonanceExport)
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
        OutputExtendIntoOtherSave();
        LockedOutputIntoOtherSave();
        FissionPointPoolIntoOtherSave();
        ResonanceIntoOtherSave();
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
