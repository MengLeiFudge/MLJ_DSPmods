using FE.Logic.Civilization.Monument;
using NebulaAPI;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Utils.Utils;

namespace FE.Compatibility.Nebula;

/// <summary>接收模式由主机核对真实空仓后修改；客户端不提交库存、产量或成长值。</summary>
public static class MonumentNetwork {
    public static void RequestReceiver(PlanetFactory factory, int entity, bool enabled) {
        if (factory == null) { return; }
        var packet = new MonumentReceiverPacket {
            World = ChallengeNetwork.WorldId, Planet = factory.planetId, Entity = entity, Enabled = enabled,
            Requester = AssemblyNetwork.LocalOwner,
        };
        if (NebulaMultiplayerModAPI.IsClient) { NebulaModAPI.MultiplayerSession.Network.SendPacket(packet); }
        else { Execute(packet); }
    }

    internal static void Execute(MonumentReceiverPacket packet) {
        PlanetFactory factory = GameMain.galaxy?.PlanetById(packet.Planet)?.factory;
        packet.Success = MonumentReceivers.SetMode(factory, packet.Entity, packet.Enabled);
        packet.Reply = true;
        if (packet.Success) {
            int id = factory.entityPool[packet.Entity].powerGenId;
            packet.Product = factory.powerSystem.genPool[id].productId;
            packet.Count = factory.powerSystem.genPool[id].productCount;
        }
        Apply(packet);
        if (NebulaModAPI.IsMultiplayerActive) { NebulaModAPI.MultiplayerSession.Network.SendPacket(packet); }
    }

    internal static void Apply(MonumentReceiverPacket packet) {
        if (!packet.Success) {
            if (packet.Requester == AssemblyNetwork.LocalOwner) { UIRealtimeTip.Popup("FE.Monument.EmptyReceiver".Translate(), true); }
            return;
        }
        if (!NebulaMultiplayerModAPI.IsClient) { return; }
        PlanetFactory factory = GameMain.galaxy?.PlanetById(packet.Planet)?.factory;
        if (factory == null || packet.Entity <= 0 || packet.Entity >= factory.entityCursor || factory.entityPool[packet.Entity].id != packet.Entity) { return; }
        int id = factory.entityPool[packet.Entity].powerGenId;
        if (id <= 0 || id >= factory.powerSystem.genCursor || !factory.powerSystem.genPool[id].gamma) { return; }
        if (packet.Product is not 0 and not IFE丰碑结晶 || !(packet.Count >= 0f && packet.Count <= 20f)) { return; }
        factory.powerSystem.genPool[id].productId = packet.Product;
        factory.powerSystem.genPool[id].productCount = packet.Count;
    }
}

public sealed class MonumentReceiverPacket {
    public string World { get; set; }
    public int Planet { get; set; }
    public int Entity { get; set; }
    public bool Enabled { get; set; }
    public int Requester { get; set; }
    public bool Reply { get; set; }
    public bool Success { get; set; }
    public int Product { get; set; }
    public float Count { get; set; }
}

[RegisterPacketProcessor]
public sealed class MonumentReceiverProcessor : BasePacketProcessor<MonumentReceiverPacket> {
    public override void ProcessPacket(MonumentReceiverPacket packet, INebulaConnection connection) {
        if (packet == null || packet.World != ChallengeNetwork.WorldId) { return; }
        if (IsHost) {
            var player = AssemblyNetwork.GetPlayer(connection);
            if (packet.Reply || player == null || player.LocalPlanetId != packet.Planet) { return; }
            packet.Requester = player.PlayerId;
            MonumentNetwork.Execute(packet);
        } else if (packet.Reply) { MonumentNetwork.Apply(packet); }
    }
}
