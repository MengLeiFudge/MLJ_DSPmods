using FE.Compatibility.Mods;
using HarmonyLib;

namespace FE.Logic.Items.Presentation;

/// <summary>
/// 兜底处理图标信号选择器里的物品 GridIndex 同格冲突，避免蓝图本体图标无法选中被覆盖物品。
/// </summary>
public static class SignalPickerDuplicateGridPatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.RefreshIcons))]
    private static void UISignalPicker_RefreshIcons_Postfix(UISignalPicker __instance) {
        if (__instance?.signalArray == null || __instance.indexArray == null || GameMain.iconSet == null) {
            return;
        }

        int currentPage = GetCurrentItemPage(__instance.currentType);
        if (currentPage <= 0) {
            return;
        }

        int columnCount = GetColumnCount();
        int rowCount = GetRowCount();
        int visibleSlotCount = rowCount * columnCount;
        if (visibleSlotCount > __instance.signalArray.Length) {
            visibleSlotCount = __instance.signalArray.Length;
        }
        if (visibleSlotCount > __instance.indexArray.Length) {
            visibleSlotCount = __instance.indexArray.Length;
        }

        bool[] occupied = BuildOccupiedSlots(__instance, visibleSlotCount);
        if (AllSlotsOccupied(occupied)) {
            return;
        }

        foreach (ItemProto item in LDB.items.dataArray) {
            if (!ShouldShowInSignalPicker(item, currentPage, rowCount, columnCount)) {
                continue;
            }

            int preferredSlot = GetGridSlot(item.GridIndex, columnCount);
            if (preferredSlot < 0 || preferredSlot >= visibleSlotCount) {
                continue;
            }

            int signalId = SignalProtoSet.SignalId(ESignalType.Item, item.ID);
            if (__instance.signalArray[preferredSlot] == signalId || IsAlreadyVisible(__instance, signalId)) {
                continue;
            }

            int fallbackSlot = FindFirstFreeSlot(occupied);
            if (fallbackSlot < 0) {
                return;
            }

            __instance.indexArray[fallbackSlot] = GameMain.iconSet.signalIconIndex[signalId];
            __instance.signalArray[fallbackSlot] = signalId;
            occupied[fallbackSlot] = true;
        }
    }

    private static int GetColumnCount() {
        return GenesisBook.Enable ? 17 : 14;
    }

    private static int GetRowCount() {
        return GenesisBook.Enable ? 7 : 10;
    }

    private static int GetCurrentItemPage(int currentType) {
        if (currentType == 2) {
            return 1;
        }
        if (currentType == 3) {
            return 2;
        }
        if (GenesisBook.Enable && currentType > 8) {
            return currentType - 6;
        }
        if (OrbitalRing.Enable && currentType > 7) {
            return currentType - 5;
        }
        if (currentType > 8) {
            return currentType - 6;
        }
        return 0;
    }

    private static bool[] BuildOccupiedSlots(UISignalPicker picker, int visibleSlotCount) {
        bool[] occupied = new bool[visibleSlotCount];
        for (int i = 0; i < visibleSlotCount; i++) {
            occupied[i] = picker.signalArray[i] != 0;
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

    private static bool ShouldShowInSignalPicker(ItemProto item, int currentPage, int rowCount, int columnCount) {
        if (item == null || item.GridIndex < 1101) {
            return false;
        }

        int page = item.GridIndex / 1000;
        if (page != currentPage) {
            return false;
        }

        int row = GetGridRow(item.GridIndex, page);
        int column = GetGridColumn(item.GridIndex);
        return row >= 0 && row < rowCount && column >= 0 && column < columnCount;
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

    private static bool IsAlreadyVisible(UISignalPicker picker, int signalId) {
        for (int i = 0; i < picker.signalArray.Length; i++) {
            if (picker.signalArray[i] == signalId) {
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
