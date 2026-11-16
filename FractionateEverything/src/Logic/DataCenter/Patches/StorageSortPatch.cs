using System;

using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using FE.Logic.Items;

namespace FE.Logic.DataCenter.Patches;

/// <summary>
/// 在原版整理后补充物流栏，并只对玩家手动双击执行最后的上传。
/// </summary>
public static class StorageSortPatch {
    private static DateTime lastManualSortTime = DateTime.MinValue;
    private static Button lastManualSortButton;
    private static int lastManualSortFrame = -1;

    /// <summary>
    /// 原版已完成锁定格回填；只从非锁定格向物流栏转移物品。
    /// </summary>
    /// <param name="__instance">刚完成整理的仓储。</param>
    /// <param name="raiseNotify">是否按本次整理请求通知库存变化。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StorageComponent), nameof(StorageComponent.Sort))]
    private static void StorageComponent_Sort_Postfix(StorageComponent __instance, bool raiseNotify) {
        Player player = GameMain.mainPlayer;
        if (DSPGame.IsMenuDemo || player == null || __instance != player.package
            || !player.deliveryPackage.unlocked) {
            return;
        }

        bool changed = false;
        // 从末尾取料，尽量保留原版整理后的前部堆叠。
        for (int index = __instance.size - 1; index >= 0; index--) {
            ref StorageComponent.GRID grid = ref __instance.grids[index];
            if (grid.filter != 0 || grid.itemId <= 0 || grid.count <= 0) {
                continue;
            }
            int count = player.deliveryPackage.AddItem(grid.itemId, grid.count, grid.inc, out int remainInc);
            if (count <= 0) {
                continue;
            }
            grid.count -= count;
            grid.inc = remainInc;
            if (grid.count == 0) {
                grid.itemId = 0;
                grid.stackSize = 0;
            }
            changed = true;
        }
        if (changed) {
            __instance.ResetOptimizationFlags();
            if (raiseNotify) {
                __instance.NotifyStorageChange();
            }
        }
    }

    /// <summary>
    /// 从绑定玩家背包的真实鼠标点击识别双击，在整理及物流补充完成后上传余量。
    /// </summary>
    /// <param name="__instance">收到指针点击的原版按钮。</param>
    /// <param name="eventData">本次鼠标点击数据。</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Button), nameof(Button.OnPointerClick))]
    private static void Button_OnPointerClick_Postfix(Button __instance, PointerEventData eventData) {
        Player player = GameMain.mainPlayer;
        // 原按钮点击回调可能销毁自身，须先按 Unity 对象语义判空再读取组件状态。
        if (__instance == null || DSPGame.IsMenuDemo || player == null || eventData == null
            || eventData.button != PointerEventData.InputButton.Left || !Input.GetMouseButtonUp(0)
            || !__instance.IsActive() || !__instance.IsInteractable()
            || lastManualSortFrame == Time.frameCount) {
            return;
        }

        bool isPlayerSortButton = false;
        for (int index = 0; index < __instance.onClick.GetPersistentEventCount(); index++) {
            if (__instance.onClick.GetPersistentListenerState(index) != UnityEventCallState.Off
                && __instance.onClick.GetPersistentMethodName(index) == nameof(UIStorageGrid.OnSort)
                && __instance.onClick.GetPersistentTarget(index) is UIStorageGrid grid
                && grid != null && grid.storage != null && grid.storage == player.package
                && (grid.storage.type == EStorageType.Default || grid.sortableFilter)) {
                isPlayerSortButton = true;
                break;
            }
        }
        if (!isPlayerSortButton) {
            return;
        }

        // OnSort 也可由自动整理模组直接调用，只有本次真实点击能更新双击状态。
        DateTime now = DateTime.Now;
        bool isDoubleClick = __instance == lastManualSortButton
                             && (now - lastManualSortTime).TotalMilliseconds < 400;
        lastManualSortTime = now;
        lastManualSortButton = __instance;
        lastManualSortFrame = Time.frameCount;
        if (!isDoubleClick || !PackageAccessRules.TechItemInteractionUnlocked) {
            return;
        }

        StorageComponent package = player.package;
        bool changed = false;
        for (int index = 0; index < package.size; index++) {
            ref StorageComponent.GRID grid = ref package.grids[index];
            if (grid.filter != 0 || grid.count <= 0 || !ItemManager.CanAutoStoreItem(grid.itemId)) {
                continue;
            }
            int originalCount = grid.count;
            int originalInc = grid.inc;
            long accepted = DataCenterUploadRouter.Upload(grid.itemId, originalCount, originalInc, true);
            if (accepted <= 0) {
                continue;
            }
            int acceptedCount = (int)Math.Min(originalCount, accepted);
            int acceptedInc = (int)((long)originalInc * acceptedCount / originalCount);
            grid.count -= acceptedCount;
            grid.inc -= acceptedInc;
            if (grid.count == 0) {
                grid.itemId = 0;
                grid.stackSize = 0;
                grid.inc = 0;
            }
            changed = true;
        }
        if (changed) {
            package.ResetOptimizationFlags();
            package.NotifyStorageChange();
        }
    }
}
