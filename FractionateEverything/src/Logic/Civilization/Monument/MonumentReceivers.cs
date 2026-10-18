using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility.Nebula;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Monument;

/// <summary>接收站共享全档供帆预算；每站小数独立保存，满仓份额不转移或积攒。</summary>
internal static class MonumentReceivers {
    private sealed class Credit { internal double Fraction; internal long LastTick = long.MinValue; }
    private static readonly object Sync = new();
    private static readonly Dictionary<(int Planet, int Entity), Credit> Credits = [];
    private static HashSet<(int Planet, int Entity)> valid = [];
    private static double share;
    private static bool dirty;

    /// <summary>每秒建立只读候选快照；不在发电线程扫描其他工厂。</summary>
    internal static void Refresh() {
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        HashSet<(int Planet, int Entity)> next = [];
        GameData data = GameMain.data;
        if (data?.factories == null) { return; }
        for (int f = 0; f < data.factoryCount; f++) {
            PlanetFactory factory = data.factories[f];
            PowerSystem power = factory?.powerSystem;
            if (power == null || !MonumentManager.HasConstruction(factory.planet.star.id)) { continue; }
            for (int id = 1; id < power.genCursor; id++) {
                ref PowerGeneratorComponent receiver = ref power.genPool[id];
                if (receiver.id == id && receiver.gamma && receiver.productId == IFE丰碑结晶 && receiver.networkId > 0
                    && receiver.currentStrength > 0f) { next.Add((factory.planetId, receiver.entityId)); }
            }
        }
        lock (Sync) {
            valid = next;
            share = next.Count > 0 ? MonumentManager.MaterialRate / next.Count / 60d : 0d;
        }
    }

    /// <summary>模式改变须先取尽整件产物；模式本身保存在原版发电组件，不能覆盖库存。</summary>
    internal static bool SetMode(PlanetFactory factory, int entity, bool monument) {
        if (NebulaMultiplayerModAPI.IsClient || factory == null || entity <= 0 || entity >= factory.entityCursor
            || factory.entityPool[entity].id != entity) { return false; }
        int id = factory.entityPool[entity].powerGenId;
        if (id <= 0 || id >= factory.powerSystem.genCursor) { return false; }
        lock (factory.entityMutexs[entity]) {
            ref PowerGeneratorComponent receiver = ref factory.powerSystem.genPool[id];
            if (receiver.id != id || !receiver.gamma) { return false; }
            if (receiver.productId == (monument ? IFE丰碑结晶 : 0)) { return true; }
            if (receiver.productCount >= 1f) { return false; }
            receiver.productId = monument ? IFE丰碑结晶 : 0;
            receiver.productCount = 0f;
        }
        lock (Sync) { dirty = true; }
        return true;
    }

