using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public class Multfunction_mod {
    internal const string GUID = "starfi5h.plugin.Multfunction_mod";

    internal static bool Enable;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.Multfunction_mod");
        harmony.PatchAll(typeof(DeliverySlotsTweaks));
        CheckPlugins.LogInfo("Multfunction_mod Compat finish.");
    }
}
