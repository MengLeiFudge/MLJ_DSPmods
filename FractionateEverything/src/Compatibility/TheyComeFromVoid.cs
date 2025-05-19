using BepInEx.Bootstrap;
using HarmonyLib;
using static FractionateEverything.Compatibility.CheckPlugins;

namespace FractionateEverything.Compatibility;

public class TheyComeFromVoid {
    internal const string GUID = "com.ckcz123.DSP_Battle";

    internal static bool Enable;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;

        var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.TheyComeFromVoid");
        harmony.PatchAll(typeof(TheyComeFromVoid));
        LogInfo("TheyComeFromVoid Compat finish.");
    }
}
