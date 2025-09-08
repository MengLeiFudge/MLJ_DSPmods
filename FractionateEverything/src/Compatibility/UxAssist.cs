using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public class UxAssist {
    public const string GUID = "org.soardev.uxassist";
    public static bool Enable;
    public static Assembly assembly;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.UxAssist");
        harmony.PatchAll(typeof(UxAssist));
        CheckPlugins.LogInfo("UxAssist Compat finish.");
    }
}
