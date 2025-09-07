using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public static class CheatEnabler {
    internal const string GUID = "org.soardev.cheatenabler";

    internal static bool Enable;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.CheatEnabler");
        harmony.PatchAll(typeof(CheatEnabler));
        CheckPlugins.LogInfo("CheatEnabler Compat finish.");
    }
}
