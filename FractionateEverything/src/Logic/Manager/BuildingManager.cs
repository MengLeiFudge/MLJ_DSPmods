using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using HarmonyLib;
using static FE.Utils.ProtoID;
using static FE.Logic.Manager.ProcessManager;

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
    }

    public static void LoadConfig(ConfigFile configFile) {
        InteractionTower.LoadConfig(configFile);
        MineralCopyTower.LoadConfig(configFile);
        PointAggregateTower.LoadConfig(configFile);
        QuantumCopyTower.LoadConfig(configFile);
        AlchemyTower.LoadConfig(configFile);
        DeconstructionTower.LoadConfig(configFile);
        ConversionTower.LoadConfig(configFile);
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

        //初始给予1个交互塔
        TechProto tech = LDB.techs.Select(T戴森球计划);
        tech.AddItems = [..tech.AddItems, IFE交互塔];
        tech.AddItemCounts = [..tech.AddItemCounts, 1];
    }

    public static void SetFractionatorMaterials() {
        InteractionTower.SetMaterials();
        MineralCopyTower.SetMaterials();
        PointAggregateTower.SetMaterials();
        QuantumCopyTower.SetMaterials();
        AlchemyTower.SetMaterials();
        DeconstructionTower.SetMaterials();
        ConversionTower.SetMaterials();
    }

    /// <summary>
    /// 调整Model的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
    /// </summary>
    public static void SetFractionatorCacheSize() {
        foreach (ModelProto modelProto in LDB.models.dataArray) {
            if (modelProto.prefabDesc.isFractionator) {
                modelProto.prefabDesc.fracFluidInputMax = FracFluidInputMax;
                modelProto.prefabDesc.fracProductOutputMax = FracProductOutputMax;
                modelProto.prefabDesc.fracFluidOutputMax = FracFluidOutputMax;
            }
        }
    }

    /// <summary>
    /// 更改已放置的分馏塔的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
    public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
        __instance.fluidInputMax = FracFluidInputMax;
        __instance.productOutputMax = FracProductOutputMax;
        __instance.fluidOutputMax = FracFluidOutputMax;
    }

    #region 分馏塔产物输出拓展

    /// <summary>
    /// 存储分馏塔所有副产物。结构：
    /// (planetId, entityId) => Dictionary&lt;itemId, itemCount&gt;
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), Dictionary<int, int>> outputExtend = [];

    public static void OutputExtendImport(BinaryReader r) {
        outputExtend.Clear();
        int fractionatorNum = r.ReadInt32();
        for (int i = 0; i < fractionatorNum; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            Dictionary<int, int> outputDic = [];
            int outputKinds = r.ReadInt32();
            for (int j = 0; j < outputKinds; j++) {
                int outputId = r.ReadInt32();
                int outputCount = r.ReadInt32();
                if (LDB.items.Select(outputId) == null) {
                    continue;
                }
                outputDic.Add(outputId, outputCount);
            }
            outputExtend.TryAdd((planetId, entityId), outputDic);
        }
    }

    public static void OutputExtendExport(BinaryWriter w) {
        w.Write(outputExtend.Count);
        foreach (var p in outputExtend) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            Dictionary<int, int> outputDic = outputExtend[p.Key];
            //去除所有物品数目为0的情况，节约存储体积
            List<int> keys = outputDic.Keys.Where(Key => outputDic[Key] > 0).ToList();
            w.Write(keys.Count);
            for (int i = 0; i < keys.Count; i++) {
                w.Write(keys[i]);
                w.Write(outputDic[keys[i]]);
            }
        }
    }

    public static void OutputExtendIntoOtherSave() {
        outputExtend.Clear();
    }

    public static Dictionary<int, int> otherProductOutput(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (!outputExtend.ContainsKey((planetId, entityId))) {
            outputExtend.TryAdd((planetId, entityId), []);
        }
        return outputExtend[(planetId, entityId)];
    }

    #endregion

    public static bool EnableFluidOutputStack(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.EnableFluidOutputStackEntry.Value;
            case IFE矿物复制塔:
                return MineralCopyTower.EnableFluidOutputStackEntry.Value;
            case IFE点数聚集塔:
                return PointAggregateTower.EnableFluidOutputStackEntry.Value;
            case IFE量子复制塔:
                return QuantumCopyTower.EnableFluidOutputStackEntry.Value;
            case IFE点金塔:
                return AlchemyTower.EnableFluidOutputStackEntry.Value;
            case IFE分解塔:
                return DeconstructionTower.EnableFluidOutputStackEntry.Value;
            case IFE转化塔:
                return ConversionTower.EnableFluidOutputStackEntry.Value;
            default:
                return false;
        }
    }

    public static int MaxProductOutputStack(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.MaxProductOutputStackEntry.Value;
            case IFE矿物复制塔:
                return MineralCopyTower.MaxProductOutputStackEntry.Value;
            case IFE点数聚集塔:
                return PointAggregateTower.MaxProductOutputStackEntry.Value;
            case IFE量子复制塔:
                return QuantumCopyTower.MaxProductOutputStackEntry.Value;
            case IFE点金塔:
                return AlchemyTower.MaxProductOutputStackEntry.Value;
            case IFE分解塔:
                return DeconstructionTower.MaxProductOutputStackEntry.Value;
            case IFE转化塔:
                return ConversionTower.MaxProductOutputStackEntry.Value;
            default:
                return 1;
        }
    }

    public static bool EnableFracForever(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.EnableFracForeverEntry.Value;
            case IFE矿物复制塔:
                return MineralCopyTower.EnableFracForeverEntry.Value;
            case IFE点数聚集塔:
                return PointAggregateTower.EnableFracForeverEntry.Value;
            case IFE量子复制塔:
                return QuantumCopyTower.EnableFracForeverEntry.Value;
            case IFE点金塔:
                return AlchemyTower.EnableFracForeverEntry.Value;
            case IFE分解塔:
                return DeconstructionTower.EnableFracForeverEntry.Value;
            case IFE转化塔:
                return ConversionTower.EnableFracForeverEntry.Value;
            default:
                return false;
        }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        OutputExtendImport(r);
        InteractionTower.Import(r);
        MineralCopyTower.Import(r);
        PointAggregateTower.Import(r);
        QuantumCopyTower.Import(r);
        AlchemyTower.Import(r);
        DeconstructionTower.Import(r);
        ConversionTower.Import(r);
    }

    public static void Export(BinaryWriter w) {
        OutputExtendExport(w);
        InteractionTower.Export(w);
        MineralCopyTower.Export(w);
        PointAggregateTower.Export(w);
        QuantumCopyTower.Export(w);
        AlchemyTower.Export(w);
        DeconstructionTower.Export(w);
        ConversionTower.Export(w);
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
    }

    #endregion
}
