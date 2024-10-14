using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using xiaoye97;
using static VerticalConstruction.Compatibility.CheckPlugins;

namespace VerticalConstruction.Compatibility {
    public static class GenesisBook {
        internal const string GUID = "org.LoShin.GenesisBook";

        internal static bool Enable;
        internal static int tab精炼;
        internal static int tab化工;
        internal static int tab防御;
        private static bool _finished;

        #region 创世ERecipeType拓展

        internal const ERecipeType 基础制造 = ERecipeType.Assemble;
        internal const ERecipeType 标准制造 = (ERecipeType)9;
        internal const ERecipeType 高精度加工 = (ERecipeType)10;

        #endregion

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
            if (!Enable) return;

            tab精炼 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab1");
            tab化工 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab2");
            tab防御 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab3");

            var harmony = new Harmony(VerticalConstruction.GUID + ".Compatibility.GenesisBook");
            harmony.PatchAll(typeof(GenesisBook));
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(GenesisBook), nameof(AfterLDBToolPostAddData)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            LogInfo("GenesisBook Compat finish.");
        }

        public static void AfterLDBToolPostAddData() {
            if (_finished) return;

            _finished = true;
            LogInfo("GenesisBook Compatibility LDBToolOnPostAddDataAction finish.");
        }
    }
}
