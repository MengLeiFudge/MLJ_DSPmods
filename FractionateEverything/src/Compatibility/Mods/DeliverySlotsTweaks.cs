using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using xiaoye97;

namespace FE.Compatibility.Mods;

/// <summary>
/// DeliverySlotsTweaks 模组检测、建筑师模式与背包整理兼容入口。
/// </summary>
public static class DeliverySlotsTweaks {
    public const string GUID = "starfi5h.plugin.DeliverySlotsTweaks";
    public static bool Enable;
    public static Assembly assembly;

    public static ConfigEntry<bool> ArchitectModeEnabledEntry;
    public static bool ArchitectMode => ArchitectModeEnabledEntry?.Value == true;
    public static ConfigEntry<bool> UseLogisticSlotsEntry;
    public static bool UseLogisticSlots => UseLogisticSlotsEntry?.Value == true;

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.DeliverySlotsTweaks");
        harmony.PatchAll(typeof(DeliverySlotsTweaks));
        CheckPlugins.LogInfo("DeliverySlotsTweaks Compat finish.");
    }

    private static bool _finished = false;

    /// <summary>读取兼容设置，并在外部模组完成初始化后接管整理时的物品转移。</summary>
    [HarmonyPostfix]
    [HarmonyAfter(LDBToolPlugin.MODGUID)]
    [HarmonyPatch(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded))]
    private static void AfterLDBToolPostAddData() {
        if (_finished) return;
        try {
            Type classType = assembly.GetType("DeliverySlotsTweaks.Plugin");
            ArchitectModeEnabledEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "EnableArchitectMode").GetValue(null);
            UseLogisticSlotsEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "UseLogisticSlots").GetValue(null);

            MethodInfo sortPrefix = AccessTools.Method(
                assembly.GetType("DeliverySlotsTweaks.DeliveryPackagePatch"), "Sort_Prefix");
            if (sortPrefix == null) {
                CheckPlugins.LogWarning("无法接管 DeliverySlotsTweaks 的整理转移：未找到 Sort_Prefix 补丁。");
            } else {
                MethodInfo sort = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.Sort));
                new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.DeliverySlotsTweaks").Unpatch(sort, sortPrefix);
                CheckPlugins.LogInfo("DeliverySlotsTweaks 的整理转移已由 FE 接管。");
            }
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"Failed to compat DeliverySlotsTweaks: {ex}");
        }
        _finished = true;
    }
}
