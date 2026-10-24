using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using FE.Logic.DataCenter;
using NebulaAPI;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Utils.Utils;

namespace FE.Compatibility.Nebula;

/// <summary>主机先划拨自由库存，客户端同步取物仅消费已授额度；累计核销先于调用方的后续游戏动作发送。</summary>
public static class MaterialLeases {
    private sealed class Lease {
        public string Id;
        public int Item, Total, Inc, Spent, SpentInc, Requested, Manual;
        public long Touched, Sent;
        public bool Closing;
        public int Remaining => Total - Spent;
        public int RemainingInc => Inc - SpentInc;
    }
    private static readonly Dictionary<INebulaConnection, Dictionary<string, Lease>> host = [];
    private static readonly Dictionary<int, Lease> client = [];
    private static readonly HashSet<string> closed = [];
    private const int MaximumQuota = 1000000;
    private static bool ready;
    private static PropertyInfo processingEnabled;
    private static FieldInfo pendingPackets;

    /// <summary>断线返还必须晚于Nebula排空既有可靠消息，不能凭超时提前收回可能已使用的额度。</summary>
    internal static void Initialize() {
        if (ready) { return; }
        Type processor = AccessTools.TypeByName("NebulaModel.Networking.Serialization.NebulaNetPacketProcessor");
        MethodInfo method = processor == null ? null : AccessTools.Method(processor, "ProcessPacketQueue");
        processingEnabled = processor == null ? null : AccessTools.Property(processor, "EnablePacketProcessing");
        pendingPackets = processor == null ? null : AccessTools.Field(processor, "pendingPackets");
        if (method == null || processingEnabled == null || pendingPackets == null) { LogWarning("未找到Nebula消息排空入口，客户端取料额度未启用。"); return; }
        new Harmony(PluginInfo.PLUGIN_GUID + ".MaterialLeases").Patch(method,
            prefix: new HarmonyMethod(typeof(MaterialLeases), nameof(CaptureDisconnected)),
            postfix: new HarmonyMethod(typeof(MaterialLeases), nameof(ReleaseDisconnected)));
        ready = true;
    }
    private static void CaptureDisconnected(object __instance, out INebulaConnection[] __state) {
        __state = [];
        if (NebulaMultiplayerModAPI.IsClient || !ReferenceEquals(__instance, NebulaModAPI.MultiplayerSession?.Network?.PacketProcessor)
            || processingEnabled.GetValue(__instance) is not true) { return; }
        lock (DataCenterInventory.centerItemCount) { __state = host.Keys.Where(connection => NebulaMultiplayerModAPI.GetPlayer(connection) == null).ToArray(); }
    }
    private static void ReleaseDisconnected(object __instance, INebulaConnection[] __state) {
        if (__state.Length == 0 || processingEnabled.GetValue(__instance) is not true
            || pendingPackets.GetValue(__instance) is not System.Collections.ICollection queue) { return; }
        lock (queue) {
            if (queue.Count != 0) { return; }
            lock (DataCenterInventory.centerItemCount) {
                foreach (INebulaConnection connection in __state) {
                    if (!host.TryGetValue(connection, out var leases)) { continue; }
                    foreach (Lease lease in leases.Values) { Refund(lease); }
                    host.Remove(connection);
                }
            }
        }
    }

    public static long Count(int item, out long inc) {
        lock (DataCenterInventory.centerItemCount) {
            if (!MemoryDeliverySync.ClientBusy && client.TryGetValue(item, out Lease lease) && !lease.Closing) { inc = lease.RemainingInc; return lease.Remaining; }
            inc = 0; return 0;
        }
    }

    /// <summary>预检查只请求真实背包与已授额度不能满足的缺口；浏览目录不申请额度。</summary>
    public static void Ensure(int item, int required) {
        if (!NebulaMultiplayerModAPI.IsClient || required <= 0) { return; }
        long physical = PlayerInventoryAccess.GetPackageItemCount(item, out _) + (long)PlayerInventoryAccess.GetDeliveryPackageItemCount(item, out _);
        if (physical < required) { Request(item, (int)Math.Min(MaximumQuota, required - physical), false); }
    }

