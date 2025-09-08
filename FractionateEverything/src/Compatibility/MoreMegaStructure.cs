using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using xiaoye97;

namespace FE.Compatibility;

public static class MoreMegaStructure {
    internal const string GUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";

    internal static bool Enable;
    internal static int tab巨构;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) return;

        tab巨构 = TabSystem.GetTabId("MegaStructures:MegaStructuresTab");

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.MoreMegaStructure");
        harmony.PatchAll(typeof(MoreMegaStructure));
        CheckPlugins.LogInfo("MoreMegaStructure Compat finish.");
    }
}
