using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public class TheyComeFromVoid {
    internal const string GUID = "com.ckcz123.DSP_Battle";

    internal static bool Enable;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.TheyComeFromVoid");
        harmony.PatchAll(typeof(TheyComeFromVoid));
        CheckPlugins.LogInfo("TheyComeFromVoid Compat finish.");
    }
}
