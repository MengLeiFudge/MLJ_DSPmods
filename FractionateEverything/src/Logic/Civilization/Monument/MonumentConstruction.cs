using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Monument;

internal static partial class MonumentManager {
    private sealed class ShellRecord {
        internal int Quota;
        internal readonly Dictionary<int, int> Counts = [];
    }

    private static readonly Dictionary<(int Star, int Layer, int Kind, int Id), int> Structures = [];
    private static readonly Dictionary<(int Star, int Layer, int Id), ShellRecord> Shells = [];
    private static readonly Dictionary<(int Star, int Matrix), (long Tick, bool Available)> SailAvailability = [];
    [ThreadStatic] private static bool constructingSail;

    private static DysonSphere Sphere(int star) {
        var spheres = GameMain.data?.dysonSpheres;
        return spheres != null && star > 0 && star <= spheres.Length ? spheres[star - 1] : null;
    }

    internal static void RecordStructure(int star, object part) {
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        var key = part switch {
            DysonNode node => (star, node.layerId, 0, node.id),
            DysonFrame frame => (star, frame.layerId, 1, frame.id),
            _ => default,
        };
        if (key.Item4 <= 0) { return; }
        lock (Sync) {
            Structures.TryGetValue(key, out int previous);
            Structures[key] = checked(previous + 1);
            dirty = true;
        }
    }

    /// <summary>每片专属帆只建一个真实细胞；首次进入球壳时按剩余空间为各类等额留位。</summary>
    internal static bool ReceiveSail(int star, int matrix) {
        if (NebulaMultiplayerModAPI.IsClient || Array.BinarySearch(matrixIds, matrix) < 0) { return false; }
        DysonSphere sphere = Sphere(star);
        if (sphere == null) { return false; }
        lock (sphere.dysonSphere_mx) {
            lock (Sync) {
                foreach (DysonSphereLayer layer in sphere.layersIdBased) {
                    if (layer == null) { continue; }
                    for (int id = 1; id < layer.shellCursor; id++) {
                        DysonShell shell = layer.shellPool[id];
                        if (shell == null || shell.id != id || shell.cellPoint >= shell.cellPointMax) { continue; }
                        var key = (star, layer.id, id);
                        Shells.TryGetValue(key, out ShellRecord record);
                        int pending = shell.nodeIndexMap.Keys.Sum(nodeId => layer.nodePool[nodeId]?.cpOrdered ?? 0);
                        int quota = record?.Quota ?? Math.Max(0, shell.cellPointMax - shell.cellPoint - pending) / matrixIds.Length;
                        int count = record != null && record.Counts.TryGetValue(matrix, out int current) ? current : 0;
                        if (count >= quota) { continue; }
                        foreach (var pair in shell.nodeIndexMap) {
                            DysonNode node = layer.nodePool[pair.Key];
                            int index = pair.Value;
                            if (node == null || node.sp < node.spMax || node.cpReqOrder <= 0
                                || shell.nodecps[index] >= (shell.vertsqOffset[index + 1] - shell.vertsqOffset[index]) * shell.cpPerVertex) { continue; }
                            constructingSail = true;
                            try { if (!shell.Construct(index)) { continue; } }
                            finally { constructingSail = false; }
                            if (record == null) { Shells.Add(key, record = new ShellRecord { Quota = quota }); }
                            record.Counts[matrix] = count + 1;
                            node.RecalcCpReq();
                            MonumentProjectiles.SendCell(star, layer.id, id, index, shell.nodecps[index]);
                            sphere.needRecalculatePower = true;
                            dirty = true;
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }

    /// <summary>配额满时弹射器暂停；已发射但未能吸附的帆不冒领建造成果。</summary>
    internal static bool CanReceiveSail(int star, int matrix) {
        lock (Sync) {
            var key = (star, matrix);
            if (SailAvailability.TryGetValue(key, out var cached) && cached.Tick == GameMain.gameTick / 60) { return cached.Available; }
            bool available = FindSailSpace(star, matrix);
            SailAvailability[key] = (GameMain.gameTick / 60, available);
            return available;
        }
    }

    private static bool FindSailSpace(int star, int matrix) {
        DysonSphere sphere = Sphere(star);
        if (sphere == null || matrixIds.Length == 0) { return false; }
        lock (Sync) {
            foreach (DysonSphereLayer layer in sphere.layersIdBased) {
                if (layer == null) { continue; }
                for (int id = 1; id < layer.shellCursor; id++) {
                    DysonShell shell = layer.shellPool[id];
                    if (shell == null || shell.id != id) { continue; }
                    if (!shell.nodeIndexMap.Any(pair => {
                        DysonNode node = layer.nodePool[pair.Key];
                        return node != null && node.sp >= node.spMax && node.cpReqOrder > 0
                            && shell.nodecps[pair.Value] < (shell.vertsqOffset[pair.Value + 1] - shell.vertsqOffset[pair.Value]) * shell.cpPerVertex;
                    })) { continue; }
                    if (!Shells.TryGetValue((star, layer.id, id), out ShellRecord record)) {
                        if (shell.cellPointMax - shell.cellPoint >= matrixIds.Length) { return true; }
                    } else if ((!record.Counts.TryGetValue(matrix, out int count) || count < record.Quota)
                               && shell.cellPoint < shell.cellPointMax) { return true; }
                }
            }
            return false;
        }
    }

    private static bool OrdinaryCellAllowed(DysonShell shell) {
        lock (Sync) {
            if (!Shells.TryGetValue((shell.dysonSphere.starData.id, shell.layerId, shell.id), out ShellRecord record)) { return true; }
            long reserved = (long)record.Quota * matrixIds.Length - record.Counts.Values.Sum(value => (long)value);
            return shell.cellPointMax - shell.cellPoint > reserved;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonShell), nameof(DysonShell.Construct))]
    private static bool DysonShell_Construct_Prefix(DysonShell __instance, ref bool __result) {
        if (constructingSail || OrdinaryCellAllowed(__instance)) { return true; }
        __result = false; return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonNode), nameof(DysonNode.OrderConstructCp))]
    private static bool DysonNode_OrderConstructCp_Prefix(DysonNode __instance) {
        long space = 0;
        lock (Sync) {
            foreach (DysonShell shell in __instance.shells) {
                int index = shell.nodeIndexMap[__instance.id];
                long free = shell.cellPointMax - shell.cellPoint;
                if (Shells.TryGetValue((shell.dysonSphere.starData.id, shell.layerId, shell.id), out ShellRecord record)) {
                    free -= (long)record.Quota * matrixIds.Length - record.Counts.Values.Sum(value => (long)value);
                }
                int pending = shell.nodes.Sum(node => node.cpOrdered);
                space += Math.Max(0, Math.Min(free - pending,
                    (shell.vertsqOffset[index + 1] - shell.vertsqOffset[index]) * shell.cpPerVertex - shell.nodecps[index]));
            }
        }
        return space > 0;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonNode), nameof(DysonNode.ConstructCp))]
    private static void SelectOrdinaryShell(DysonNode __instance) {
        for (int i = 0; i < __instance.shells.Count; i++) {
            int turn = (__instance.shellTurn + i) % __instance.shells.Count;
            DysonShell shell = __instance.shells[turn];
            int index = shell.nodeIndexMap[__instance.id];
            if (OrdinaryCellAllowed(shell) && shell.nodecps[index] < (shell.vertsqOffset[index + 1] - shell.vertsqOffset[index]) * shell.cpPerVertex) {
                __instance.shellTurn = turn; return;
            }
        }
    }

