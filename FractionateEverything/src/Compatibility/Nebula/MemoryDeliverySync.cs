using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using FE.Logic.Civilization;
using HarmonyLib;
using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Utils.Utils;

namespace FE.Compatibility.Nebula;

/// <summary>真实背包交付事务；主机在扣物与奖励提交后等待客户端应用回执，再恢复普通背包覆盖。</summary>
public static class MemoryDeliverySync {
    private static readonly Dictionary<INebulaConnection, (string RequestId, IPlayerData Player)> AwaitingConfirmation = [];
    private static MemoryDeliveryPacket pending;
    private static string lastConfirmed;
    private static long lastSendTick;
    private static bool guardInstalled;
    private static FieldInfo mechaDataField;
    private static PropertyInfo mechaDataProperty;
    private static bool unavailableShown;

    /// <summary>同星图不同创建时间仍是不同世界，切档不能重放旧交付。</summary>
    private static string WorldId => GameMain.data?.gameDesc is GameDesc game
        ? game.seedKey64.ToString(CultureInfo.InvariantCulture) + ":" + game.creationTime.Ticks.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    /// <summary>只拦截事务等待期间的远端背包整包覆盖，不改动Nebula的磁盘格式。</summary>
    public static void Initialize() {
        if (guardInstalled) { return; }
        Type type = AccessTools.TypeByName("NebulaNetwork.PacketProcessors.Players.PlayerMechaDataProcessor");
        MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, "ProcessPacket");
        Type packetType = method?.GetParameters().FirstOrDefault()?.ParameterType;
        mechaDataField = packetType == null ? null : AccessTools.Field(packetType, "Data");
        mechaDataProperty = packetType == null ? null : AccessTools.Property(packetType, "Data");
        if (method == null || mechaDataField == null && mechaDataProperty == null) {
            LogWarning("未找到Nebula背包同步入口，客户端自动交付暂不可用。");
            return;
        }
        new Harmony(PluginInfo.PLUGIN_GUID + ".MemoryDelivery").Patch(method,
            prefix: new HarmonyMethod(typeof(MemoryDeliverySync), nameof(PreservePendingInventory)));
        guardInstalled = true;
    }

    /// <summary>事务等待期间保留已经结算的库存，能量和其他机甲字段仍按Nebula原有流程更新。</summary>
    private static void PreservePendingInventory(object packet, object conn) {
        if (conn is not INebulaConnection connection || !AwaitingConfirmation.TryGetValue(connection, out var waiting)) { return; }
        if ((mechaDataField?.GetValue(packet) ?? mechaDataProperty?.GetValue(packet)) is IMechaData incoming) {
            incoming.Inventory = waiting.Player.Mecha.Inventory;
        }
    }

    /// <summary>会话结束后远端背包由Nebula持久状态恢复，不对未知结算结果盲目退款。</summary>
    public static void ResetSession() {
        AwaitingConfirmation.Clear();
        pending = null; lastConfirmed = null; lastSendTick = 0; unavailableShown = false;
    }

    /// <summary>等待回执时沿用相同请求重发，不能重新扣背包。</summary>
    public static bool PumpPending() {
        if (!NebulaMultiplayerModAPI.IsClient) {
            foreach (INebulaConnection connection in AwaitingConfirmation.Keys.Where(c => !c.IsAlive).ToArray()) {
                AwaitingConfirmation.Remove(connection);
            }
            return false;
        }
        if (pending == null) { return false; }
        if (pending.World != WorldId) { pending = null; return false; }
        if (GameMain.gameTick - lastSendTick >= 300) { SendPending(); }
        return true;
    }

    /// <summary>暂存真实背包中的源点，并携带交付前库存供主机原子结算；不读取亚空间。</summary>
    public static bool Request(int markId, int count) {
        if (!NebulaMultiplayerModAPI.IsClient || pending != null || count <= 0 || count > 2400) { return false; }
        if (!guardInstalled) {
            if (!unavailableShown) {
                unavailableShown = true;
                CivilizationModule.QueueNotification("FE.Exploration.DeliveryUnavailable".Translate());
            }
            return false;
        }
        StorageComponent package = GameMain.mainPlayer?.package;
        if (package == null) { return false; }
        byte[] before;
        using (var stream = new MemoryStream()) {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) { package.Export(writer); }
            before = stream.ToArray();
        }
        StorageComponent check = ReadInventory(before);
        if (check == null) {
            if (!unavailableShown) {
                unavailableShown = true;
                LogWarning("真实背包格式不符合交付协议，未扣除源点。");
                CivilizationModule.QueueNotification("FE.Exploration.DeliveryUnavailable".Translate());
            }
            return false;
        }
        int itemId = IFE记忆源点;
        int expected = count;
        check.TakeTailItems(ref itemId, ref expected, out int expectedInc);
        if (expected != count || expectedInc < 0 || expectedInc > count * 10) { return false; }
        package.TakeTailItems(ref itemId, ref count, out int inc);
        if (count <= 0) { return false; }
        pending = new MemoryDeliveryPacket {
            World = WorldId, RequestId = Guid.NewGuid().ToString("N"), MarkId = markId,
            Count = count, Inc = inc, InventoryBefore = before,
        };
        SendPending();
        return true;
    }

    private static void SendPending() {
        lastSendTick = GameMain.gameTick;
        NebulaModAPI.MultiplayerSession.Network.SendPacket(pending);
    }

    /// <summary>主机从连接取得身份和位置；只有原始流水可以复用既有回执。</summary>
    internal static void ReceiveRequest(MemoryDeliveryPacket packet, INebulaConnection connection) {
        if (packet.World != WorldId || !Guid.TryParseExact(packet.RequestId, "N", out _)) { return; }
        if (packet.Confirm) {
            if (AwaitingConfirmation.TryGetValue(connection, out var waiting) && waiting.RequestId == packet.RequestId) {
                AwaitingConfirmation.Remove(connection);
            }
            return;
        }
        if (!guardInstalled || packet.MarkId <= 0 || packet.Count is <= 0 or > 2400
            || packet.Inc < 0 || packet.Inc > packet.Count * 10 || packet.InventoryBefore == null
            || packet.InventoryBefore.Length > 200040) { return; }
        IPlayerData player = GetConnectedPlayer(connection);
        if (player?.Mecha == null || !NebulaMultiplayerModAPI.TryGetAuthoritativePlayerState(connection,
                out VectorLF3 position, out int planetId)) { return; }
        string fingerprint = Fingerprint(packet);
        if (ExplorationManager.TryGetMemoryReceipt(packet.RequestId, out MemoryDeliveryReceipt previous)) {
            if (previous.Owner == player.PlayerId && previous.MarkId == packet.MarkId && previous.Fingerprint == fingerprint) {
                SendReceipt(connection, packet, previous);
            }
            return;
        }
        if (AwaitingConfirmation.ContainsKey(connection)) { return; }
        StorageComponent inventory = ReadInventory(packet.InventoryBefore);
        if (inventory == null) { return; }
        // 在独立副本上复现客户端暂存，确认件数和增产点都对应该背包。
        StorageComponent offeredInventory = ReadInventory(packet.InventoryBefore);
        int itemId = IFE记忆源点;
        int offered = packet.Count;
        offeredInventory.TakeTailItems(ref itemId, ref offered, out int offeredInc);
        if (offered != packet.Count || offeredInc != packet.Inc) { return; }
        MemoryDeliveryReceipt receipt = ExplorationManager.DeliverMemory(packet.RequestId, player.PlayerId, fingerprint,
            packet.MarkId, position, planetId, inventory, packet.Count, packet.Inc, updated => player.Mecha.Inventory = updated);
        if (receipt == null) { return; }
        AwaitingConfirmation[connection] = (packet.RequestId, player);
        SendReceipt(connection, packet, receipt);
    }

    private static IPlayerData GetConnectedPlayer(INebulaConnection connection) {
        object network = NebulaModAPI.MultiplayerSession?.Network;
        PropertyInfo property = network?.GetType().GetProperty("Players", BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(network) is ConcurrentPlayerCollection players ? players.Get(connection)?.Data : null;
    }

    /// <summary>先校验原版格式的固定长度与数组上限，再调用其导入器，避免不可信长度分配。</summary>
    private static StorageComponent ReadInventory(byte[] bytes) {
        if (bytes.Length < 40 || BitConverter.ToInt32(bytes, 0) != 2) { return null; }
        int size = BitConverter.ToInt32(bytes, 32);
        int bans = BitConverter.ToInt32(bytes, 36);
        if (size is < 1 or > 10000 || bans < 0 || bans > size || bytes.Length != 40 + size * 20) { return null; }
        for (int index = 0; index < size; index++) {
            int offset = 40 + index * 20;
            int item = BitConverter.ToInt32(bytes, offset);
            int count = BitConverter.ToInt32(bytes, offset + 8);
            int inc = BitConverter.ToInt32(bytes, offset + 16);
            if (count < 0 || inc < 0 || item < 0 || item == 0 && (count != 0 || inc != 0)
                || item > 0 && LDB.items.Select(item) == null) { return null; }
        }
        var result = new StorageComponent(1);
        using var reader = new BinaryReader(new MemoryStream(bytes, false));
        result.Import(reader);
        return result;
    }

    private static string Fingerprint(MemoryDeliveryPacket packet) {
        using var hash = SHA256.Create();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) {
            writer.Write(packet.MarkId); writer.Write(packet.Count); writer.Write(packet.Inc); writer.Write(packet.InventoryBefore);
        }
        return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "");
    }

    private static void SendReceipt(INebulaConnection connection, MemoryDeliveryPacket packet, MemoryDeliveryReceipt receipt) {
        connection.SendPacket(new MemoryDeliveryResultPacket {
            World = packet.World, RequestId = packet.RequestId, MarkId = receipt.MarkId,
            Accepted = receipt.Accepted, AcceptedInc = receipt.AcceptedInc, Remaining = receipt.Remaining,
        });
    }

    /// <summary>客户端只退回未接收部分，重复回执不重复退款；之后允许主机恢复常规库存同步。</summary>
    internal static void ReceiveReceipt(MemoryDeliveryResultPacket packet) {
        if (packet.World != WorldId || !Guid.TryParseExact(packet.RequestId, "N", out _)) { return; }
        if (pending == null) {
            if (lastConfirmed == packet.RequestId) { SendConfirmation(packet.RequestId); }
            return;
        }
        if (packet.RequestId != pending.RequestId || packet.MarkId != pending.MarkId
            || packet.Accepted < 0 || packet.Accepted > pending.Count || packet.AcceptedInc < 0
            || packet.AcceptedInc > pending.Inc || packet.Remaining is < 0 or > 2400) { return; }
        int refund = pending.Count - packet.Accepted;
        int refundInc = pending.Inc - packet.AcceptedInc;
        if (refundInc > refund * 10) { return; }
        if (refund > 0) { GameMain.mainPlayer.TryAddItemToPackage(IFE记忆源点, refund, refundInc, throwTrash: true); }
        lastConfirmed = pending.RequestId;
        pending = null;
        ExplorationManager.NotifyDeliveryRemaining(packet.MarkId, packet.Remaining);
        SendConfirmation(packet.RequestId);
    }

    private static void SendConfirmation(string requestId) => NebulaModAPI.MultiplayerSession.Network.SendPacket(
        new MemoryDeliveryPacket { World = WorldId, RequestId = requestId, Confirm = true });
}

