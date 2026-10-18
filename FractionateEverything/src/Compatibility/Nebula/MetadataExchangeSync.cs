using System;
using System.Globalization;
using FE.Logic.Civilization;
using NebulaAPI;
using NebulaAPI.Networking;
using NebulaAPI.Packets;

namespace FE.Compatibility.Nebula;

/// <summary>额度兑换只修改主机账本和共享仓储；客户端不提前发货、不发送可采信的额度。</summary>
public static class MetadataExchangeSync {
    private static string World => GameMain.data?.gameDesc is GameDesc game
        ? game.seedKey64.ToString(CultureInfo.InvariantCulture) + ":" + game.creationTime.Ticks.ToString(CultureInfo.InvariantCulture) : "";

    public static void Request(int matrixId, bool energyShard) {
        var packet = new MetadataExchangePacket {
            World = World, RequestId = Guid.NewGuid().ToString("N"), MatrixId = matrixId, EnergyShard = energyShard, Count = 100,
        };
        if (NebulaMultiplayerModAPI.IsClient) { NebulaModAPI.MultiplayerSession.Network.SendPacket(packet); }
        else { Execute(packet); }
    }

    internal static void Execute(MetadataExchangePacket packet) {
        if (packet.World != World || string.IsNullOrEmpty(World)) { return; }
        MetadataExtraction.Exchange(packet.RequestId, packet.MatrixId, packet.EnergyShard, packet.Count);
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
    }
}

/// <summary>单次额度提取命令；世界身份和流水防止跨档及重复扣账。</summary>
public sealed class MetadataExchangePacket {
    public string World { get; set; }
    public string RequestId { get; set; }
    public int MatrixId { get; set; }
    public bool EnergyShard { get; set; }
    public int Count { get; set; }
}

[RegisterPacketProcessor]
public sealed class MetadataExchangeProcessor : BasePacketProcessor<MetadataExchangePacket> {
    public override void ProcessPacket(MetadataExchangePacket packet, INebulaConnection conn) {
        if (IsHost && packet != null && NebulaMultiplayerModAPI.TryGetAuthoritativePlayerState(conn, out _, out _)) {
            MetadataExchangeSync.Execute(packet);
        }
    }
}
