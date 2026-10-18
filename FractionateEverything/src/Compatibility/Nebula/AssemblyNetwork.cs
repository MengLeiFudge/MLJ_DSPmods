using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FE.Logic.DataCenter.Assembly;
using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Networking;
using NebulaAPI.Packets;

namespace FE.Compatibility.Nebula;

/// <summary>装配命令只传用户选择，主机重算配方树与预留，不接受客户端提供的物料清单或完成量。</summary>
public static class AssemblyNetwork {
    public static int LocalOwner => NebulaModAPI.IsMultiplayerActive ? NebulaModAPI.MultiplayerSession.LocalPlayer.Id : 0;

    internal static IPlayerData GetPlayer(INebulaConnection connection) {
        object network = NebulaModAPI.MultiplayerSession?.Network;
        return network?.GetType().GetProperty("Players", BindingFlags.Instance | BindingFlags.Public)?.GetValue(network)
            is ConcurrentPlayerCollection players ? players.Get(connection)?.Data : null;
    }

    internal static HashSet<int> ConnectedOwners() {
        var result = new HashSet<int> { LocalOwner };
        object network = NebulaModAPI.MultiplayerSession?.Network;
        if (network?.GetType().GetProperty("Players", BindingFlags.Instance | BindingFlags.Public)?.GetValue(network)
            is ConcurrentPlayerCollection players) {
            foreach (var pair in players.Connected) { if (pair.Value?.Data != null) { result.Add(pair.Value.Data.PlayerId); } }
        }
        return result;
    }

    public static void Preview(string id, long revision, int item, int count, Dictionary<string, AssemblyChoice> choices) => Send(new() {
        Action = 0, Id = id, Revision = revision, Item = item, Count = count,
        Paths = choices.Keys.ToArray(), Recipes = choices.Values.Select(choice => choice.RecipeId).ToArray(), StockOnly = choices.Values.Select(choice => choice.StockOnly).ToArray(),
    });
    public static void Confirm(string id, long revision) => Send(new() { Action = 1, Id = id, Revision = revision });
    public static void Cancel(string id) { if (id != null) { Send(new() { Action = 2, Id = id }); } }
    public static void Touch(string id) { if (id != null) { Send(new() { Action = 3, Id = id }); } }

    private static void Send(AssemblyCommandPacket packet) {
        packet.World = ChallengeNetwork.WorldId;
        if (NebulaMultiplayerModAPI.IsClient) { NebulaModAPI.MultiplayerSession.Network.SendPacket(packet); }
        else { Execute(packet, LocalOwner); }
    }

    internal static void Execute(AssemblyCommandPacket packet, int owner) {
        if (packet.World != ChallengeNetwork.WorldId || !Guid.TryParseExact(packet.Id, "N", out _)) { return; }
        switch (packet.Action) {
            case 0:
                if (packet.Paths == null || packet.Recipes == null || packet.StockOnly == null || packet.Paths.Length > 512
                    || packet.Paths.Length != packet.Recipes.Length || packet.Paths.Length != packet.StockOnly.Length) { return; }
                var choices = new Dictionary<string, AssemblyChoice>();
                for (int i = 0; i < packet.Paths.Length; i++) {
                    string path = packet.Paths[i];
                    if (path == null || choices.ContainsKey(path)) { return; }
                    choices.Add(path, new() { RecipeId = packet.Recipes[i], StockOnly = packet.StockOnly[i] });
                }
                AssemblyManager.Preview(owner, packet.Id, packet.Revision, packet.Item, packet.Count, choices);
                break;
            case 1: AssemblyManager.Confirm(owner, packet.Id, packet.Revision); break;
            case 2: AssemblyManager.Cancel(owner, packet.Id); break;
            case 3: AssemblyManager.Touch(owner, packet.Id); return;
            default: return;
        }
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
    }
}

/// <summary>单个页面草稿的带修订命令；没有副本库存或可由客户端决定的生产状态。</summary>
public sealed class AssemblyCommandPacket {
    public string World { get; set; }
    public string Id { get; set; }
    public int Action { get; set; }
    public long Revision { get; set; }
    public int Item { get; set; }
    public int Count { get; set; }
    public string[] Paths { get; set; }
    public int[] Recipes { get; set; }
    public bool[] StockOnly { get; set; }
}

[RegisterPacketProcessor]
public sealed class AssemblyCommandProcessor : BasePacketProcessor<AssemblyCommandPacket> {
    public override void ProcessPacket(AssemblyCommandPacket packet, INebulaConnection conn) {
        if (IsHost && packet != null && AssemblyNetwork.GetPlayer(conn) is IPlayerData player) {
            AssemblyNetwork.Execute(packet, player.PlayerId);
        }
    }
}