/// <summary>客户端真实背包暂存请求或回执确认；重发使用同一流水和同一库存快照。</summary>
public sealed class MemoryDeliveryPacket {
    public string World { get; set; }
    public string RequestId { get; set; }
    public bool Confirm { get; set; }
    public int MarkId { get; set; }
    public int Count { get; set; }
    public int Inc { get; set; }
    public byte[] InventoryBefore { get; set; }
}

/// <summary>主机已提交的交付结果，不允许客户端据此再次推进任务。</summary>
public sealed class MemoryDeliveryResultPacket {
    public string World { get; set; }
    public string RequestId { get; set; }
    public int MarkId { get; set; }
    public int Accepted { get; set; }
    public int AcceptedInc { get; set; }
    public int Remaining { get; set; }
}

[RegisterPacketProcessor]
public sealed class MemoryDeliveryProcessor : BasePacketProcessor<MemoryDeliveryPacket> {
    public override void ProcessPacket(MemoryDeliveryPacket packet, INebulaConnection conn) {
        if (IsHost && packet != null) { MemoryDeliverySync.ReceiveRequest(packet, conn); }
    }
}

[RegisterPacketProcessor]
public sealed class MemoryDeliveryResultProcessor : BasePacketProcessor<MemoryDeliveryResultPacket> {
    public override void ProcessPacket(MemoryDeliveryResultPacket packet, INebulaConnection conn) {
        if (!IsHost && packet != null) { MemoryDeliverySync.ReceiveReceipt(packet); }
    }
}
