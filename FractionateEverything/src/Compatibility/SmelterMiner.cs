using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public static class SmelterMiner {
    internal const string GUID = "Gnimaerd.DSP.plugin.SmelterMiner";

    internal static bool Enable;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;
        
        CheckPlugins.LogInfo("SmelterMiner Compat finish.");
    }
}