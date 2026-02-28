using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using HarmonyLib;
using NebulaAPI;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Utils.Utils;

namespace FE.Compatibility;

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
}

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

public class RecipeChangePacket {
    public int eRecipe { get; set; }
    public int inputId { get; set; }
    public int packetType { get; set; }
    public int intVal { get; set; } = 0;
    public float floatVal { get; set; } = 0;

    /// <summary>
    /// 空构造方法必须保留
    /// </summary>
    public RecipeChangePacket() { }

    public RecipeChangePacket(ERecipe eRecipe, int inputId, int packetType) {
        this.eRecipe = Convert.ToInt32(eRecipe);
        this.inputId = inputId;
        this.packetType = packetType;
    }

    public RecipeChangePacket(ERecipe eRecipe, int inputId, int packetType, int intVal) {
        this.eRecipe = Convert.ToInt32(eRecipe);
        this.inputId = inputId;
        this.packetType = packetType;
        this.intVal = intVal;
    }

    public RecipeChangePacket(ERecipe eRecipe, int inputId, int packetType, float floatVal) {
        this.eRecipe = Convert.ToInt32(eRecipe);
        this.inputId = inputId;
        this.packetType = packetType;
        this.floatVal = floatVal;
    }
}

/// <summary>
/// 在多人游戏中，当配方发生改变时，向其他玩家推送此事件。
/// </summary>
[RegisterPacketProcessor]
public class RecipeChangePacketProcessor : BasePacketProcessor<RecipeChangePacket> {
    public override void ProcessPacket(RecipeChangePacket packet, INebulaConnection conn) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>((ERecipe)packet.eRecipe, packet.inputId);
        switch (packet.packetType) {
            case 1:
                recipe.RewardEcho();
                break;
            case 2:
                recipe.ChangeEchoTo(packet.intVal);
                break;
            case 3:
                recipe.RewardExp(packet.floatVal);
                break;
            case 4:
                recipe.ChangeLevelTo(packet.intVal);
                break;
        }
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}

public class BuildingChangePacket {
    public int buildingId { get; set; }
    public int packetType { get; set; }
    public int intVal { get; set; } = 0;
    public float floatVal { get; set; } = 0;

    public BuildingChangePacket() { }

    public BuildingChangePacket(int buildingId, int packetType) {
        this.buildingId = buildingId;
        this.packetType = packetType;
    }

    public BuildingChangePacket(int buildingId, int packetType, int intVal) {
        this.buildingId = buildingId;
        this.packetType = packetType;
        this.intVal = intVal;
    }

    public BuildingChangePacket(int buildingId, int packetType, float floatVal) {
        this.buildingId = buildingId;
        this.packetType = packetType;
        this.floatVal = floatVal;
    }
}

/// <summary>
/// 在多人游戏中，当建筑强化进度发生改变时，向其他玩家推送此事件。
/// </summary>
[RegisterPacketProcessor]
public class BuildingChangePacketProcessor : BasePacketProcessor<BuildingChangePacket> {
    public override void ProcessPacket(BuildingChangePacket packet, INebulaConnection conn) {
        ItemProto selectedBuilding = LDB.items.Select(packet.buildingId);
        switch (packet.packetType) {
            case 1:
                selectedBuilding.Level(packet.intVal);
                break;
        }
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}
