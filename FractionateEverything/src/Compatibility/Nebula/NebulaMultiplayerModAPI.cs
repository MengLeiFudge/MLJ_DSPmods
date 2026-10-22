using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Fractionators;
using HarmonyLib;
using NebulaAPI;
using NebulaAPI.DataStructures;
using NebulaAPI.GameState;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;

namespace FE.Compatibility.Nebula;

/// <summary>
/// Nebula 联机同步的注册、发送与接收入口。
/// </summary>
public static class NebulaMultiplayerModAPI {
    public const string GUID = NebulaModAPI.API_GUID;
    /// <summary>联机规则修订号；堆叠科技、设备接管及库存结算要求双方一致。</summary>
    public const int ProtocolVersion = 14;
    public static bool Enable;
    public static Assembly assembly;

    /// <summary>
    /// 玩家是否在多人游戏中。
    /// </summary>
    public static bool IsMultiplayerActive = false;
    /// <summary>
    /// 玩家是否为客户端。
    /// </summary>
    public static bool IsClient = false;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.NebulaMultiplayerModAPI");
        harmony.PatchAll(typeof(NebulaMultiplayerModAPI));
        NebulaModAPI.OnMultiplayerGameStarted += OnMultiplayerGameStarted;
        NebulaModAPI.OnMultiplayerGameEnded += OnMultiplayerGameEnded;
        NebulaModAPI.OnPlayerJoinedGame += _ => {
            if (NebulaModAPI.MultiplayerSession?.LocalPlayer?.IsHost == true) {
                BroadcastCivilizationState();
                BroadcastAdvancedFractionatorState(fullSnapshot: true);
            }
        };
        CheckPlugins.LogInfo("NebulaMultiplayerModAPI Compat finish.");
    }

    public static void OnMultiplayerGameStarted() {
        IsMultiplayerActive = NebulaModAPI.IsMultiplayerActive;
        IsClient = IsMultiplayerActive && NebulaModAPI.MultiplayerSession.LocalPlayer.IsClient;
        MemoryDeliverySync.Initialize();
    }

    public static void OnMultiplayerGameEnded() {
        MemoryDeliverySync.ResetSession();
        IsMultiplayerActive = false;
        IsClient = false;
        CivilizationRecoveryManager.ResetNetworkRequestThrottle();
    }

    public static bool IsOthers()// 由其他玩家的数据包触发
    {
        var factoryManager = NebulaModAPI.MultiplayerSession.Factories;
        return factoryManager.IsIncomingRequest.Value
               && factoryManager.PacketAuthor != NebulaModAPI.MultiplayerSession.LocalPlayer.Id;
    }

    /// <summary>
    /// 广播研究、探索和亚空间库存的主机状态，并同步全局堆叠。
    /// </summary>
    public static void BroadcastCivilizationState() {
        if (!NebulaModAPI.IsMultiplayerActive
            || NebulaModAPI.MultiplayerSession?.LocalPlayer?.IsHost != true
            || NebulaModAPI.MultiplayerSession.Network == null) {
            return;
        }

        byte[] stateData;
        using (IWriterProvider provider = NebulaModAPI.GetBinaryWriter()) {
            BinaryWriter writer = provider.BinaryWriter;
            writer.WriteBlocks(
                ("Civilization", CivilizationModule.Export),
                // 亚空间快照覆盖完整稀疏库存，避免仅同步 Frag/Memory 时遗漏普通物资。
                ("DataCenter", Export),
                ("StationModes", FE.Logic.Station.StationManager.Export)
            );
            stateData = provider.CloseAndGetBytes();
        }

        long fragments;
        long fragmentInc;
        long memory;
        long memoryInc;
        lock (centerItemCount) {
            fragments = centerItemCount[IFE残片];
            fragmentInc = centerItemInc[IFE残片];
            memory = centerItemCount[IFE记忆源点];
            memoryInc = centerItemInc[IFE记忆源点];
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(new CivilizationStatePacket(stateData,
            fragments, fragmentInc, memory, memoryInc, StackingManager.ConfiguredMaxStack));
    }

    /// <summary>
    /// 主机按秒广播变化的高级塔，玩家加入时补发完整状态。
    /// </summary>
    /// <param name="fullSnapshot">加入时发送完整快照；普通秒更新只发送增量。</param>
    public static void BroadcastAdvancedFractionatorState(bool fullSnapshot = false) {
        if (!NebulaModAPI.IsMultiplayerActive
            || NebulaModAPI.MultiplayerSession?.LocalPlayer?.IsHost != true
            || NebulaModAPI.MultiplayerSession.Network == null) {
            return;
        }
        byte[] data;
        using (IWriterProvider provider = NebulaModAPI.GetBinaryWriter()) {
            if (fullSnapshot) {
                AdvancedFractionatorRuntime.Export(provider.BinaryWriter);
            } else if (!AdvancedFractionatorRuntime.ExportChanges(provider.BinaryWriter)) {
                return;
            }
            data = provider.CloseAndGetBytes();
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(new AdvancedFractionatorStatePacket(data, fullSnapshot));
    }

    /// <summary>
    /// 客户端请求主机验证并结算一个探索点位。
    /// </summary>
    public static bool RequestExplorationClaim(int markId) {
        if (!CanSendCivilizationAction()) {
            return false;
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(
            CivilizationActionPacket.CreateExplorationClaim(markId));
        return true;
    }

    /// <summary>广播已经结算的回收事实，各客户端仅用自己的语言显示，不执行奖励。</summary>
    /// <param name="mark">已结算的点位副本，包含实际奖励或剧情身份。</param>
    public static void BroadcastExplorationRecovery(ExplorationMark mark) {
        if (mark != null && NebulaModAPI.MultiplayerSession?.LocalPlayer?.IsHost == true) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new ExplorationRecoveryPacket {
                kind = (int)mark.Kind, contentId = mark.ContentId ?? string.Empty,
                itemId = mark.ItemId, count = mark.Count, quality = mark.Quality,
            });
        }
    }

    /// <summary>请求主机按连接位置记录邻近发现，不由客户端声明坐标或发现清单。</summary>
    public static void RequestExplorationDiscovery() {
        if (!CanSendCivilizationAction()) { return; }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(new CivilizationActionPacket {
            actionType = CivilizationActionPacket.DiscoverExploration,
        });
    }

    /// <summary>从主机持有的连接数据读取玩家位置与星球，不采信 FE 动作包里的位置声明。</summary>
    /// <param name="connection">发包连接。</param>
    /// <param name="position">宇宙位置。</param>
    /// <param name="planetId">所在星球。</param>
    /// <returns>是否取得连接玩家数据。</returns>
    public static bool TryGetAuthoritativePlayerState(INebulaConnection connection,
        out VectorLF3 position, out int planetId) {
        position = default;
        planetId = 0;
        object network = NebulaModAPI.MultiplayerSession?.Network;
        if (connection == null || network == null) { return false; }
        PropertyInfo playersProperty = network.GetType().GetProperty("Players", BindingFlags.Instance | BindingFlags.Public);
        if (playersProperty?.GetValue(network) is not ConcurrentPlayerCollection players) { return false; }
        IPlayerData data = players.Get(connection)?.Data;
        if (data == null) { return false; }
        position = new VectorLF3(data.UPosition.x, data.UPosition.y, data.UPosition.z);
        planetId = data.LocalPlanetId;
        return true;
    }

    private static bool CanSendCivilizationAction() =>
        IsMultiplayerActive && IsClient && NebulaModAPI.MultiplayerSession?.Network != null;
}

