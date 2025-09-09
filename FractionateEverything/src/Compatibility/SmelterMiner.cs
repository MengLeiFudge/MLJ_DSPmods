using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public static class SmelterMiner {
    public const string GUID = "Gnimaerd.DSP.plugin.SmelterMiner";
    public static bool Enable;
    public static Assembly assembly;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.SmelterMiner");
        harmony.PatchAll(typeof(SmelterMiner));
        CheckPlugins.LogInfo("SmelterMiner Compat finish.");
    }
}