    internal static void ApplyCell(int star, int layerId, int shellId, int index, int count) {
        DysonSphere sphere = Sphere(star);
        if (sphere == null || !NebulaMultiplayerModAPI.IsClient) { return; }
        lock (sphere.dysonSphere_mx) {
            DysonSphereLayer layer = sphere.GetLayer(layerId);
            DysonShell shell = layer != null && shellId > 0 && shellId < layer.shellCursor ? layer.shellPool[shellId] : null;
            if (shell?.id != shellId || index < 0 || index >= shell.nodecps.Length - 1
                || count > (shell.vertsqOffset[index + 1] - shell.vertsqOffset[index]) * shell.cpPerVertex) { return; }
            constructingSail = true;
            try { while (shell.nodecps[index] < count && shell.Construct(index)) { } }
            finally { constructingSail = false; }
            foreach (DysonNode node in shell.nodes) { node.RecalcCpReq(); }
            sphere.needRecalculatePower = true;
        }
    }

    private static void RefreshConstruction(double external) {
        var structures = new Dictionary<int, long>();
        foreach (var pair in Structures.ToArray()) {
            var key = pair.Key;
            DysonSphereLayer layer = Sphere(key.Star)?.GetLayer(key.Layer);
            int built = 0;
            if (layer != null && key.Kind == 0 && key.Id < layer.nodeCursor) {
                DysonNode node = layer.nodePool[key.Id];
                if (node?.id == key.Id) { built = node.sp; }
            } else if (layer != null && key.Kind == 1 && key.Id < layer.frameCursor) {
                DysonFrame frame = layer.framePool[key.Id];
                if (frame?.id == key.Id) { built = frame.spA + frame.spB; }
            }
            if (built == 0) { if (!NebulaMultiplayerModAPI.IsClient) { Structures.Remove(key); dirty = true; } continue; }
            structures.TryGetValue(key.Star, out long total);
            structures[key.Star] = total + Math.Min(built, pair.Value);
        }
        var sails = new Dictionary<int, Dictionary<int, long>>();
        foreach (var pair in Shells.ToArray()) {
            var key = pair.Key;
            DysonSphereLayer layer = Sphere(key.Star)?.GetLayer(key.Layer);
            DysonShell shell = layer != null && key.Id < layer.shellCursor ? layer.shellPool[key.Id] : null;
            if (shell?.id != key.Id) {
                if (!NebulaMultiplayerModAPI.IsClient) { Shells.Remove(key); dirty = true; }
                continue;
            }
            if (!sails.TryGetValue(key.Star, out var counts)) { sails.Add(key.Star, counts = []); }
            foreach (var amount in pair.Value.Counts) {
                counts.TryGetValue(amount.Key, out long total); counts[amount.Key] = total + amount.Value;
            }
        }
        StarRates.Clear(); effectiveSets = 0; structureCount = structures.Values.Sum();
        foreach (var pair in structures) {
            if (!sails.TryGetValue(pair.Key, out var counts) || matrixIds.Length == 0) { continue; }
            long sets = matrixIds.Min(id => counts.TryGetValue(id, out long count) ? count : 0L);
            // 每枚实际建成的丰碑火箭支持十套；一百套提供两件结晶/分钟。
            sets = Math.Min(sets, pair.Value * 10L);
            effectiveSets += sets;
            StarRates[pair.Key] = sets * 0.02d * external / 60d;
        }
        MaterialRate = StarRates.Values.Sum();
    }

