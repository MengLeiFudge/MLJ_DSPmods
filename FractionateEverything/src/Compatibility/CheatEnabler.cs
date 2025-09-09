using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using xiaoye97;

namespace FE.Compatibility;

public static class CheatEnabler {
    public const string GUID = "org.soardev.cheatenabler";
    public static bool Enable;
    public static Assembly assembly;

    public static ConfigEntry<bool> ArchitectModeEnabledEntry;
    public static bool ArchitectMode => ArchitectModeEnabledEntry?.Value == true;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.CheatEnabler");
        harmony.PatchAll(typeof(CheatEnabler));
        CheckPlugins.LogInfo("CheatEnabler Compat finish.");
    }

    private static bool _finished = false;

    [HarmonyPostfix]
    [HarmonyAfter(LDBToolPlugin.MODGUID)]
    [HarmonyPatch(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded))]
    private static void AfterLDBToolPostAddData() {
        if (_finished) return;
        try {
            Type classType = assembly.GetType("CheatEnabler.Patches.FactoryPatch");
            ArchitectModeEnabledEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "ArchitectModeEnabled").GetValue(null);
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"Failed to compat CheatEnabler: {ex}");
        }
        _finished = true;
    }
}
