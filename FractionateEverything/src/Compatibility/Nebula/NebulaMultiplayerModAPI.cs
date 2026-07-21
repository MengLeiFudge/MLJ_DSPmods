using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using FE.Logic.Buildings;
using FE.Logic.Civilization;
using FE.Logic.Civilization.Analysis;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.Process;
using FE.Logic.Fractionation.Fractionators;
using HarmonyLib;
using NebulaAPI;
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
        NebulaModAPI.RegisterPackets(Assembly.GetExecutingAssembly());
        NebulaModAPI.OnMultiplayerGameStarted += OnMultiplayerGameStarted;
        NebulaModAPI.OnMultiplayerGameEnded += OnMultiplayerGameEnded;
        NebulaModAPI.OnPlayerJoinedGame += _ => {
            if (NebulaModAPI.MultiplayerSession?.LocalPlayer?.IsHost == true) {
                BroadcastCivilizationState();
            }
        };
        CheckPlugins.LogInfo("NebulaMultiplayerModAPI Compat finish.");
    }

    public static void OnMultiplayerGameStarted() {
        IsMultiplayerActive = NebulaModAPI.IsMultiplayerActive;
        IsClient = IsMultiplayerActive && NebulaModAPI.MultiplayerSession.LocalPlayer.IsClient;
    }

    public static void OnMultiplayerGameEnded() {
        IsMultiplayerActive = false;
        IsClient = false;
    }

    public static bool IsOthers()// Action triggered by packets from other player
    {
        var factoryManager = NebulaModAPI.MultiplayerSession.Factories;
        return factoryManager.IsIncomingRequest.Value
               && factoryManager.PacketAuthor != NebulaModAPI.MultiplayerSession.LocalPlayer.Id;
    }

    /// <summary>
    /// 广播文明进度、配方校准和两种检索货币的当前权威快照。
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
                ("Recipes", RecipeManager.Export)
            );
            stateData = provider.CloseAndGetBytes();
        }

        long fragments;
        long fragmentInc;
        long memorySourcePoints;
        long memorySourcePointInc;
        lock (centerItemCount) {
            fragments = centerItemCount[IFE残片];
            fragmentInc = centerItemInc[IFE残片];
            memorySourcePoints = centerItemCount[IFE记忆源点];
            memorySourcePointInc = centerItemInc[IFE记忆源点];
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(new CivilizationStatePacket(stateData,
            fragments, fragmentInc, memorySourcePoints, memorySourcePointInc));
    }

    /// <summary>
    /// 客户端请求主机执行一次或一批协议检索。
    /// </summary>
    public static bool RequestProtocolRetrieval(ProtocolRetrievalRequest request, int count) {
        if (!CanSendCivilizationAction()) {
            return false;
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(
            CivilizationActionPacket.CreateRetrieval(request, count));
        return true;
    }

    /// <summary>
    /// 客户端请求主机切换阶段优先协议。
    /// </summary>
    public static bool RequestPreferredProtocolCycle(string stageKey) {
        if (!CanSendCivilizationAction()) {
            return false;
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(
            CivilizationActionPacket.CreatePreferredProtocolCycle(stageKey));
        return true;
    }

    /// <summary>
    /// 客户端请求主机购买远古科技节点。
    /// </summary>
    public static bool RequestAncientTechPurchase(string nodeKey) {
        if (!CanSendCivilizationAction()) {
            return false;
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(
            CivilizationActionPacket.CreateAncientTechPurchase(nodeKey));
        return true;
    }

    /// <summary>
    /// 客户端请求主机结算一次手动解析数据上传。
    /// </summary>
    public static bool RequestAnalysisDataUpload(int itemId, long count) {
        if (!CanSendCivilizationAction()) {
            return false;
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(
            CivilizationActionPacket.CreateAnalysisDataUpload(itemId, count));
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
        AddItemToModData(itemId, count, inc);
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
        AddItemToModData(itemId, count, inc);
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}

/// <summary>
/// 文明进度、配方校准和检索货币的联机同步快照。
/// </summary>
public class CivilizationStatePacket {
    public byte[] data { get; set; }
    public long fragments { get; set; }
    public long fragmentInc { get; set; }
    public long memorySourcePoints { get; set; }
    public long memorySourcePointInc { get; set; }

    public CivilizationStatePacket() { }

    public CivilizationStatePacket(byte[] data, long fragments, long fragmentInc,
        long memorySourcePoints, long memorySourcePointInc) {
        this.data = data;
        this.fragments = fragments;
        this.fragmentInc = fragmentInc;
        this.memorySourcePoints = memorySourcePoints;
        this.memorySourcePointInc = memorySourcePointInc;
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
                ("Recipes", RecipeManager.Import)
            );
        }
        lock (centerItemCount) {
            long fragmentCount = Math.Max(0L, packet.fragments);
            long memorySourcePointCount = Math.Max(0L, packet.memorySourcePoints);
            long fragmentMaxInc = fragmentCount > long.MaxValue / 10L ? long.MaxValue : fragmentCount * 10L;
            long memorySourcePointMaxInc = memorySourcePointCount > long.MaxValue / 10L
                ? long.MaxValue
                : memorySourcePointCount * 10L;
            centerItemCount[IFE残片] = fragmentCount;
            centerItemInc[IFE残片] = Math.Max(0L, Math.Min(packet.fragmentInc, fragmentMaxInc));
            centerItemCount[IFE记忆源点] = memorySourcePointCount;
            centerItemInc[IFE记忆源点] = Math.Max(0L,
                Math.Min(packet.memorySourcePointInc, memorySourcePointMaxInc));
        }
        CivilizationModule.AfterImport();
    }
}

/// <summary>
/// 客户端提交给主机的文明状态变更请求。
/// </summary>
public class CivilizationActionPacket {
    public const int Retrieve = 1;
    public const int CyclePreferredProtocol = 2;
    public const int PurchaseAncientTech = 3;
    public const int UploadAnalysisData = 4;

    public int actionType { get; set; }
    public string stageKey { get; set; }
    public int retrievalMode { get; set; }
    public int directionalRecipeType { get; set; }
    public int anchoredRecipeType { get; set; }
    public int anchoredInputId { get; set; }
    public bool hasAnchoredRecipe { get; set; }
    public int count { get; set; }
    public string nodeKey { get; set; }
    public int itemId { get; set; }
    public long itemCount { get; set; }

    public CivilizationActionPacket() { }

    public static CivilizationActionPacket CreateRetrieval(ProtocolRetrievalRequest request, int count) => new() {
        actionType = Retrieve,
        stageKey = request.StageKey,
        retrievalMode = (int)request.Mode,
        directionalRecipeType = (int)request.DirectionalRecipeType,
        anchoredRecipeType = (int)request.AnchoredRecipeKey.RecipeType,
        anchoredInputId = request.AnchoredRecipeKey.InputId,
        hasAnchoredRecipe = request.HasAnchoredRecipe,
        count = Math.Max(1, count),
    };

    public static CivilizationActionPacket CreatePreferredProtocolCycle(string stageKey) => new() {
        actionType = CyclePreferredProtocol,
        stageKey = stageKey,
    };

    public static CivilizationActionPacket CreateAncientTechPurchase(string nodeKey) => new() {
        actionType = PurchaseAncientTech,
        nodeKey = nodeKey,
    };

    public static CivilizationActionPacket CreateAnalysisDataUpload(int itemId, long count) => new() {
        actionType = UploadAnalysisData,
        itemId = itemId,
        itemCount = count,
    };
}

/// <summary>
/// 仅在主机执行客户端提交的文明动作，并在状态改变后广播权威快照。
/// </summary>
[RegisterPacketProcessor]
public class CivilizationActionPacketProcessor : BasePacketProcessor<CivilizationActionPacket> {
    public override void ProcessPacket(CivilizationActionPacket packet, INebulaConnection conn) {
        if (!IsHost || packet == null) {
            return;
        }

        switch (packet.actionType) {
            case CivilizationActionPacket.Retrieve:
                ProtocolRetrievalMode mode = (ProtocolRetrievalMode)packet.retrievalMode;
                if (mode is not (ProtocolRetrievalMode.Broad
                    or ProtocolRetrievalMode.Directional
                    or ProtocolRetrievalMode.Anchored)) {
                    break;
                }
                ProtocolRetrievalRequest request = new(packet.stageKey,
                    mode,
                    (ERecipe)packet.directionalRecipeType,
                    new RecipeKey((ERecipe)packet.anchoredRecipeType, packet.anchoredInputId),
                    packet.hasAnchoredRecipe);
                int retrievalCount = Math.Min(ProtocolRetrievalService.DefaultBatchCount,
                    Math.Max(1, packet.count));
                if (retrievalCount == 1) {
                    ProtocolRetrievalService.TryRetrieve(request, out _);
                } else {
                    ProtocolRetrievalService.RetrieveBatch(request, retrievalCount);
                }
                break;
            case CivilizationActionPacket.CyclePreferredProtocol:
                ProtocolRetrievalService.CyclePreferredProtocol(packet.stageKey);
                break;
            case CivilizationActionPacket.PurchaseAncientTech:
                AncientTechTreeService.TryPurchase(packet.nodeKey);
                break;
            case CivilizationActionPacket.UploadAnalysisData:
                if (AnalysisService.TrySubmitDataItem(packet.itemId, packet.itemCount, out _)) {
                    NebulaMultiplayerModAPI.BroadcastCivilizationState();
                }
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

    public BuildingChangePacket(int buildingId, int packetType) {
        this.buildingId = buildingId;
        this.packetType = packetType;
    }

    public BuildingChangePacket(int buildingId, int packetType, int planetId, int entityId, int itemId) {
        this.buildingId = buildingId;
        this.packetType = packetType;
        this.planetId = planetId;
        this.entityId = entityId;
        this.itemId = itemId;
    }
}

/// <summary>
/// 在多人游戏中同步分馏塔实例级主产物目标。
/// </summary>
[RegisterPacketProcessor]
public class BuildingChangePacketProcessor : BasePacketProcessor<BuildingChangePacket> {
    public override void ProcessPacket(BuildingChangePacket packet, INebulaConnection conn) {
        switch (packet.packetType) {
            case 2:
                FractionatorSingleLock.ApplyLockedOutputPacket(packet.planetId, packet.entityId, packet.itemId);
                break;
            case 3:
                AnalysisLineageTarget.ApplyLineageTargetPacket(packet.planetId, packet.entityId, packet.itemId);
                break;
            case 4:
                FractionatorByproductDiscard.ApplyPacket(packet.planetId, packet.entityId, packet.itemId != 0);
                break;
        }
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}