    public static void RequestManual(int item, int count) => Request(item, count, true);
    private static void Request(int item, int count, bool manual) {
        if (!ready || !NebulaMultiplayerModAPI.IsClient || !PackageAccessRules.TechItemInteractionUnlocked || count <= 0
            || item is <= 0 or >= 12000 || item == I沙土 || !LDB.items.Exist(item) || !PackageAccessRules.CanDownloadFromDataCenter(item)) { return; }
        MaterialLeasePacket request = null;
        lock (DataCenterInventory.centerItemCount) {
            if (!client.TryGetValue(item, out Lease lease)) {
                lease = new() { Id = Guid.NewGuid().ToString("N"), Item = item, Sent = -300 };
                client.Add(item, lease);
            }
            if (lease.Closing) { return; }
            lease.Touched = GameMain.gameTick;
            if (manual) { lease.Manual = Math.Min(MaximumQuota, count); }
            int desired = (int)Math.Min(MaximumQuota, lease.Spent + (long)Math.Max(count, Math.Max(1, LDB.items.Select(item).StackSize)));
            if (lease.Total < desired && GameMain.gameTick - lease.Sent >= 30) {
                lease.Requested = Math.Max(lease.Requested, desired); lease.Sent = GameMain.gameTick;
                request = Packet(lease, 0); request.Wanted = lease.Requested;
            }
        }
        if (request != null) { Send(request); }
        CompleteManual(item);
    }

    /// <summary>只能扣已授物料；不读取共享库存副本、不凭欠额发货，增产点随实际件数拆分。</summary>
    public static int Take(int item, int requested, out int inc, bool manual = false) {
        inc = 0;
        MaterialLeasePacket consumed;
        int count;
        lock (DataCenterInventory.centerItemCount) {
            if (requested <= 0 || MemoryDeliverySync.ClientBusy || !client.TryGetValue(item, out Lease lease) || lease.Closing || lease.Remaining <= 0) { return 0; }
            count = Math.Min(requested, lease.Remaining);
            int left = lease.Remaining, points = lease.RemainingInc;
            inc = split_inc(ref left, ref points, count);
            lease.Spent += count; lease.SpentInc += inc; lease.Touched = GameMain.gameTick;
            consumed = Packet(lease, 2); consumed.Manual = manual;
        }
        // 在返回物品给建造、补给或制造调用方前发核销；Nebula可靠连接保持与后续动作的顺序。
        Send(consumed);
        return count;
    }

    private static void CompleteManual(int item) {
        if (MemoryDeliverySync.ClientBusy) { return; }
        int amount;
        lock (DataCenterInventory.centerItemCount) {
            if (!client.TryGetValue(item, out Lease lease) || lease.Closing || lease.Manual == 0 || lease.Remaining == 0) { return; }
            amount = lease.Manual; lease.Manual = 0;
        }
        int received = Take(item, amount, out int points, true);
        DataCenterInventory.PlaceExtractedItem(item, received, points);
    }

    private static MaterialLeasePacket Packet(Lease lease, int action) => new() {
        World = ChallengeNetwork.WorldId, Id = lease.Id, Item = lease.Item, Action = action,
        Spent = lease.Spent, SpentInc = lease.SpentInc, Total = lease.Total, Inc = lease.Inc,
    };
    private static void Send(MaterialLeasePacket packet) => NebulaModAPI.MultiplayerSession.Network.SendPacket(packet);

    internal static void Pump() {
        if (NebulaMultiplayerModAPI.IsClient) {
            var outgoing = new List<MaterialLeasePacket>();
            lock (DataCenterInventory.centerItemCount) {
                foreach (Lease lease in client.Values) {
                    if (!lease.Closing && (GameMain.gameTick - lease.Touched >= 300 || lease.Spent == MaximumQuota)) {
                        lease.Closing = true; lease.Manual = 0; lease.Sent = -300;
                    }
                    if (GameMain.gameTick - lease.Sent < 300) { continue; }
                    if (lease.Closing) { outgoing.Add(Packet(lease, 3)); lease.Sent = GameMain.gameTick; }
                    else if (lease.Requested > lease.Total) {
                        MaterialLeasePacket packet = Packet(lease, 0); packet.Wanted = lease.Requested;
                        outgoing.Add(packet); lease.Sent = GameMain.gameTick;
                    }
                }
            }
            foreach (MaterialLeasePacket packet in outgoing) { Send(packet); }
            int[] pendingManual;
            lock (DataCenterInventory.centerItemCount) { pendingManual = client.Values.Where(lease => lease.Manual > 0).Select(lease => lease.Item).ToArray(); }
            foreach (int item in pendingManual) { CompleteManual(item); }
        }
    }

