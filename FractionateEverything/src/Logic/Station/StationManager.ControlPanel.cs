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
/// <summary>
/// StationManager 类型。
/// </summary>
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

    /// <summary>总控面板：主窗口原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalMasterWidth = [];
    /// <summary>总控面板：顶部栏位原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalTopGroupWidth = [];
    /// <summary>总控面板：背景栏位原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalBgWidth = [];
    /// <summary>总控面板：状态筛选组原始位置缓存</summary>
    private static readonly Dictionary<RectTransform, float> filterOriginalStateGroupPosition = [];
    /// <summary>总控面板：右侧组原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> inspectorOriginalRightGroupWidth = [];

    /// <summary>总控面板：原版物流按钮原始位置缓存</summary>
    private static readonly Dictionary<RectTransform, float> controlPanelSdButtonOriginalPosition = [];
    /// <summary>总控面板：记录弹窗当前是否处于偏移状态</summary>
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, bool> controlPanelStoragePopup = new();
    /// <summary>总控面板：记录弹窗原始X坐标，保证左移/回归可逆</summary>
    private static readonly Dictionary<RectTransform, float> controlPanelStoragePopupOriginalX = [];
    /// <summary>总控面板：传输模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject>
        controlPanelTransferGameObjects = new();
    /// <summary>总控面板：容量模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject>
        controlPanelCapacityGameObjects = new();

    /// <summary>
    /// 在总控面板中查找模式按钮的Transform
    /// </summary>
    /// <param name="storage">总控面板存储槽位</param>
    /// <param name="buttonName">按钮名称</param>
    /// <returns>找到的Transform，未找到则返回null</returns>
    private static Transform FindControlPanelModeButtonTransform(UIControlPanelStationStorage storage,
        string buttonName) {
        // 参数校验：存储槽位为空时返回null
        if (storage == null) {
            return null;
        }

        // 优先在存储槽位自身子对象中查找
        Transform byStorage = storage.transform.Find(buttonName);
        if (byStorage != null) {
            return byStorage;
        }

        // 未找到时，在原版物流按钮的父对象中继续查找
        Transform parent = storage.localSdButton?.transform?.parent;
        return parent?.Find(buttonName);
    }

    /// <summary>
    /// 确保总控面板间距已计算，如未计算则通过存储槽位按钮尺寸计算
    /// </summary>
    /// <param name="inspector">总控面板检查器实例</param>
    /// <returns>计算得到的间距值</returns>
    private static float EnsureControlPanelSpacing(UIControlPanelStationInspector inspector) {
        // 如果间距已计算过，直接返回缓存值
        if (spacingX > 0f) {
            return spacingX;
        }

        // 通过存储槽位预置体的原版按钮尺寸计算间距
        RectTransform rectTransform = inspector?.storageUIPrefab?.localSdButton?.GetComponent<RectTransform>();
        if (rectTransform != null) {
            spacingX = rectTransform.sizeDelta.x + ExtraSpacing;
        }

        return spacingX;
    }

    /// <summary>
    /// 将总控面板弹窗切换到“左移”或“回归原位”。
    /// </summary>
    private static void SetControlPanelStoragePopupShift(UIControlPanelStationStorage storage, bool shiftLeft) {
        RectTransform popupRect = storage?.popupBoxRect;
        if (popupRect == null) {
            return;
        }

        // 第一次遇到该弹窗时，缓存“原始 X 坐标”。
        float originalX = GetOrCacheOriginal(controlPanelStoragePopupOriginalX, popupRect, x => x.anchoredPosition.x);
        // 总控面板规则：原版按钮 -> 左移；mod 按钮 -> 回归。
        float targetX = shiftLeft ? originalX - spacingX : originalX;
        popupRect.anchoredPosition = new Vector2(targetX, popupRect.anchoredPosition.y);

        // 记录当前是否处于“偏移态”，用于后续恢复。
        if (shiftLeft) {
            controlPanelStoragePopup[storage] = true;
        } else {
            controlPanelStoragePopup.TryRemove(storage, out _);
        }
    }

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
