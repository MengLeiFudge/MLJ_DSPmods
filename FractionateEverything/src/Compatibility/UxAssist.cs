using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public class UxAssist {
    internal const string GUID = "org.soardev.uxassist";

    internal static bool Enable;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.UxAssist");
        harmony.PatchAll(typeof(UxAssist));
        CheckPlugins.LogInfo("UxAssist Compat finish.");
    }
}
