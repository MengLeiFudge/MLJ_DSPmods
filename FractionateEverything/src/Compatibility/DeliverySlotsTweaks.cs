using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using DeliverySlotsTweaks;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Compatibility;

public static class DeliverySlotsTweaks {
    internal const string GUID = "starfi5h.plugin.DeliverySlotsTweaks";

    internal static bool Enable;
    public static ConfigEntry<bool> ArchitectModeEnabledEntry;
    public static bool ArchitectMode => ArchitectModeEnabledEntry?.Value == true;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable) return;

        try {
            Assembly assembly = pluginInfo.Instance.GetType().Assembly;
            Type classType = AccessTools.TypeByName("DeliverySlotsTweaks.Plugin");
            ArchitectModeEnabledEntry =
                (ConfigEntry<bool>)AccessTools.Field(classType, "EnableArchitectMode").GetValue(null);
        }
        catch (Exception ex) {
            CheckPlugins.LogWarning($"Failed to compat DeliverySlotsTweaks: {ex}");
        }

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.DeliverySlotsTweaks");
        harmony.PatchAll(typeof(DeliverySlotsTweaks));
        CheckPlugins.LogInfo("DeliverySlotsTweaks Compat finish.");
    }

    /// <summary>
    /// 修改物品数目。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DeliveryPackagePatch), nameof(DeliveryPackagePatch.GetItemCount))]
    public static void DeliveryPackagePatch_GetItemCount_Postfix(int itemId, ref int __result) {
        if (!DeliveryPackagePatch.architectMode) {
            __result = (int)Math.Min(int.MaxValue, __result + GetModDataItemCount(itemId));
        }
    }

    // /// <summary>
    // /// 修改物品拿取。
    // /// </summary>
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(DeliveryPackagePatch), nameof(DeliveryPackagePatch.TakeItem))]
    // public static void DeliveryPackagePatch_TakeItem_Postfix(StorageComponent storage,
    //     int itemId, int count, ref int inc, int __result) {
    //     if (__result < count) {
    //         int takeCount = TakeItemFromModData(itemId, count - __result, out int modItemInc);
    //         __result += takeCount;
    //         inc += modItemInc;
    //     }
    // }

    // /// <summary>
    // /// 修改物品拿取。
    // /// </summary>
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(DeliveryPackagePatch), nameof(DeliveryPackagePatch.TakeTailItems))]
    // public static void DeliveryPackagePatch_TakeTailItems_Postfix(StorageComponent storage,
    //     ref  int itemId,ref  int count, ref int inc, int __result) {
    //     if (__result < count) {
    //         int takeCount = TakeItemFromModData(itemId, count - __result, out int modItemInc);
    //         __result += takeCount;
    //         inc += modItemInc;
    //     }
    // }
    //
    //
    // public static void TakeTailItems(
    //     StorageComponent storage,
    //     ref int itemId,
    //     ref int count,
    //     out int inc,
    //     bool _) {
    //     if (DeliveryPackagePatch.architectMode)
    //         inc = 0;
    //     else if (Compatibility.Nebula_Patch.IsActive && Compatibility.Nebula_Patch.IsOthers()) {
    //         inc = 0;
    //     } else {
    //         int gridIndex;
    //         if (DeliveryPackagePatch.deliveryGridindex.TryGetValue(itemId, out gridIndex)) {
    //             GameMain.mainPlayer.packageUtility.TakeItemFromAllPackages(gridIndex, ref itemId, ref count, out inc,
    //                 false);
    //             if (DeliveryPackagePatch.packageItemCount.ContainsKey(itemId)) {
    //                 int num = DeliveryPackagePatch.packageItemCount[itemId] - count;
    //                 DeliveryPackagePatch.packageItemCount[itemId] = num;
    //                 if (num > 0)
    //                     return;
    //                 DeliveryPackagePatch.packageItemCount.Remove(itemId);
    //             } else {
    //                 if (!DeliveryPackagePatch.deliveryItemCount.ContainsKey(itemId))
    //                     return;
    //                 int num = DeliveryPackagePatch.deliveryItemCount[itemId] - count;
    //                 DeliveryPackagePatch.deliveryItemCount[itemId] = num;
    //                 if (num > 0)
    //                     return;
    //                 DeliveryPackagePatch.deliveryItemCount.Remove(itemId);
    //             }
    //         } else {
    //             storage.TakeTailItems(ref itemId, ref count, out inc);
    //             if (!DeliveryPackagePatch.packageItemCount.ContainsKey(itemId))
    //                 return;
    //             int num = DeliveryPackagePatch.packageItemCount[itemId] - count;
    //             DeliveryPackagePatch.packageItemCount[itemId] = num;
    //             if (num > 0)
    //                 return;
    //             DeliveryPackagePatch.packageItemCount.Remove(itemId);
    //         }
    //     }
    // }


    [HarmonyPostfix]
    [HarmonyPatch(typeof(DeliveryPackagePatch), nameof(DeliveryPackagePatch.AddConstructableCountsInStorage))]
    public static void DeliveryPackagePatch_AddConstructableCountsInStorage_Postfix(
        ConstructionModuleComponent constructionModule, Player player, ref int num) {
        AddConstructableCountsInStorage(constructionModule, player, ref num);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DeliveryPackagePatch), nameof(DeliveryPackagePatch.TakeTailItems))]
    public static bool DeliveryPackagePatch_TakeTailItems_Prefix(StorageComponent storage,
        ref int itemId, ref int count, out int inc, bool _) {
        TakeTailItems(storage, ref itemId, ref count, out inc, false);
        return false;
    }
}
