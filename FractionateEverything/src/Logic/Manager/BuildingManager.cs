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
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.EnableFluidOutputStack;
            case IFE矿物复制塔:
                return MineralCopyTower.EnableFluidOutputStack;
            case IFE点数聚集塔:
                return PointAggregateTower.EnableFluidOutputStack;
            case IFE量子复制塔:
                return QuantumCopyTower.EnableFluidOutputStack;
            case IFE点金塔:
                return AlchemyTower.EnableFluidOutputStack;
            case IFE分解塔:
                return DeconstructionTower.EnableFluidOutputStack;
            case IFE转化塔:
                return ConversionTower.EnableFluidOutputStack;
            default:
                return false;
        }
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
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.MaxProductOutputStack;
            case IFE矿物复制塔:
                return MineralCopyTower.MaxProductOutputStack;
            case IFE点数聚集塔:
                return PointAggregateTower.MaxProductOutputStack;
            case IFE量子复制塔:
                return QuantumCopyTower.MaxProductOutputStack;
            case IFE点金塔:
                return AlchemyTower.MaxProductOutputStack;
            case IFE分解塔:
                return DeconstructionTower.MaxProductOutputStack;
            case IFE转化塔:
                return ConversionTower.MaxProductOutputStack;
            default:
                return 1;
        }
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
            default:
                return;
        }
    }

    public static bool EnableFracForever(this ItemProto building) {
        switch (building.ID) {
            case IFE交互塔:
                return InteractionTower.EnableFracForever;
            case IFE矿物复制塔:
                return MineralCopyTower.EnableFracForever;
            case IFE点数聚集塔:
                return PointAggregateTower.EnableFracForever;
            case IFE量子复制塔:
                return QuantumCopyTower.EnableFracForever;
            case IFE点金塔:
                return AlchemyTower.EnableFracForever;
            case IFE分解塔:
                return DeconstructionTower.EnableFracForever;
            case IFE转化塔:
                return ConversionTower.EnableFracForever;
            default:
                return false;
        }
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
