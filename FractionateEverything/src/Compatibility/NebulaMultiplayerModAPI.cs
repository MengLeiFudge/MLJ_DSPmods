using System;
using BepInEx.Bootstrap;
using DeliverySlotsTweaks;
using HarmonyLib;
using NebulaAPI;

namespace FE.Compatibility;

public static class NebulaMultiplayerModAPI {
    internal const string GUID = "dsp.nebula-multiplayer";

    internal static bool Enable;
    internal static bool IsPatched;
    internal static bool IsActive;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) return;

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.NebulaMultiplayerModAPI");
        harmony.PatchAll(typeof(NebulaMultiplayerModAPI));
        CheckPlugins.LogInfo("NebulaMultiplayerModAPI Compat finish.");
    }

    public static bool IsOthers()// Action triggered by packets from other player
    {
        var factoryManager = NebulaModAPI.MultiplayerSession.Factories;
        return factoryManager.IsIncomingRequest.Value
               && factoryManager.PacketAuthor != NebulaModAPI.MultiplayerSession.LocalPlayer.Id;
    }

    private static void Patch(Harmony harmony) {
        // Separate for using NebulaModAPI
        if (!NebulaModAPI.NebulaIsInstalled || IsPatched)
            return;
        NebulaModAPI.OnMultiplayerGameStarted += OnMultiplayerGameStarted;
        NebulaModAPI.OnMultiplayerGameEnded += OnMultiplayerGameEnded;

        Type classType = AccessTools.TypeByName("NebulaWorld.SimulatedWorld");
        harmony.Patch(AccessTools.Method(classType, "SetupInitialPlayerState"), null,
            new HarmonyMethod(AccessTools.Method(typeof(Plugin), nameof(Plugin.ApplyConfigs))));

#if DEBUG
        OnMultiplayerGameStarted();
#endif
        IsPatched = true;
    }

    private static void OnMultiplayerGameStarted() {
        IsActive = NebulaModAPI.IsMultiplayerActive;
    }

    private static void OnMultiplayerGameEnded() {
        IsActive = false;
    }
}
