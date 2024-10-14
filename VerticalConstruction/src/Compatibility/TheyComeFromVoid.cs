using BepInEx.Bootstrap;
using HarmonyLib;
using static VerticalConstruction.Compatibility.CheckPlugins;

namespace VerticalConstruction.Compatibility {
    public class TheyComeFromVoid {
        internal const string GUID = "com.ckcz123.DSP_Battle";

        internal static bool Enable;

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
            if (!Enable) return;

            var harmony = new Harmony(VerticalConstruction.GUID + ".Compatibility.TheyComeFromVoid");
            harmony.PatchAll(typeof(TheyComeFromVoid));
            LogInfo("TheyComeFromVoid Compat finish.");
        }
    }
}
