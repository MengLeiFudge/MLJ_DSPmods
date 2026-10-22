using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FE.Logic.Enhancements.Stacking;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.Logic.Station;

/// <summary>
/// 物流站独立窗口按钮创建与槽位交互逻辑。
/// </summary>
public static partial class StationManager {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnCreate))]
    public static void UIStationWindow_OnCreate_Postfix(UIStationWindow __instance) {
        if (__instance?.windowTrans != null) {
            // 缓存窗口的原始宽度，用于之后根据是否为交互站动态扩展
            GetOrCacheOriginal(windowOriginalWidth, __instance.windowTrans, x => x.sizeDelta.x);
        }

    }

    /// <summary>窗口销毁前清理缓存的窗口尺寸和按钮状态</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnDestroy))]
    public static void UIStationWindow__OnDestroy_Prefix(UIStationWindow __instance) {
        windowOriginalWidth.Clear();
        sliderOriginalSize.Clear();
        sliderOriginalPosition.Clear();
        storageWidth.Clear();
        storagePopup.Clear();
        storagePopupOriginalX.Clear();
        transferGameObjects.Clear();
        capacityGameObjects.Clear();
        stationPilerLabelOriginalState.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    /// <summary>每个实际打开的槽位独立注入按钮，覆盖其他模组动态新增的行。</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例。</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnOpen))]
    public static void UIStationStorage__OnOpen_Prefix(UIStationStorage __instance) {
        // 原始 _OnOpen 会先调用 RefreshValues，按钮必须在此之前就绪。
        transferGameObjects[__instance] = CreateStorageModeButton(__instance, "FE_transferModeButton",
            () => ShowTransferPopup(__instance));
        capacityGameObjects[__instance] = CreateStorageModeButton(__instance, "FE_capacityModeButton",
            () => ShowCapacityPopup(__instance));
        __instance.popupBoxRect.SetAsLastSibling();
    }

    /// <summary>按本行按钮名复用控件，每次打开只绑定当前行的一个回调。</summary>
    /// <param name="storage">按钮所属的物流槽。</param>
    /// <param name="name">本行内部唯一的对象名。</param>
    /// <param name="onClick">本次打开时的模式弹窗操作。</param>
    /// <returns>本行创建或复用的模式按钮。</returns>
    private static GameObject CreateStorageModeButton(UIStationStorage storage, string name, Action onClick) {
        Transform parent = storage.localSdButton.transform.parent;
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject
            : GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
        go.name = name;
        Button button = go.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        go.SetActive(false);
        return go;
    }

    /// <summary>关闭独立物流站存储槽时移除模式按钮事件</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnClose))]
    public static void UIStationStorage__OnClose_Postfix(UIStationStorage __instance) {
        if (transferGameObjects.TryGetValue(__instance, out GameObject transfer)) {
            transfer.GetComponent<Button>().onClick.RemoveAllListeners();
        }
        if (capacityGameObjects.TryGetValue(__instance, out GameObject capacity)) {
            capacity.GetComponent<Button>().onClick.RemoveAllListeners();
        }
    }

    /// <summary>刷新独立物流站存储槽时同步按钮位置、颜色与宽度</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
    public static void UIStationStorage_RefreshValues_Postfix(UIStationStorage __instance) {
        // 重命名
        UIStationStorage storage = __instance;
        float spacingX = GetStorageSpacing(storage);
        // 获取塔对应的物品ID
        int buildingID = storage.stationWindow.factory.entityPool[storage.station.entityId].protoId;

        // 栏位本身
        RectTransform rectTransform = storage.transform.GetComponent<RectTransform>();
        // 如果不是物流交互站，则恢复原版布局和按钮状态
        if (!IsInteractionStation(buildingID)) {
            SetStoragePopupShift(storage, false);
            // 还原栏位宽度、沙盒锁位置
            if (rectTransform != null && storageWidth.TryRemove(storage, out float addedWidth)) {
                rectTransform.sizeDelta =
                    new Vector2(rectTransform.sizeDelta.x - addedWidth, rectTransform.sizeDelta.y);
                RectTransform keepModeComponent = storage.keepModeButton.GetComponent<RectTransform>();
                keepModeComponent.anchoredPosition = new Vector2(keepModeComponent.anchoredPosition.x + addedWidth,
                    keepModeComponent.anchoredPosition.y);
            }

            // 隐藏两个新增的按钮
            if (transferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
                transferGO.SetActive(false);
            }

            if (capacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
                capacityGO.SetActive(false);
            }

            return;
        }

        // 增加栏位宽度、向前移动沙盒锁位置
        // 初次遇到自定义塔时扩展栏位宽度，并把沙盒锁按钮向前移
        if (rectTransform != null && !storageWidth.ContainsKey(storage)) {
            storageWidth.TryAdd(storage, spacingX);
            rectTransform.sizeDelta =
                new Vector2(rectTransform.sizeDelta.x + spacingX, rectTransform.sizeDelta.y);
            RectTransform keepModeComponent = storage.keepModeButton.GetComponent<RectTransform>();
            keepModeComponent.anchoredPosition = new Vector2(keepModeComponent.anchoredPosition.x - spacingX,
                keepModeComponent.anchoredPosition.y);
        }

        // 向前移动两个原版的按钮
        // PS:这两个按钮每次都会自动回去，所有每次都需要移动
        if (storageWidth.ContainsKey(storage)) {
            RectTransform localComponent = storage.localSdButton.GetComponent<RectTransform>();
            localComponent.anchoredPosition = new Vector2(localComponent.anchoredPosition.x - spacingX,
                localComponent.anchoredPosition.y);
            RectTransform remoteComponent = storage.remoteSdButton.GetComponent<RectTransform>();
            remoteComponent.anchoredPosition = new Vector2(remoteComponent.anchoredPosition.x - spacingX,
                remoteComponent.anchoredPosition.y);
        }

        // 根据塔类型计算垂直位置
        var localImgRT = storage.localSdImage?.rectTransform;
        var remoteImgRT = storage.remoteSdImage?.rectTransform;
        float topY, bottomY;
        if (storage.station != null && storage.station.isStellar && localImgRT != null && remoteImgRT != null) {
            // 大塔：使用与官方按钮相同的 Y (14 / -14)
            topY = localImgRT.anchoredPosition.y;
            bottomY = remoteImgRT.anchoredPosition.y;
        } else if (localImgRT != null) {
            // 小塔：位于官方按钮的中心
            float centerY = localImgRT.anchoredPosition.y;
            float halfGap = (localImgRT.sizeDelta.y - BtnHeight) / 2f;
            // 中间增加 4f 的空隙（上下各 +2f）
            const float PlanetExtraGap = 4f;
            halfGap += PlanetExtraGap / 2f;
            topY = centerY + halfGap;
            bottomY = centerY - halfGap;
        } else {
            topY = BtnYOffset;
            bottomY = -BtnYOffset;
        }

        // 原版本地物流模式按钮用于确定我们新按钮的起始坐标
        RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();

        int uploadMode = GetUploadMode(storage.station.entityId, storage.index);
        bool download = IsDownloadEnabled(storage.station.entityId, storage.index);
        if (transferGameObjects.TryGetValue(storage, out GameObject tTrans)) {
            tTrans.SetActive(storage.localSdButton.gameObject.activeSelf);
            tTrans.GetComponentInChildren<Text>().text = UploadModeText(uploadMode);
            tTrans.GetComponent<Image>().color = uploadMode == 0 ? storage.noneSpColor
                : uploadMode == 1 ? storage.supplyColor : storage.demandColor;
            RectTransform rt = tTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, topY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
        }
        if (capacityGameObjects.TryGetValue(storage, out GameObject cTrans)) {
            cTrans.SetActive(storage.localSdButton.gameObject.activeSelf);
            cTrans.GetComponentInChildren<Text>().text = (download ? "开启下载" : "关闭下载").Translate();
            cTrans.GetComponent<Image>().color = download ? storage.demandColor : storage.noneSpColor;
            RectTransform rt = cTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, bottomY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
        }
    }


    /// <summary>独立窗口每帧更新时扩展能量条与位置</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnUpdate))]
    public static void UIStationWindow_OnUpdate_Postfix(UIStationWindow __instance) {
        // 重命名
        UIStationWindow stationWindow = __instance;
        StationComponent station = stationWindow.transport.stationPool[stationWindow.stationId];
        // 获取塔对应的物品ID
        int buildingID = stationWindow.factory.entityPool[station.entityId].protoId;
        // 如果不是自定义的塔
        if (!IsInteractionStation(buildingID)) {
            return;
        }
        float spacingX = GetWindowSpacing(stationWindow);

        // 是否解锁曲速
        bool logisticShipWarpDrive = GameMain.history.logisticShipWarpDrive;
        // 增加能量条的宽度
        stationWindow.powerGroupRect.sizeDelta =
            new Vector2((station.isStellar ? (logisticShipWarpDrive ? 320f : 380f) : 440f) + spacingX, 40f);
        // 调整能量数值的位置
        float num2 = (float)station.energy / (float)station.energyMax;
        float num4 = (station.isStellar ? (logisticShipWarpDrive ? 180f : 240f) : 300f) + spacingX;
        if ((double)num2 > 0.699999988079071) {
            stationWindow.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round((float)((double)num4 * (double)num2 - 30.0)), 0.0f);
        } else {
            stationWindow.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round((float)((double)num4 * (double)num2 + 30.0)), 0.0f);
        }
    }

    /// <summary>切换站点 ID 时更新 UI 面板宽度与文本</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.OnStationIdChange))]
    public static void UIStationWindow_OnStationIdChange_Postfix(UIStationWindow __instance) {
        __instance.event_lock = true;
        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        bool isModStation = IsModStation(__instance, station);
        SetWindowWidenState(__instance, isModStation);
        if (station == null || station.id != __instance.stationId) {
            __instance.event_lock = false;
            return;
        }

        // 六层前只扩展交互站；六层后普通物流站也显示统一上限。
        if (!isModStation && !StackingManager.IsUnlocked) {
            RestoreStationPilerLabel(__instance.techPilerButton);
            __instance.event_lock = false;
            return;
        }

        ApplyInteractionStationPilerLabel(__instance.techPilerButton);
        RefreshInteractionStationPilerUI(
            station,
            __instance.minPilerSlider,
            __instance.minPilerValue,
            __instance.minPilerGroup.gameObject,
            __instance.pilerTechGroup.gameObject
        );

        __instance.event_lock = false;
    }

    /// <summary>
    /// 修改物流交互站的面板的高度
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.RefreshTrans))]
    public static void UIStationWindow_RefreshTrans_Postfix(UIStationWindow __instance, StationComponent station) {
        if (__instance?.factory == null || station == null || station.entityId <= 0) {
            return;
        }

        bool isModStation = IsModStation(__instance, station);
        AdjustSliders(__instance, isModStation);
        RectTransform windowTrans = __instance.windowTrans;
        if (windowTrans == null) {
            return;
        }

        float originalWidth = GetOrCacheOriginal(windowOriginalWidth, windowTrans, x => x.sizeDelta.x);
        float targetWidth = originalWidth + (isModStation ? GetWindowSpacing(__instance) : 0f);
        windowTrans.sizeDelta = new Vector2(targetWidth, windowTrans.sizeDelta.y);

        if (!isModStation) {
            return;
        }

        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxStack = building.MaxStack();
        if (maxStack <= 1) {
            // 没解锁堆叠，不调整
            return;
        }

        if (station.isStellar) {
            __instance.windowTrans.sizeDelta = new Vector2(targetWidth,
                360f + 76f * station.storage.Length + 36f);
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 186f);
        } else {
            __instance.windowTrans.sizeDelta = new Vector2(targetWidth,
                300f + 76f * station.storage.Length + 36f);
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 126f);
        }
    }

    /// <summary>在独立面板显示上传模式弹窗并更新按钮提示</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    private static void ShowTransferPopup(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length)
            stationStore = station.storage[storage.index];
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 =
            LDB.items.Select((int)storage.stationWindow.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null)
            return;

        int mode = GetUploadMode(station.entityId, storage.index);
        int first = GetUploadOption(mode, 0);
        int second = GetUploadOption(mode, 1);
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = first == 0 ? storage.noneSpColor : first == 1 ? storage.supplyColor : storage.demandColor;
        storage.optionImage1.color = second == 0 ? storage.noneSpColor : second == 1 ? storage.supplyColor : storage.demandColor;
        storage.optionText0.text = UploadModeText(first);
        storage.optionText1.text = UploadModeText(second);
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // 按钮点击后记录这是 FE 控制的传输弹窗，以便拦截后续事件
        // 记录：这个弹窗是 FE 接管的、当前是否打开、当前是上传模式弹窗。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = true;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 独立面板：点击 mod 按钮时，弹窗放到右侧，避免挡住原按钮区域。
        SetStoragePopupShift(storage, true);
    }

    /// <summary>在独立面板显示下载模式弹窗并设置文本与颜色</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    private static void ShowCapacityPopup(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new StationStore();
        if (station != null && storage.index < station.storage.Length)
            stationStore = station.storage[storage.index];
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 =
            LDB.items.Select((int)storage.stationWindow.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null)
            return;

        // 容量弹窗无需根据当前模式读取字典，直接展示选项
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = storage.noneSpColor;
        storage.optionImage1.color = storage.demandColor;
        storage.optionText0.text = "关闭下载".Translate();
        storage.optionText1.text = "开启下载".Translate();
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // false 代表当前是“下载模式弹窗”，后续点击 option 时走容量逻辑。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = false;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 独立面板：点击 mod 按钮时，弹窗放到右侧。
        SetStoragePopupShift(storage, true);
    }

    /// <summary>拦截原始本地物流按钮点击并复位弹窗</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnLocalSdButtonClick))]
    public static void UIStationStorage_OnLocalSdButtonClick_Prefix(UIStationStorage __instance) {
        HandleStationStorageSdButtonClick(__instance);
    }

    /// <summary>拦截原始远程物流按钮点击并复位弹窗</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnRemoteSdButtonClick))]
    public static void UIStationStorage_OnRemoteSdButtonClick_Prefix(UIStationStorage __instance) {
        HandleStationStorageSdButtonClick(__instance);
    }

    private static void HandleStationStorageSdButtonClick(UIStationStorage storage) {
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        // 点击原版物流按钮时，先清理 FE 弹窗状态，再把弹窗复位。
        ClearPopupState(popupStateKey);
        SetStoragePopupShift(storage, false);
    }

    // 弹窗激活时拦截选项点击
    /// <summary>拦截独立存储选项 0 点击并应用 FE 模式</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    /// <returns>是否让原版继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnOptionButton0Click))]
    public static bool UIStationStorage_OnOptionButton0Click_Prefix(UIStationStorage __instance) {
        return HandleOptionClick(__instance, 0);
    }

    /// <summary>拦截独立存储选项 1 点击并应用 FE 模式</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    /// <returns>是否让原版继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnOptionButton1Click))]
    public static bool UIStationStorage_OnOptionButton1Click_Prefix(UIStationStorage __instance) {
        return HandleOptionClick(__instance, 1);
    }

    /// <summary>基于 FE 弹窗逻辑处理独立面板选项点击并更新模式</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    /// <param name="idx">选项索引（0/1）</param>
    /// <returns>是否允许原始逻辑继续执行</returns>
    private static bool HandleOptionClick(UIStationStorage __instance, int idx) {
        try {
            UIStationStorage storage = __instance;
            // 统一使用 storage 便于后续代码复用
            (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
            // 只有 FE 打开的弹窗才拦截；否则让原游戏逻辑继续执行。
            // 仅当 FE 弹窗激活时才拦截，其他情况交还给原始逻辑
            if (!GetPopupFlag(slotIsMyPopup, popupStateKey)) {
                return true;
            }

            StationComponent station = __instance.station;
            UIStationWindow stationWindow = __instance.stationWindow;
            if (storage.popupBoxRect == null
                || !slotPopupBoxRect.TryGetValue(popupStateKey, out RectTransform popupRect)
                || popupRect != storage.popupBoxRect
                || !storage.popupBoxRect.gameObject.activeSelf) {
                // 防御性检查：弹窗对象变化或已关闭时，不接管点击。
                return true;
            }

            StationStore stationStore = new StationStore();
            // 安全读取槽位数据，避免数组越界
            if (station != null && storage.index < station.storage.Length)
                stationStore = station.storage[storage.index];
            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = LDB.items.Select((int)stationWindow.factory.entityPool[station.entityId].protoId);
            if (itemProto1 == null || itemProto2 == null)
                return false;
            // 数据收集完成后继续，根据 popup 类型走不同逻辑
            ApplyModeOption(stationWindow.factory, station, storage.index, GetPopupFlag(slotIsTransfer, popupStateKey), idx);

            // 选完就关闭弹窗，并标记为“非 FE 弹窗激活态”。
            storage.popupBoxRect.gameObject.SetActive(false);
            slotIsMyPopup[popupStateKey] = false;
            return false;// 阻止原版 OnOptionButton* 处理方法
        }
        catch (Exception ex) {
            LogError($"FE HandleOptionClick error: {ex}");
            return true;
        }
    }

    /// <summary>在独立窗口修改集装滑块时同步强化上限</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    /// <param name="value">新的滑块数值</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.OnMinPilerValueChange))]
    public static bool UIStationWindow_OnMinPilerValueChange_Prefix(UIStationWindow __instance, float value) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 六层研究后，普通物流站也使用同一堆叠选择范围。
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID) && !StackingManager.IsUnlocked) {
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

    /// <summary>切换独立窗口集装自动选项并刷新 UI</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    /// <returns>是否让原始逻辑继续执行</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow.OnTechPilerClick))]
    public static bool UIStationWindow_OnTechPilerClick_Prefix(UIStationWindow __instance) {
        if (__instance.event_lock || __instance.stationId == 0 || __instance.factory == null) {
            return false;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return false;
        }

        // 六层研究后，普通物流站也使用同一堆叠选择范围。
        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        if (!IsInteractionStation(buildingID) && !StackingManager.IsUnlocked) {
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

    /// <summary>独立物流站面板：窗口原始宽度缓存</summary>
    private static readonly Dictionary<RectTransform, float> windowOriginalWidth = [];
    /// <summary>独立物流站面板：滑块原始尺寸缓存</summary>
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalSize = [];
    /// <summary>独立物流站面板：滑块原始位置缓存</summary>
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalPosition = [];

    /// <summary>独立物流站面板：记录栏位是否已加宽</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, float> storageWidth = new();
    /// <summary>独立物流站面板：记录弹窗当前是否处于偏移状态</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, bool> storagePopup = new();
    /// <summary>独立物流站面板：记录弹窗原始X坐标，避免多次点击累加偏移</summary>
    private static readonly Dictionary<RectTransform, float> storagePopupOriginalX = [];

    /// <summary>独立物流站面板：传输模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> transferGameObjects = new();
    /// <summary>独立物流站面板：容量模式按钮GameObject缓存</summary>
    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> capacityGameObjects = new();

    private sealed class StationPilerLabelState {
        public StationPilerLabelState(string text, string stringKey, string translation, bool hasLocalizer) {
            Text = text;
            StringKey = stringKey;
            Translation = translation;
            HasLocalizer = hasLocalizer;
        }

        public string Text { get; }
        public string StringKey { get; }
        public string Translation { get; }
        public bool HasLocalizer { get; }
    }

    /// <summary>缓存原版集装上限 label 状态，用于从交互站切回原版站时恢复。</summary>
    private static readonly ConcurrentDictionary<Text, StationPilerLabelState> stationPilerLabelOriginalState = new();

    private static void ApplyInteractionStationPilerLabel(UIButton techPilerButton) {
        Text text = GetStationPilerLabelText(techPilerButton);
        if (text == null) {
            return;
        }

        CacheStationPilerLabelState(text);
        Localizer localizer = text.GetComponent<Localizer>();
        if (localizer != null) {
            localizer.stringKey = "使用强化上限";
            localizer.translation = "使用强化上限".Translate();
        }
        text.text = "使用强化上限".Translate();
    }

    private static void RestoreStationPilerLabel(UIButton techPilerButton) {
        Text text = GetStationPilerLabelText(techPilerButton);
        if (text == null || !stationPilerLabelOriginalState.TryGetValue(text, out StationPilerLabelState state)) {
            return;
        }

        Localizer localizer = text.GetComponent<Localizer>();
        if (state.HasLocalizer && localizer != null) {
            localizer.stringKey = state.StringKey;
            localizer.translation = state.Translation;
        }
        text.text = state.Text;
    }

    private static Text GetStationPilerLabelText(UIButton techPilerButton) {
        Transform label = techPilerButton?.transform.Find("label");
        return label?.GetComponent<Text>();
    }

    private static void CacheStationPilerLabelState(Text text) {
        if (stationPilerLabelOriginalState.ContainsKey(text)) {
            return;
        }

        Localizer localizer = text.GetComponent<Localizer>();
        stationPilerLabelOriginalState[text] = new StationPilerLabelState(
            text.text,
            localizer?.stringKey,
            localizer?.translation,
            localizer != null
        );
    }

    /// <summary>
    /// 调整独立物流站面板中滑块控件的位置和尺寸
    /// </summary>
    /// <param name="window">物流站窗口实例</param>
    /// <param name="shouldWiden">是否需要加宽</param>
    private static void AdjustSliders(UIStationWindow window, bool shouldWiden) {
        // 如果窗口为空，直接返回
        if (window == null) {
            return;
        }

        // 窗口扩宽使用实际槽位的最大按钮宽度，不受其他面板的布局缓存影响。
        float delta = shouldWiden ? GetWindowSpacing(window) : 0f;

        // 定义需要调整的所有滑块控件及其对应的数值文本组件
        (Slider slider, Component valueText)[] controls = [
            (window.maxChargePowerSlider, window.maxChargePowerValue),
            (window.maxTripDroneSlider, window.maxTripDroneValue),
            (window.maxTripVesselSlider, window.maxTripVesselValue),
            (window.warperDistanceSlider, window.warperDistanceValue),
            (window.minDeliverDroneSlider, window.minDeliverDroneValue),
            (window.minDeliverVesselSlider, window.minDeliverVesselValue),
            (window.maxMiningSpeedSlider, window.maxMiningSpeedValue),
            (window.minPilerSlider, window.minPilerValue)
        ];
        // 遍历所有滑块控件，调整其位置和尺寸
        foreach ((Slider slider, Component valueText) in controls) {
            // 跳过空的滑块控件
            if (slider == null) {
                continue;
            }

            // 获取滑块的RectTransform组件
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            if (sliderRect == null) {
                continue;
            }

            // 获取滑块的原始尺寸和位置（首次访问时缓存）
            Vector2 originalSize = GetOrCacheOriginal(sliderOriginalSize, sliderRect, x => x.sizeDelta);
            Vector2 originalPosition = GetOrCacheOriginal(sliderOriginalPosition, sliderRect, x => x.anchoredPosition);

            // 调整滑块的宽度和水平位置
            sliderRect.sizeDelta = new Vector2(originalSize.x + delta, originalSize.y);
            sliderRect.anchoredPosition = new Vector2(originalPosition.x + delta, originalPosition.y);

            // 如果存在数值文本，也调整其位置
            RectTransform valueRect = valueText?.GetComponent<RectTransform>();
            if (valueRect != null) {
                // 获取数值文本的原始位置（首次访问时缓存）
                Vector2 originalValuePosition =
                    GetOrCacheOriginal(sliderOriginalPosition, valueRect, x => x.anchoredPosition);
                // 调整数值文本的水平位置
                valueRect.anchoredPosition =
                    new Vector2(originalValuePosition.x + delta, originalValuePosition.y);
            }
        }
    }

    /// <summary>
    /// 判断窗口中的物流站是否为Mod添加的交互站
    /// </summary>
    /// <param name="window">物流站窗口</param>
    /// <param name="station">物流站组件</param>
    /// <returns>是Mod交互站返回true，否则返回false</returns>
    private static bool IsModStation(UIStationWindow window, StationComponent station) {
        // 校验窗口、工厂和实体ID的有效性
        if (window?.factory == null || station == null || station.entityId <= 0) {
            return false;
        }

        // 从实体池获取建筑ID并判断是否为交互站
        int buildingID = window.factory.entityPool[station.entityId].protoId;
        return IsInteractionStation(buildingID);
    }

    /// <summary>
    /// 设置独立物流站面板的加宽状态
    /// </summary>
    /// <param name="window">物流站窗口</param>
    /// <param name="shouldWiden">是否加宽</param>
    private static void SetWindowWidenState(UIStationWindow window, bool shouldWiden) {
        // 校验窗口和窗口Transform的有效性
        if (window?.windowTrans == null) {
            return;
        }

        // 调用AdjustSliders调整所有滑块的位置和尺寸
        AdjustSliders(window, shouldWiden);
    }

    /// <summary>
    /// 将独立物流站面板弹窗切换到“右移”或“回归原位”。
    /// </summary>
    private static void SetStoragePopupShift(UIStationStorage storage, bool shiftRight) {
        RectTransform popupRect = storage?.popupBoxRect;
        if (popupRect == null) {
            return;
        }

        // 第一次遇到该弹窗时，缓存“原始 X 坐标”。
        float originalX = GetOrCacheOriginal(storagePopupOriginalX, popupRect, x => x.anchoredPosition.x);
        // 独立面板规则：mod 按钮 -> 右移；原版按钮 -> 回归。
        float targetX = shiftRight ? originalX + GetStorageSpacing(storage) : originalX;
        popupRect.anchoredPosition = new Vector2(targetX, popupRect.anchoredPosition.y);

        // 记录当前是否处于“偏移态”，用于后续恢复。
        if (shiftRight) {
            storagePopup[storage] = true;
        } else {
            storagePopup.TryRemove(storage, out _);
        }
    }
    /// <summary>按钮额外间距（像素）</summary>
    private const float ExtraSpacing = 12f;
    /// <summary>模式按钮高度</summary>
    private const float BtnHeight = 26f;
    /// <summary>按钮Y轴偏移量</summary>
    private const float BtnYOffset = 14f;
    /// <summary>读取当前行模式按钮需要的横向空间，单位为像素。</summary>
    /// <param name="storage">物流窗口中的实际槽位。</param>
    /// <returns>本地物流按钮宽度加额外间距；无槽位时为0。</returns>
    private static float GetStorageSpacing(UIStationStorage storage) {
        RectTransform button = storage?.localSdButton?.GetComponent<RectTransform>();
        return button != null ? button.sizeDelta.x + ExtraSpacing : 0f;
    }

    /// <summary>按窗口当前站点的实际槽位取得扩宽量，兼容不同数量与宽度的行。</summary>
    /// <param name="window">当前物流站窗口。</param>
    /// <returns>实际槽位中模式按钮所需的最大横向空间，单位为像素。</returns>
    private static float GetWindowSpacing(UIStationWindow window) {
        UIStationStorage[] rows = window?.storageUIs;
        if (rows == null) { return 0f; }
        StationComponent station = window.stationId > 0 && window.transport != null
            ? window.transport.stationPool[window.stationId] : null;
        int count = Math.Min(rows.Length, station?.storage?.Length ?? rows.Length);
        float spacing = 0f;
        for (int i = 0; i < count; i++) { spacing = Math.Max(spacing, GetStorageSpacing(rows[i])); }
        return spacing;
    }
    /// <summary>槽位弹窗状态：记录弹窗是否由本Mod接管</summary>
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsMyPopup = new();
    /// <summary>槽位弹窗状态：记录当前是传输模式(true)还是容量模式(false)</summary>
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsTransfer = new();
    /// <summary>槽位弹窗状态：记录弹窗RectTransform引用</summary>
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), RectTransform>
        slotPopupBoxRect =
            new();

    /// <summary>获取或缓存RectTransform的原始Vector2值</summary>
    private static Vector2 GetOrCacheOriginal(Dictionary<RectTransform, Vector2> cache, RectTransform rect,
        Func<RectTransform, Vector2> getter) {
        // 检查缓存中是否已有该RectTransform的原始值
        if (cache.TryGetValue(rect, out Vector2 value)) {
            return value;
        }

        // 首次访问时调用getter获取原始值并存入缓存
        value = getter(rect);
        cache[rect] = value;
        return value;
    }

    /// <summary>获取或缓存RectTransform的原始float值</summary>
    private static float GetOrCacheOriginal(Dictionary<RectTransform, float> cache, RectTransform rect,
        Func<RectTransform, float> getter) {
        // 检查缓存中是否已有该RectTransform的原始float值
        if (cache.TryGetValue(rect, out float value)) {
            return value;
        }

        // 首次访问时调用getter获取原始值并存入缓存
        value = getter(rect);
        cache[rect] = value;
        return value;
    }

    /// <summary>
    /// 判断建筑ID是否为物流交互站（行星内或星际）
    /// </summary>
    /// <param name="buildingID">建筑物品ID</param>
    /// <returns>是物流交互站返回true，否则返回false</returns>
    private static bool IsInteractionStation(int buildingID) {
        return buildingID is IFE行星内物流交互站 or IFE星际物流交互站;
    }

    /// <summary>
    /// 获取经过上限限制后的集装数量
    /// </summary>
    /// <param name="value">滑块数值</param>
    /// <returns>限制后的整数值</returns>
    private static int GetClampedPilerCount(float value) {
        // 获取当前交互站由全局堆叠科技允许的最大堆叠数
        int maxStack = GetInteractionStationMaxStack();
        // 将滑块浮点值四舍五入为整数
        int newValue = Mathf.RoundToInt(value);
        // 如果超过上限则限制为上限值，否则保持原值
        return newValue > maxStack ? maxStack : newValue;
    }

    /// <summary>
    /// 获取物流交互站由全局堆叠科技允许的最大堆叠数
    /// </summary>
    /// <returns>最大堆叠数量</returns>
    private static int GetInteractionStationMaxStack() {
        // 从LDB获取交互站物品原型并返回其最大堆叠数
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        return building.MaxStack();
    }

    /// <summary>
    /// 刷新物流交互站的集装UI显示
    /// </summary>
    /// <param name="station">物流站组件</param>
    /// <param name="minPilerSlider">集装滑块</param>
    /// <param name="minPilerValue">集装数值文本</param>
    /// <param name="minPilerGroup">集装组GameObject</param>
    /// <param name="pilerTechGroup">科技组GameObject</param>
    private static void RefreshInteractionStationPilerUI(StationComponent station, Slider minPilerSlider,
        Text minPilerValue, GameObject minPilerGroup, GameObject pilerTechGroup) {
        // 轨道采集器没有传送带端口，保留原版隐藏集装控件的状态。
        if (station.isCollector) { return; }
        int maxStack = GetInteractionStationMaxStack();
        minPilerSlider.maxValue = maxStack;

        // 获取当前集装数量，若为0（自动模式）则显示最大值
        int pilerCount = station.pilerCount;
        int showValue = pilerCount == 0 ? maxStack : pilerCount;
        minPilerSlider.value = showValue;
        minPilerValue.text = showValue.ToString();

        // 当最大堆叠数大于1时（已解锁堆叠功能），显示集装相关UI
        if (maxStack > 1) {
            minPilerGroup.SetActive(true);
            pilerTechGroup.SetActive(true);
        }
    }

    /// <summary>
    /// 从独立面板存储槽位获取弹窗状态键值
    /// </summary>
    /// <param name="storage">存储槽位</param>
    /// <returns>(实体ID, 槽位索引)元组</returns>
    private static (long stationEntityId, int slotIndex) GetPopupStateKey(UIStationStorage storage) {
        // 存储槽位为空时返回无效键值
        if (storage == null) {
            return (0L, -1);
        }

        // 获取物流站实体ID（为空则返回0）和槽位索引
        long stationEntityId = storage.station != null ? storage.station.entityId : 0L;
        return (stationEntityId, storage.index);
    }

    /// <summary>
    /// 从总控面板存储槽位获取弹窗状态键值
    /// </summary>
    /// <param name="storage">总控面板存储槽位</param>
    /// <returns>(实体ID, 槽位索引)元组</returns>
    private static (long stationEntityId, int slotIndex) GetPopupStateKey(UIControlPanelStationStorage storage) {
        // 存储槽位为空时返回无效键值
        if (storage == null) {
            return (0L, -1);
        }

        // 获取物流站实体ID（为空则返回0）和槽位索引
        long stationEntityId = storage.station != null ? storage.station.entityId : 0L;
        return (stationEntityId, storage.index);
    }

    /// <summary>
    /// 从状态字典读取弹窗标记，不存在时返回false
    /// </summary>
    /// <param name="stateDictionary">状态字典</param>
    /// <param name="key">键值</param>
    /// <returns>标记状态</returns>
    private static bool GetPopupFlag(ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> stateDictionary,
        (long stationEntityId, int slotIndex) key) {
        // 统一读取字典中的布尔状态，不存在时按 false 处理。
        return stateDictionary.TryGetValue(key, out bool value) && value;
    }

    /// <summary>
    /// 清理指定键的所有弹窗状态
    /// </summary>
    /// <param name="key">键值</param>
    private static void ClearPopupState((long stationEntityId, int slotIndex) key) {
        // 一个键对应三份弹窗状态，这里一次性清干净，避免残留脏状态。
        slotIsMyPopup.TryRemove(key, out _);
        slotIsTransfer.TryRemove(key, out _);
        slotPopupBoxRect.TryRemove(key, out _);
    }

}