    private static void RemovePart(int star, int layer, int kind, int id) {
        lock (Sync) {
            dirty |= kind == 2 ? Shells.Remove((star, layer, id)) : Structures.Remove((star, layer, kind, id));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSphereLayer), nameof(DysonSphereLayer.RemoveDysonNode))]
    private static void RemoveNode(DysonSphereLayer __instance, int nodeId) => RemovePart(__instance.starData.id, __instance.id, 0, nodeId);
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSphereLayer), nameof(DysonSphereLayer.RemoveDysonFrame))]
    private static void RemoveFrame(DysonSphereLayer __instance, int frameId) => RemovePart(__instance.starData.id, __instance.id, 1, frameId);
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSphereLayer), nameof(DysonSphereLayer.RemoveDysonShell))]
    private static void RemoveShell(DysonSphereLayer __instance, int shellId) => RemovePart(__instance.starData.id, __instance.id, 2, shellId);
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RemoveLayer), [typeof(DysonSphereLayer)])]
    private static void RemoveLayer(DysonSphere __instance, DysonSphereLayer layer) {
        if (layer == null) { return; }
        lock (Sync) {
            foreach (var key in Structures.Keys.Where(key => key.Star == __instance.starData.id && key.Layer == layer.id).ToArray()) { Structures.Remove(key); dirty = true; }
            foreach (var key in Shells.Keys.Where(key => key.Star == __instance.starData.id && key.Layer == layer.id).ToArray()) { Shells.Remove(key); dirty = true; }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RemoveLayer), [typeof(int)])]
    private static void RemoveLayerById(DysonSphere __instance, int id) => RemoveLayer(__instance, __instance.GetLayer(id));

    private static void ExportStructures(BinaryWriter writer) => writer.WriteList(Structures, (w, pair) => w.WriteBlocks(
        ("Identity", f => { f.Write(pair.Key.Star); f.Write(pair.Key.Layer); f.Write(pair.Key.Kind); f.Write(pair.Key.Id); }),
        ("Count", f => f.Write(pair.Value))));

    private static void ImportStructures(BinaryReader reader) => reader.ReadList(r => {
        (int Star, int Layer, int Kind, int Id) key = default; int count = 0;
        r.ReadBlocks(("Identity", f => key = (f.ReadInt32(), f.ReadInt32(), f.ReadInt32(), f.ReadInt32())), ("Count", f => count = f.ReadInt32()));
        if (key.Star <= 0 || key.Layer is < 1 or > 10 || key.Kind is < 0 or > 1 || key.Id <= 0 || count <= 0) { throw new InvalidDataException("丰碑结构身份无效。"); }
        Structures.Add(key, count);
    }, 1000000);

    private static void ExportShells(BinaryWriter writer) => writer.WriteList(Shells, (w, pair) => w.WriteBlocks(
        ("Identity", f => { f.Write(pair.Key.Star); f.Write(pair.Key.Layer); f.Write(pair.Key.Id); }),
        ("Quota", f => f.Write(pair.Value.Quota)),
        ("Counts", f => f.WriteList(pair.Value.Counts, (entry, amount) => { entry.Write(amount.Key); entry.Write(amount.Value); }))));

    private static void ImportShells(BinaryReader reader) => reader.ReadList(r => {
        (int Star, int Layer, int Id) key = default; ShellRecord record = new();
        r.ReadBlocks(
            ("Identity", f => key = (f.ReadInt32(), f.ReadInt32(), f.ReadInt32())),
            ("Quota", f => record.Quota = f.ReadInt32()),
            ("Counts", f => f.ReadList(entry => {
                int matrix = entry.ReadInt32(), count = entry.ReadInt32();
                if (Array.BinarySearch(matrixIds, matrix) < 0 || count < 0) { throw new InvalidDataException("丰碑构件来源无效。"); }
                record.Counts.Add(matrix, count);
            }, 12000)));
        if (key.Star <= 0 || key.Layer is < 1 or > 10 || key.Id <= 0 || record.Quota <= 0
            || record.Counts.Values.Any(count => count > record.Quota)) { throw new InvalidDataException("丰碑球壳配额无效。"); }
        Shells.Add(key, record);
    }, 1000000);
}
