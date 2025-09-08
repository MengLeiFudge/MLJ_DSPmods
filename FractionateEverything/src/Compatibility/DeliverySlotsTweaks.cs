using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using DeliverySlotsTweaks;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Compatibility;

public static class DeliverySlotsTweaks {
    internal const string GUID = "starfi5h.plugin.DeliverySlotsTweaks";

    internal static bool Enable;
    public static ConfigEntry<bool> ArchitectModeEnabledEntry;
    public static bool ArchitectMode => ArchitectModeEnabledEntry?.Value == true;
    public static ConfigEntry<bool> UseLogisticSlotsEntry;
    public static bool UseLogisticSlots => UseLogisticSlotsEntry?.Value == true;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) return;

        try {
            Assembly assembly = pluginInfo.Instance.GetType().Assembly;
            Type classType = AccessTools.TypeByName("DeliverySlotsTweaks.Plugin");
            ArchitectModeEnabledEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "EnableArchitectMode").GetValue(null);
            UseLogisticSlotsEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "UseLogisticSlots").GetValue(null);
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"Failed to compat DeliverySlotsTweaks: {ex}");
        }

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.DeliverySlotsTweaks");
        harmony.PatchAll(typeof(DeliverySlotsTweaks));
        CheckPlugins.LogInfo("DeliverySlotsTweaks Compat finish.");
    }
}
