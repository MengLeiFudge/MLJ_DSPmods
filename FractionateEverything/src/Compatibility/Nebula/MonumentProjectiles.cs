using System;
using System.IO;
using System.Linq;
using FE.Logic.Civilization.Monument;
using NebulaAPI;
using NebulaAPI.Networking;
using NebulaAPI.Packets;

namespace FE.Compatibility.Nebula;

/// <summary>主机发射的专属弹道只在客户端补视觉；奖励仍由主机抵达结算及文明快照决定。</summary>
internal static class MonumentProjectiles {
    internal static void Send(int star, SailBullet bullet) => Send(star, false, bullet.Export);
    internal static void Send(int star, DysonRocket rocket) => Send(star, true, rocket.Export);

    private static void Send(int star, bool rocket, Action<BinaryWriter> export) {
        if (!NebulaModAPI.IsMultiplayerActive || NebulaMultiplayerModAPI.IsClient) { return; }
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) { export(writer); }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(new MonumentProjectilePacket {
            World = ChallengeNetwork.WorldId, Star = star, Rocket = rocket, Data = stream.ToArray(),
        });
    }

    internal static void Apply(MonumentProjectilePacket packet) {
        // 可靠有序连接只发送一次；不创建未加载的远端戴森球，也不在客户端给建设贡献。
        DysonSphere[] spheres = GameMain.data?.dysonSpheres;
        if (spheres == null || packet.Star <= 0 || packet.Star > spheres.Length) { return; }
        DysonSphere sphere = spheres[packet.Star - 1];
        if (sphere == null || packet.Data == null || packet.Data.Length != (packet.Rocket ? 92 : 116)) { return; }
        using var stream = new MemoryStream(packet.Data, false);
        using var reader = new BinaryReader(stream);
        lock (sphere.dysonSphere_mx) {
            if (packet.Rocket) {
                DysonRocket rocket = default;
                rocket.Import(reader, sphere);
                if (rocket.nodeLayerId != -8180 || rocket.planetId / 100 != packet.Star) { return; }
                MonumentFlight.AddRocket(sphere, rocket);
            } else {
                SailBullet bullet = default;
                bullet.Import(reader, sphere);
                if (bullet.state >= 0 || !MonumentManager.MatrixIds.Contains(-bullet.state)) { return; }
                lock (sphere.swarm.bullet_pool_lock) {
                    int id = sphere.swarm.AddBullet(bullet, bullet.state);
                    bullet.id = id;
                    sphere.swarm.bulletPool[id] = bullet;
                }
            }
        }
    }
}

/// <summary>仅主机下发的完整原版弹道字段，来源矩阵由state保留。</summary>
public sealed class MonumentProjectilePacket {
    public string World { get; set; }
    public int Star { get; set; }
    public bool Rocket { get; set; }
    public byte[] Data { get; set; }
}

[RegisterPacketProcessor]
public sealed class MonumentProjectileProcessor : BasePacketProcessor<MonumentProjectilePacket> {
    public override void ProcessPacket(MonumentProjectilePacket packet, INebulaConnection connection) {
        if (!IsHost && packet?.World == ChallengeNetwork.WorldId) { MonumentProjectiles.Apply(packet); }
    }
}
