using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Recipe;
using HarmonyLib;
using NebulaAPI;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class BuildingManager {
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
}
