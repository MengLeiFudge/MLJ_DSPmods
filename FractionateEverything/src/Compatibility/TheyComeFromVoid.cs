using BepInEx.Bootstrap;
using HarmonyLib;
using xiaoye97;
using static FractionateEverything.Compatibility.CheckPlugins;

namespace FractionateEverything.Compatibility {
    public class TheyComeFromVoid {
        internal const string GUID = "com.ckcz123.DSP_Battle";

        internal static bool Enable;
        private static bool _finished;

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);

            if (!Enable) return;

            var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.TheyComeFromVoid");
            harmony.PatchAll(typeof(TheyComeFromVoid));
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(TheyComeFromVoid), nameof(AfterLDBToolPostAddData)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            LogInfo("TheyComeFromVoid Compatibility Compatible finish.");
        }

        public static void AfterLDBToolPostAddData() {
            if (_finished) return;

            //xxx

            _finished = true;
            LogInfo("TheyComeFromVoid Compatibility LDBToolOnPostAddDataAction finish.");
        }
    }
}
