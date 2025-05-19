using HarmonyLib;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FractionateEverything.Logic.FracProcess;
using static FractionateEverything.Logic.FracItemManager;

namespace FractionateEverything.Logic;

public static class FracItem {
    /// <summary>
    /// 调整Model的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
    /// </summary>
    public static void SetFractionatorCacheSize() {
        foreach (var unlock in buildingInfoList) {
            var prefabDesc = LDB.items.Select(unlock.itemID).prefabDesc;
            prefabDesc.fracFluidInputMax = FracFluidInputMax;
            prefabDesc.fracProductOutputMax = FracProductOutputMax;
            prefabDesc.fracFluidOutputMax = FracFluidOutputMax;
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

    #region 分馏塔字段拓展

    /// <summary>
    /// 存储分馏塔所有副产物。结构：
    /// (planetId, entityId) => Dictionary&lt;itemId, itemCount&gt;
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), Dictionary<int, int>> outputExtend = [];

    public static void Import(BinaryReader r) {
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

    public static void Export(BinaryWriter w) {
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

    public static void IntoOtherSave() {
        outputExtend.Clear();
    }

    public static Dictionary<int, int> productExpansion(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (!outputExtend.ContainsKey((planetId, entityId))) {
            outputExtend.TryAdd((planetId, entityId), []);
        }
        return outputExtend[(planetId, entityId)];
    }

    #endregion
}
