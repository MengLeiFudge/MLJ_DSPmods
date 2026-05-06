using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using FE.Logic.Building;
using FE.UI.View.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class StationManager {
    /// <summary>拦截总控面板 SD 按钮点击以清理 FE 弹窗状态</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnLocalSdButtonClick))]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnRemoteSdButtonClick))]
    public static void UIControlPanelStationStorage_OnSdButtonClick_Prefix(UIControlPanelStationStorage __instance) {
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(__instance);
        ClearPopupState(popupStateKey);
        SetControlPanelStoragePopupShift(__instance, true);
    }

    /// <summary>处理总控面板选项按钮 0 的点击逻辑</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton0Click))]
    public static bool
        UIControlPanelStationStorage_OnOptionButton0Click_Prefix(UIControlPanelStationStorage __instance) {
        return HandleOptionClick(__instance, 0);
    }

    /// <summary>处理总控面板选项按钮 1 的点击逻辑</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton1Click))]
    public static bool
        UIControlPanelStationStorage_OnOptionButton1Click_Prefix(UIControlPanelStationStorage __instance) {
        return HandleOptionClick(__instance, 1);
    }

    /// <summary>在总控面板显示传输模式弹窗并同步 FE 弹窗状态</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    private static void ShowTransferPopup(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        StationComponent station = storage.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }

        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 = station != null && storage.factory != null
            ? LDB.items.Select(storage.factory.entityPool[storage.station.entityId].protoId)
            : null;
        if (itemProto1 == null || itemProto2 == null) {
            return;
        }

        // 确认弹窗存在，否则无法展示
        if (storage.popupBoxRect == null) {
            return;
        }

        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
        ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = transferMode switch {
            ETransferMode.Sync => storage.masterInspector.storageDemandColor,
            ETransferMode.Upload => storage.masterInspector.storageSupplyColor,
            _ => storage.masterInspector.storageNoneSpColor
        };
        storage.optionImage1.color = transferMode switch {
            ETransferMode.Sync => storage.masterInspector.storageSupplyColor,
            ETransferMode.Upload => storage.masterInspector.storageNoneSpColor,
            _ => storage.masterInspector.storageDemandColor
        };
        storage.optionText0.text = transferMode switch {
            ETransferMode.Sync => "仅上传".Translate(),
            ETransferMode.Upload => "仅下载".Translate(),
            _ => "双向同步".Translate()
        };
        storage.optionText1.text = transferMode switch {
            ETransferMode.Sync => "仅下载".Translate(),
            ETransferMode.Upload => "双向同步".Translate(),
            _ => "仅上传".Translate()
        };
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // 记录弹窗归属与类型，确保 option 点击时只拦截 FE 的弹窗。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = true;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 总控面板：点击 mod 按钮时，弹窗回归原位（不再左移）。
        SetControlPanelStoragePopupShift(storage, false);
    }

    /// <summary>在总控面板显示容量模式弹窗并记录弹窗类型</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    private static void ShowCapacityPopup(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        StationComponent station = storage.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }

        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 = station != null && storage.factory != null
            ? LDB.items.Select(storage.factory.entityPool[storage.station.entityId].protoId)
            : null;
        if (itemProto1 == null || itemProto2 == null) {
            return;
        }

        // 确保弹窗存在，后续才能设置选项与记录状态
        if (storage.popupBoxRect == null) {
            return;
        }

        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = storage.masterInspector.storageDemandColor;
        storage.optionImage1.color = storage.masterInspector.storageSupplyColor;
        storage.optionText0.text = "无限上传".Translate();
        storage.optionText1.text = "有限上传".Translate();
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // false 代表当前是“容量模式弹窗”。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = false;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 总控面板：点击 mod 按钮时，弹窗回归原位。
        SetControlPanelStoragePopupShift(storage, false);
    }

    /// <summary>基于 FE 弹窗逻辑处理总控面板选项点击并更新模式</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    /// <param name="idx">选项索引（0/1）</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    private static bool HandleOptionClick(UIControlPanelStationStorage __instance, int idx) {
        try {
            UIControlPanelStationStorage storage = __instance;
            (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
            // 只有 FE 打开的弹窗才拦截；否则走原版逻辑。
            if (!GetPopupFlag(slotIsMyPopup, popupStateKey)) {
                return true;
            }

            StationComponent station = __instance.station;
            if (storage.popupBoxRect == null
                || !slotPopupBoxRect.TryGetValue(popupStateKey, out RectTransform popupRect)
                || popupRect != storage.popupBoxRect
                || !storage.popupBoxRect.gameObject.activeSelf) {
                // 弹窗引用不一致时，不接管，避免误拦截。
                return true;
            }

            StationStore stationStore = new StationStore();
            if (station != null && storage.index < station.storage.Length) {
                stationStore = station.storage[storage.index];
            }
            // 复制槽位数据用于安全检查

            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = station != null && storage.factory != null
                ? LDB.items.Select(storage.factory.entityPool[station.entityId].protoId)
                : null;
            if (itemProto1 == null || itemProto2 == null) {
                return false;
            }
            // 有效性检查过后开始应用对应模式逻辑

            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                // 传输模式切换。
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                ETransferMode nextMode = GetNextTransferMode(transferMode, idx);
                transferDictionary.TryUpdate(storage.index, nextMode, transferMode);
            } else {
                // 容量模式切换。
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
                ECapacityMode capacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
                if (idx == 0) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Infinite, capacityMode);
                } else if (idx == 1) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Limited, capacityMode);
                }
            }

            // 与独立面板保持一致：处理完立即收起弹窗。
            storage.popupBoxRect.gameObject.SetActive(false);
            slotIsMyPopup[popupStateKey] = false;
            return false;
        }
        catch (Exception ex) {
            LogError($"FE HandleOptionClick(UIControlPanelStationStorage) error: {ex}");
            return true;
        }
    }
}
