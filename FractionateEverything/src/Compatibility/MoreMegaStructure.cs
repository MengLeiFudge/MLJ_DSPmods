using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.Compatibility.CheckPlugins;

namespace FractionateEverything.Compatibility {
    public static class MoreMegaStructure {
        internal const string GUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";

        internal static bool Enable;
        internal static int tab巨构;
        private static Sprite alienmatrix;
        private static Sprite alienmatrixGray;
        private static bool _finished;

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);

            if (!Enable) return;

            tab巨构 = TabSystem.GetTabId("MegaStructures:MegaStructuresTab");

            var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.MoreMegaStructure");
            harmony.PatchAll(typeof(MoreMegaStructure));
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(MoreMegaStructure), nameof(AfterLDBToolPostAddData)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            LogInfo("MoreMegaStructure Compatibility Compatible finish.");
        }

        public static void AfterLDBToolPostAddData() {
            if (_finished) return;

            //水滴物品、配方迁移到巨构tab112
            ItemProto item = LDB.items.Select(IMS水滴);
            item.GridIndex = tab巨构 * 1000 + 112;
            item.maincraft.GridIndex = item.GridIndex;

            _finished = true;
            LogInfo("MoreMegaStructure Compatibility LDBToolOnPostAddDataAction finish.");
        }
    }
}
