using FE.Logic.Items;
using HarmonyLib;

namespace FE.Logic.DataCenter.Patches;

/// <summary>
/// 玩家丢弃物品自动回收到数据中心的补丁。
/// </summary>
public static class TrashRecyclePatch {
    /// <summary>
    /// 将可接收部分回收到亚空间，容量不足的余量继续走原版垃圾路径。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowTrash))]
    private static bool Player_ThrowTrash_Prefix(Player __instance, int itemId, ref int count, ref int inc) {
        if (!PackageAccessRules.TechItemInteractionUnlocked) {
            return true;
        }
        if (!ItemManager.CanAutoStoreItem(itemId)) {
            return true;
        }
        int originalCount = count;
        long accepted = DataCenterUploadRouter.Upload(itemId, originalCount, inc, true);
        int acceptedCount = (int)System.Math.Min(originalCount, accepted);
        if (acceptedCount <= 0) {
            return true;
        }
        DataCenterInventory.RecordManualUpload();
        int acceptedInc = originalCount <= 0
            ? 0
            : (int)System.Math.Min(int.MaxValue, (long)System.Math.Max(0, inc) * acceptedCount / originalCount);
        count -= acceptedCount;
        inc = System.Math.Max(0, inc - acceptedInc);
        return count > 0;
    }

    /// <summary>
    /// 将手持物中可接收部分回收到亚空间，余量仍由原版逻辑丢到地面。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowHandItems))]
    private static bool Player_ThrowHandItems_Prefix(Player __instance) {
        if (!PackageAccessRules.TechItemInteractionUnlocked) {
            return true;
        }
        int itemId = __instance.inhandItemId;
        if (!ItemManager.CanAutoStoreItem(itemId)) {
            return true;
        }
        int originalCount = __instance.inhandItemCount;
        int originalInc = __instance.inhandItemInc;
        if (originalCount <= 0) {
            return true;
        }
        long accepted = DataCenterUploadRouter.Upload(itemId, originalCount, originalInc, true);
        int acceptedCount = (int)System.Math.Min(originalCount, accepted);
        if (acceptedCount <= 0) {
            return true;
        }
        DataCenterInventory.RecordManualUpload();
        int acceptedInc = (int)System.Math.Min(int.MaxValue,
            (long)System.Math.Max(0, originalInc) * acceptedCount / originalCount);
        __instance.inhandItemCount = originalCount - acceptedCount;
        __instance.inhandItemInc = System.Math.Max(0, originalInc - acceptedInc);
        if (__instance.inhandItemCount > 0) {
            return true;
        }
        __instance.inhandItemId = 0;
        __instance.inhandItemInc = 0;
        return false;
    }
}
