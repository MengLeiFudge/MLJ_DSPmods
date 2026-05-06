using System;
using FE.UI.MainPanel.Setting;
using HarmonyLib;

namespace FE.Logic.DataCenter.Patches;
/// <summary>
/// 背包排序后恢复数据中心物品缓存的补丁。
/// </summary>
public static class StorageSortPatch {
    #region 背包排序

    private static DateTime lastSortTime = DateTime.MinValue;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStorageGrid), nameof(UIStorageGrid.OnSort))]
    private static bool PrefixUIStorageGridOnSort(UIStorageGrid __instance) {
        if (Miscellaneous.EnablePackageAutoSortTwice) {
            return true;
        }
        if (__instance.storage.type != EStorageType.Default && !__instance.sortableFilter) {
            return false;
        }
        if (__instance.storage != GameMain.mainPlayer?.package) {
            return true;
        }
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return false;
        }
        return sortUpload();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StorageComponent), nameof(StorageComponent.Sort))]
    private static bool StorageComponent_Sort_Prefix(StorageComponent __instance) {
        if (!Miscellaneous.EnablePackageAutoSortTwice) {
            return true;
        }
        if (__instance != GameMain.mainPlayer?.package) {
            return true;
        }
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return true;
        }
        return !Miscellaneous.EnablePackageAutoSortTwice || sortUpload();
    }

    private static bool sortUpload() {
        bool isDoubleClick = (DateTime.Now - lastSortTime).TotalMilliseconds < 400 && PackageAccessRules.TechItemInteractionUnlocked;
        lastSortTime = DateTime.Now;
        if (!isDoubleClick) {
            //一次排序
            if (!GameMain.mainPlayer.deliveryPackage.unlocked) {
                return true;
            }
            StorageComponent package = GameMain.mainPlayer.package;
            DeliveryPackage deliveryPackage = GameMain.mainPlayer.deliveryPackage;
            for (int gridIndex = 99; gridIndex >= 0; gridIndex--) {
                int itemId = deliveryPackage.grids[gridIndex].itemId;
                for (int index = 0; index < package.size; index++) {
                    if (package.grids[index].itemId != itemId) {
                        continue;
                    }

                    int count = deliveryPackage.AddItem(itemId,
                        package.grids[index].count, package.grids[index].inc, out int remainInc);
                    package.grids[index].count -= count;
                    package.grids[index].inc = remainInc;
                }
            }
        } else {
            //二次排序
            if (!PackageAccessRules.TechItemInteractionUnlocked || !Miscellaneous.EnablePackageSortTwice) {
                return true;
            }
            StorageComponent package = GameMain.mainPlayer.package;
            for (int index = 0; index < package.size; index++) {
                // 忽略过滤格
                if (package.grids[index].filter != 0) {
                    continue;
                }
                int itemId = package.grids[index].itemId;
                if (itemId <= 0 || itemId >= 12000 || FE.Logic.Items.ItemManager.itemValue[itemId] >= FE.Logic.Items.ItemManager.maxValue) {
                    continue;
                }
                DataCenterInventory.AddItemToModData(itemId, package.grids[index].count, package.grids[index].inc,
                    true);
                package.grids[index].itemId = 0;
                package.grids[index].count = 0;
                package.grids[index].inc = 0;
            }
        }
        return true;
    }

    #endregion
}