    private static void Refund(Lease lease) {
        if (lease.Remaining > 0) {
            // 额度原本来自已存在的库存，不受玩法容量约束，也不因外部物品暂未加载而丢弃。
            Restore(lease.Item, lease.Remaining, lease.RemainingInc);
        }
        closed.Add(lease.Id);
    }
    private static void Restore(int item, int count, int inc) {
        if (DataCenterInventory.centerItemCount[item] > long.MaxValue - count || DataCenterInventory.centerItemInc[item] > long.MaxValue - inc) {
            throw new InvalidDataException("取料额度返还超过数值边界。");
        }
        DataCenterInventory.centerItemCount[item] += count; DataCenterInventory.centerItemInc[item] += inc;
        DataCenterInventory.MarkSnapshotDirty();
    }

    internal static void ReceiveHost(MaterialLeasePacket packet, INebulaConnection connection) {
        if (!ready || packet.Action is not (0 or 2 or 3) || packet.World != ChallengeNetwork.WorldId || !Guid.TryParseExact(packet.Id, "N", out _)
            || packet.Item is <= 0 or >= 12000 || packet.Item == I沙土) { return; }
        MaterialLeasePacket result;
        lock (DataCenterInventory.centerItemCount) {
            if (closed.Contains(packet.Id)) { connection.SendPacket(new MaterialLeasePacket { World = packet.World, Id = packet.Id, Item = packet.Item, Action = 4 }); return; }
            if (!host.TryGetValue(connection, out var leases)) { leases = []; host.Add(connection, leases); }
            if (!leases.TryGetValue(packet.Id, out Lease lease)) {
                if (packet.Action != 0 || packet.Spent != 0 || packet.SpentInc != 0 || leases.Count >= 256 || NebulaMultiplayerModAPI.GetPlayer(connection) == null
                    || leases.Values.Any(other => other.Item == packet.Item) || !PackageAccessRules.TechItemInteractionUnlocked
                    || !PackageAccessRules.CanDownloadFromDataCenter(packet.Item)) { return; }
                lease = new() { Id = packet.Id, Item = packet.Item }; leases.Add(packet.Id, lease);
            }
            if (lease.Item != packet.Item || packet.Spent < lease.Spent || packet.Spent > lease.Total
                || packet.SpentInc < lease.SpentInc || packet.SpentInc > lease.Inc
                || packet.SpentInc > (long)packet.Spent * 10 || lease.Inc - packet.SpentInc > (long)(lease.Total - packet.Spent) * 10
                || packet.Spent == lease.Spent && packet.SpentInc != lease.SpentInc) { return; }
            bool newlyUsed = packet.Spent > lease.Spent;
            lease.Spent = packet.Spent; lease.SpentInc = packet.SpentInc;
            if (newlyUsed && packet.Action == 2 && packet.Manual) { DataCenterInventory.RecordManualExtract(); }
            if (packet.Action == 3) {
                Refund(lease); leases.Remove(packet.Id); result = Packet(lease, 4);
            } else if (packet.Action == 0) {
                if (packet.Wanted < 1 || packet.Wanted > MaximumQuota) { return; }
                int additional = Math.Max(0, packet.Wanted - lease.Total);
                int actual = DataCenterInventory.TakeItemFromModData(packet.Item, additional, out int points);
                lease.Total += actual; lease.Inc += points; result = Packet(lease, 1);
            } else { return; }
        }
        connection.SendPacket(result);
    }

