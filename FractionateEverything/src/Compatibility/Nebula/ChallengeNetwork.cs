using System.Collections.Generic;
using System.Reflection;
using FE.Logic.Civilization;
using NebulaAPI;
using NebulaAPI.GameState;
using NebulaAPI.DataStructures;
using NebulaAPI.Networking;
using NebulaAPI.Packets;

namespace FE.Compatibility.Nebula;

/// <summary>挑战只使用主机持有的连接玩家位置；客户端不能通过动作包自报路线节点或完成量。</summary>
public static class ChallengeNetwork {
    internal static string WorldId => GameMain.data?.gameDesc is GameDesc game
        ? $"{game.seedKey64}:{game.creationTime.Ticks}" : "";
    internal static List<(int Id, VectorLF3 Position, int Planet)> GetPlayers() {
        var result = new List<(int, VectorLF3, int)>();
        Player main = GameMain.mainPlayer;
        if (main != null && main.isAlive) {
            result.Add((NebulaModAPI.IsMultiplayerActive ? NebulaModAPI.MultiplayerSession.LocalPlayer.Id : 0,
                main.uPosition, main.planetId));
        }
        object network = NebulaModAPI.MultiplayerSession?.Network;
        if (network?.GetType().GetProperty("Players", BindingFlags.Instance | BindingFlags.Public)?.GetValue(network)
            is ConcurrentPlayerCollection players) {
            foreach (var pair in players.Connected) {
                IPlayerData data = pair.Value?.Data;
                if (data != null && pair.Key.IsAlive) {
                    result.Add((data.PlayerId, new VectorLF3(data.UPosition.x, data.UPosition.y, data.UPosition.z), data.LocalPlanetId));
                }
            }
        }
        return result;
    }

    public static void RequestRoute(int markId) {
        if (NebulaMultiplayerModAPI.IsClient) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new ChallengeRoutePacket { MarkId = markId, World = WorldId });
        } else if (GameMain.mainPlayer != null) {
            ExplorationManager.SwitchChallengeToRoute(markId, GameMain.mainPlayer.uPosition, GameMain.mainPlayer.planetId);
        }
    }
}

/// <summary>请求在现场改走航迹；不含位置与进度。</summary>
public sealed class ChallengeRoutePacket {
    public int MarkId { get; set; }
    public string World { get; set; }
}

[RegisterPacketProcessor]
public sealed class ChallengeRouteProcessor : BasePacketProcessor<ChallengeRoutePacket> {
    public override void ProcessPacket(ChallengeRoutePacket packet, INebulaConnection conn) {
        if (IsHost && packet != null && packet.World == ChallengeNetwork.WorldId
            && NebulaMultiplayerModAPI.TryGetAuthoritativePlayerState(conn, out VectorLF3 position, out int planet)) {
            ExplorationManager.SwitchChallengeToRoute(packet.MarkId, position, planet);
        }
    }
}
