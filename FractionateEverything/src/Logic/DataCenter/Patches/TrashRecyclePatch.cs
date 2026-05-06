using FE.Logic.DataCenter;
using HarmonyLib;

namespace FE.Logic.DataCenter.Patches;

public static class TrashRecyclePatch {
    /// <summary>
    /// 扔掉的垃圾会自动回收到Mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowTrash))]
    private static bool Player_ThrowTrash_Prefix(Player __instance, int itemId, int count, int inc) {
        if (!PackageAccessRules.TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId <= 0 || itemId >= 12000 || FE.Logic.Items.ItemManager.itemValue[itemId] >= FE.Logic.Items.ItemManager.maxValue) {
            return true;
        }
        DataCenterInventory.ManualUploadCount++;
        DataCenterInventory.AddItemToModData(itemId, count, inc, true);
        return false;
    }

    /// <summary>
    /// 扔掉的垃圾会自动回收到Mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowHandItems))]
    private static bool Player_ThrowHandItems_Prefix(Player __instance) {
        if (!PackageAccessRules.TechItemInteractionUnlocked) {
            return true;
        }
        int itemId = __instance.inhandItemId;
        if (itemId <= 0 || itemId >= 12000 || FE.Logic.Items.ItemManager.itemValue[itemId] >= FE.Logic.Items.ItemManager.maxValue) {
            return true;
        }
        if (__instance.inhandItemId > 0 && __instance.inhandItemCount > 0) {
            DataCenterInventory.ManualUploadCount++;
            DataCenterInventory.AddItemToModData(__instance.inhandItemId, __instance.inhandItemCount,
                __instance.inhandItemInc, true);
        }
        __instance.inhandItemId = 0;
        __instance.inhandItemCount = 0;
        __instance.inhandItemInc = 0;
        return false;
    }

}
