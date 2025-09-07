using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using DeliverySlotsTweaks;
using HarmonyLib;

namespace FE.Compatibility;

public class Multfunction_mod {
    internal const string GUID = "cn.blacksnipe.dsp.Multfuntion_mod";

    internal static bool Enable;
    public static ConfigEntry<bool> ArchitectModeEnabledEntry;
    public static bool ArchitectMode => ArchitectModeEnabledEntry?.Value == true;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable) return;

        try {
            Assembly assembly = pluginInfo.Instance.GetType().Assembly;
            Type classType = AccessTools.TypeByName("Multifunction_mod.Multifunction");
            ArchitectModeEnabledEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "ArchitectMode").GetValue(null);
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"Failed to compat Multfunction_mod: {ex}");
        }

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.Multfunction_mod");
        harmony.PatchAll(typeof(DeliverySlotsTweaks));
        CheckPlugins.LogInfo("Multfunction_mod Compat finish.");
    }
}