/// <summary>
/// 数据中心物品变化的联机同步包。
/// </summary>
public class CenterItemChangePacket {
    public byte[] data { get; set; }

    public CenterItemChangePacket() { }

    public CenterItemChangePacket(int itemId, int count, int inc = 0) {
        using IWriterProvider p = NebulaModAPI.GetBinaryWriter();
        BinaryWriter w = p.BinaryWriter;
        w.Write(itemId);
        w.Write(count);
        w.Write(inc);
        data = p.CloseAndGetBytes();
    }
}

/// <summary>
/// 数据中心大额物品变化的联机同步包。
/// </summary>
public class CenterItemChangeLongPacket {
    public byte[] data { get; set; }

    public CenterItemChangeLongPacket() { }

    public CenterItemChangeLongPacket(int itemId, long count, long inc = 0) {
        using IWriterProvider p = NebulaModAPI.GetBinaryWriter();
        BinaryWriter w = p.BinaryWriter;
        w.Write(itemId);
        w.Write(count);
        w.Write(inc);
        data = p.CloseAndGetBytes();
    }
}

/// <summary>
/// 在多人游戏中，当物品发生改变时，向其他玩家推送此事件。
/// </summary>
[RegisterPacketProcessor]
public class CenterItemChangePacketProcessor : BasePacketProcessor<CenterItemChangePacket> {
    public override void ProcessPacket(CenterItemChangePacket packet, INebulaConnection conn) {
        using IReaderProvider p = NebulaModAPI.GetBinaryReader(packet.data);
        BinaryReader r = p.BinaryReader;
        int itemId = r.ReadInt32();
        int count = r.ReadInt32();
        int inc = r.ReadInt32();
        if (IsHost && count < 0) { return; } // 远端提取必须先走主机授权事务。
        CenterItemChangeApply.Apply(itemId, count, inc);
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}

/// <summary>
/// 在多人游戏中，当大额物品发生改变时，向其他玩家推送此事件。
/// </summary>
[RegisterPacketProcessor]
public class CenterItemChangeLongPacketProcessor : BasePacketProcessor<CenterItemChangeLongPacket> {
    public override void ProcessPacket(CenterItemChangeLongPacket packet, INebulaConnection conn) {
        using IReaderProvider p = NebulaModAPI.GetBinaryReader(packet.data);
        BinaryReader r = p.BinaryReader;
        int itemId = r.ReadInt32();
        long count = r.ReadInt64();
        long inc = r.ReadInt64();
        if (IsHost && count < 0) { return; } // 远端提取必须先走主机授权事务。
        CenterItemChangeApply.Apply(itemId, count, inc);
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}

/// <summary>
/// 应用数据中心增量：正数入账，负数走 Take 路径；防御 int/long.MinValue 回绕。
/// </summary>
internal static class CenterItemChangeApply {
    public static void Apply(int itemId, int count, int inc) {
        if (itemId == I沙土) {
            return;
        }
        if (count > 0) {
            if (FractionatorTowerCatalog.IsAdvancedTower(itemId)) {
                DataCenterUploadRouter.Upload(itemId, count, Math.Max(0, inc));
            } else {
                AddItemToModData(itemId, count, Math.Max(0, inc));
            }
            return;
        }
        if (count >= 0) {
            return;
        }
        // int.MinValue 取负会回绕；按最大可扣量处理，Take 会再按余额钳制。
        int takeCount = count == int.MinValue ? int.MaxValue : -count;
        if (takeCount > 0) {
            TakeItemFromModData(itemId, takeCount, out _);
        }
    }