    /// <summary>普通接收站仍取原版能量/热值；专属材料只消费分到本台的预算。</summary>
    private static double Yield(ref PowerGeneratorComponent receiver, PlanetFactory factory) {
        if (receiver.productId != IFE丰碑结晶) { return (double)receiver.capacityCurrentTick / receiver.productHeat; }
        if (NebulaMultiplayerModAPI.IsClient || receiver.networkId <= 0 || receiver.currentStrength <= 0f) { return 0d; }
        var key = (factory.planetId, receiver.entityId);
        lock (Sync) {
            if (!valid.Contains(key) || share <= 0d) { return 0d; }
            if (!Credits.TryGetValue(key, out Credit credit)) { Credits.Add(key, credit = new()); }
            if (credit.LastTick == GameMain.gameTick) { return 0d; }
            credit.LastTick = GameMain.gameTick;
            double accumulated = credit.Fraction + share * Math.Min(1d, receiver.currentStrength);
            double whole = Math.Floor(accumulated);
            credit.Fraction = accumulated - whole;
            dirty = true;
            return Math.Min(whole, Math.Max(0d, 20d - receiver.productCount));
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PowerGeneratorComponent), nameof(PowerGeneratorComponent.GameTick_Gamma))]
    private static IEnumerable<CodeInstruction> PowerGeneratorComponent_GameTick_Gamma_Transpiler(IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldarg_0), new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PowerGeneratorComponent), nameof(PowerGeneratorComponent.capacityCurrentTick))),
            new CodeMatch(OpCodes.Conv_R8), new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(PowerGeneratorComponent), nameof(PowerGeneratorComponent.productHeat))),
            new CodeMatch(OpCodes.Conv_R8), new CodeMatch(OpCodes.Div));
        if (matcher.IsInvalid) { throw new InvalidOperationException("丰碑接收站未匹配原版产量计算。"); }
        var labels = matcher.Instruction.labels.ToArray();
        matcher.RemoveInstructions(7).Insert(new CodeInstruction(OpCodes.Ldarg_0).WithLabels(labels),
            new CodeInstruction(OpCodes.Ldarg_S, (byte)4), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MonumentReceivers), nameof(Yield))));
        return matcher.InstructionEnumeration();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerGeneratorComponent), nameof(PowerGeneratorComponent.EnergyCap_Gamma_Req))]
    private static void PowerGeneratorComponent_EnergyCap_Gamma_Req_Postfix(ref PowerGeneratorComponent __instance, ref long __result) {
        if (__instance.productId == IFE丰碑结晶) { __result = 0; }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(PowerGeneratorComponent), nameof(PowerGeneratorComponent.EnergyCap_Gamma))]
    private static bool PowerGeneratorComponent_EnergyCap_Gamma_Prefix(ref PowerGeneratorComponent __instance, ref long __result) {
        if (__instance.productId != IFE丰碑结晶) { return true; }
        // 丰碑有独立供帆预算，不请求普通戴森能量，也不因普通戴森供给为零抑制接收预热。
        __instance.capacityCurrentTick = (long)(__instance.currentStrength * __instance.genEnergyPerTick);
        __result = 0;
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.RequestDysonSpherePower))]
    private static void PowerSystem_RequestDysonSpherePower_Postfix(PowerSystem __instance) {
        if (!MonumentManager.HasConstruction(__instance.planet.star.id)) { return; }
        Vector3 sun = __instance.planet.runtimeLocalSunDirection.normalized;
        float increase = (float)((__instance.factory.dysonSphere?.defOrbitRadius ?? 0f) / (__instance.planet.sunDistance * 40000d));
        float eta = 1f - GameMain.history.solarEnergyLossRate;
        for (int id = 1; id < __instance.genCursor; id++) {
            ref PowerGeneratorComponent receiver = ref __instance.genPool[id];
            if (receiver.id == id && receiver.gamma && receiver.productId == IFE丰碑结晶) {
                receiver.EnergyCap_Gamma_Req(sun.x, sun.y, sun.z, increase, eta);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.RemoveGeneratorComponent))]
    private static void PowerSystem_RemoveGeneratorComponent_Prefix(PowerSystem __instance, int id) {
        if (id <= 0 || id >= __instance.genCursor) { return; }
        lock (Sync) { dirty |= Credits.Remove((__instance.planet.id, __instance.genPool[id].entityId)); }
    }

    internal static double GetRate(int planet, int entity) {
        lock (Sync) { return valid.Contains((planet, entity)) ? share * 3600d : 0d; }
    }
    internal static bool TakeDirty() { lock (Sync) { bool result = dirty; dirty = false; return result; } }

    internal static void Export(BinaryWriter writer) {
        lock (Sync) {
            writer.Write(1); writer.Write(Credits.Count);
            foreach (var pair in Credits.OrderBy(pair => pair.Key.Planet).ThenBy(pair => pair.Key.Entity)) {
                writer.Write(pair.Key.Planet); writer.Write(pair.Key.Entity); writer.Write(pair.Value.Fraction);
            }
            writer.Write(share);
            var receivers = new List<(int Planet, int Entity, float Count, bool Active)>();
            GameData data = GameMain.data;
            for (int f = 0; data != null && f < data.factoryCount; f++) {
                PlanetFactory factory = data.factories[f];
                if (factory?.powerSystem == null) { continue; }
                for (int id = 1; id < factory.powerSystem.genCursor; id++) {
                    ref PowerGeneratorComponent receiver = ref factory.powerSystem.genPool[id];
                    if (receiver.id == id && receiver.gamma && receiver.productId == IFE丰碑结晶) {
                        receivers.Add((factory.planetId, receiver.entityId, receiver.productCount, valid.Contains((factory.planetId, receiver.entityId))));
                    }
                }
            }
            writer.Write(receivers.Count);
            foreach (var receiver in receivers) {
                writer.Write(receiver.Planet); writer.Write(receiver.Entity); writer.Write(receiver.Count); writer.Write(receiver.Active);
            }
        }
    }

    internal static void Import(BinaryReader reader) {
        if (reader.ReadInt32() != 1) { throw new InvalidDataException("不支持的丰碑接收格式。"); }
        int count = reader.ReadInt32();
        if (count < 0 || count > 1000000) { throw new InvalidDataException("丰碑接收数量越界。"); }
        Dictionary<(int Planet, int Entity), Credit> restored = [];
        for (int i = 0; i < count; i++) {
            int planet = reader.ReadInt32(), entity = reader.ReadInt32(); double fraction = reader.ReadDouble();
            if (planet <= 0 || entity <= 0 || !(fraction >= 0d && fraction < 1d) || restored.ContainsKey((planet, entity))) {
                throw new InvalidDataException("丰碑接收余量无效。");
            }
            restored.Add((planet, entity), new Credit { Fraction = fraction });
        }
        double restoredShare = reader.ReadDouble();
        int receiverCount = reader.ReadInt32();
        if (double.IsNaN(restoredShare) || double.IsInfinity(restoredShare) || restoredShare < 0d || receiverCount < 0 || receiverCount > 1000000) {
            throw new InvalidDataException("丰碑接收快照无效。");
        }
        var receivers = new Dictionary<(int Planet, int Entity), (float Count, bool Active)>();
        for (int i = 0; i < receiverCount; i++) {
            var key = (reader.ReadInt32(), reader.ReadInt32());
            float amount = reader.ReadSingle(); bool active = reader.ReadBoolean();
            if (key.Item1 <= 0 || key.Item2 <= 0 || !(amount >= 0f && amount <= 20f) || receivers.ContainsKey(key)) {
                throw new InvalidDataException("丰碑接收库存无效。");
            }
            receivers.Add(key, (amount, active));
        }
        if (NebulaMultiplayerModAPI.IsClient) {
            foreach (var pair in receivers) {
                PlanetFactory factory = GameMain.galaxy?.PlanetById(pair.Key.Planet)?.factory;
                int entity = pair.Key.Entity;
                if (factory == null || entity >= factory.entityCursor || factory.entityPool[entity].id != entity) { continue; }
                int id = factory.entityPool[entity].powerGenId;
                if (id <= 0 || id >= factory.powerSystem.genCursor || !factory.powerSystem.genPool[id].gamma) { continue; }
                factory.powerSystem.genPool[id].productId = IFE丰碑结晶;
                factory.powerSystem.genPool[id].productCount = pair.Value.Count;
            }
        }
        lock (Sync) {
            Credits.Clear(); foreach (var pair in restored) { Credits.Add(pair.Key, pair.Value); }
            valid = receivers.Where(pair => pair.Value.Active).Select(pair => pair.Key).ToHashSet();
            share = restoredShare; dirty = false;
        }
    }

    internal static void IntoOtherSave() { lock (Sync) { Credits.Clear(); valid.Clear(); share = 0d; dirty = false; } }
}
