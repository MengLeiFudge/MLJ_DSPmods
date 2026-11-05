using System;
using System.Globalization;
using FE.Logic.Civilization;
using NebulaAPI;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Utils.Utils;

namespace FE.Compatibility.Nebula;

/// <summary>亚空间元数据兑换由主机扣除实体并入库能量碎片，客户端只提交带世界身份的命令。</summary>
public static class MetadataExchangeSync {
    private static string World => GameMain.data?.gameDesc is GameDesc game
        ? game.seedKey64.ToString(CultureInfo.InvariantCulture) + ":" + game.creationTime.Ticks.ToString(CultureInfo.InvariantCulture) : "";

    /// <summary>请求将最多100件亚空间实体元数据兑换为能量碎片。</summary>
    public static void Request() {
        var packet = new MetadataExchangePacket {
            World = World, RequestId = Guid.NewGuid().ToString("N"), MatrixId = I电磁矩阵, EnergyShard = true, Count = 100,
        };
        if (NebulaMultiplayerModAPI.IsClient) { NebulaModAPI.MultiplayerSession.Network.SendPacket(packet); }
        else { Execute(packet); }
    }

    internal static void Execute(MetadataExchangePacket packet) {
        // 保留既有包字段；旧手动提取请求失效，矩阵来源不再参与实体兑换。
        if (packet.World != World || string.IsNullOrEmpty(World) || !packet.EnergyShard) { return; }
        MetadataExtraction.Exchange(packet.RequestId, packet.Count);
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
    }
}

/// <summary>单次仓储兑换命令；保留既有字段布局，世界身份和流水防止跨档及重复扣除实体。</summary>
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