    public static void Apply(int itemId, long count, long inc) {
        if (itemId == I沙土) {
            return;
        }
        if (count > 0) {
            if (FractionatorTowerCatalog.IsAdvancedTower(itemId)) {
                DataCenterUploadRouter.Upload(itemId, count, Math.Max(0L, inc));
            } else {
                AddItemToModData(itemId, count, Math.Max(0L, inc));
            }
            return;
        }
        if (count >= 0L) {
            return;
        }
        long takeCount = count == long.MinValue ? long.MaxValue : -count;
        if (takeCount > 0L) {
            TakeItemFromModData(itemId, takeCount, out _);
        }
    }
}

/// <summary>
/// 研究、探索、亚空间库存和全局堆叠的联机同步快照。
/// </summary>
public class CivilizationStatePacket {
    public byte[] data { get; set; }
    public long fragments { get; set; }
    public long fragmentInc { get; set; }
    public long memory { get; set; }
    public long memoryInc { get; set; }
    public int configuredMaxStack { get; set; }

    public CivilizationStatePacket() { }

    public CivilizationStatePacket(byte[] data, long fragments, long fragmentInc, long memory, long memoryInc,
        int configuredMaxStack) {
        this.data = data;
        this.fragments = fragments;
        this.fragmentInc = fragmentInc;
        this.memory = memory;
        this.memoryInc = memoryInc;
        this.configuredMaxStack = configuredMaxStack;
    }
}

/// <summary>
/// 接收主机文明快照后覆盖客户端本地进度；主机拒绝客户端全量快照。
/// </summary>
[RegisterPacketProcessor]
public class CivilizationStatePacketProcessor : BasePacketProcessor<CivilizationStatePacket> {
    public override void ProcessPacket(CivilizationStatePacket packet, INebulaConnection conn) {
        if (IsHost || packet?.data == null) {
            return;
        }

        using (IReaderProvider provider = NebulaModAPI.GetBinaryReader(packet.data)) {
            BinaryReader reader = provider.BinaryReader;
            reader.ReadBlocks(
                ("Civilization", CivilizationModule.Import),
                // 旧版统计块已按长度隔离；静默跳过，不恢复配方或过程计数。
                ("Recipes", _ => { }),
                ("DataCenter", br => {
                    // 稀疏 Import 不会清除缺席槽；先清空旧库存再导入主机全量快照。
                    IntoOtherSave();
                    Import(br);
                }),
                ("StationModes", FE.Logic.Station.StationManager.Import),
                ("Process", _ => { })
            );
        }
        StackingManager.ConfiguredMaxStack = packet.configuredMaxStack;
        StackingManager.SyncRuntimeState();
        // Frag 与 Memory 以快照里的绝对值为准，直接覆盖本地余额，和上面导入的库存块保持一致。
        lock (centerItemCount) {
            long fragmentCount = Math.Max(0L, packet.fragments);
            long fragmentMaxInc = fragmentCount > long.MaxValue / 10L ? long.MaxValue : fragmentCount * 10L;
            centerItemCount[IFE残片] = fragmentCount;
            centerItemInc[IFE残片] = Math.Max(0L, Math.Min(packet.fragmentInc, fragmentMaxInc));

            long memoryCount = Math.Max(0L, packet.memory);
            long memoryMaxInc = memoryCount > long.MaxValue / 10L ? long.MaxValue : memoryCount * 10L;
            centerItemCount[IFE记忆源点] = memoryCount;
            centerItemInc[IFE记忆源点] = Math.Max(0L, Math.Min(packet.memoryInc, memoryMaxInc));
        }
        CivilizationModule.AfterImport();
    }
}

/// <summary>
/// 同步高级塔的多输入缓存、产物、营养与选定配方。
/// </summary>
public class AdvancedFractionatorStatePacket {
    /// <summary>实例序列；增量包在实例序列前携带删除身份列表。</summary>
    public byte[] data { get; set; }
    /// <summary>完整快照覆盖存活集合，增量只更新出现的实例。</summary>
    public bool fullSnapshot { get; set; }

