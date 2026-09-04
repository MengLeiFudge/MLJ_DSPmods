using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using FE.Logic.Civilization;
using FE.Logic.Civilization.Achievements;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
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
        CivilizationRecoveryManager.ResetNetworkRequestThrottle();
    }

    public static bool IsOthers()// 由其他玩家的数据包触发
    {
        var factoryManager = NebulaModAPI.MultiplayerSession.Factories;
        return factoryManager.IsIncomingRequest.Value
               && factoryManager.PacketAuthor != NebulaModAPI.MultiplayerSession.LocalPlayer.Id;
    }

    /// <summary>
    /// 广播文明进度、成就、配方校准、分馏过程统计、完整 DataCenter 稀疏库存与基础 Frag/Memory 的当前权威快照。
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
                ("Achievements", AchievementModule.Export),
                ("Recipes", RecipeManager.Export),
                // 正式解锁会原子扣除普通实物；权威快照必须覆盖完整稀疏库存。
                ("DataCenter", Export),
                // 分馏成功计数等过程统计仅主机权威，客户端 Import 覆盖本地显示。
                ("Process", ProcessManager.Export)
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
    /// 客户端请求主机执行一次或一批协议检索；不预扣本地余额。
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
    /// 客户端请求主机正式解锁协议；不预扣本地余额。
    /// </summary>
    public static bool RequestProtocolFormalUnlock(RecipeKey recipeKey) {
        if (!CanSendCivilizationAction()) {
            return false;
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(
            CivilizationActionPacket.CreateFormalUnlock(recipeKey));
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
    /// 客户端请求主机校验出生星启动信号位置并执行启动套件恢复；不写本地权威状态。
    /// </summary>
    /// <param name="planetId">客户端当前星球 ID。</param>
    /// <param name="posX">客户端玩家世界坐标 X。</param>
    /// <param name="posY">客户端玩家世界坐标 Y。</param>
    /// <param name="posZ">客户端玩家世界坐标 Z。</param>
    /// <returns>已成功发包时为 true。</returns>
    public static bool RequestStarterKitRecovery(int planetId, float posX, float posY, float posZ) {
        if (!CanSendCivilizationAction()) {
            return false;
        }
        NebulaModAPI.MultiplayerSession.Network.SendPacket(
            CivilizationActionPacket.CreateStarterKitRecovery(planetId, posX, posY, posZ));
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
        CenterItemChangeApply.Apply(itemId, count, inc);
        if (IsHost && count > 0 && FractionatorTowerCatalog.IsActiveFractionator(itemId)) {
            TechManager.CheckTechUnlockCondition(itemId);
        }
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
        CenterItemChangeApply.Apply(itemId, count, inc);
        if (IsHost && count > 0 && FractionatorTowerCatalog.IsActiveFractionator(itemId)) {
            TechManager.CheckTechUnlockCondition(itemId);
        }
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
        if (count > 0) {
            AddItemToModData(itemId, count, Math.Max(0, inc));
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
        if (count > 0) {
            AddItemToModData(itemId, count, Math.Max(0L, inc));
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
/// 文明进度、成就、配方校准、分馏过程统计、完整 DataCenter 库存与基础 Frag/Memory 的联机同步快照。
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
                ("Achievements", AchievementModule.Import),
                ("Recipes", RecipeManager.Import),
                ("DataCenter", br => {
                    // 稀疏 Import 不会清除缺席槽；先清空旧库存再导入主机全量快照。
                    IntoOtherSave();
                    Import(br);
                }),
                // 仅客户端 Import 主机过程统计；主机拒绝本包，不会被客户端 Process 覆盖。
                ("Process", ProcessManager.Import)
            );
        }
        StackingManager.ConfiguredMaxStack = packet.configuredMaxStack;
        StackingManager.SyncRuntimeState();
        // 保留绝对 Frag/Memory 字段作为与稀疏块一致的权威覆盖。
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
        AchievementModule.AfterImport();
    }
}

/// <summary>
/// 客户端提交给主机的文明状态变更请求。
/// </summary>
public class CivilizationActionPacket {
    public const int Retrieve = 1;
    public const int PurchaseAncientTech = 3;
    public const int FormalUnlock = 5;
    /// <summary>客户端请求主机校验并结算启动套件恢复。</summary>
    public const int RecoverStarterKit = 6;

    public int actionType { get; set; }
    public int retrievalMode { get; set; }
    public int directionalRecipeType { get; set; }
    public int anchoredRecipeType { get; set; }
    public int anchoredInputId { get; set; }
    public bool hasAnchoredRecipe { get; set; }
    public int count { get; set; }
    public string nodeKey { get; set; }
    public int unlockRecipeType { get; set; }
    public int unlockInputId { get; set; }
    /// <summary>启动恢复请求的星球 ID。</summary>
    public int planetId { get; set; }
    /// <summary>启动恢复请求的玩家世界坐标 X。</summary>
    public float posX { get; set; }
    /// <summary>启动恢复请求的玩家世界坐标 Y。</summary>
    public float posY { get; set; }
    /// <summary>启动恢复请求的玩家世界坐标 Z。</summary>
    public float posZ { get; set; }

    public CivilizationActionPacket() { }

    public static CivilizationActionPacket CreateRetrieval(ProtocolRetrievalRequest request, int count) => new() {
        actionType = Retrieve,
        retrievalMode = (int)request.Mode,
        directionalRecipeType = (int)request.DirectionalRecipeType,
        anchoredRecipeType = (int)request.AnchoredRecipeKey.RecipeType,
        anchoredInputId = request.AnchoredRecipeKey.InputId,
        hasAnchoredRecipe = request.HasAnchoredRecipe,
        count = Math.Max(1, count),
    };

    public static CivilizationActionPacket CreateAncientTechPurchase(string nodeKey) => new() {
        actionType = PurchaseAncientTech,
        nodeKey = nodeKey,
    };

    public static CivilizationActionPacket CreateFormalUnlock(RecipeKey recipeKey) => new() {
        actionType = FormalUnlock,
        unlockRecipeType = (int)recipeKey.RecipeType,
        unlockInputId = recipeKey.InputId,
    };

    /// <summary>
    /// 构造启动套件恢复请求包；坐标仅供主机校验，不表示客户端已恢复。
    /// </summary>
    public static CivilizationActionPacket CreateStarterKitRecovery(int planetId, float posX, float posY, float posZ) =>
        new() {
            actionType = RecoverStarterKit,
            planetId = planetId,
            posX = posX,
            posY = posY,
            posZ = posZ,
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
            case CivilizationActionPacket.Retrieve: {
                ProtocolRetrievalMode mode = (ProtocolRetrievalMode)packet.retrievalMode;
                if (mode is not (ProtocolRetrievalMode.Broad
                    or ProtocolRetrievalMode.Directional
                    or ProtocolRetrievalMode.Anchored)) {
                    break;
                }
                ProtocolRetrievalRequest request = new(
                    mode,
                    (ERecipe)packet.directionalRecipeType,
                    new RecipeKey((ERecipe)packet.anchoredRecipeType, packet.anchoredInputId),
                    packet.hasAnchoredRecipe);
                int retrievalCount = Math.Min(ProtocolRetrievalService.DefaultBatchCount,
                    Math.Max(1, packet.count));
                // 主机直接跑核心，避免 Request* 再折回客户端分支。
                if (retrievalCount == 1) {
                    if (ProtocolRetrievalService.TryRetrieveCore(request, out _)) {
                        NebulaMultiplayerModAPI.BroadcastCivilizationState();
                    }
                } else {
                    int executed = 0;
                    for (int i = 0; i < retrievalCount; i++) {
                        if (!ProtocolRetrievalService.TryRetrieveCore(request, out _)) {
                            break;
                        }
                        executed++;
                    }
                    if (executed > 0) {
                        NebulaMultiplayerModAPI.BroadcastCivilizationState();
                    }
                }
                break;
            }
            case CivilizationActionPacket.PurchaseAncientTech:
                AncientTechTreeService.TryPurchase(packet.nodeKey);
                break;
            case CivilizationActionPacket.FormalUnlock: {
                RecipeKey recipeKey = new((ERecipe)packet.unlockRecipeType, packet.unlockInputId);
                if (ProtocolUnlockService.TryUnlockCore(recipeKey, out _)) {
                    NebulaMultiplayerModAPI.BroadcastCivilizationState();
                }
                break;
            }
            case CivilizationActionPacket.RecoverStarterKit:
                // 校验与结算均在主机 CivilizationRecoveryManager 内完成；成功时由其广播快照。
                CivilizationRecoveryManager.TryRecoverStarterKitFromClientRequest(
                    packet.planetId, packet.posX, packet.posY, packet.posZ);
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
/// 在多人游戏中同步分馏塔实例级主产物目标和副产物弃置状态。
/// </summary>
[RegisterPacketProcessor]
public class BuildingChangePacketProcessor : BasePacketProcessor<BuildingChangePacket> {
    public override void ProcessPacket(BuildingChangePacket packet, INebulaConnection conn) {
        if (packet == null || !FractionatorTowerCatalog.IsActiveFractionator(packet.buildingId)) {
            return;
        }
        switch (packet.packetType) {
            case 2:
                FractionatorMainOutputLock.ApplyLockedOutputPacket(packet.planetId, packet.entityId, packet.itemId);
                break;
            case 4:
                FractionatorByproductDiscard.ApplyPacket(packet.planetId, packet.entityId, packet.itemId != 0);
                break;
            default:
                return;
        }
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}
