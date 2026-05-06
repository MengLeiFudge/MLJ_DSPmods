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
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnCreate))]
    public static void UIStationWindow_OnCreate_Postfix(UIStationWindow __instance) {
        if (__instance?.windowTrans != null) {
            // 缓存窗口的原始宽度，用于之后根据是否为交互站动态扩展
            GetOrCacheOriginal(windowOriginalWidth, __instance.windowTrans, x => x.sizeDelta.x);
        }

        for (int index = 0; index < 6; ++index) {
            UIStationStorage storage = __instance.storageUIs[index];
            // 每个栏位都要注入自定义的传输/容量按钮
            try {
                // 原版本地物流模式的按钮
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                // 原版按钮的宽度
                float btnWidth = refRect.sizeDelta.x;
                // 按钮的宽度+间隔=外面盒子需要增加的宽度
                spacingX = btnWidth + ExtraSpacing;
                // 创建交互模式按钮
                // 希望每个栏位都只有一个同名按钮，不存在时才复制
                string tName = "FE_transferModeButton_" + index;
                Transform tTrans = storage.transform.Find(tName);
                if (tTrans == null) {
                    GameObject go =
                        GameObject.Instantiate(storage.localSdButton.gameObject,
                            storage.localSdButton.transform.parent, false);
                    go.name = tName;
                    transferGameObjects.TryAdd(storage, go);
                }

                // 创建容量按钮
                // 同理为容量模式复制一个按钮，并缓存备用
                string cName = "FE_capacityModeButton_" + index;
                Transform cTrans = storage.transform.Find(cName);
                if (cTrans == null) {
                    GameObject go =
                        GameObject.Instantiate(storage.localSdButton.gameObject,
                            storage.localSdButton.transform.parent, false);
                    go.name = cName;
                    capacityGameObjects.TryAdd(storage, go);
                }

                // 保证弹窗层级高于新增按钮，避免遮挡
                storage.popupBoxRect.SetSiblingIndex(storage.popupBoxRect.GetSiblingIndex() + 10);
            }
            catch (Exception ex) {
                LogError($"FE StationManager: create mode buttons failed: {ex}");
            }
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
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    /// <summary>在总控窗口创建时为所有存储槽注入模式按钮</summary>
    /// <param name="__instance">当前的 UIControlPanelWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnOpen))]
    public static void UIStationStorage__OnOpen_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = __instance.transform.Find(tName);
        if (tTrans != null) {
            // 给自定义传输按钮绑定弹窗展示，替代原选项逻辑
            Button transferBtn = tTrans.GetComponent<Button>();
            transferBtn.onClick.AddListener(() => ShowTransferPopup(__instance));
        }

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = __instance.transform.Find(cName);
        if (cTrans != null) {
            // 同步为容量按钮绑定弹窗入口
            Button capBtn = cTrans.GetComponent<Button>();
            capBtn.onClick.AddListener(() => ShowCapacityPopup(__instance));
        }
    }

    /// <summary>关闭独立物流站存储槽时移除模式按钮事件</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnClose))]
    public static void UIStationStorage__OnClose_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = __instance.transform.Find(tName);
        if (tTrans != null) {
            Button transferBtn = tTrans.GetComponent<Button>();
            // 关闭时断开自定义按钮事件，防止下一次打开时重复绑定
            transferBtn.onClick.RemoveAllListeners();
        }

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = __instance.transform.Find(cName);
        if (cTrans != null) {
            Button capBtn = cTrans.GetComponent<Button>();
            // 容量按钮同样清理事件
            capBtn.onClick.RemoveAllListeners();
        }
    }

    /// <summary>刷新独立物流站存储槽时同步按钮位置、颜色与宽度</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
    public static void UIStationStorage_RefreshValues_Postfix(UIStationStorage __instance) {
        // 重命名
        UIStationStorage storage = __instance;
        // 给局部变量起个名字便于以后对 UI 进行操作
        // 获取塔对应的物品ID
        int buildingID = storage.stationWindow.factory.entityPool[storage.station.entityId].protoId;

        // 栏位本身
        RectTransform rectTransform = storage.transform.GetComponent<RectTransform>();
        // 如果不是我们的交互塔，则恢复原版布局和按钮状态
        if (!IsInteractionStation(buildingID)) {
            SetStoragePopupShift(storage, false);
            // 还原栏位宽度、沙盒锁位置
            if (rectTransform != null && storageWidth.ContainsKey(storage)) {
                storageWidth.TryRemove(storage, out _);
                rectTransform.sizeDelta =
                    new Vector2(rectTransform.sizeDelta.x - spacingX, rectTransform.sizeDelta.y);
                RectTransform keepModeComponent = storage.keepModeButton.GetComponent<RectTransform>();
                keepModeComponent.anchoredPosition = new Vector2(keepModeComponent.anchoredPosition.x + spacingX,
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
            storageWidth.TryAdd(storage, true);
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

        // 通过实体ID拿到整个塔的同步模式
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ETransferMode>());

        // 通过栏位索引，拿到对应栏位的同步模式
        ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);

        if (transferGameObjects.TryGetValue(storage, out GameObject tTrans)) {
            // 显示按钮并同步当前传输模式的文本与颜色
            // 显示按钮
            tTrans.SetActive(storage.localSdButton.gameObject.activeSelf);
            Text transferText = tTrans.GetComponentInChildren<Text>();
            // 根据当前模式更新文本
            if (transferText != null) {
                transferText.text = eTransferMode switch {
                    ETransferMode.Sync => "双向同步".Translate(), ETransferMode.Upload => "仅上传".Translate(),
                    ETransferMode.Download => "仅下载".Translate(),
                    _ => "双向同步".Translate()
                };
            }
            Image capImage = tTrans.GetComponent<Image>();
            if (capImage != null) {
                capImage.color = eTransferMode switch {
                    ETransferMode.Sync => storage.noneSpColor, ETransferMode.Upload => storage.demandColor,
                    ETransferMode.Download => storage.supplyColor,
                    _ => storage.noneSpColor
                };
            }

            // 更新位置
            RectTransform rt = tTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, topY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
        }

        if (capacityGameObjects.TryGetValue(storage, out GameObject cTrans)) {
            // 根据当前传输/容量设置决定容量按钮是否可见以及文本颜色
            // 通过实体ID拿到整个塔的同步模式
            ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                slotCapacityMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ECapacityMode>());

            // 通过栏位索引，拿到对应栏位的同步模式
            ECapacityMode eCapacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
            cTrans.SetActive(storage.localSdButton.gameObject.activeSelf
                             && eTransferMode is ETransferMode.Sync or ETransferMode.Upload);
            Text capText = cTrans.GetComponentInChildren<Text>();
            if (capText != null) {
                capText.text = eCapacityMode switch {
                    ECapacityMode.Limited => "有限上传".Translate(), ECapacityMode.Infinite => "无限上传".Translate(),
                    _ => "有限上传".Translate()
                };
            }
            Image capImage = cTrans.GetComponent<Image>();
            if (capImage != null) {
                capImage.color = eCapacityMode switch {
                    ECapacityMode.Limited => storage.supplyColor, ECapacityMode.Infinite => storage.demandColor,
                    _ => storage.supplyColor
                };
            }

            RectTransform rt = cTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, bottomY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
        }
    }


    /// <summary>独立窗口每帧更新时扩展能量条与位置</summary>
    /// <param name="__instance">当前的 UIStationWindow 实例</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnUpdate))]
    public static void UIStationWindow_OnUpdate_Prefix(UIStationWindow __instance) {
        // 重命名
        UIStationWindow stationWindow = __instance;
        StationComponent station = stationWindow.transport.stationPool[stationWindow.stationId];
        // 获取塔对应的物品ID
        int buildingID = stationWindow.factory.entityPool[station.entityId].protoId;
        // 如果不是自定义的塔
        if (!IsInteractionStation(buildingID)) {
            return;
        }

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

        // 修改集装输出的描述
        Component label = __instance.techPilerButton.transform.Find("label");
        Text text = label.GetComponent<Text>();
        // 只处理物流交互站
        if (!isModStation) {
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
        float targetWidth = originalWidth + (isModStation ? spacingX : 0f);
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

    /// <summary>在独立面板显示传输模式弹窗并更新按钮提示</summary>
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

        // 通过实体和槽位读取当前传输模式，以便为选项按钮提示下一步

        // 通过实体ID拿到整个塔的同步模式
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());

        // 通过栏位索引，拿到对应栏位的同步模式
        ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        // option0 / option1 的文字和颜色，按“当前模式”动态提示“点下去会切到什么模式”。
        storage.optionImage0.color = eTransferMode switch {
            ETransferMode.Sync => storage.demandColor,
            ETransferMode.Upload => storage.supplyColor,
            _ => storage.noneSpColor
        };
        storage.optionImage1.color = eTransferMode switch {
            ETransferMode.Sync => storage.supplyColor,
            ETransferMode.Upload => storage.noneSpColor,
            _ => storage.demandColor
        };
        storage.optionText0.text = eTransferMode switch {
            ETransferMode.Sync => "仅上传".Translate(),
            ETransferMode.Upload => "仅下载".Translate(),
            _ => "双向同步".Translate()
        };
        storage.optionText1.text = eTransferMode switch {
            ETransferMode.Sync => "仅下载".Translate(),
            ETransferMode.Upload => "双向同步".Translate(),
            _ => "仅上传".Translate()
        };
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // 按钮点击后记录这是 FE 控制的传输弹窗，以便拦截后续事件
        // 记录：这个弹窗是 FE 接管的、当前是否打开、当前是传输模式弹窗。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = true;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 独立面板：点击 mod 按钮时，弹窗放到右侧，避免挡住原按钮区域。
        SetStoragePopupShift(storage, true);
    }

    /// <summary>在独立面板显示容量模式弹窗并设置文本与颜色</summary>
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
        storage.optionImage0.color = storage.demandColor;
        storage.optionImage1.color = storage.supplyColor;
        storage.optionText0.text = "无限上传".Translate();
        storage.optionText1.text = "有限上传".Translate();
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        // false 代表当前是“容量模式弹窗”，后续点击 option 时走容量逻辑。
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = false;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        // 独立面板：点击 mod 按钮时，弹窗放到右侧。
        SetStoragePopupShift(storage, true);
    }

    /// <summary>拦截原始存储按钮点击并复位弹窗</summary>
    /// <param name="__instance">当前的 UIStationStorage 实例</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnLocalSdButtonClick))]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnRemoteSdButtonClick))]
    public static void UIStationStorage_OnSdButtonClick_Prefix(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        // 点击原版物流按钮时，先清理 FE 弹窗状态，再把弹窗复位。
        ClearPopupState(popupStateKey);
        SetStoragePopupShift(storage, false);
    }

    // Intercept option clicks when our popup is active
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
            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                // 当前是“传输模式弹窗”：按 idx 计算下一个传输模式。
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                ETransferMode nextMode = GetNextTransferMode(eTransferMode, idx);
                transferDictionary.TryUpdate(storage.index, nextMode, eTransferMode);
            } else {
                // 当前是“容量模式弹窗”：option0=无限上传，option1=有限上传。
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
                ECapacityMode eCapacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
                if (idx == 0) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Infinite, eCapacityMode);
                } else if (idx == 1) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Limited, eCapacityMode);
                }
            }

            // 选完就关闭弹窗，并标记为“非 FE 弹窗激活态”。
            storage.popupBoxRect.gameObject.SetActive(false);
            slotIsMyPopup[popupStateKey] = false;
            return false;// prevent original OnOptionButton* handlers
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
}
