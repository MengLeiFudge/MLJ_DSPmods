using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public static class CustomCreateBirthStar {
    internal const string GUID = "kumor.plugin.CustomCreateBirthStar";

    internal static bool Enable;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;
        
        CheckPlugins.LogInfo("CustomCreateBirthStar Compat finish.");
    }
}