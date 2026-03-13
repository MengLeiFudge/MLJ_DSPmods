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

public static class StationManager {
    public static void AddTranslations() {
        // 传输模式按钮文本
        Register("双向同步", "Sync", "双向同步");
        Register("仅上传", "Upload Only", "仅上传");
        Register("仅下载", "Download Only", "仅下载");
        // 容量模式按钮文本
        Register("有限上传", "Limited Upload", "有限上传");
        Register("无限上传", "Infinite Upload", "无限上传");
    }

    private static int updateTick = 30;
    private static readonly int[] itemModSaveCount = new int[12000];

    public static void CalculateItemModSaveCount() {
        foreach (var item in LDB.items.dataArray) {
            if (item.BuildMode != 0) {
                //建筑10组
                itemModSaveCount[item.ID] = item.StackSize * 10;
            } else {
                //其他至多100组
                itemModSaveCount[item.ID] = (int)Math.Min(100000 / itemValue[item.ID] + 1, item.StackSize * 100);
            }
        }
    }

    private static readonly ConcurrentDictionary<StationComponent[], long> lastTickDic = [];

    // UI extension state for per-slot mode buttons and popups
    private enum ETransferMode {
        Sync = 0,
        Upload = 1,
        Download = 2
    }

    private enum ECapacityMode {
        Limited = 0,
        Infinite = 1
    }

    private static ConcurrentDictionary<long, ConcurrentDictionary<int, ETransferMode>> slotTransferMode =
        new();

    private static ConcurrentDictionary<long, ConcurrentDictionary<int, ECapacityMode>> slotCapacityMode =
        new();

    private static readonly Dictionary<RectTransform, float> windowOriginalWidth = [];
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalSize = [];
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalPosition = [];

    private static readonly Dictionary<RectTransform, float> inspectorOriginalPowerWidth = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalMasterWidth = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalTopGroupWidth = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalBgWidth = [];
    private static readonly Dictionary<RectTransform, float> filterOriginalStateGroupPosition = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalRightGroupWidth = [];
    private static bool inspectorRightGroupMissingLogged;

    private static readonly ConcurrentDictionary<UIStationStorage, bool> storageWidth = new();
    private static readonly ConcurrentDictionary<UIStationStorage, bool> storagePopup = new();

    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> transferGameObjects = new();
    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> capacityGameObjects = new();

    private static readonly Dictionary<RectTransform, float> controlPanelSdButtonOriginalPosition = [];
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, bool> controlPanelStoragePopup = new();
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject> controlPanelTransferGameObjects =
        new();
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject> controlPanelCapacityGameObjects =
        new();