    /// <summary>Nebula 反序列化入口。</summary>
    public AdvancedFractionatorStatePacket() { }

    /// <summary>构造一个已序列化的高级塔快照。</summary>
    /// <param name="data">主机状态字节。</param>
    /// <param name="fullSnapshot">是否替换完整存活集合。</param>
    public AdvancedFractionatorStatePacket(byte[] data, bool fullSnapshot) {
        this.data = data;
        this.fullSnapshot = fullSnapshot;
    }
}

/// <summary>
/// 客户端原位应用主机高级塔状态，保留未变化实例及窗口引用。
/// </summary>
[RegisterPacketProcessor]
public class AdvancedFractionatorStatePacketProcessor : BasePacketProcessor<AdvancedFractionatorStatePacket> {
    /// <inheritdoc />
    public override void ProcessPacket(AdvancedFractionatorStatePacket packet, INebulaConnection conn) {
        if (IsHost || packet?.data == null) {
            return;
        }
        using IReaderProvider provider = NebulaModAPI.GetBinaryReader(packet.data);
        AdvancedFractionatorRuntime.ApplySnapshot(provider.BinaryReader, packet.fullSnapshot);
    }
}

/// <summary>主机发送的回收事实，足以独立生成本地化提示，不包含可执行的奖励指令。</summary>
public sealed class ExplorationRecoveryPacket {
    /// <summary>点位类型，取值对应 ExplorationMarkKind，也决定弹窗或浮现提示。</summary>
    public int kind { get; set; }
    /// <summary>研究线索的稳定身份或终极任务分支；不直接展示。</summary>
    public string contentId { get; set; }
    /// <summary>实际奖励或样本的物品ID；剧情点存节点序号。</summary>
    public int itemId { get; set; }
    /// <summary>实际奖励件数；研究线索和剧情节点为零。</summary>
    public int count { get; set; }
    /// <summary>遗迹固定质量，1～3，用于生成本地标题。</summary>
    public int quality { get; set; }
}

/// <summary>在客户端排队显示主机的实际回收结果。</summary>
[RegisterPacketProcessor]
public sealed class ExplorationRecoveryPacketProcessor : BasePacketProcessor<ExplorationRecoveryPacket> {
    /// <inheritdoc />
    public override void ProcessPacket(ExplorationRecoveryPacket packet, INebulaConnection conn) {
        if (IsHost || packet == null) { return; }
        if (!Enum.IsDefined(typeof(ExplorationMarkKind), packet.kind)
            || packet.quality is < 1 or > 3 || packet.count < 0) {
            LogError("Ignored exploration recovery packet with invalid kind, quality or count.");
            return;
        }
        CivilizationModule.QueueRecoveryPopup(new ExplorationMark {
            Kind = (ExplorationMarkKind)packet.kind, ContentId = packet.contentId ?? string.Empty,
            ItemId = packet.itemId, Count = packet.count, Quality = packet.quality,
        });
    }
}

/// <summary>
/// 客户端提交给主机的文明状态变更请求。
/// </summary>
public class CivilizationActionPacket {
    /// <summary>客户端请求主机校验并结算探索点位。</summary>
    public const int ClaimExploration = 1;

