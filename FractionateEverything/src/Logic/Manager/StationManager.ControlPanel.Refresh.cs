using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using FE.Logic.Building;
using FE.UI.MainPanel.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class StationManager {
    /// <summary>刷新总控面板存储槽的 UI 状态并同步自定义按钮</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.RefreshValues))]
    public static void UIControlPanelStationStorage_RefreshValues_Postfix(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        // 使用本地变量方便复用监听逻辑
        if (storage?.station == null || storage.factory == null || storage.masterInspector == null) {
            return;
        }

        int buildingID = storage.factory.entityPool[storage.station.entityId].protoId;
        bool isModStation = IsInteractionStation(buildingID);
        bool isInfoTab = storage.masterInspector.currentTabPanel == EUIControlPanelStationPanel.Info;
        if (!isModStation || !isInfoTab) {
            RestoreControlPanelStorageSlot(storage);
            return;
        }

        RectTransform localRect = storage.localSdButton?.GetComponent<RectTransform>();
        // 原版 SD 按钮需要整体左移，为自定义按钮预留空间
        RectTransform remoteRect = storage.remoteSdButton?.GetComponent<RectTransform>();
        if (localRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(localRect)) {
                controlPanelSdButtonOriginalPosition[localRect] = localRect.anchoredPosition.x;
            }

            float localOriginal = controlPanelSdButtonOriginalPosition[localRect];
            localRect.anchoredPosition = new Vector2(localOriginal - spacingX, localRect.anchoredPosition.y);
        }

        // 远程按钮也需要移动以保持整体对齐
        if (remoteRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(remoteRect)) {
                controlPanelSdButtonOriginalPosition[remoteRect] = remoteRect.anchoredPosition.x;
            }

            float remoteOriginal = controlPanelSdButtonOriginalPosition[remoteRect];
            remoteRect.anchoredPosition = new Vector2(remoteOriginal - spacingX, remoteRect.anchoredPosition.y);
        }

        // 保证沙盒锁按钮也向左移动，与新增按钮保持一致
        RectTransform keepModeRect = storage.keepModeButton?.GetComponent<RectTransform>();
        if (keepModeRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(keepModeRect)) {
                controlPanelSdButtonOriginalPosition[keepModeRect] = keepModeRect.anchoredPosition.x;
            }

            float keepModeOriginal = controlPanelSdButtonOriginalPosition[keepModeRect];
            keepModeRect.anchoredPosition = new Vector2(keepModeOriginal - spacingX, keepModeRect.anchoredPosition.y);
        }

        var localImgRT = storage.localSdImage?.rectTransform;
        var remoteImgRT = storage.remoteSdImage?.rectTransform;
        // 按钮垂直位置需要基于塔的大小差异调整，避免遮挡原按钮
        float topY;
        float bottomY;
        if (storage.station.isStellar && localImgRT != null && remoteImgRT != null) {
            topY = localImgRT.anchoredPosition.y;
            bottomY = remoteImgRT.anchoredPosition.y;
        } else if (localImgRT != null) {
            float centerY = localImgRT.anchoredPosition.y;
            float halfGap = (localImgRT.sizeDelta.y - BtnHeight) / 2f + 2f;
            topY = centerY + halfGap;
            bottomY = centerY - halfGap;
        } else {
            topY = BtnYOffset;
            bottomY = -BtnYOffset;
        }

        if (localRect == null) {
            return;
        }

        // 确保缓存中有自定义的传输按钮引用
        if (!controlPanelTransferGameObjects.TryGetValue(storage, out _)) {
            Transform transferTrans =
                FindControlPanelModeButtonTransform(storage, "FE_cp_transferModeButton_" + storage.index);
            if (transferTrans != null) {
                controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
            }
        }

        // 同时保证容量按钮也已准备好
        if (!controlPanelCapacityGameObjects.TryGetValue(storage, out _)) {
            Transform capacityTrans =
                FindControlPanelModeButtonTransform(storage, "FE_cp_capacityModeButton_" + storage.index);
            if (capacityTrans != null) {
                controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
            }
        }

        // 获取或创建当前站点的传输模式配置
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ETransferMode>());
        ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        if (controlPanelTransferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
            // 同步按钮文本与配色，体现当前传输模式
            transferGO.SetActive(storage.localSdButton.gameObject.activeSelf);
            Text transferText = transferGO.GetComponentInChildren<Text>();
            if (transferText != null) {
                transferText.text = transferMode switch {
                    ETransferMode.Sync => "双向同步".Translate(),
                    ETransferMode.Upload => "仅上传".Translate(),
                    ETransferMode.Download => "仅下载".Translate(),
                    _ => "双向同步".Translate()
                };
            }

            Image transferImage = transferGO.GetComponent<Image>();
            if (transferImage != null) {
                transferImage.color = transferMode switch {
                    ETransferMode.Sync => storage.masterInspector.storageNoneSpColor,
                    ETransferMode.Upload => storage.masterInspector.storageDemandColor,
                    ETransferMode.Download => storage.masterInspector.storageSupplyColor,
                    _ => storage.masterInspector.storageNoneSpColor
                };
            }

            RectTransform transferRt = transferGO.GetComponent<RectTransform>();
            // 调整按钮坐标与高度，匹配我们预留的 spacingX 和 BtnHeight
            transferRt.anchoredPosition = new Vector2(localRect.anchoredPosition.x + spacingX, topY);
            transferRt.sizeDelta = new Vector2(transferRt.sizeDelta.x, BtnHeight);
        }

        if (controlPanelCapacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
            // 容量按钮根据模式开/关，并同步文字颜色
            ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                slotCapacityMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
            ECapacityMode capacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
            capacityGO.SetActive(storage.localSdButton.gameObject.activeSelf
                                 && transferMode is ETransferMode.Sync or ETransferMode.Upload);

            Text capText = capacityGO.GetComponentInChildren<Text>();
            if (capText != null) {
                capText.text = capacityMode switch {
                    ECapacityMode.Limited => "有限上传".Translate(),
                    ECapacityMode.Infinite => "无限上传".Translate(),
                    _ => "有限上传".Translate()
                };
            }

            Image capImage = capacityGO.GetComponent<Image>();
            if (capImage != null) {
                capImage.color = capacityMode switch {
                    ECapacityMode.Limited => storage.masterInspector.storageSupplyColor,
                    ECapacityMode.Infinite => storage.masterInspector.storageDemandColor,
                    _ => storage.masterInspector.storageSupplyColor
                };
            }

            RectTransform capRt = capacityGO.GetComponent<RectTransform>();
            // 宽高调整保证新按钮位于原位置之上
            capRt.anchoredPosition = new Vector2(localRect.anchoredPosition.x + spacingX, bottomY);
            capRt.sizeDelta = new Vector2(capRt.sizeDelta.x, BtnHeight);
        }
    }
}
