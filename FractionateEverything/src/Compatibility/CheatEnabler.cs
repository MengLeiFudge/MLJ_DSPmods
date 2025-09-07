using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using DeliverySlotsTweaks;
using HarmonyLib;

namespace FE.Compatibility;

public static class CheatEnabler {
    internal const string GUID = "org.soardev.cheatenabler";

    internal static bool Enable;
    public static ConfigEntry<bool> ArchitectModeEnabledEntry;
    public static bool ArchitectMode => ArchitectModeEnabledEntry?.Value == true;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable) return;

        try {
            Assembly assembly = pluginInfo.Instance.GetType().Assembly;
            Type classType = assembly.GetType("CheatEnabler.Patches.FactoryPatch");
            ArchitectModeEnabledEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "ArchitectModeEnabled").GetValue(null);
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"Failed to compat CheatEnabler: {ex}");
        }

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.CheatEnabler");
        harmony.PatchAll(typeof(CheatEnabler));
        CheckPlugins.LogInfo("CheatEnabler Compat finish.");
    }
}
