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
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelWindow), nameof(UIControlPanelWindow._OnCreate))]
    public static void UIControlPanelWindow_OnCreate_Postfix(UIControlPanelWindow __instance) {
        // 检查总控面板检查器和存储槽数组是否为空
        if (__instance?.stationInspector?.storageUIs == null) {
            return;
        }

        // 遍历总控面板的所有存储槽
        for (int index = 0; index < __instance?.stationInspector.storageUIs.Length; ++index) {
            UIControlPanelStationStorage storage = __instance?.stationInspector.storageUIs[index];
            // 跳过没有本地物流按钮的存储槽
            if (storage?.localSdButton == null) {
                continue;
            }

            try {
                // 获取原版物流按钮的尺寸，用于计算间距
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                if (refRect != null) {
                    // 计算间距：按钮宽度 + 额外间距
                    spacingX = refRect.sizeDelta.x + ExtraSpacing;
                }

                // 每个存储槽需要注入传输模式按钮，若已存在就直接缓存
                string transferName = "FE_cp_transferModeButton_" + index;
                Transform transferTrans = FindControlPanelModeButtonTransform(storage, transferName);
                if (transferTrans == null) {
                    // 创建新的传输模式按钮，使用原版按钮作为模板
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = transferName;
                    Button transferBtn = go.GetComponent<Button>();
                    // 清除原版的点击事件监听
                    transferBtn?.onClick.RemoveAllListeners();
                    go.SetActive(true);
                    controlPanelTransferGameObjects[storage] = go;
                } else {
                    // 按钮已存在，直接缓存引用
                    controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
                }

                // 再注入容量模式按钮，逻辑同上
                string capacityName = "FE_cp_capacityModeButton_" + index;
                Transform capacityTrans = FindControlPanelModeButtonTransform(storage, capacityName);
                if (capacityTrans == null) {
                    // 创建新的容量模式按钮，使用原版按钮作为模板
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = capacityName;
                    Button capacityBtn = go.GetComponent<Button>();
                    // 清除原版的点击事件监听
                    capacityBtn?.onClick.RemoveAllListeners();
                    go.SetActive(true);
                    controlPanelCapacityGameObjects[storage] = go;
                } else {
                    // 按钮已存在，直接缓存引用
                    controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
                }

                // 将弹窗移到最上层，确保显示在按钮之上
                if (storage.popupBoxRect != null) {
                    storage.popupBoxRect.SetSiblingIndex(storage.popupBoxRect.GetSiblingIndex() + 10);
                }
            }
            catch (Exception ex) {
                // 记录按钮创建失败的错误信息
                LogError($"FE StationManager: create control panel mode buttons failed: {ex}");
            }
        }
    }

    /// <summary>总控窗口销毁前恢复所有 UI 缓存</summary>
    /// <param name="__instance">当前的 UIControlPanelWindow 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelWindow), nameof(UIControlPanelWindow._OnDestroy))]
    public static void UIControlPanelWindow_OnDestroy_Prefix(UIControlPanelWindow __instance) {
        controlPanelSdButtonOriginalPosition.Clear();
        inspectorOriginalTopGroupWidth.Clear();
        inspectorOriginalBgWidth.Clear();
        controlPanelStoragePopup.Clear();
        controlPanelStoragePopupOriginalX.Clear();
        controlPanelTransferGameObjects.Clear();
        controlPanelCapacityGameObjects.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    /// <summary>打开总控面板存储槽时初始化自定义按钮并绑定点击事件</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage._OnOpen))]
    public static void UIControlPanelStationStorage_OnOpen_Postfix(UIControlPanelStationStorage __instance) {
        string transferName = "FE_cp_transferModeButton_" + __instance.index;
        Transform transferTrans = FindControlPanelModeButtonTransform(__instance, transferName);
        if (transferTrans != null) {
            // 替换转移按钮的监听，确保我们弹窗逻辑被优先调用
            Button transferBtn = transferTrans.GetComponent<Button>();
            transferBtn?.onClick.RemoveAllListeners();
            transferBtn.onClick.AddListener(() => ShowTransferPopup(__instance));
            transferTrans.gameObject.SetActive(true);
            controlPanelTransferGameObjects[__instance] = transferTrans.gameObject;
        }

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
        if (capacityTrans != null) {
            // 同样处理容量按钮，绑定弹窗显示逻辑
            Button capBtn = capacityTrans.GetComponent<Button>();
            capBtn?.onClick.RemoveAllListeners();
            capBtn.onClick.AddListener(() => ShowCapacityPopup(__instance));
            capacityTrans.gameObject.SetActive(true);
            controlPanelCapacityGameObjects[__instance] = capacityTrans.gameObject;
        }
    }

    /// <summary>关闭总控面板存储槽时移除自定义按钮的事件监听</summary>
    /// <param name="__instance">当前的 UIControlPanelStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage._OnClose))]
    public static void UIControlPanelStationStorage_OnClose_Postfix(UIControlPanelStationStorage __instance) {
        string transferName = "FE_cp_transferModeButton_" + __instance.index;
        Transform transferTrans = FindControlPanelModeButtonTransform(__instance, transferName);
        if (transferTrans != null) {
            Button transferBtn = transferTrans.GetComponent<Button>();
            // 关闭时断开自定义监听，防止残留引用继续触发弹窗弹起
            transferBtn.onClick.RemoveAllListeners();
        }

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
        if (capacityTrans != null) {
            Button capBtn = capacityTrans.GetComponent<Button>();
            // 容量按钮也同步清理事件，保持一致
            capBtn.onClick.RemoveAllListeners();
        }
    }
}