    private const float ExtraSpacing = 12f;
    private const float BtnHeight = 26f;
    private const float BtnYOffset = 14f;
    private static float spacingX;
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsMyPopup = new();
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsTransfer = new();
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), RectTransform> slotPopupBoxRect =
        new();

    private const string ControlPanelLogPrefix = "[FE][ControlPanelStationInspector]";

    private static Vector2 GetOrCacheOriginal(Dictionary<RectTransform, Vector2> cache, RectTransform rect,
        Func<RectTransform, Vector2> getter) {
        if (cache.TryGetValue(rect, out Vector2 value)) {
            return value;
        }

        value = getter(rect);
        cache[rect] = value;
        return value;
    }

    private static float GetOrCacheOriginal(Dictionary<RectTransform, float> cache, RectTransform rect,
        Func<RectTransform, float> getter) {
        if (cache.TryGetValue(rect, out float value)) {
            return value;
        }

        value = getter(rect);
        cache[rect] = value;
        return value;
    }

    private static void AdjustSliders(UIStationWindow window, bool shouldWiden) {
        if (window == null) {
            return;
        }

        float delta = shouldWiden ? spacingX : 0f;

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
        foreach ((Slider slider, Component valueText) in controls) {
            if (slider == null) {
                continue;
            }

            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            if (sliderRect == null) {
                continue;
            }

            Vector2 originalSize = GetOrCacheOriginal(sliderOriginalSize, sliderRect, x => x.sizeDelta);
            Vector2 originalPosition = GetOrCacheOriginal(sliderOriginalPosition, sliderRect, x => x.anchoredPosition);

            sliderRect.sizeDelta = new Vector2(originalSize.x + delta, originalSize.y);
            sliderRect.anchoredPosition = new Vector2(originalPosition.x + delta, originalPosition.y);

            RectTransform valueRect = valueText?.GetComponent<RectTransform>();
            if (valueRect != null) {
                Vector2 originalValuePosition =
                    GetOrCacheOriginal(sliderOriginalPosition, valueRect, x => x.anchoredPosition);
                valueRect.anchoredPosition =
                    new Vector2(originalValuePosition.x + delta, originalValuePosition.y);
            }
        }
    }

    private static Transform FindControlPanelModeButtonTransform(UIControlPanelStationStorage storage, string buttonName) {
        if (storage == null) {
            return null;
        }

        Transform byStorage = storage.transform.Find(buttonName);
        if (byStorage != null) {
            return byStorage;
        }

        Transform parent = storage.localSdButton?.transform?.parent;
        return parent?.Find(buttonName);
    }

    private static float EnsureControlPanelSpacing(UIControlPanelStationInspector inspector) {
        if (spacingX > 0f) {
            return spacingX;
        }

        RectTransform rectTransform = inspector?.storageUIPrefab?.localSdButton?.GetComponent<RectTransform>();
        if (rectTransform != null) {
            spacingX = rectTransform.sizeDelta.x + ExtraSpacing;
        }

        return spacingX;
    }

    private static bool IsModStation(UIStationWindow window, StationComponent station) {
        if (window?.factory == null || station == null || station.entityId <= 0) {
            return false;
        }

        int buildingID = window.factory.entityPool[station.entityId].protoId;
        return buildingID is IFE行星内物流交互站 or IFE星际物流交互站;
    }

    private static void SetWindowWidenState(UIStationWindow window, bool shouldWiden) {
        if (window?.windowTrans == null) {
            return;
        }

        AdjustSliders(window, shouldWiden);
    }

    private static (long stationEntityId, int slotIndex) GetPopupStateKey(UIStationStorage storage) {
        if (storage == null) {
            return (0L, -1);
        }

        long stationEntityId = storage.station != null ? storage.station.entityId : 0L;
        return (stationEntityId, storage.index);
    }

    private static (long stationEntityId, int slotIndex) GetPopupStateKey(UIControlPanelStationStorage storage) {
        if (storage == null) {
            return (0L, -1);
        }

        long stationEntityId = storage.station != null ? storage.station.entityId : 0L;
        return (stationEntityId, storage.index);
    }

    private static bool GetPopupFlag(ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> stateDictionary,
        (long stationEntityId, int slotIndex) key) {
        return stateDictionary.TryGetValue(key, out bool value) && value;
    }

    private static void ClearPopupState((long stationEntityId, int slotIndex) key) {
        slotIsMyPopup.TryRemove(key, out _);
        slotIsTransfer.TryRemove(key, out _);
        slotPopupBoxRect.TryRemove(key, out _);
    }

    public static void Clear() {
        slotTransferMode.Clear();
        slotCapacityMode.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.GameTick))]
    public static void PlanetTransportGameTickPostPatch(PlanetTransport __instance, long time) {
        // 30帧更新一次，取3作为特殊值
        if (time % updateTick != 3L) {
            return;
        }

        try {
            // 避免同一帧内多次处理
            if (lastTickDic.TryGetValue(__instance.stationPool, out long lastTick) && lastTick == time) {
                return;
            }

            lastTickDic[__instance.stationPool] = time;
            // 获取所有的交互站后，执行随机排序，让每个塔都有机会拿到物品
            List<StationComponent> stations = [];
            for (int index = 1; index < __instance.stationCursor; ++index) {
                StationComponent stationComponent = __instance.stationPool[index];
                if (stationComponent == null || stationComponent.id != index) {
                    continue;
                }

                int buildingID = __instance.factory.entityPool[stationComponent.entityId].protoId;
                if (buildingID == IFE行星内物流交互站 || buildingID == IFE星际物流交互站) {
                    stations.Add(stationComponent);
                }
            }

            // 使用 Fisher-Yates 洗牌算法进行真正的随机排序
            for (int i = stations.Count - 1; i > 0; i--) {
                int j = GetRandInt(0, i + 1);
                (stations[i], stations[j]) = (stations[j], stations[i]);
            }

            // 循环所有的交互站
            float downloadThreshold = Miscellaneous.DownloadThreshold;
            float uploadThreshold = Miscellaneous.UploadThreshold;
            foreach (StationComponent stationComponent in stations) {
                // 单个槽位可用最大电量
                long maxSlotEnergy = stationComponent.energy / stationComponent.storage.Length;
                long entityId = stationComponent.entityId;

                // 获取该交互站的槽位模式字典
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(entityId, new ConcurrentDictionary<int, ETransferMode>());
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(entityId, new ConcurrentDictionary<int, ECapacityMode>());

                // 循环交互站的所有的栏位
                for (int i = 0; i < stationComponent.storage.Length; i++) {
                    // StationStore为struct，必须使用引用修改内容
                    ref StationStore store = ref stationComponent.storage[i];
                    if (store.itemId <= 0 || itemValue[store.itemId] >= maxValue) {
                        continue;
                    }

                    // 获取该槽位的传输模式和容量模式
                    ETransferMode transferMode = transferDictionary.GetOrAdd(i, ETransferMode.Sync);
                    ECapacityMode capacityMode = capacityDictionary.GetOrAdd(i, ECapacityMode.Limited);

                    // 根据传输模式决定行为
                    switch (transferMode) {
                        case ETransferMode.Upload: {
                            // 仅上传模式：交互站 -> 数据中心
                            // 当交互站数量超过上传阈值时，将超出部分上传到数据中心
                            if (store.count > store.max * uploadThreshold) {
                                int modTargetCount = itemModSaveCount[store.itemId];
                                long modCurrCount = GetModDataItemCount(store.itemId);

                                // 计算超出阈值部分的物品数量
                                int transferCount = store.count - (int)(store.max * uploadThreshold);
                                // 有限上传模式下，受数据中心目标数量限制
                                if (capacityMode == ECapacityMode.Limited && modCurrCount < modTargetCount) {
                                    transferCount = Math.Min(transferCount, modTargetCount - (int)modCurrCount);
                                } else if (capacityMode == ECapacityMode.Limited && modCurrCount >= modTargetCount) {
                                    // 有限上传且数据中心已达目标数量，停止上传
                                    break;
                                }

                                if (transferCount > 0) {
                                    stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy);
                                }
                            }
                            break;
                        }
                        case ETransferMode.Download: {
                            // 仅下载模式：数据中心 -> 交互站
                            // 当总供应量低于下载阈值时，从数据中心下载至阈值数量
                            if (store.totalSupplyCount < store.max * downloadThreshold) {
                                // 使用四舍五入避免浮点精度问题（如10000*0.2=1999.999...->2000）
                                int targetCount = Mathf.RoundToInt(store.max * downloadThreshold);
                                // 目标数量不能低于当前数量
                                if (targetCount < store.count) {
                                    targetCount = store.count;
                                }
                                stationComponent.SetTargetCount(i, targetCount, maxSlotEnergy);
                            }
                            break;
                        }
                        case ETransferMode.Sync:
                        default: {
                            // 双向同步模式
                            // 优先处理上传，再处理下载
                            if (store.count > store.max * uploadThreshold) {
                                // 数量高于上传阈值，上传超出部分，尊重容量模式
                                int transferCount = store.count - (int)(store.max * uploadThreshold);
                                if (capacityMode == ECapacityMode.Limited) {
                                    // 有限上传：受数据中心目标数量限制
                                    int modTargetCount = itemModSaveCount[store.itemId];
                                    long modCurrCount = GetModDataItemCount(store.itemId);
                                    if (modCurrCount < modTargetCount) {
                                        transferCount = Math.Min(transferCount, modTargetCount - (int)modCurrCount);
                                    } else {
                                        // 数据中心已达目标数量，不执行上传
                                        transferCount = 0;
                                    }
                                }
                                // 无限上传时不限制数量
                                if (transferCount > 0) {
                                    stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy);
                                }
                            } else if (store.totalSupplyCount < store.max * downloadThreshold) {
                                // 数量低于下载阈值，下载数量至阈值
                                // 使用四舍五入避免浮点精度问题
                                int targetCount = Mathf.RoundToInt(store.max * downloadThreshold);
                                if (targetCount < store.count) {
                                    targetCount = store.count;
                                }
                                stationComponent.SetTargetCount(i, targetCount, maxSlotEnergy);
                            }
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex) {
            LogError(ex);
        }
    }

    private static void SetTargetCount(this StationComponent stationComponent, int index, int targetCount,
        long maxSlotEnergy) {
        ref StationStore store = ref stationComponent.storage[index];
        try {
            if (store.count == targetCount || itemValue[store.itemId] == float.MaxValue) {
                return;
            }

            ItemProto itemProto = LDB.items.Select(IFE行星内物流交互站);
            // 物品价值(100价值=1000000J=1MJ，即每1价值，耗电10000J)
            float cost = (float)Math.Sqrt(itemValue[store.itemId]) * 10000 * itemProto.InteractEnergyRatio();
            if (store.count < targetCount) {
                // 将数据中心的物品下载到交互站
                int count = targetCount - store.count;
                // 总耗电大于剩余电量，修改数量
                if (cost * count > maxSlotEnergy) {
                    // 1个都玩不起直接放弃
                    if (cost > maxSlotEnergy) {
                        return;
                    }

                    count = Mathf.FloorToInt(maxSlotEnergy / cost);
                }

                count = TakeItemFromModData(store.itemId, count, out int inc);
                store.count += count;
                store.inc += inc;
                stationComponent.energy -= Mathf.CeilToInt(cost * count);
            } else {
                // 将交互站的物品上传到数据中心
                int count = store.count - targetCount;
                // 总耗电大于剩余电量，修改数量
                if (cost * count > maxSlotEnergy) {
                    // 1个都玩不起直接放弃
                    if (cost > maxSlotEnergy) {
                        return;
                    }

                    count = Mathf.FloorToInt(maxSlotEnergy / cost);
                }

                int inc = store.count <= 0 ? 0 : split_inc(ref store.count, ref store.inc, count);
                AddItemToModData(store.itemId, count, inc);
                stationComponent.energy -= Mathf.CeilToInt(cost * count);
            }
        }
        finally {
            if (PlanetaryInteractionStation.Level >= 3) {
                AddIncToItem(store.count, ref store.inc);
            }
        }
    }


    /**
     * 给所有的塔新增两个按钮
     */
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnCreate))]
    public static void UIStationWindow_OnCreate_Postfix(UIStationWindow __instance) {
        if (__instance?.windowTrans != null) {
            GetOrCacheOriginal(windowOriginalWidth, __instance.windowTrans, x => x.sizeDelta.x);
        }

        for (int index = 0; index < 6; ++index) {
            UIStationStorage storage = __instance.storageUIs[index];
            // --- 新增两个模式按钮（Transfer / Capacity） ---
            try {
                // 原版本地物流模式的按钮
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                // 原版按钮的宽度
                float btnWidth = refRect.sizeDelta.x;
                // 按钮的宽度+间隔=外面盒子需要增加的宽度
                spacingX = btnWidth + ExtraSpacing;
                // 创建交互模式按钮
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
                string cName = "FE_capacityModeButton_" + index;
                Transform cTrans = storage.transform.Find(cName);
                if (cTrans == null) {
                    GameObject go =
                        GameObject.Instantiate(storage.localSdButton.gameObject,
                            storage.localSdButton.transform.parent, false);
                    go.name = cName;
                    capacityGameObjects.TryAdd(storage, go);
                }

                storage.popupBoxRect.SetSiblingIndex(storage.popupBoxRect.GetSiblingIndex() + 10);
            }
            catch (Exception ex) {
                LogError($"FE StationManager: create mode buttons failed: {ex}");
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnDestroy))]
    public static void UIStationWindow__OnDestroy_Prefix(UIStationWindow __instance) {
        windowOriginalWidth.Clear();
        sliderOriginalSize.Clear();
        sliderOriginalPosition.Clear();
        storageWidth.Clear();
        storagePopup.Clear();
        transferGameObjects.Clear();
        capacityGameObjects.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector._OnCreate))]
    public static void UIControlPanelStationInspector_OnCreate_Postfix(UIControlPanelStationInspector __instance) {
        if (__instance?.storageUIs == null) {
            Debug.Log($"{ControlPanelLogPrefix} _OnCreate skipped: storageUIs is null");
            return;
        }

        for (int index = 0; index < __instance.storageUIs.Length; ++index) {
            UIControlPanelStationStorage storage = __instance.storageUIs[index];
            if (storage?.localSdButton == null) {
                Debug.Log($"{ControlPanelLogPrefix} slot {index} invalid: storage={storage}, localSdButton={storage?.localSdButton}");
                continue;
            }

            try {
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                if (refRect != null) {
                    spacingX = refRect.sizeDelta.x + ExtraSpacing;
                } else {
                    Debug.Log($"{ControlPanelLogPrefix} slot {index} localSdButton rectTransform is null");
                }

                string transferName = "FE_cp_transferModeButton_" + index;
                Transform transferTrans = FindControlPanelModeButtonTransform(storage, transferName);
                if (transferTrans == null) {
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = transferName;
                    Button transferBtn = go.GetComponent<Button>();
                    transferBtn?.onClick.RemoveAllListeners();
                    go.SetActive(true);
                    controlPanelTransferGameObjects[storage] = go;
                } else {
                    controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
                    Debug.Log($"{ControlPanelLogPrefix} slot {index} transfer button found, parent={transferTrans.parent?.name}");
                }

                string capacityName = "FE_cp_capacityModeButton_" + index;
                Transform capacityTrans = FindControlPanelModeButtonTransform(storage, capacityName);
                if (capacityTrans == null) {
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = capacityName;
                    Button capacityBtn = go.GetComponent<Button>();
                    capacityBtn?.onClick.RemoveAllListeners();
                    go.SetActive(true);
                    controlPanelCapacityGameObjects[storage] = go;
                } else {
                    controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
                    Debug.Log($"{ControlPanelLogPrefix} slot {index} capacity button found, parent={capacityTrans.parent?.name}");
                }

                if (storage.popupBoxRect != null) {
                    storage.popupBoxRect.SetSiblingIndex(storage.popupBoxRect.GetSiblingIndex() + 10);
                } else {
                    Debug.Log($"{ControlPanelLogPrefix} slot {index} popupBoxRect is null");
                }
            }
            catch (Exception ex) {
                LogError($"FE StationManager: create control panel mode buttons failed: {ex}");
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector._OnDestroy))]
    public static void UIControlPanelStationInspector_OnDestroy_Prefix(UIControlPanelStationInspector __instance) {
        controlPanelSdButtonOriginalPosition.Clear();
        inspectorOriginalTopGroupWidth.Clear();
        inspectorOriginalBgWidth.Clear();
        controlPanelStoragePopup.Clear();
        controlPanelTransferGameObjects.Clear();
        controlPanelCapacityGameObjects.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage._OnOpen))]
    public static void UIControlPanelStationStorage_OnOpen_Postfix(UIControlPanelStationStorage __instance) {
        string transferName = "FE_cp_transferModeButton_" + __instance.index;
        Transform transferTrans = FindControlPanelModeButtonTransform(__instance, transferName);
            if (transferTrans != null) {
                Button transferBtn = transferTrans.GetComponent<Button>();
                transferBtn?.onClick.RemoveAllListeners();
                transferBtn.onClick.AddListener(() => ShowTransferPopup(__instance));
                transferTrans.gameObject.SetActive(true);
                controlPanelTransferGameObjects[__instance] = transferTrans.gameObject;
            } else {
                Debug.Log($"{ControlPanelLogPrefix} _OnOpen slot {__instance.index} transfer button not found");
            }

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
            if (capacityTrans != null) {
                Button capBtn = capacityTrans.GetComponent<Button>();
                capBtn?.onClick.RemoveAllListeners();
                capBtn.onClick.AddListener(() => ShowCapacityPopup(__instance));
                capacityTrans.gameObject.SetActive(true);
                controlPanelCapacityGameObjects[__instance] = capacityTrans.gameObject;
            } else {
                Debug.Log($"{ControlPanelLogPrefix} _OnOpen slot {__instance.index} capacity button not found");
            }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage._OnClose))]
    public static void UIControlPanelStationStorage_OnClose_Postfix(UIControlPanelStationStorage __instance) {
        string transferName = "FE_cp_transferModeButton_" + __instance.index;
        Transform transferTrans = FindControlPanelModeButtonTransform(__instance, transferName);
        if (transferTrans != null) {
            Button transferBtn = transferTrans.GetComponent<Button>();
            transferBtn.onClick.RemoveAllListeners();
        }

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
        if (capacityTrans != null) {
            Button capBtn = capacityTrans.GetComponent<Button>();
            capBtn.onClick.RemoveAllListeners();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.RefreshValues))]
    public static void UIControlPanelStationStorage_RefreshValues_Postfix(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        if (storage?.station == null || storage.factory == null || storage.masterInspector == null) {
            return;
        }

        int buildingID = storage.factory.entityPool[storage.station.entityId].protoId;
        bool isModStation = buildingID is IFE行星内物流交互站 or IFE星际物流交互站;
        bool isInfoTab = storage.masterInspector.currentTabPanel == EUIControlPanelStationPanel.Info;
        if (!isModStation || !isInfoTab) {
            RestoreControlPanelStorageSlot(storage);
            return;
        }

        RectTransform localRect = storage.localSdButton?.GetComponent<RectTransform>();
        RectTransform remoteRect = storage.remoteSdButton?.GetComponent<RectTransform>();
        if (localRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(localRect)) {
                controlPanelSdButtonOriginalPosition[localRect] = localRect.anchoredPosition.x;
            }

            float localOriginal = controlPanelSdButtonOriginalPosition[localRect];
            localRect.anchoredPosition = new Vector2(localOriginal - spacingX, localRect.anchoredPosition.y);
        }

        if (remoteRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(remoteRect)) {
                controlPanelSdButtonOriginalPosition[remoteRect] = remoteRect.anchoredPosition.x;
            }

            float remoteOriginal = controlPanelSdButtonOriginalPosition[remoteRect];
            remoteRect.anchoredPosition = new Vector2(remoteOriginal - spacingX, remoteRect.anchoredPosition.y);
        }

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

        if (!controlPanelTransferGameObjects.TryGetValue(storage, out _)) {
            Transform transferTrans = FindControlPanelModeButtonTransform(storage, "FE_cp_transferModeButton_" + storage.index);
            if (transferTrans != null) {
                controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
                Debug.Log($"{ControlPanelLogPrefix} slot {storage.index} transfer button recovered in RefreshValues");
            }
        }

        if (!controlPanelCapacityGameObjects.TryGetValue(storage, out _)) {
            Transform capacityTrans = FindControlPanelModeButtonTransform(storage, "FE_cp_capacityModeButton_" + storage.index);
            if (capacityTrans != null) {
                controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
                Debug.Log($"{ControlPanelLogPrefix} slot {storage.index} capacity button recovered in RefreshValues");
            }
        }

        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ETransferMode>());
        ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        if (controlPanelTransferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
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
            transferRt.anchoredPosition = new Vector2(localRect.anchoredPosition.x + spacingX, topY);
            transferRt.sizeDelta = new Vector2(transferRt.sizeDelta.x, BtnHeight);
        }

        if (controlPanelCapacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
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
            capRt.anchoredPosition = new Vector2(localRect.anchoredPosition.x + spacingX, bottomY);
            capRt.sizeDelta = new Vector2(capRt.sizeDelta.x, BtnHeight);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnLocalSdButtonClick))]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnRemoteSdButtonClick))]
    public static void UIControlPanelStationStorage_OnSdButtonClick_Prefix(UIControlPanelStationStorage __instance) {
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(__instance);
        ClearPopupState(popupStateKey);
        if (controlPanelStoragePopup.ContainsKey(__instance)) {
            __instance.popupBoxRect.anchoredPosition =
                new Vector2(__instance.popupBoxRect.anchoredPosition.x - spacingX,
                    __instance.popupBoxRect.anchoredPosition.y);
            controlPanelStoragePopup.TryRemove(__instance, out _);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton0Click))]
    public static bool UIControlPanelStationStorage_OnOptionButton0Click_Prefix(UIControlPanelStationStorage __instance) {
        return HandleOptionClick(__instance, 0);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton1Click))]
    public static bool UIControlPanelStationStorage_OnOptionButton1Click_Prefix(UIControlPanelStationStorage __instance) {
        return HandleOptionClick(__instance, 1);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnOpen))]
    public static void UIStationStorage__OnOpen_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = __instance.transform.Find(tName);
        if (tTrans != null) {
            Button transferBtn = tTrans.GetComponent<Button>();
            transferBtn.onClick.AddListener(() => ShowTransferPopup(__instance));
        }

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = __instance.transform.Find(cName);
        if (cTrans != null) {
            Button capBtn = cTrans.GetComponent<Button>();
            capBtn.onClick.AddListener(() => ShowCapacityPopup(__instance));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnClose))]
    public static void UIStationStorage__OnClose_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = __instance.transform.Find(tName);
        if (tTrans != null) {
            Button transferBtn = tTrans.GetComponent<Button>();
            transferBtn.onClick.RemoveAllListeners();
        }

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = __instance.transform.Find(cName);
        if (cTrans != null) {
            Button capBtn = cTrans.GetComponent<Button>();
            capBtn.onClick.RemoveAllListeners();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
    public static void UIStationStorage_RefreshValues_Postfix(UIStationStorage __instance) {
        // 重命名
        UIStationStorage storage = __instance;
        // 获取塔对应的物品ID
        int buildingID = storage.stationWindow.factory.entityPool[storage.station.entityId].protoId;

        // 栏位本身
        RectTransform rectTransform = storage.transform.GetComponent<RectTransform>();
        // 如果不是自定义的塔
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
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

        // 原版本地物流模式的按钮
        RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();

        // 通过实体ID拿到整个塔的同步模式
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ETransferMode>());

        // 通过栏位索引，拿到对应栏位的同步模式
        ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);

        if (transferGameObjects.TryGetValue(storage, out GameObject tTrans)) {
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


    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnUpdate))]
    public static void UIStationWindow_OnUpdate_Prefix(UIStationWindow __instance) {
        // 重命名
        UIStationWindow stationWindow = __instance;
        StationComponent station = stationWindow.transport.stationPool[stationWindow.stationId];
        // 获取塔对应的物品ID
        int buildingID = stationWindow.factory.entityPool[station.entityId].protoId;
        // 如果不是自定义的塔
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
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

    /// <summary>
    /// 修改物流交互站的面板
    /// </summary>
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
        // 修改集装输出的可选上限
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxStack = building.MaxStack();
        // 获取集装输出的当前上限
        __instance.minPilerSlider.maxValue = maxStack;
        int pilerCount = station.pilerCount;
        if (pilerCount == 0) {
            // 自动，设置为上限
            __instance.minPilerSlider.value = maxStack;
            __instance.minPilerValue.text = maxStack.ToString();
        } else {
            // 手动，设置为当前值
            __instance.minPilerSlider.value = pilerCount;
            __instance.minPilerValue.text = pilerCount.ToString();
        }

        if (maxStack > 1) {
            // 堆叠上限大于1，显示修改滑条
            __instance.minPilerGroup.gameObject.SetActive(true);
            __instance.pilerTechGroup.gameObject.SetActive(true);
        }

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

    // Show transfer-mode popup shifted to the right
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

        // 通过实体ID拿到整个塔的同步模式
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());

        // 通过栏位索引，拿到对应栏位的同步模式
        ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
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
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = true;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        if (!storagePopup.ContainsKey(storage)) {
            // 原版本地物流模式的按钮
            storage.popupBoxRect.anchoredPosition =
                new Vector2(storage.popupBoxRect.anchoredPosition.x + spacingX,
                    storage.popupBoxRect.anchoredPosition.y);
            storagePopup.TryAdd(storage, true);
        }
    }

    // Show capacity-mode popup shifted to the right
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

        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = storage.demandColor;
        storage.optionImage1.color = storage.supplyColor;
        storage.optionText0.text = "无限上传".Translate();
        storage.optionText1.text = "有限上传".Translate();
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = false;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        if (!storagePopup.ContainsKey(storage)) {
            storage.popupBoxRect.anchoredPosition =
                new Vector2(storage.popupBoxRect.anchoredPosition.x + spacingX,
                    storage.popupBoxRect.anchoredPosition.y);
            storagePopup.TryAdd(storage, true);
        }
    }

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
            Debug.Log($"{ControlPanelLogPrefix} ShowTransferPopup aborted: itemProto1={itemProto1}, itemProto2={itemProto2}");
            return;
        }

        if (storage.popupBoxRect == null) {
            Debug.Log($"{ControlPanelLogPrefix} ShowTransferPopup aborted: popupBoxRect is null");
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
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = true;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        if (!controlPanelStoragePopup.ContainsKey(storage)) {
            storage.popupBoxRect.anchoredPosition =
                new Vector2(storage.popupBoxRect.anchoredPosition.x + spacingX,
                    storage.popupBoxRect.anchoredPosition.y);
            controlPanelStoragePopup.TryAdd(storage, true);
        }
    }

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
            Debug.Log($"{ControlPanelLogPrefix} ShowCapacityPopup aborted: itemProto1={itemProto1}, itemProto2={itemProto2}");
            return;
        }

        if (storage.popupBoxRect == null) {
            Debug.Log($"{ControlPanelLogPrefix} ShowCapacityPopup aborted: popupBoxRect is null");
            return;
        }

        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = storage.masterInspector.storageDemandColor;
        storage.optionImage1.color = storage.masterInspector.storageSupplyColor;
        storage.optionText0.text = "无限上传".Translate();
        storage.optionText1.text = "有限上传".Translate();
        storage.popupBoxRect.gameObject.SetActive(!storage.popupBoxRect.gameObject.activeSelf);
        bool isPopupActive = storage.popupBoxRect.gameObject.activeSelf;
        slotIsMyPopup[popupStateKey] = isPopupActive;
        slotIsTransfer[popupStateKey] = false;
        slotPopupBoxRect[popupStateKey] = storage.popupBoxRect;
        if (!controlPanelStoragePopup.ContainsKey(storage)) {
            storage.popupBoxRect.anchoredPosition =
                new Vector2(storage.popupBoxRect.anchoredPosition.x + spacingX,
                    storage.popupBoxRect.anchoredPosition.y);
            controlPanelStoragePopup.TryAdd(storage, true);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnLocalSdButtonClick))]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnRemoteSdButtonClick))]
    public static void UIStationStorage_OnSdButtonClick_Prefix(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        ClearPopupState(popupStateKey);
        if (storagePopup.ContainsKey(storage)) {
            storage.popupBoxRect.anchoredPosition =
                new Vector2(storage.popupBoxRect.anchoredPosition.x - spacingX,
                    storage.popupBoxRect.anchoredPosition.y);
            storagePopup.TryRemove(storage, out _);
        }
    }

    // Intercept option clicks when our popup is active
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnOptionButton0Click))]
    public static bool UIStationStorage_OnOptionButton0Click_Prefix(UIStationStorage __instance) {
        return HandleOptionClick(__instance, 0);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnOptionButton1Click))]
    public static bool UIStationStorage_OnOptionButton1Click_Prefix(UIStationStorage __instance) {
        return HandleOptionClick(__instance, 1);
    }

    private static bool HandleOptionClick(UIStationStorage __instance, int idx) {
        try {
            UIStationStorage storage = __instance;
            (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
            if (!GetPopupFlag(slotIsMyPopup, popupStateKey)) {
                return true;
            }

            StationComponent station = __instance.station;
            UIStationWindow stationWindow = __instance.stationWindow;
            if (storage.popupBoxRect == null
                || !slotPopupBoxRect.TryGetValue(popupStateKey, out RectTransform popupRect)
                || popupRect != storage.popupBoxRect
                || !storage.popupBoxRect.gameObject.activeSelf) {
                return true;
            }

            StationStore stationStore = new StationStore();
            if (station != null && storage.index < station.storage.Length)
                stationStore = station.storage[storage.index];
            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = LDB.items.Select((int)stationWindow.factory.entityPool[station.entityId].protoId);
            if (itemProto1 == null || itemProto2 == null)
                return false;
            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode eTransferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                if (idx == 0) {
                    transferDictionary.TryUpdate(storage.index, eTransferMode switch {
                        ETransferMode.Sync => ETransferMode.Upload,
                        ETransferMode.Upload => ETransferMode.Download,
                        _ => ETransferMode.Sync
                    }, eTransferMode);
                } else if (idx == 1) {
                    transferDictionary.TryUpdate(storage.index, eTransferMode switch {
                        ETransferMode.Sync => ETransferMode.Download,
                        ETransferMode.Upload => ETransferMode.Sync,
                        _ => ETransferMode.Upload
                    }, eTransferMode);
                }
            } else {
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
                ECapacityMode eCapacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
                if (idx == 0) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Infinite, eCapacityMode);
                } else if (idx == 1) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Limited, eCapacityMode);
                }
            }

            storage.popupBoxRect.gameObject.SetActive(false);
            slotIsMyPopup[popupStateKey] = false;
            return false;// prevent original OnOptionButton* handlers
        }
        catch (Exception ex) {
            LogError($"FE HandleOptionClick error: {ex}");
            return true;
        }
    }

    private static bool HandleOptionClick(UIControlPanelStationStorage __instance, int idx) {
        try {
            UIControlPanelStationStorage storage = __instance;
            (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
            if (!GetPopupFlag(slotIsMyPopup, popupStateKey)) {
                return true;
            }

            StationComponent station = __instance.station;
            if (storage.popupBoxRect == null
                || !slotPopupBoxRect.TryGetValue(popupStateKey, out RectTransform popupRect)
                || popupRect != storage.popupBoxRect
                || !storage.popupBoxRect.gameObject.activeSelf) {
                return true;
            }

            StationStore stationStore = new StationStore();
            if (station != null && storage.index < station.storage.Length) {
                stationStore = station.storage[storage.index];
            }

            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = station != null && storage.factory != null
                ? LDB.items.Select(storage.factory.entityPool[station.entityId].protoId)
                : null;
            if (itemProto1 == null || itemProto2 == null) {
                return false;
            }

            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                if (idx == 0) {
                    transferDictionary.TryUpdate(storage.index, transferMode switch {
                        ETransferMode.Sync => ETransferMode.Upload,
                        ETransferMode.Upload => ETransferMode.Download,
                        _ => ETransferMode.Sync
                    }, transferMode);
                } else if (idx == 1) {
                    transferDictionary.TryUpdate(storage.index, transferMode switch {
                        ETransferMode.Sync => ETransferMode.Download,
                        ETransferMode.Upload => ETransferMode.Sync,
                        _ => ETransferMode.Upload
                    }, transferMode);
                }
            } else {
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
                ECapacityMode capacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
                if (idx == 0) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Infinite, capacityMode);
                } else if (idx == 1) {
                    capacityDictionary.TryUpdate(storage.index, ECapacityMode.Limited, capacityMode);
                }
            }

            storage.popupBoxRect.gameObject.SetActive(false);
            slotIsMyPopup[popupStateKey] = false;
            return false;
        }
        catch (Exception ex) {
            LogError($"FE HandleOptionClick(UIControlPanelStationStorage) error: {ex}");
            return true;
        }
    }

    /// <summary>
    /// 修改物流交互站的面板（新加的面板）
    /// 内容同上面那个一摸一样，唯一的不同是参数类型，怎么合并一下
    /// </summary>
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
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            // 还原，避免不关窗口直接切换的时候显示错误
            text.text = "  使用科技上限";
            __instance.event_lock = false;
            return;
        }

        text.text = "  使用强化上限";
        // 修改集装输出的可选上限
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxStack = building.MaxStack();
        // 获取集装输出的当前上限
        __instance.minPilerSlider.maxValue = maxStack;
        int pilerCount = station.pilerCount;
        if (pilerCount == 0) {
            // 自动，设置为上限
            __instance.minPilerSlider.value = maxStack;
            __instance.minPilerValue.text = maxStack.ToString();
        } else {
            // 手动，设置为当前值
            __instance.minPilerSlider.value = pilerCount;
            __instance.minPilerValue.text = pilerCount.ToString();
        }

        if (maxStack > 1) {
            // 堆叠上限大于1，显示修改滑条
            __instance.minPilerGroup.gameObject.SetActive(true);
            __instance.pilerTechGroup.gameObject.SetActive(true);
        }

        __instance.event_lock = false;
    }

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

        RectTransform rightGroupRect = rightGroupTransform as RectTransform;
        if (rightGroupRect == null && !inspectorRightGroupMissingLogged) {
            string path = inspector.masterWindow != null
                ? inspector.masterWindow.name
                : inspector.gameObject.name;
            Debug.Log($"[FractionateEverything] right-group not found or path: {path}");
            inspectorRightGroupMissingLogged = true;
        }

        return rightGroupRect;
    }

    private static void RestoreInspectorRightGroup(RectTransform rightGroupRect) {
        if (rightGroupRect == null) {
            return;
        }

        if (inspectorOriginalRightGroupWidth.TryGetValue(rightGroupRect, out float originalWidth)) {
            rightGroupRect.sizeDelta = new Vector2(originalWidth, rightGroupRect.sizeDelta.y);
        }
    }

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

        if (controlPanelStoragePopup.ContainsKey(storage) && storage.popupBoxRect != null) {
            storage.popupBoxRect.anchoredPosition =
                new Vector2(storage.popupBoxRect.anchoredPosition.x, storage.popupBoxRect.anchoredPosition.y);
            controlPanelStoragePopup.TryRemove(storage, out _);
        }

        ClearPopupState(GetPopupStateKey(storage));

        if (controlPanelTransferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
            transferGO.SetActive(false);
        }

        if (controlPanelCapacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
            capacityGO.SetActive(false);
        }
    }

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
        // 只处理物流交互站
        if (__instance.stationId == 0 || __instance.factory == null) {
            return;
        }

        StationComponent station = __instance.transport?.stationPool[__instance.stationId];
        if (station == null || station.id != __instance.stationId) {
            return;
        }

        EnsureControlPanelSpacing(__instance);

        int buildingID = __instance.factory.entityPool[station.entityId].protoId;
        RectTransform rightGroupRect = FindInspectorRightGroupRect(__instance);
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            if (__instance.storageUIs != null) {
                foreach (UIControlPanelStationStorage storage in __instance.storageUIs) {
                    RestoreControlPanelStorageSlot(storage);
                }
            }

            RestoreInspectorStorageWidths(__instance);

            RectTransform stateGroupTransRestore = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
            if (stateGroupTransRestore != null
                && filterOriginalStateGroupPosition.TryGetValue(stateGroupTransRestore, out float stateGroupX)) {
                stateGroupTransRestore.anchoredPosition =
                    new Vector2(stateGroupX, stateGroupTransRestore.anchoredPosition.y);
            }

            RectTransform masterRectRestore = __instance.masterWindow != null
                ? __instance.masterWindow.transform as RectTransform
                : null;
            if (masterRectRestore != null
                && inspectorOriginalMasterWidth.TryGetValue(masterRectRestore, out float masterWidth)) {
                masterRectRestore.sizeDelta = new Vector2(masterWidth, masterRectRestore.sizeDelta.y);
            }

            RestoreInspectorRightGroup(rightGroupRect);

            return;
        }

        // 只在信息页签生效
        if (__instance.currentTabPanel != EUIControlPanelStationPanel.Info) {
            if (__instance.storageUIs != null) {
                foreach (UIControlPanelStationStorage storage in __instance.storageUIs) {
                    RestoreControlPanelStorageSlot(storage);
                }
            }

            RestoreInspectorStorageWidths(__instance);

            RectTransform stateGroupTransRestore = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
            if (stateGroupTransRestore != null
                && filterOriginalStateGroupPosition.TryGetValue(stateGroupTransRestore, out float stateGroupX)) {
                stateGroupTransRestore.anchoredPosition =
                    new Vector2(stateGroupX, stateGroupTransRestore.anchoredPosition.y);
            }

            RectTransform masterRectRestore = __instance.masterWindow != null
                ? __instance.masterWindow.transform as RectTransform
                : null;
            if (masterRectRestore != null
                && inspectorOriginalMasterWidth.TryGetValue(masterRectRestore, out float masterWidth)) {
                masterRectRestore.sizeDelta = new Vector2(masterWidth, masterRectRestore.sizeDelta.y);
            }

            RestoreInspectorRightGroup(rightGroupRect);

            return;
        }

        WidenInspectorStorageWidths(__instance);

        // 是否解锁曲速
        bool logisticShipWarpDrive = GameMain.history.logisticShipWarpDrive;

        RectTransform powerRect = __instance.powerGroupRect;

        float basePowerWidth = station.isStellar
            ? (logisticShipWarpDrive ? 320f : 380f)
            : 440f;
        powerRect.sizeDelta = new Vector2(basePowerWidth + spacingX, 40f);

        // 调整能量数值的位置（基于原始长度+spacingX）
        float num2 = (float)station.energy / (float)station.energyMax;
        float num4 = (station.isStellar ? (logisticShipWarpDrive ? 180f : 240f) : 300f) + spacingX;

        if (num2 > 0.7f) {
            __instance.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round(num4 * num2 - 30f), 0f);
        } else {
            __instance.energyText.rectTransform.anchoredPosition =
                new Vector2(Mathf.Round(num4 * num2 + 30f), 0f);
        }

        RectTransform masterRect = __instance.masterWindow != null
            ? __instance.masterWindow.transform as RectTransform
            : null;
        if (masterRect != null) {
            if (!inspectorOriginalMasterWidth.ContainsKey(masterRect)) {
                inspectorOriginalMasterWidth[masterRect] = masterRect.sizeDelta.x;
            }
            masterRect.sizeDelta = new Vector2(inspectorOriginalMasterWidth[masterRect] + spacingX, masterRect.sizeDelta.y);
        }

        RectTransform stateGroupTrans = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
        if (stateGroupTrans != null) {
            if (!filterOriginalStateGroupPosition.ContainsKey(stateGroupTrans)) {
                filterOriginalStateGroupPosition[stateGroupTrans] = stateGroupTrans.anchoredPosition.x;
            }

            stateGroupTrans.anchoredPosition =
                new Vector2(filterOriginalStateGroupPosition[stateGroupTrans] + spacingX, stateGroupTrans.anchoredPosition.y);
        }

        if (rightGroupRect != null) {
            if (!inspectorOriginalRightGroupWidth.ContainsKey(rightGroupRect)) {
                inspectorOriginalRightGroupWidth[rightGroupRect] = rightGroupRect.sizeDelta.x;
            }

            rightGroupRect.sizeDelta = new Vector2(
                inspectorOriginalRightGroupWidth[rightGroupRect] + spacingX, rightGroupRect.sizeDelta.y);
        }
    }

    /// <summary>
    /// 拖动集装数滑条时（说明必然没有使用当前上限），修改物流交互站的集装数
    /// </summary>
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
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            return true;
        }

        // 不是自动
        if (!__instance.techPilerCheck.enabled) {
            ItemProto building = LDB.items.Select(IFE行星内物流交互站);
            int maxStack = building.MaxStack();
            int newVal = Mathf.RoundToInt(value);
            // 如果修改之后的值超过上限，设为上限
            if (newVal > maxStack) {
                newVal = maxStack;
            }

            __instance.transport.stationPool[__instance.stationId].pilerCount = newVal;
            __instance.minPilerValue.text = newVal.ToString();
        }

        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>
    /// 修改物流交互站的集装数
    /// 内容同上面那个一摸一样，唯一的不同是参数类型，怎么合并一下
    /// </summary>
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
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            return true;
        }

        // 不是自动
        if (!__instance.techPilerCheck.enabled) {
            ItemProto building = LDB.items.Select(IFE行星内物流交互站);
            int maxStack = building.MaxStack();
            int newVal = Mathf.RoundToInt(value);
            // 如果修改之后的值超过上限，设为上限
            if (newVal > maxStack) {
                newVal = maxStack;
            }

            __instance.transport.stationPool[__instance.stationId].pilerCount = newVal;
            __instance.minPilerValue.text = newVal.ToString();
        }

        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>
    /// 修改物流交互站的集装使用强化上限
    /// </summary>
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
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            return true;
        }

        __instance.techPilerCheck.enabled = !__instance.techPilerCheck.enabled;
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxStack = building.MaxStack();
        __instance.transport.stationPool[__instance.stationId].pilerCount =
            __instance.techPilerCheck.enabled
                ? 0
                : maxStack;
        __instance.OnStationIdChange();
        return false;
    }

    /// <summary>
    /// 修改物流交互站的集装使用强化上限
    /// 内容同上面那个一摸一样，唯一的不同是参数类型，怎么合并一下
    /// </summary>
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
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            return true;
        }

        __instance.techPilerCheck.enabled = !__instance.techPilerCheck.enabled;
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxStack = building.MaxStack();
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
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic._station_output_parallel))]
    private static IEnumerable<CodeInstruction> GameLogic__station_output_parallel_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        // 查找 UpdateOutputSlots 调用的模式
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldloc_S),// cargoTraffic
            new CodeMatch(OpCodes.Ldloc_S),// entitySignPool
            new CodeMatch(OpCodes.Ldloc_S),// stationPilerLevel
            new CodeMatch(OpCodes.Ldloc_S),// active
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.Method(typeof(StationComponent), nameof(StationComponent.UpdateOutputSlots)))
        );
        if (matcher.IsInvalid) {
            LogError("Failed to find UpdateOutputSlots call pattern in GameLogic._station_output_parallel");
            return instructions;
        }

        // 移动到 stationPilerLevel 参数加载的位置
        matcher.Advance(2);
        // 替换为 GetOutputStack
        matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, (byte)7))// 加载 factory
            .Insert(
                new CodeInstruction(OpCodes.Ldloc_S, (byte)19),// 加载 &local2
                new CodeInstruction(OpCodes.Ldind_Ref),// 解引用得到 StationComponent
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(StationManager), nameof(GetOutputStack)))// 调用方法
            );
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 实际处理时，物流交互站的集装上限使用强化上限
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.GameTick_OutputToBelt))]
    private static IEnumerable<CodeInstruction> PlanetTransport_GameTick_OutputToBelt_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var matcher = new CodeMatcher(instructions);
        // 查找 UpdateOutputSlots 调用的模式
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldloc_0),// cargoTraffic
            new CodeMatch(OpCodes.Ldloc_1),// entitySignPool
            new CodeMatch(OpCodes.Ldarg_1),// maxPilerCount
            new CodeMatch(OpCodes.Ldloc_2),// active
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.Method(typeof(StationComponent), nameof(StationComponent.UpdateOutputSlots)))
        );
        if (matcher.IsInvalid) {
            LogError("Failed to find UpdateOutputSlots call pattern in PlanetTransport.GameTick_OutputToBelt");
            return instructions;
        }

        // 移动到 maxPilerCount 参数加载的位置
        matcher.Advance(2);
        // 替换为 GetOutputStack
        matcher.SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))// this (PlanetTransport)
            .Insert(
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetTransport), "factory")),// factory
                new CodeInstruction(OpCodes.Ldarg_0),// this (PlanetTransport) 再次加载
                new CodeInstruction(OpCodes.Ldfld,
                    AccessTools.Field(typeof(PlanetTransport), "stationPool")),// stationPool
                new CodeInstruction(OpCodes.Ldloc_3),// index
                new CodeInstruction(OpCodes.Ldelem_Ref),// stationPool[index]
                new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(StationManager), nameof(GetOutputStack)))// 调用方法
            );
        return matcher.InstructionEnumeration();
    }

    private static int GetOutputStack(PlanetFactory factory, StationComponent station) {
        int buildingID = factory.entityPool[station.entityId].protoId;
        return buildingID is IFE行星内物流交互站 or IFE星际物流交互站
            ? LDB.items.Select(buildingID).MaxStack()
            : GameMain.history.stationPilerLevel;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("SlotTransferMode", br => {
                int entityCount = br.ReadInt32();
                for (int i = 0; i < entityCount; i++) {
                    long entityId = br.ReadInt64();
                    int slotCount = br.ReadInt32();
                    var dict = new ConcurrentDictionary<int, ETransferMode>();
                    for (int j = 0; j < slotCount; j++) {
                        int slotIndex = br.ReadInt32();
                        dict[slotIndex] = (ETransferMode)br.ReadInt32();
                    }
                    slotTransferMode[entityId] = dict;
                }
            }),
            ("SlotCapacityMode", br => {
                int entityCount = br.ReadInt32();
                for (int i = 0; i < entityCount; i++) {
                    long entityId = br.ReadInt64();
                    int slotCount = br.ReadInt32();
                    var dict = new ConcurrentDictionary<int, ECapacityMode>();
                    for (int j = 0; j < slotCount; j++) {
                        int slotIndex = br.ReadInt32();
                        dict[slotIndex] = (ECapacityMode)br.ReadInt32();
                    }
                    slotCapacityMode[entityId] = dict;
                }
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("SlotTransferMode", bw => {
                bw.Write(slotTransferMode.Count);
                foreach (var kvp in slotTransferMode) {
                    bw.Write(kvp.Key);
                    bw.Write(kvp.Value.Count);
                    foreach (var slot in kvp.Value) {
                        bw.Write(slot.Key);
                        bw.Write((int)slot.Value);
                    }
                }
            }),
            ("SlotCapacityMode", bw => {
                bw.Write(slotCapacityMode.Count);
                foreach (var kvp in slotCapacityMode) {
                    bw.Write(kvp.Key);
                    bw.Write(kvp.Value.Count);
                    foreach (var slot in kvp.Value) {
                        bw.Write(slot.Key);
                        bw.Write((int)slot.Value);
                    }
                }
            })
        );
    }

    public static void IntoOtherSave() {
        Clear();
    }

    #endregion
}