    internal static void ReceiveClient(MaterialLeasePacket packet) {
        if (packet.World != ChallengeNetwork.WorldId) { return; }
        lock (DataCenterInventory.centerItemCount) {
            if (!client.TryGetValue(packet.Item, out Lease lease) || lease.Id != packet.Id) { return; }
            if (packet.Action == 4 && lease.Closing) { client.Remove(packet.Item); return; }
            if (packet.Action != 1 || packet.Total < lease.Total || packet.Total > MaximumQuota || packet.Inc < lease.Inc
                || packet.Inc > (long)packet.Total * 10 || packet.Inc - lease.SpentInc > (long)(packet.Total - lease.Spent) * 10) { return; }
            lease.Total = packet.Total; lease.Inc = packet.Inc; lease.Requested = lease.Total;
            // 零余额手动提取结束，避免以后自动取得与当次点击无关的来料。
            if (lease.Remaining == 0) { lease.Manual = 0; }
        }
        CompleteManual(packet.Item);
    }

    /// <summary>磁盘保存未核销余额，网络读取只跳过该块；读档恢复自由库存而非恢复旧会话额度。</summary>
    public static void Export(BinaryWriter writer) {
        lock (DataCenterInventory.centerItemCount) {
            Lease[] active = host.Values.SelectMany(leases => leases.Values).Where(lease => lease.Remaining > 0).ToArray();
            writer.WriteList(active, (item, lease) => item.WriteBlocks(
                ("Item", field => field.Write(lease.Item)),
                ("Remaining", field => field.Write(lease.Remaining)),
                ("Inc", field => field.Write(lease.RemainingInc))
            ));
        }
    }
    public static void Import(BinaryReader reader) {
        reader.ReadList(entry => {
            int item = 0, amount = 0, inc = 0;
            entry.ReadBlocks(
                ("Item", field => item = field.ReadInt32()),
                ("Remaining", field => amount = field.ReadInt32()),
                ("Inc", field => inc = field.ReadInt32())
            );
            if (item is <= 0 or >= 12000 || amount is < 1 or > MaximumQuota || inc < 0 || inc > amount * 10L) { throw new InvalidDataException("取料额度余额无效。"); }
            if (!NebulaMultiplayerModAPI.IsClient) { Restore(item, amount, inc); }
        }, 65536);
    }

    public static void ImportLegacy(BinaryReader reader) {
        int version = reader.ReadInt32(), count = reader.ReadInt32();
        if (version != 1 || count is < 0 or > 65536) { throw new InvalidDataException("取料额度快照无效。"); }
        for (int i = 0; i < count; i++) {
            int item = reader.ReadInt32(), amount = reader.ReadInt32(), inc = reader.ReadInt32();
            if (item is <= 0 or >= 12000 || amount is < 1 or > MaximumQuota || inc < 0 || inc > amount * 10L) { throw new InvalidDataException("取料额度余额无效。"); }
            if (!NebulaMultiplayerModAPI.IsClient) { Restore(item, amount, inc); }
        }
    }
    public static void BeforeImport() { if (!NebulaMultiplayerModAPI.IsClient) { lock (DataCenterInventory.centerItemCount) { host.Clear(); closed.Clear(); } } }
    public static void ClearWorld() { lock (DataCenterInventory.centerItemCount) { host.Clear(); client.Clear(); closed.Clear(); } }
    public static void EndSession() {
        lock (DataCenterInventory.centerItemCount) {
            if (!NebulaMultiplayerModAPI.IsClient) { foreach (Lease lease in host.Values.SelectMany(leases => leases.Values)) { Refund(lease); } }
            host.Clear(); client.Clear(); closed.Clear();
        }
    }
}

/// <summary>额度身份为世界内唯一流水；总授予只增、消费累计只增，关闭必须携带最终消费。</summary>
public sealed class MaterialLeasePacket {
    public string World { get; set; }
    public string Id { get; set; }
    public int Item { get; set; }
    public int Action { get; set; }
    public int Wanted { get; set; }
    public int Total { get; set; }
    public int Inc { get; set; }
    public int Spent { get; set; }
    public int SpentInc { get; set; }
    public bool Manual { get; set; }
}
[RegisterPacketProcessor]
public sealed class MaterialLeaseProcessor : BasePacketProcessor<MaterialLeasePacket> {
    public override void ProcessPacket(MaterialLeasePacket packet, INebulaConnection conn) {
        if (packet == null) { return; }
        if (IsHost) { MaterialLeases.ReceiveHost(packet, conn); }
        else { MaterialLeases.ReceiveClient(packet); }
    }
}