    /// <summary>按主机持有的玩家位置记录邻近发现。</summary>
    public const int DiscoverExploration = 3;

    public int actionType { get; set; }

    /// <summary>待认领的探索点位 ID；位置由主机连接数据提供。</summary>
    public int markId { get; set; }

    public CivilizationActionPacket() { }

    public static CivilizationActionPacket CreateExplorationClaim(int markId) => new() {
        actionType = ClaimExploration,
        markId = markId,
    };
}

/// <summary>
/// 只允许主机执行客户端提交的文明动作，并在状态改变后同步给所有玩家。
/// </summary>
[RegisterPacketProcessor]
public class CivilizationActionPacketProcessor : BasePacketProcessor<CivilizationActionPacket> {
    public override void ProcessPacket(CivilizationActionPacket packet, INebulaConnection conn) {
        if (!IsHost || packet == null) {
            return;
        }

        if (!NebulaMultiplayerModAPI.TryGetAuthoritativePlayerState(conn,
                out VectorLF3 position, out int planetId)) { return; }
        switch (packet.actionType) {
            case CivilizationActionPacket.DiscoverExploration:
                ExplorationManager.DiscoverNearby(position, planetId);
                break;
            case CivilizationActionPacket.ClaimExploration:
                ExplorationManager.TryClaimFromClient(packet.markId, position, planetId);
                break;
        }
    }
}

/// <summary>
/// 分馏塔实例级主产物目标变化的联机同步包。
/// </summary>
public class BuildingChangePacket {
    public int buildingId { get; set; }
    public int packetType { get; set; }
    public int planetId { get; set; } = 0;
    public int entityId { get; set; } = 0;
    public int itemId { get; set; } = 0;

    public BuildingChangePacket() { }

    public BuildingChangePacket(int buildingId, int packetType, int planetId, int entityId, int itemId) {
        this.buildingId = buildingId;
        this.packetType = packetType;
        this.planetId = planetId;
        this.entityId = entityId;
        this.itemId = itemId;
    }
}

/// <summary>
/// 在多人游戏中同步高级塔的稳定配方选择。
/// </summary>
[RegisterPacketProcessor]
public class BuildingChangePacketProcessor : BasePacketProcessor<BuildingChangePacket> {
    public override void ProcessPacket(BuildingChangePacket packet, INebulaConnection conn) {
        if (packet == null || !FractionatorTowerCatalog.IsActiveFractionator(packet.buildingId)) {
            return;
        }
        bool applied;
        switch (packet.packetType) {
            case 2:
                applied = AdvancedRecipeSelectionSync.ApplyPacket(
                    packet.planetId, packet.entityId, packet.itemId);
                break;
            default:
                return;
        }
        if (applied && NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}
