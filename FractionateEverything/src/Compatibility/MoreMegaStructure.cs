using System.Reflection;
using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;

namespace FE.Compatibility;

public static class MoreMegaStructure {
    public const string GUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool Enable;
    public static Assembly assembly;

    public static int tab巨构;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        tab巨构 = TabSystem.GetTabId("MegaStructures:MegaStructuresTab");
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.MoreMegaStructure");
        harmony.PatchAll(typeof(MoreMegaStructure));
        CheckPlugins.LogInfo("MoreMegaStructure Compat finish.");
    }
}
