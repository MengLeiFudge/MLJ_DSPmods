using FE.Logic.Civilization;
using NebulaAPI;
using NebulaAPI.Networking;
using NebulaAPI.Packets;

namespace FE.Compatibility.Nebula;

/// <summary>目标选择只传内容身份；资格、已知线索与共享状态由主机复核。</summary>
public static class AnalysisTargetSync {
    public static void SetTarget(int item, string content) {
        if (NebulaMultiplayerModAPI.IsClient) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new AnalysisTargetPacket {
                World = ChallengeNetwork.WorldId, Item = item, Content = content ?? "",
            });
        } else { ResearchManager.SetAnalysisTarget(item, content); }
    }
}

public sealed class AnalysisTargetPacket {
    public string World { get; set; }
    public int Item { get; set; }
    public string Content { get; set; }
}

[RegisterPacketProcessor]
public sealed class AnalysisTargetProcessor : BasePacketProcessor<AnalysisTargetPacket> {
    public override void ProcessPacket(AnalysisTargetPacket packet, INebulaConnection connection) {
        if (IsHost && packet?.World == ChallengeNetwork.WorldId && packet.Content?.Length <= 256
            && NebulaMultiplayerModAPI.GetPlayer(connection) != null) {
            ResearchManager.SetAnalysisTarget(packet.Item, packet.Content);
        }
    }
}
