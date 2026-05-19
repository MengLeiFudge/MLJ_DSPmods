using CommonAPI.Systems;
using FE.Compatibility;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.UI.Patches;

/// <summary>
/// 兜底处理外部模组造成的物品 GridIndex 同格冲突，避免原版物品选择器把前一个物品覆盖到不可选。
/// </summary>
public static class ItemPickerDuplicateGridPatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(UIItemPicker), nameof(UIItemPicker.RefreshIcons))]
    private static void UIItemPicker_RefreshIcons_Postfix(UIItemPicker __instance) {
        if (__instance?.protoArray == null || __instance.indexArray == null || GameMain.iconSet == null) {
            return;
        }

        int columnCount = GenesisBook.Enable ? 17 : 14;
        int rowCount = GenesisBook.Enable ? 7 : 8;
        int visibleSlotCount = rowCount * columnCount;
        if (visibleSlotCount > __instance.protoArray.Length) {
            visibleSlotCount = __instance.protoArray.Length;
        }

        int currentPage = __instance.currentType;
        if (currentPage <= 0) {
            return;
        }

        bool[] occupied = BuildOccupiedSlots(__instance, visibleSlotCount);
        if (AllSlotsOccupied(occupied)) {
            return;
        }

        foreach (ItemProto item in LDB.items.dataArray) {
            if (!ShouldShowInPicker(item, currentPage, rowCount, columnCount)) {
                continue;
            }

            int preferredSlot = GetGridSlot(item.GridIndex, columnCount);
            if (preferredSlot < 0 || preferredSlot >= visibleSlotCount) {
                continue;
            }

            if (__instance.protoArray[preferredSlot]?.ID == item.ID || IsAlreadyVisible(__instance, item.ID)) {
                continue;
            }

            int fallbackSlot = FindFirstFreeSlot(occupied);
            if (fallbackSlot < 0) {
                return;
            }

            __instance.indexArray[fallbackSlot] = GameMain.iconSet.itemIconIndex[item.ID];
            __instance.protoArray[fallbackSlot] = item;
            occupied[fallbackSlot] = true;
        }
    }

    private static bool[] BuildOccupiedSlots(UIItemPicker picker, int visibleSlotCount) {
        bool[] occupied = new bool[visibleSlotCount];
        for (int i = 0; i < visibleSlotCount; i++) {
            occupied[i] = picker.protoArray[i] != null;
        }
        return occupied;
    }

    private static bool AllSlotsOccupied(bool[] occupied) {
        for (int i = 0; i < occupied.Length; i++) {
            if (!occupied[i]) {
                return false;
            }
        }
        return true;
    }

    private static bool ShouldShowInPicker(ItemProto item, int currentPage, int rowCount, int columnCount) {
        if (item == null || item.GridIndex < 1101) {
            return false;
        }

        int page = item.GridIndex / 1000;
        if (page != currentPage) {
            return false;
        }

        int row = GetGridRow(item.GridIndex, page);
        int column = GetGridColumn(item.GridIndex);
        if (row < 0 || row >= rowCount || column < 0 || column >= columnCount) {
            return false;
        }

        if (UIItemPickerExtension.currentFilter != null && !UIItemPickerExtension.currentFilter(item)) {
            return false;
        }

        return UIItemPicker.showAll
               || UIItemPickerExtension.showLocked
               || (GameMain.history != null
                   && (GameMain.history.ItemUnlocked(item.ID)
                       || GetItemTotalCount(item.ID) > 0));
    }

    private static int GetGridRow(int gridIndex, int page) {
        return (gridIndex - page * 1000) / 100 - 1;
    }

    private static int GetGridColumn(int gridIndex) {
        return gridIndex % 100 - 1;
    }

    private static int GetGridSlot(int gridIndex, int columnCount) {
        int page = gridIndex / 1000;
        return GetGridRow(gridIndex, page) * columnCount + GetGridColumn(gridIndex);
    }

    private static bool IsAlreadyVisible(UIItemPicker picker, int itemId) {
        for (int i = 0; i < picker.protoArray.Length; i++) {
            if (picker.protoArray[i]?.ID == itemId) {
                return true;
            }
        }
        return false;
    }

    private static int FindFirstFreeSlot(bool[] occupied) {
        for (int i = 0; i < occupied.Length; i++) {
            if (!occupied[i]) {
                return i;
            }
        }
        return -1;
    }
}
