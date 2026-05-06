using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using FE.Logic.Buildings.Definitions;
using FE.Logic.Manager;
using FE.UI.MainPanel.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Station;

public static partial class StationManager {
    /// <summary>切换总控站点 ID 时刷新面板的堆叠描述和按钮</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.OnStationIdChange))]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.RefreshTabPanelUI))]
    public static void UIControlPanelStationInspector_OnStationIdChange_Postfix(
        UIControlPanelStationInspector __instance) {
        __instance.event_lock = true;
        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            __instance.event_lock = false;
            return;
        }

        // 修改集装输出的描述
        Component label = __instance.techPilerButton.transform.Find("label");
        Text text = label.GetComponent<Text>();
        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            // 还原，避免不关窗口直接切换的时候显示错误
            text.text = "  使用科技上限";
            __instance.event_lock = false;
            return;
        }

        text.text = "  使用强化上限";
        RefreshInteractionStationPilerUI(
            station,
            __instance.minPilerSlider,
            __instance.minPilerValue,
            __instance.minPilerGroup.gameObject,
            __instance.pilerTechGroup.gameObject
        );

        __instance.event_lock = false;
    }

    /// <summary>查找总控面板右侧区域的 RectTransform 引用</summary>
    /// <param name="inspector">目标的 UIControlPanelStationInspector</param>
    /// <returns>右侧区域的 RectTransform 或 null</returns>
    private static RectTransform FindInspectorRightGroupRect(UIControlPanelStationInspector inspector) {
        if (inspector == null) {
            return null;
        }

        RectTransform inspectorRect = inspector.rectTransform;
        Transform rightGroupTransform = inspectorRect?.parent?.Find("right-group");
        if (rightGroupTransform == null && inspector.masterWindow != null) {
            rightGroupTransform = inspector.masterWindow.transform.Find("right-group");
        }

        if (rightGroupTransform == null && inspectorRect != null && inspectorRect.name == "right-group") {
            return inspectorRect;
        }

        if (rightGroupTransform == null && inspector.masterWindow != null) {
            RectTransform[] children = inspector.masterWindow.GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform child in children) {
                if (child != null && child.name == "right-group") {
                    rightGroupTransform = child;
                    break;
                }
            }
        }

        return rightGroupTransform as RectTransform;
    }

    /// <summary>将右侧区域恢复到原始宽度</summary>
    /// <param name="rightGroupRect">右侧组的 RectTransform</param>
    private static void RestoreInspectorRightGroup(RectTransform rightGroupRect) {
        if (rightGroupRect == null) {
            return;
        }

        if (inspectorOriginalRightGroupWidth.TryGetValue(rightGroupRect, out float originalWidth)) {
            rightGroupRect.sizeDelta = new Vector2(originalWidth, rightGroupRect.sizeDelta.y);
        }
    }

    /// <summary>还原总控面板单个存储槽的按钮位置与状态</summary>
    /// <param name="storage">目标的 UIControlPanelStationStorage</param>
    private static void RestoreControlPanelStorageSlot(UIControlPanelStationStorage storage) {
        if (storage == null) {
            return;
        }

        RectTransform localRect = storage.localSdButton?.GetComponent<RectTransform>();
        if (localRect != null
            && controlPanelSdButtonOriginalPosition.TryGetValue(localRect, out float localOriginal)) {
            localRect.anchoredPosition = new Vector2(localOriginal, localRect.anchoredPosition.y);
        }

        RectTransform remoteRect = storage.remoteSdButton?.GetComponent<RectTransform>();
        if (remoteRect != null
            && controlPanelSdButtonOriginalPosition.TryGetValue(remoteRect, out float remoteOriginal)) {
            remoteRect.anchoredPosition = new Vector2(remoteOriginal, remoteRect.anchoredPosition.y);
        }

        RectTransform keepModeRect = storage.keepModeButton?.GetComponent<RectTransform>();
        if (keepModeRect != null
            && controlPanelSdButtonOriginalPosition.TryGetValue(keepModeRect, out float keepModeOriginal)) {
            keepModeRect.anchoredPosition = new Vector2(keepModeOriginal, keepModeRect.anchoredPosition.y);
        }

        SetControlPanelStoragePopupShift(storage, false);

        ClearPopupState(GetPopupStateKey(storage));

        if (controlPanelTransferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
            transferGO.SetActive(false);
        }

        if (controlPanelCapacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
            capacityGO.SetActive(false);
        }
    }

    /// <summary>还原总控面板所有存储槽的宽度</summary>
    /// <param name="inspector">目标的 UIControlPanelStationInspector</param>
    private static void RestoreInspectorStorageWidths(UIControlPanelStationInspector inspector) {
        if (inspector?.storageUIs == null) {
            return;
        }

        for (int i = 0; i < inspector.storageUIs.Length; i++) {
            UIControlPanelStationStorage storage = inspector.storageUIs[i];
            if (storage == null) {
                continue;
            }

            RectTransform topGroup = storage.transform.Find("top-group") as RectTransform;
            if (topGroup != null
                && inspectorOriginalTopGroupWidth.TryGetValue(topGroup, out float originalTopGroupWidth)) {
                topGroup.sizeDelta = new Vector2(originalTopGroupWidth, topGroup.sizeDelta.y);
            }

            RectTransform bg = storage.transform.Find("bg") as RectTransform;
            if (bg != null && inspectorOriginalBgWidth.TryGetValue(bg, out float originalBgWidth)) {
                bg.sizeDelta = new Vector2(originalBgWidth, bg.sizeDelta.y);
            }
        }
    }

    /// <summary>扩展总控面板存储槽的宽度以容纳模式按钮</summary>
    /// <param name="inspector">目标的 UIControlPanelStationInspector</param>
    private static void WidenInspectorStorageWidths(UIControlPanelStationInspector inspector) {
        if (inspector?.storageUIs == null) {
            return;
        }

        for (int i = 0; i < inspector.storageUIs.Length; i++) {
            UIControlPanelStationStorage storage = inspector.storageUIs[i];
            if (storage == null) {
                continue;
            }

            RectTransform topGroup = storage.transform.Find("top-group") as RectTransform;
            if (topGroup != null) {
                float originalTopGroupWidth = GetOrCacheOriginal(inspectorOriginalTopGroupWidth, topGroup,
                    x => x.rect.width > 0f ? x.rect.width : x.sizeDelta.x);
                topGroup.sizeDelta = new Vector2(originalTopGroupWidth + spacingX, topGroup.sizeDelta.y);
            }

            RectTransform bg = storage.transform.Find("bg") as RectTransform;
            if (bg != null) {
                float originalBgWidth = GetOrCacheOriginal(inspectorOriginalBgWidth, bg,
                    x => x.rect.width > 0f ? x.rect.width : x.sizeDelta.x);
                bg.sizeDelta = new Vector2(originalBgWidth + spacingX, bg.sizeDelta.y);
            }
        }
    }

    /// <summary>
    /// 加宽总控面板物流站信息页签（能量条和数值位置）
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector._OnUpdate))]
    public static void UIControlPanelStationInspector_OnUpdate_Postfix(UIControlPanelStationInspector __instance) {
        // 只处理物流交互站：检查stationId和工厂是否有效
        if (__instance.stationId == 0 || __instance.factory == null) {
            return;
        }

        // 从stationPool获取物流站组件，并验证ID匹配
        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return;
        }

        // 确保总控面板间距已计算
        EnsureControlPanelSpacing(__instance);

        // 获取建筑ID，判断是否为物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        // 获取右侧组RectTransform，用于后续调整
        RectTransform rightGroupRect = FindInspectorRightGroupRect(__instance);

        // 如果不是物流交互站，恢复所有UI到原始状态
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            // 恢复所有存储槽的按钮位置和状态
            if (__instance.storageUIs != null) {
                foreach (UIControlPanelStationStorage storage in __instance.storageUIs) {
                    RestoreControlPanelStorageSlot(storage);
                }
            }

            // 恢复存储槽宽度到原始值
            RestoreInspectorStorageWidths(__instance);

            // 恢复状态筛选组的位置
            RectTransform stateGroupTransRestore = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
            if (stateGroupTransRestore != null
                && filterOriginalStateGroupPosition.TryGetValue(stateGroupTransRestore, out float stateGroupX)) {
                stateGroupTransRestore.anchoredPosition =
                    new Vector2(stateGroupX, stateGroupTransRestore.anchoredPosition.y);
            }

            // 恢复主窗口宽度到原始值
            RectTransform masterRectRestore = __instance.masterWindow != null
                ? __instance.masterWindow.transform as RectTransform
                : null;
            if (masterRectRestore != null
                && inspectorOriginalMasterWidth.TryGetValue(masterRectRestore, out float masterWidth)) {
                masterRectRestore.sizeDelta = new Vector2(masterWidth, masterRectRestore.sizeDelta.y);
            }

            // 恢复右侧组宽度到原始值
            RestoreInspectorRightGroup(rightGroupRect);

            return;
        }

        // 只在信息页签生效，其他页签恢复原始状态
        if (__instance.currentTabPanel != EUIControlPanelStationPanel.Info) {
            // 恢复所有存储槽的按钮位置和状态
            if (__instance.storageUIs != null) {
                foreach (UIControlPanelStationStorage storage in __instance.storageUIs) {
                    RestoreControlPanelStorageSlot(storage);
                }
            }

            // 恢复存储槽宽度到原始值
            RestoreInspectorStorageWidths(__instance);

            // 恢复状态筛选组的位置
            RectTransform stateGroupTransRestore = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
            if (stateGroupTransRestore != null
                && filterOriginalStateGroupPosition.TryGetValue(stateGroupTransRestore, out float stateGroupX)) {
                stateGroupTransRestore.anchoredPosition =
                    new Vector2(stateGroupX, stateGroupTransRestore.anchoredPosition.y);
            }

            // 恢复主窗口宽度到原始值
            RectTransform masterRectRestore = __instance.masterWindow != null
                ? __instance.masterWindow.transform as RectTransform
                : null;
            if (masterRectRestore != null
                && inspectorOriginalMasterWidth.TryGetValue(masterRectRestore, out float masterWidth)) {
                masterRectRestore.sizeDelta = new Vector2(masterWidth, masterRectRestore.sizeDelta.y);
            }

            // 恢复右侧组宽度到原始值
            RestoreInspectorRightGroup(rightGroupRect);

            return;
        }

        // 加宽存储槽宽度以适应自定义按钮
        WidenInspectorStorageWidths(__instance);

        // 检查是否解锁曲速科技，影响能量条宽度计算
        bool logisticShipWarpDrive = GameMain.history.logisticShipWarpDrive;

        RectTransform powerRect = __instance.powerGroupRect;

        // 根据是否为星际站和曲速解锁状态计算基础能量条宽度
        float basePowerWidth = station.isStellar
            ? (logisticShipWarpDrive ? 320f : 380f)
            : 440f;
        // 设置能量条宽度（加上额外的间距）
        powerRect.sizeDelta = new Vector2(basePowerWidth + spacingX, 40f);

        // 调整能量数值的位置（基于原始长度+spacingX）
        // 计算当前能量百分比
        float num2 = (float)station.energy / (float)station.energyMax;
        // 计算能量数值显示位置的基础长度
        float num4 = (station.isStellar ? (logisticShipWarpDrive ? 180f : 240f) : 300f) + spacingX;

        // 根据能量百分比决定数值显示位置，避免与能量条填充部分重叠
        if (num2 > 0.7f) {
            // 高能量时，数值显示在能量条左侧
            __instance.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round(num4 * num2 - 30f), 0f);
        } else {
            // 低能量时，数值显示在能量条右侧
            __instance.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round(num4 * num2 + 30f), 0f);
        }

        // 调整主窗口宽度
        RectTransform masterRect = __instance.masterWindow != null
            ? __instance.masterWindow.transform as RectTransform
            : null;
        if (masterRect != null) {
            // 首次访问时缓存原始宽度
            if (!inspectorOriginalMasterWidth.ContainsKey(masterRect)) {
                inspectorOriginalMasterWidth[masterRect] = masterRect.sizeDelta.x;
            }
            // 设置主窗口宽度为原始宽度加上额外间距
            masterRect.sizeDelta =
                new Vector2(inspectorOriginalMasterWidth[masterRect] + spacingX, masterRect.sizeDelta.y);
        }

        // 调整状态筛选组的位置
        RectTransform stateGroupTrans = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
        if (stateGroupTrans != null) {
            // 首次访问时缓存原始位置
            if (!filterOriginalStateGroupPosition.ContainsKey(stateGroupTrans)) {
                filterOriginalStateGroupPosition[stateGroupTrans] = stateGroupTrans.anchoredPosition.x;
            }

            // 设置状态筛选组位置为原始位置加上额外间距
            stateGroupTrans.anchoredPosition =
                new Vector2(filterOriginalStateGroupPosition[stateGroupTrans] + spacingX,
                    stateGroupTrans.anchoredPosition.y);
        }

        // 调整右侧组宽度
        if (rightGroupRect != null) {
            // 首次访问时缓存原始宽度
            if (!inspectorOriginalRightGroupWidth.ContainsKey(rightGroupRect)) {
                inspectorOriginalRightGroupWidth[rightGroupRect] = rightGroupRect.sizeDelta.x;
            }

            // 设置右侧组宽度为原始宽度加上额外间距
            rightGroupRect.sizeDelta = new Vector2(
                inspectorOriginalRightGroupWidth[rightGroupRect] + spacingX, rightGroupRect.sizeDelta.y);
        }
    }

    /// <summary>在总控面板修改集装滑块时同步强化上限</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    /// <param name="value">新的滑块数值</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.OnMinPilerValueChange))]
    public static bool UIControlPanelStationInspector_OnMinPilerValueChange_Prefix(
        UIControlPanelStationInspector __instance, float value) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            return true;
        }

        // 不是自动
        if (!__instance.techPilerCheck.enabled) {
            int newVal = GetClampedPilerCount(value);

            __instance.transport.stationPool[__instance.stationId].pilerCount = newVal;
            __instance.minPilerValue.text = newVal.ToString();
        }

        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>切换总控面板集装自动选项并刷新 UI</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector.OnTechPilerClick))]
    public static bool
        UIControlPanelStationInspector_OnTechPilerClick_Prefix(UIControlPanelStationInspector __instance) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 只处理物流交互站
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID)) {
            return true;
        }

        __instance.techPilerCheck.enabled = !__instance.techPilerCheck.enabled;
        int maxStack = GetInteractionStationMaxStack();
        __instance.transport.stationPool[__instance.stationId].pilerCount =
            __instance.techPilerCheck.enabled
                ? 0
                : maxStack;
        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>
    /// 实际处理时，物流交互站的集装上限使用强化上限
    /// </summary>
}
