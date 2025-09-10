using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public static class BuildToolOpt {
    public const string GUID = "starfi5h.plugin.BuildToolOpt";
    public static bool Enable;
    public static Assembly assembly;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.BuildToolOpt");
        harmony.PatchAll(typeof(BuildToolOpt));
        CheckPlugins.LogInfo("BuildToolOpt Compat finish.");
    }
}
