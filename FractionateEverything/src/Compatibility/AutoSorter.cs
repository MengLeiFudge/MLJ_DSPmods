using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility;

public static class AutoSorter {
    public const string GUID = "Appun.DSP.plugin.AutoSorter";
    public static bool Enable;
    public static Assembly assembly;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        // 如果 AutoSorter 启用了，停止其运行并撤销补丁。
        CheckPlugins.LogWarning("AutoSorter detected. Stopping AutoSorter to avoid conflicts.");
        // 撤销 AutoSorter 的所有 Harmony 补丁。
        new Harmony(GUID).UnpatchAll(GUID);
        // 停止 AutoSorter 插件实例的所有协程。
        if (pluginInfo.Instance != null) {
            pluginInfo.Instance.StopAllCoroutines();
            // 如果可能，禁用该插件对应的组件。
            pluginInfo.Instance.enabled = false;
        }
        CheckPlugins.LogInfo("AutoSorter has been stopped and unpatched.");
    }
}
