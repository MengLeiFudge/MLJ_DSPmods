using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.View.CoreOperate;
using HarmonyLib;
using NebulaAPI;
using NebulaAPI.Interfaces;
using NebulaAPI.Networking;
using NebulaAPI.Packets;
using static FE.Utils.Utils;

namespace FE.Compatibility;

public static class NebulaMultiplayerModAPI {
    public const string GUID = "dsp.nebula-multiplayer-api";
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

    public int mode { get; set; }

    public float num { get; set; }

    public RecipeChangePacket() { }

    public RecipeChangePacket(ERecipe eRecipe, int inputId, int mode, float num = 0) {
        this.eRecipe = Convert.ToInt32(eRecipe);
        this.inputId = inputId;
        this.mode = mode;
        this.num = num;
    }
}

/// <summary>
/// 在多人游戏中，当配方发生改变时，向其他玩家推送此事件。
/// </summary>
[RegisterPacketProcessor]
public class RecipeChangePacketProcessor : BasePacketProcessor<RecipeChangePacket> {
    public override void ProcessPacket(RecipeChangePacket packet, INebulaConnection conn) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>((ERecipe)packet.eRecipe, packet.inputId);
        switch (packet.mode) {
            case 1:
                recipe.RewardThis();
                break;
            case 2:
                recipe.AddExp(packet.num, false);
                break;
        }
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}

public class BuildingChangePacket {
    public int index { get; set; }

    public int mode { get; set; }

    public int num { get; set; }

    public BuildingChangePacket() { }

    public BuildingChangePacket(int index, int mode, int num = 0) {
        this.index = index;
        this.mode = mode;
        this.num = num;
    }
}

/// <summary>
/// 在多人游戏中，当建筑强化进度发生改变时，向其他玩家推送此事件。
/// </summary>
[RegisterPacketProcessor]
public class BuildingChangePacketProcessor : BasePacketProcessor<BuildingChangePacket> {
    public override void ProcessPacket(BuildingChangePacket packet, INebulaConnection conn) {
        ItemProto selectedBuilding = packet.mode == 5
            ? LDB.items.Select(packet.index)
            : BuildingOperate.GetItemProto(packet.index);
        switch (packet.mode) {
            case 1:
                selectedBuilding.EnableFluidOutputStack(true);
                break;
            case 2:
                selectedBuilding.MaxProductOutputStack(selectedBuilding.MaxProductOutputStack() + 1);
                break;
            case 3:
                selectedBuilding.EnableFracForever(true);
                break;
            case 4:
                PointAggregateTower.Level++;
                break;
            case 5:
                selectedBuilding.ReinforcementLevel(packet.num);
                break;
        }
        if (NebulaModAPI.IsMultiplayerActive && IsHost) {
            NebulaModAPI.MultiplayerSession.Network.SendPacketExclude(packet, conn);
        }
    }
}
