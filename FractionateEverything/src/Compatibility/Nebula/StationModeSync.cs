using FE.Logic.Station;
using NebulaAPI;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Utils.Utils;

namespace FE.Compatibility.Nebula;

/// <summary>站点上传下载设置由主机合并单方向修改，并通过既有全量快照同步。</summary>
public static class StationModeSync {
    /// <summary>提交当前窗口的单方向选项，不携带或修改库存。</summary>
    public static void Request(PlanetFactory factory, StationComponent station, int slot, bool upload, int value) {
        if (factory == null || station == null) { return; }
        var packet = new StationModePacket {
            World = ChallengeNetwork.WorldId, Planet = factory.planetId, Entity = station.entityId,
            Slot = slot, Upload = upload, Value = value,
        };
        if (NebulaMultiplayerModAPI.IsClient) { NebulaModAPI.MultiplayerSession.Network.SendPacket(packet); }
        else { Execute(packet); }
    }

    /// <summary>核对真实交互站与槽位后应用命令，保留另一方向的最新设置。</summary>
    internal static void Execute(StationModePacket packet) {
        PlanetFactory factory = GameMain.galaxy?.PlanetById(packet.Planet)?.factory;
        if (factory == null || packet.Entity <= 0 || packet.Entity >= factory.entityCursor
            || factory.entityPool[packet.Entity].id != packet.Entity || packet.Value < 0
            || packet.Value > (packet.Upload ? 2 : 1)) { return; }
        ref EntityData entity = ref factory.entityPool[packet.Entity];
        if (entity.protoId is not IFE行星内物流交互站 and not IFE星际物流交互站
            || entity.stationId <= 0 || entity.stationId >= factory.transport.stationCursor) { return; }
        StationComponent station = factory.transport.stationPool[entity.stationId];
        if (station?.entityId != packet.Entity || packet.Slot < 0 || packet.Slot >= station.storage.Length) { return; }
        StationManager.ApplySlotDirection(packet.Entity, packet.Slot, packet.Upload, packet.Value);
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
    }
}

/// <summary>单个槽位的方向设置请求；上传值0/1/2为关/有限/无限，下载值0/1为关/开。</summary>
public sealed class StationModePacket {
    public string World { get; set; }
    public int Planet { get; set; }
    public int Entity { get; set; }
    public int Slot { get; set; }
    public bool Upload { get; set; }
    public int Value { get; set; }
}

/// <summary>仅主机接收已连接玩家的设置请求，客户端只应用主机快照。</summary>
[RegisterPacketProcessor]
public sealed class StationModeProcessor : BasePacketProcessor<StationModePacket> {
    /// <inheritdoc />
    public override void ProcessPacket(StationModePacket packet, INebulaConnection connection) {
        if (IsHost && packet != null && packet.World == ChallengeNetwork.WorldId
            && AssemblyNetwork.GetPlayer(connection) != null) { StationModeSync.Execute(packet); }
    }
}
