using BepInEx.Bootstrap;
using HarmonyLib;
using static FractionateEverything.Compatibility.CheckPlugins;

namespace FractionateEverything.Compatibility {
    public static class BluePrintTweaks {
        internal const string GUID = "org.kremnev8.plugin.BlueprintTweaks";

        internal static bool Enable;

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
            if (!Enable) return;

            var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.BlueprintTweaks");
            harmony.PatchAll(typeof(BluePrintTweaks));
            LogInfo("BluePrintTweaks Compat finish.");
        }
    }
}
