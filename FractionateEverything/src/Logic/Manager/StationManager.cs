using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using FE.UI.View.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class StationManager {
    private static int updateTick = 30;
    private static readonly int[] itemModSaveCount = new int[12000];
    private static int maxUploadCount;
    private static int maxDownloadCount;
    private const int StationBaseParameterLength = 2048;
    private const int InteractionStationParamMagic = 0x46455354;
    private const int InteractionStationParamVersion = 1;
    private const int InteractionStationParamHeaderLength = 3;
    private const int InteractionStationParamValuesPerSlot = 2;
    private const int InteractionStationParamMaxSlotCount = 32;

    private enum ETransferMode {
        Sync = 0,
        Upload = 1,
        Download = 2
    }

    private enum ECapacityMode {
        Limited = 0,
        Infinite = 1
    }

    private static readonly ConcurrentDictionary<long, ConcurrentDictionary<int, ETransferMode>> slotTransferMode =
        new();
    private static readonly ConcurrentDictionary<long, ConcurrentDictionary<int, ECapacityMode>> slotCapacityMode =
        new();

    public static void AddTranslations() {
        Register("双向同步", "Sync", "双向同步");
        Register("仅上传", "Upload Only", "仅上传");
        Register("仅下载", "Download Only", "仅下载");
        Register("有限上传", "Limited Upload", "有限上传");
        Register("无限上传", "Infinite Upload", "无限上传");
    }

    public static void Import(BinaryReader r) {
        slotTransferMode.Clear();
        slotCapacityMode.Clear();
        int transferEntityCount = r.ReadInt32();
        for (int i = 0; i < transferEntityCount; i++) {
            long entityId = r.ReadInt64();
            int slotCount = r.ReadInt32();
            ConcurrentDictionary<int, ETransferMode> dict = new();
            for (int j = 0; j < slotCount; j++) {
                int slotIndex = r.ReadInt32();
                dict[slotIndex] = NormalizeTransferMode(r.ReadInt32());
            }
            slotTransferMode[entityId] = dict;
        }

        int capacityEntityCount = r.ReadInt32();
        for (int i = 0; i < capacityEntityCount; i++) {
            long entityId = r.ReadInt64();
            int slotCount = r.ReadInt32();
            ConcurrentDictionary<int, ECapacityMode> dict = new();
            for (int j = 0; j < slotCount; j++) {
                int slotIndex = r.ReadInt32();
                dict[slotIndex] = NormalizeCapacityMode(r.ReadInt32());
            }
            slotCapacityMode[entityId] = dict;
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(slotTransferMode.Count);
        foreach (var kvp in slotTransferMode) {
            w.Write(kvp.Key);
            w.Write(kvp.Value.Count);
            foreach (var slot in kvp.Value) {
                w.Write(slot.Key);
                w.Write((int)slot.Value);
            }
        }

        w.Write(slotCapacityMode.Count);
        foreach (var kvp in slotCapacityMode) {
            w.Write(kvp.Key);
            w.Write(kvp.Value.Count);
            foreach (var slot in kvp.Value) {
                w.Write(slot.Key);
                w.Write((int)slot.Value);
            }
        }
    }

    public static void IntoOtherSave() {
        lastTickDic.Clear();
        slotTransferMode.Clear();
        slotCapacityMode.Clear();
        storageWidth.Clear();
        storagePopup.Clear();
        storageSdButtonOriginalPosition.Clear();
        storagePopupOriginalX.Clear();
        transferGameObjects.Clear();
        capacityGameObjects.Clear();
        controlPanelSdButtonOriginalPosition.Clear();
        inspectorOriginalMasterWidth.Clear();
        inspectorOriginalTopGroupWidth.Clear();
        inspectorOriginalBgWidth.Clear();
        inspectorOriginalRightGroupWidth.Clear();
        filterOriginalStateGroupPosition.Clear();
        controlPanelStoragePopup.Clear();
        controlPanelStoragePopupOriginalX.Clear();
        controlPanelTransferGameObjects.Clear();
        controlPanelCapacityGameObjects.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    private static ETransferMode NormalizeTransferMode(int value) {
        return Enum.IsDefined(typeof(ETransferMode), value)
            ? (ETransferMode)value
            : ETransferMode.Sync;
    }

    private static ECapacityMode NormalizeCapacityMode(int value) {
        return Enum.IsDefined(typeof(ECapacityMode), value)
            ? (ECapacityMode)value
            : ECapacityMode.Limited;
    }

    private static bool TryGetSlotModes(long entityId, int slotIndex, out int transferMode, out int capacityMode) {
        transferMode = (int)ETransferMode.Sync;
        capacityMode = (int)ECapacityMode.Limited;
        bool hasValue = false;
        if (slotTransferMode.TryGetValue(entityId, out ConcurrentDictionary<int, ETransferMode> transferDictionary)
            && transferDictionary.TryGetValue(slotIndex, out ETransferMode transfer)) {
            transferMode = (int)NormalizeTransferMode((int)transfer);
            hasValue = true;
        }
        if (slotCapacityMode.TryGetValue(entityId, out ConcurrentDictionary<int, ECapacityMode> capacityDictionary)
            && capacityDictionary.TryGetValue(slotIndex, out ECapacityMode capacity)) {
            capacityMode = (int)NormalizeCapacityMode((int)capacity);
            hasValue = true;
        }
        return hasValue;
    }

    private static void SetSlotModes(long entityId, int slotIndex, int transferMode, int capacityMode) {
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(entityId, _ => new ConcurrentDictionary<int, ETransferMode>());
        ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
            slotCapacityMode.GetOrAdd(entityId, _ => new ConcurrentDictionary<int, ECapacityMode>());
        transferDictionary[slotIndex] = NormalizeTransferMode(transferMode);
        capacityDictionary[slotIndex] = NormalizeCapacityMode(capacityMode);
    }

    private static void RemoveSlotModes(long entityId) {
        slotTransferMode.TryRemove(entityId, out _);
        slotCapacityMode.TryRemove(entityId, out _);
    }

    private static ETransferMode GetNextTransferMode(ETransferMode currentMode, int optionIndex) {
        if (optionIndex == 0) {
            return currentMode switch {
                ETransferMode.Sync => ETransferMode.Upload,
                ETransferMode.Upload => ETransferMode.Download,
                _ => ETransferMode.Sync
            };
        }
        if (optionIndex == 1) {
            return currentMode switch {
                ETransferMode.Sync => ETransferMode.Download,
                ETransferMode.Upload => ETransferMode.Sync,
                _ => ETransferMode.Upload
            };
        }
        return currentMode;
    }

    public static void CalculateItemModSaveCount() {
        foreach (var item in LDB.items.dataArray) {
            if (item.BuildMode != 0) {
                //建筑10组
                itemModSaveCount[item.ID] = item.StackSize * 10;
            } else {
                //非建筑按价值对数压低目标，避免高价值物品目标数过低
                itemModSaveCount[item.ID] = CalculateLimitedUploadTarget(itemValue[item.ID]);
            }
        }
        SetMaxCount();
    }

    private static int CalculateLimitedUploadTarget(float value) {
        if (value <= 0 || value >= maxValue) {
            return 0;
        }

        double divisor = Math.Log10(value + 1);
        if (divisor <= 0) {
            return 100000;
        }

        return Math.Min((int)(100000 / divisor), 100000);
    }

    public static void SetMaxCount() {
        ItemProto itemProto = LDB.items.Select(IFE行星内物流交互站);
        int stackSize = itemProto.MaxProductOutputStack();
        //上传速率至多12*4满带stackSize堆叠
        maxUploadCount = ProcessManager.MaxBeltSpeed * updateTick * stackSize / 60 * 12 * 4;
        //下载速率至多3*4满带stackSize堆叠
        maxDownloadCount = ProcessManager.MaxBeltSpeed * updateTick * stackSize / 60 * 3 * 4;
    }

    private static readonly ConcurrentDictionary<StationComponent[], long> lastTickDic = [];

    private static readonly Dictionary<RectTransform, float> windowOriginalWidth = [];
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalSize = [];
    private static readonly Dictionary<RectTransform, Vector2> sliderOriginalPosition = [];
    private static readonly ConcurrentDictionary<UIStationStorage, bool> storageWidth = new();
    private static readonly ConcurrentDictionary<UIStationStorage, bool> storagePopup = new();
    private static readonly Dictionary<RectTransform, float> storageSdButtonOriginalPosition = [];
    private static readonly Dictionary<RectTransform, float> storagePopupOriginalX = [];
    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> transferGameObjects = new();
    private static readonly ConcurrentDictionary<UIStationStorage, GameObject> capacityGameObjects = new();
    private static readonly Dictionary<RectTransform, float> controlPanelSdButtonOriginalPosition = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalMasterWidth = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalTopGroupWidth = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalBgWidth = [];
    private static readonly Dictionary<RectTransform, float> inspectorOriginalRightGroupWidth = [];
    private static readonly Dictionary<RectTransform, float> filterOriginalStateGroupPosition = [];
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, bool> controlPanelStoragePopup = new();
    private static readonly Dictionary<RectTransform, float> controlPanelStoragePopupOriginalX = [];
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject>
        controlPanelTransferGameObjects = new();
    private static readonly ConcurrentDictionary<UIControlPanelStationStorage, GameObject>
        controlPanelCapacityGameObjects = new();
    private const float ExtraSpacing = 12f;
    private const float BtnHeight = 26f;
    private const float BtnYOffset = 14f;
    private static float spacingX;
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsMyPopup = new();
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), bool> slotIsTransfer = new();
    private static readonly ConcurrentDictionary<(long stationEntityId, int slotIndex), RectTransform>
        slotPopupBoxRect = new();

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
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(entityId, _ => new ConcurrentDictionary<int, ETransferMode>());
                ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                    slotCapacityMode.GetOrAdd(entityId, _ => new ConcurrentDictionary<int, ECapacityMode>());
                // 循环交互站的所有的栏位
                for (int i = 0; i < stationComponent.storage.Length; i++) {
                    // StationStore为struct，必须使用引用修改内容
                    ref StationStore store = ref stationComponent.storage[i];
                    if (store.itemId <= 0 || itemValue[store.itemId] >= maxValue) {
                        continue;
                    }

                    ETransferMode transferMode = transferDictionary.GetOrAdd(i, ETransferMode.Sync);
                    ECapacityMode capacityMode = capacityDictionary.GetOrAdd(i, ECapacityMode.Limited);
                    switch (transferMode) {
                        case ETransferMode.Upload: {
                            if (store.count > store.max * uploadThreshold) {
                                int transferCount = GetUploadTransferCount(store, uploadThreshold, capacityMode);
                                if (transferCount > 0) {
                                    stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy);
                                }
                            }
                            break;
                        }
                        case ETransferMode.Download: {
                            if (store.totalSupplyCount < store.max * downloadThreshold) {
                                int targetCount = Mathf.RoundToInt(store.max * downloadThreshold);
                                if (targetCount < store.count) {
                                    targetCount = store.count;
                                }
                                stationComponent.SetTargetCount(i, targetCount, maxSlotEnergy);
                            }
                            break;
                        }
                        case ETransferMode.Sync:
                        default: {
                            if (store.count > store.max * uploadThreshold) {
                                int transferCount = GetUploadTransferCount(store, uploadThreshold, capacityMode);
                                if (transferCount > 0) {
                                    stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy);
                                }
                            } else if (store.totalSupplyCount < store.max * downloadThreshold) {
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

    private static int GetUploadTransferCount(in StationStore store, float uploadThreshold,
        ECapacityMode capacityMode) {
        int transferCount = store.count - (int)(store.max * uploadThreshold);
        if (transferCount <= 0) {
            return 0;
        }

        if (capacityMode != ECapacityMode.Limited) {
            return transferCount;
        }

        int modTargetCount = itemModSaveCount[store.itemId];
        long modCurrCount = GetModDataItemCount(store.itemId);
        if (modCurrCount >= modTargetCount) {
            return 0;
        }

        return Math.Min(transferCount, modTargetCount - (int)modCurrCount);
    }

    private static void SetTargetCount(this StationComponent stationComponent, int index, int targetCount,
        long maxSlotEnergy) {
        ref StationStore store = ref stationComponent.storage[index];
        if (store.count == targetCount || itemValue[store.itemId] == float.MaxValue) {
            return;
        }
        ItemProto itemProto = LDB.items.Select(IFE行星内物流交互站);
        // 物品价值(100价值=1000000J=1MJ，即每1价值，耗电10000J)
        float cost = (float)Math.Sqrt(itemValue[store.itemId]) * 10000 * itemProto.ReinforcementBonusEnergy();
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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationWindow), nameof(UIStationWindow._OnCreate))]
    public static void UIStationWindow_OnCreate_Postfix(UIStationWindow __instance) {
        if (__instance?.windowTrans != null) {
            GetOrCacheOriginal(windowOriginalWidth, __instance.windowTrans, x => x.sizeDelta.x);
        }

        for (int index = 0; index < 6; ++index) {
            UIStationStorage storage = __instance.storageUIs[index];
            try {
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                spacingX = refRect.sizeDelta.x + ExtraSpacing;
                string tName = "FE_transferModeButton_" + index;
                Transform tTrans = FindStationModeButtonTransform(storage, tName);
                if (tTrans == null) {
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject,
                        storage.localSdButton.transform.parent, false);
                    go.name = tName;
                    go.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    transferGameObjects[storage] = go;
                } else {
                    transferGameObjects[storage] = tTrans.gameObject;
                }

                string cName = "FE_capacityModeButton_" + index;
                Transform cTrans = FindStationModeButtonTransform(storage, cName);
                if (cTrans == null) {
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject,
                        storage.localSdButton.transform.parent, false);
                    go.name = cName;
                    go.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    capacityGameObjects[storage] = go;
                } else {
                    capacityGameObjects[storage] = cTrans.gameObject;
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
        storageSdButtonOriginalPosition.Clear();
        storagePopupOriginalX.Clear();
        transferGameObjects.Clear();
        capacityGameObjects.Clear();
        slotIsMyPopup.Clear();
        slotIsTransfer.Clear();
        slotPopupBoxRect.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnOpen))]
    public static void UIStationStorage__OnOpen_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = FindStationModeButtonTransform(__instance, tName);
        if (tTrans != null) {
            Button transferBtn = tTrans.GetComponent<Button>();
            transferBtn?.onClick.RemoveAllListeners();
            transferBtn?.onClick.AddListener(() => ShowTransferPopup(__instance));
            transferGameObjects[__instance] = tTrans.gameObject;
        }

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = FindStationModeButtonTransform(__instance, cName);
        if (cTrans != null) {
            Button capBtn = cTrans.GetComponent<Button>();
            capBtn?.onClick.RemoveAllListeners();
            capBtn?.onClick.AddListener(() => ShowCapacityPopup(__instance));
            capacityGameObjects[__instance] = cTrans.gameObject;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage._OnClose))]
    public static void UIStationStorage__OnClose_Postfix(UIStationStorage __instance) {
        string tName = "FE_transferModeButton_" + __instance.index;
        Transform tTrans = FindStationModeButtonTransform(__instance, tName);
        tTrans?.GetComponent<Button>()?.onClick.RemoveAllListeners();

        string cName = "FE_capacityModeButton_" + __instance.index;
        Transform cTrans = FindStationModeButtonTransform(__instance, cName);
        cTrans?.GetComponent<Button>()?.onClick.RemoveAllListeners();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
    public static void UIStationStorage_RefreshValues_Postfix(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        int buildingID = storage.stationWindow.factory.entityPool[storage.station.entityId].protoId;
        RectTransform rectTransform = storage.transform.GetComponent<RectTransform>();
        if (!IsInteractionStation(buildingID)) {
            SetStoragePopupShift(storage, false);
            if (rectTransform != null && storageWidth.ContainsKey(storage)) {
                storageWidth.TryRemove(storage, out _);
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x - spacingX, rectTransform.sizeDelta.y);
                RectTransform keepModeComponent = storage.keepModeButton?.GetComponent<RectTransform>();
                if (keepModeComponent != null
                    && storageSdButtonOriginalPosition.TryGetValue(keepModeComponent, out float keepModeOriginal)) {
                    keepModeComponent.anchoredPosition = new Vector2(keepModeOriginal,
                        keepModeComponent.anchoredPosition.y);
                }
            }
            if (transferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
                transferGO.SetActive(false);
            }
            if (capacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
                capacityGO.SetActive(false);
            }
            return;
        }

        if (rectTransform != null && !storageWidth.ContainsKey(storage)) {
            storageWidth.TryAdd(storage, true);
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x + spacingX, rectTransform.sizeDelta.y);
            RectTransform keepModeComponent = storage.keepModeButton?.GetComponent<RectTransform>();
            if (keepModeComponent != null) {
                float keepModeOriginal = GetOrCacheOriginal(storageSdButtonOriginalPosition, keepModeComponent,
                    x => x.anchoredPosition.x);
                keepModeComponent.anchoredPosition = new Vector2(keepModeOriginal - spacingX,
                    keepModeComponent.anchoredPosition.y);
            }
        }

        RectTransform localComponent = storage.localSdButton?.GetComponent<RectTransform>();
        RectTransform remoteComponent = storage.remoteSdButton?.GetComponent<RectTransform>();
        if (storageWidth.ContainsKey(storage)) {
            if (localComponent != null) {
                float localOriginal = GetOrCacheOriginal(storageSdButtonOriginalPosition, localComponent,
                    x => x.anchoredPosition.x);
                localComponent.anchoredPosition = new Vector2(localOriginal - spacingX,
                    localComponent.anchoredPosition.y);
            }
            if (remoteComponent != null) {
                float remoteOriginal = GetOrCacheOriginal(storageSdButtonOriginalPosition, remoteComponent,
                    x => x.anchoredPosition.x);
                remoteComponent.anchoredPosition = new Vector2(remoteOriginal - spacingX,
                    remoteComponent.anchoredPosition.y);
            }
        }

        var localImgRT = storage.localSdImage?.rectTransform;
        var remoteImgRT = storage.remoteSdImage?.rectTransform;
        float topY;
        float bottomY;
        if (storage.station != null && storage.station.isStellar && localImgRT != null && remoteImgRT != null) {
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

        RectTransform refRect = localComponent;
        if (refRect == null) {
            return;
        }
        if (!transferGameObjects.ContainsKey(storage)) {
            Transform transferTrans = FindStationModeButtonTransform(storage, "FE_transferModeButton_" + storage.index);
            if (transferTrans != null) {
                transferGameObjects[storage] = transferTrans.gameObject;
            }
        }
        if (!capacityGameObjects.ContainsKey(storage)) {
            Transform capacityTrans = FindStationModeButtonTransform(storage, "FE_capacityModeButton_" + storage.index);
            if (capacityTrans != null) {
                capacityGameObjects[storage] = capacityTrans.gameObject;
            }
        }
        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ETransferMode>());
        ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);

        if (transferGameObjects.TryGetValue(storage, out GameObject tTrans)) {
            tTrans.SetActive(storage.localSdButton.gameObject.activeSelf);
            Text transferText = tTrans.GetComponentInChildren<Text>();
            if (transferText != null) {
                transferText.text = transferMode switch {
                    ETransferMode.Sync => "双向同步".Translate(),
                    ETransferMode.Upload => "仅上传".Translate(),
                    ETransferMode.Download => "仅下载".Translate(),
                    _ => "双向同步".Translate()
                };
            }
            Image transferImage = tTrans.GetComponent<Image>();
            if (transferImage != null) {
                transferImage.color = transferMode switch {
                    ETransferMode.Sync => storage.noneSpColor,
                    ETransferMode.Upload => storage.demandColor,
                    ETransferMode.Download => storage.supplyColor,
                    _ => storage.noneSpColor
                };
            }
            RectTransform rt = tTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, topY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
        }

        if (capacityGameObjects.TryGetValue(storage, out GameObject cTrans)) {
            ConcurrentDictionary<int, ECapacityMode> capacityDictionary =
                slotCapacityMode.GetOrAdd(storage.station.entityId, new ConcurrentDictionary<int, ECapacityMode>());
            ECapacityMode capacityMode = capacityDictionary.GetOrAdd(storage.index, ECapacityMode.Limited);
            cTrans.SetActive(storage.localSdButton.gameObject.activeSelf
                             && transferMode is ETransferMode.Sync or ETransferMode.Upload);
            Text capText = cTrans.GetComponentInChildren<Text>();
            if (capText != null) {
                capText.text = capacityMode switch {
                    ECapacityMode.Limited => "有限上传".Translate(),
                    ECapacityMode.Infinite => "无限上传".Translate(),
                    _ => "有限上传".Translate()
                };
            }
            Image capImage = cTrans.GetComponent<Image>();
            if (capImage != null) {
                capImage.color = capacityMode switch {
                    ECapacityMode.Limited => storage.supplyColor,
                    ECapacityMode.Infinite => storage.demandColor,
                    _ => storage.supplyColor
                };
            }
            RectTransform rt = cTrans.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, bottomY);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, BtnHeight);
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
        bool isModStation = IsInteractionStation(__instance.factory.entityPool[station.entityId].protoId);
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
        int maxProductOutputStack = building.MaxProductOutputStack();
        if (maxProductOutputStack <= 1) {
            // 没解锁堆叠，不调整
            return;
        }
        if (station.isStellar) {
            __instance.windowTrans.sizeDelta = new Vector2(targetWidth, (float)(360 + 76 * station.storage.Length + 36));
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 186f);
        } else {
            __instance.windowTrans.sizeDelta = new Vector2(targetWidth, (float)(300 + 76 * station.storage.Length + 36));
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 126f);
        }
    }

    private static void ShowTransferPopup(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 =
            LDB.items.Select((int)storage.stationWindow.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null) {
            return;
        }

        ConcurrentDictionary<int, ETransferMode> transferDictionary =
            slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
        ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(storage);
        storage.optionImage0.color = transferMode switch {
            ETransferMode.Sync => storage.demandColor,
            ETransferMode.Upload => storage.supplyColor,
            _ => storage.noneSpColor
        };
        storage.optionImage1.color = transferMode switch {
            ETransferMode.Sync => storage.supplyColor,
            ETransferMode.Upload => storage.noneSpColor,
            _ => storage.demandColor
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
        SetStoragePopupShift(storage, true);
    }

    private static void ShowCapacityPopup(UIStationStorage __instance) {
        UIStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 =
            LDB.items.Select((int)storage.stationWindow.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null) {
            return;
        }

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
        SetStoragePopupShift(storage, true);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnLocalSdButtonClick))]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnRemoteSdButtonClick))]
    public static void UIStationStorage_OnSdButtonClick_Prefix(UIStationStorage __instance) {
        (long stationEntityId, int slotIndex) popupStateKey = GetPopupStateKey(__instance);
        ClearPopupState(popupStateKey);
        SetStoragePopupShift(__instance, false);
    }

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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelWindow), nameof(UIControlPanelWindow._OnCreate))]
    public static void UIControlPanelWindow_OnCreate_Postfix(UIControlPanelWindow __instance) {
        if (__instance?.stationInspector?.storageUIs == null) {
            return;
        }

        for (int index = 0; index < __instance.stationInspector.storageUIs.Length; ++index) {
            UIControlPanelStationStorage storage = __instance.stationInspector.storageUIs[index];
            if (storage?.localSdButton == null) {
                continue;
            }

            try {
                RectTransform refRect = storage.localSdButton.GetComponent<RectTransform>();
                spacingX = refRect.sizeDelta.x + ExtraSpacing;
                string transferName = "FE_cp_transferModeButton_" + index;
                Transform transferTrans = FindControlPanelModeButtonTransform(storage, transferName);
                if (transferTrans == null) {
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = transferName;
                    go.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    controlPanelTransferGameObjects[storage] = go;
                } else {
                    controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
                }

                string capacityName = "FE_cp_capacityModeButton_" + index;
                Transform capacityTrans = FindControlPanelModeButtonTransform(storage, capacityName);
                if (capacityTrans == null) {
                    Transform parent = storage.localSdButton.transform.parent ?? storage.transform;
                    GameObject go = GameObject.Instantiate(storage.localSdButton.gameObject, parent, false);
                    go.name = capacityName;
                    go.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    controlPanelCapacityGameObjects[storage] = go;
                } else {
                    controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
                }

                storage.popupBoxRect.SetSiblingIndex(storage.popupBoxRect.GetSiblingIndex() + 10);
            }
            catch (Exception ex) {
                LogError($"FE StationManager: create control panel mode buttons failed: {ex}");
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelWindow), nameof(UIControlPanelWindow._OnDestroy))]
    public static void UIControlPanelWindow_OnDestroy_Prefix(UIControlPanelWindow __instance) {
        controlPanelSdButtonOriginalPosition.Clear();
        controlPanelStoragePopup.Clear();
        controlPanelStoragePopupOriginalX.Clear();
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
            transferBtn?.onClick.AddListener(() => ShowTransferPopup(__instance));
            controlPanelTransferGameObjects[__instance] = transferTrans.gameObject;
        }

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
        if (capacityTrans != null) {
            Button capBtn = capacityTrans.GetComponent<Button>();
            capBtn?.onClick.RemoveAllListeners();
            capBtn?.onClick.AddListener(() => ShowCapacityPopup(__instance));
            controlPanelCapacityGameObjects[__instance] = capacityTrans.gameObject;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage._OnClose))]
    public static void UIControlPanelStationStorage_OnClose_Postfix(UIControlPanelStationStorage __instance) {
        string transferName = "FE_cp_transferModeButton_" + __instance.index;
        Transform transferTrans = FindControlPanelModeButtonTransform(__instance, transferName);
        transferTrans?.GetComponent<Button>()?.onClick.RemoveAllListeners();

        string capacityName = "FE_cp_capacityModeButton_" + __instance.index;
        Transform capacityTrans = FindControlPanelModeButtonTransform(__instance, capacityName);
        capacityTrans?.GetComponent<Button>()?.onClick.RemoveAllListeners();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.RefreshValues))]
    public static void UIControlPanelStationStorage_RefreshValues_Postfix(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
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
        RectTransform remoteRect = storage.remoteSdButton?.GetComponent<RectTransform>();
        if (localRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(localRect)) {
                controlPanelSdButtonOriginalPosition[localRect] = localRect.anchoredPosition.x;
            }
            float original = controlPanelSdButtonOriginalPosition[localRect];
            localRect.anchoredPosition = new Vector2(original - spacingX, localRect.anchoredPosition.y);
        }
        if (remoteRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(remoteRect)) {
                controlPanelSdButtonOriginalPosition[remoteRect] = remoteRect.anchoredPosition.x;
            }
            float original = controlPanelSdButtonOriginalPosition[remoteRect];
            remoteRect.anchoredPosition = new Vector2(original - spacingX, remoteRect.anchoredPosition.y);
        }
        RectTransform keepModeRect = storage.keepModeButton?.GetComponent<RectTransform>();
        if (keepModeRect != null) {
            if (!controlPanelSdButtonOriginalPosition.ContainsKey(keepModeRect)) {
                controlPanelSdButtonOriginalPosition[keepModeRect] = keepModeRect.anchoredPosition.x;
            }
            float original = controlPanelSdButtonOriginalPosition[keepModeRect];
            keepModeRect.anchoredPosition = new Vector2(original - spacingX, keepModeRect.anchoredPosition.y);
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

        if (!controlPanelTransferGameObjects.ContainsKey(storage)) {
            Transform transferTrans =
                FindControlPanelModeButtonTransform(storage, "FE_cp_transferModeButton_" + storage.index);
            if (transferTrans != null) {
                controlPanelTransferGameObjects[storage] = transferTrans.gameObject;
            }
        }
        if (!controlPanelCapacityGameObjects.ContainsKey(storage)) {
            Transform capacityTrans =
                FindControlPanelModeButtonTransform(storage, "FE_cp_capacityModeButton_" + storage.index);
            if (capacityTrans != null) {
                controlPanelCapacityGameObjects[storage] = capacityTrans.gameObject;
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
        SetControlPanelStoragePopupShift(__instance, true);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton0Click))]
    public static bool UIControlPanelStationStorage_OnOptionButton0Click_Prefix(
        UIControlPanelStationStorage __instance) {
        return HandleOptionClick(__instance, 0);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIControlPanelStationStorage), nameof(UIControlPanelStationStorage.OnOptionButton1Click))]
    public static bool UIControlPanelStationStorage_OnOptionButton1Click_Prefix(
        UIControlPanelStationStorage __instance) {
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

            StationStore stationStore = new();
            if (station != null && storage.index < station.storage.Length) {
                stationStore = station.storage[storage.index];
            }
            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = LDB.items.Select((int)stationWindow.factory.entityPool[station.entityId].protoId);
            if (itemProto1 == null || itemProto2 == null) {
                return false;
            }

            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                ETransferMode nextMode = GetNextTransferMode(transferMode, idx);
                transferDictionary.TryUpdate(storage.index, nextMode, transferMode);
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
            LogError($"FE HandleOptionClick error: {ex}");
            return true;
        }
    }

    private static void ShowTransferPopup(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 = LDB.items.Select(storage.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null) {
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
        SetControlPanelStoragePopupShift(storage, false);
    }

    private static void ShowCapacityPopup(UIControlPanelStationStorage __instance) {
        UIControlPanelStationStorage storage = __instance;
        StationComponent station = __instance.station;
        StationStore stationStore = new();
        if (station != null && storage.index < station.storage.Length) {
            stationStore = station.storage[storage.index];
        }
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 = LDB.items.Select(storage.factory.entityPool[storage.station.entityId].protoId);
        if (itemProto1 == null || itemProto2 == null) {
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
        SetControlPanelStoragePopupShift(storage, false);
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

            StationStore stationStore = new();
            if (station != null && storage.index < station.storage.Length) {
                stationStore = station.storage[storage.index];
            }
            ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
            ItemProto itemProto2 = LDB.items.Select(storage.factory.entityPool[station.entityId].protoId);
            if (itemProto1 == null || itemProto2 == null) {
                return false;
            }

            if (GetPopupFlag(slotIsTransfer, popupStateKey)) {
                ConcurrentDictionary<int, ETransferMode> transferDictionary =
                    slotTransferMode.GetOrAdd(station.entityId, new ConcurrentDictionary<int, ETransferMode>());
                ETransferMode transferMode = transferDictionary.GetOrAdd(storage.index, ETransferMode.Sync);
                ETransferMode nextMode = GetNextTransferMode(transferMode, idx);
                transferDictionary.TryUpdate(storage.index, nextMode, transferMode);
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
            int newVal = GetClampedPilerCount(value);
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
            int newVal = GetClampedPilerCount(value);
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
        int maxProductOutputStack = GetInteractionStationMaxStack();
        __instance.transport.stationPool[__instance.stationId].pilerCount =
            __instance.techPilerCheck.enabled
                ? 0
                : maxProductOutputStack;
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
        int maxProductOutputStack = GetInteractionStationMaxStack();
        __instance.transport.stationPool[__instance.stationId].pilerCount =
            __instance.techPilerCheck.enabled
                ? 0
                : maxProductOutputStack;
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
            ? LDB.items.Select(IFE行星内物流交互站).MaxProductOutputStack()
            : GameMain.history.stationPilerLevel;
    }

    private static bool IsInteractionStation(PlanetFactory factory, int entityId, out StationComponent station) {
        station = null;
        if (factory == null
            || entityId <= 0
            || entityId >= factory.entityPool.Length
            || factory.entityPool[entityId].id != entityId) {
            return false;
        }

        int stationId = factory.entityPool[entityId].stationId;
        if (stationId <= 0
            || factory.transport == null
            || stationId >= factory.transport.stationPool.Length) {
            return false;
        }

        station = factory.transport.stationPool[stationId];
        return station != null
               && station.id == stationId
               && station.entityId == entityId
               && IsInteractionStation(factory.entityPool[entityId].protoId);
    }

    private static int[] AppendInteractionStationParams(int[] parameters, StationComponent station) {
        int[] baseParameters = TrimInteractionStationParams(parameters);
        if (station?.storage == null) {
            return baseParameters;
        }

        int slotCount = Math.Min(station.storage.Length, InteractionStationParamMaxSlotCount);
        int extensionLength = InteractionStationParamHeaderLength + slotCount * InteractionStationParamValuesPerSlot;
        int baseLength = Math.Max(StationBaseParameterLength, baseParameters.Length);
        int[] result = new int[baseLength + extensionLength];
        Array.Copy(baseParameters, result, Math.Min(baseParameters.Length, result.Length));

        int tailIndex = baseLength;
        result[tailIndex] = InteractionStationParamMagic;
        result[tailIndex + 1] = InteractionStationParamVersion;
        result[tailIndex + 2] = slotCount;
        for (int i = 0; i < slotCount; i++) {
            TryGetSlotModes(station.entityId, i, out int transferMode, out int capacityMode);
            int valueIndex = tailIndex + InteractionStationParamHeaderLength
                             + i * InteractionStationParamValuesPerSlot;
            result[valueIndex] = transferMode;
            result[valueIndex + 1] = capacityMode;
        }
        return result;
    }

    private static int[] TrimInteractionStationParams(int[] parameters) {
        if (!TryGetInteractionStationParamOffset(parameters, out int offset, out _)) {
            return parameters ?? [];
        }

        int[] result = new int[offset];
        Array.Copy(parameters, result, offset);
        return result;
    }

    private static bool TryGetInteractionStationParamOffset(int[] parameters, out int offset, out int slotCount) {
        offset = 0;
        slotCount = 0;
        if (parameters == null || parameters.Length < StationBaseParameterLength + InteractionStationParamHeaderLength) {
            return false;
        }

        for (int i = StationBaseParameterLength;
             i <= parameters.Length - InteractionStationParamHeaderLength;
             i++) {
            if (parameters[i] != InteractionStationParamMagic
                || parameters[i + 1] != InteractionStationParamVersion) {
                continue;
            }

            int candidateSlotCount = parameters[i + 2];
            if (candidateSlotCount < 0 || candidateSlotCount > InteractionStationParamMaxSlotCount) {
                continue;
            }

            int expectedLength = i + InteractionStationParamHeaderLength
                                 + candidateSlotCount * InteractionStationParamValuesPerSlot;
            if (expectedLength > parameters.Length) {
                continue;
            }

            offset = i;
            slotCount = candidateSlotCount;
            return true;
        }
        return false;
    }

    private static void ApplyInteractionStationParams(PlanetFactory factory, int entityId, int[] parameters) {
        if (!IsInteractionStation(factory, entityId, out StationComponent station)
            || !TryGetInteractionStationParamOffset(parameters, out int offset, out int slotCount)) {
            return;
        }

        int maxSlotCount = Math.Min(slotCount, station.storage?.Length ?? 0);
        for (int i = 0; i < maxSlotCount; i++) {
            int valueIndex = offset + InteractionStationParamHeaderLength + i * InteractionStationParamValuesPerSlot;
            SetSlotModes(station.entityId, i, parameters[valueIndex], parameters[valueIndex + 1]);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.CopyFromFactoryObject))]
    public static void BuildingParameters_CopyFromFactoryObject_StationParams_Postfix(
        ref BuildingParameters __instance, int objectId, PlanetFactory factory, bool __result) {
        if (__result && objectId > 0 && IsInteractionStation(factory, objectId, out StationComponent station)) {
            __instance.parameters = AppendInteractionStationParams(__instance.parameters, station);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlueprintUtils), nameof(BlueprintUtils.GenerateBlueprintData))]
    public static void BlueprintUtils_GenerateBlueprintData_StationParams_Postfix(BlueprintData _blueprintData,
        PlanetData _planet, int[] _objIds, int _objCount) {
        if (_blueprintData?.buildings == null || _planet?.factory == null || _objIds == null) {
            return;
        }

        int count = Math.Min(_objCount, Math.Min(_objIds.Length, _blueprintData.buildings.Length));
        for (int i = 0; i < count; i++) {
            if (!IsInteractionStation(_planet.factory, _objIds[i], out StationComponent station)) {
                continue;
            }

            BlueprintBuilding building = _blueprintData.buildings[i];
            if (building != null) {
                building.parameters = AppendInteractionStationParams(building.parameters, station);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.GenerateBuildPreviews))]
    public static void BuildingParameters_GenerateBuildPreviews_StationParams_Postfix(List<BuildPreview> bplist) {
        if (BuildingParameters.template.type != BuildingType.Station
            || bplist == null
            || bplist.Count == 0
            || !TryGetInteractionStationParamOffset(BuildingParameters.template.parameters, out _, out _)) {
            return;
        }

        foreach (BuildPreview buildPreview in bplist) {
            if (buildPreview?.desc != null && buildPreview.desc.isStation) {
                buildPreview.parameters = BuildingParameters.template.parameters;
                buildPreview.paramCount = buildPreview.parameters.Length;
                return;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.ApplyPrebuildParametersToEntity))]
    public static void BuildingParameters_ApplyPrebuildParametersToEntity_StationParams_Postfix(int entityId,
        int[] parameters, PlanetFactory factory) {
        ApplyInteractionStationParams(factory, entityId, parameters);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.PasteToFactoryObject))]
    public static void BuildingParameters_PasteToFactoryObject_StationParams_Postfix(BuildingParameters __instance,
        int objectId, PlanetFactory factory, bool __result) {
        if (__result && objectId > 0) {
            ApplyInteractionStationParams(factory, objectId, __instance.parameters);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.PasteForceDown))]
    public static void BuildTool_BlueprintPaste_PasteForceDown_StationParams_Postfix(
        BuildTool_BlueprintPaste __instance) {
        if (__instance?.factory == null || __instance.bpPool == null) {
            return;
        }

        for (int i = 0; i < __instance.bpCursor; i++) {
            BuildPreview buildPreview = __instance.bpPool[i];
            if (buildPreview?.coverObjId > 0) {
                ApplyInteractionStationParams(__instance.factory, buildPreview.coverObjId, buildPreview.parameters);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.RemoveEntityWithComponents))]
    public static void PlanetFactory_RemoveEntityWithComponents_StationParams_Prefix(PlanetFactory __instance, int id) {
        if (IsInteractionStation(__instance, id, out StationComponent station)) {
            RemoveSlotModes(station.entityId);
        }
    }

    private static bool IsInteractionStation(int buildingID) {
        return buildingID is IFE行星内物流交互站 or IFE星际物流交互站;
    }

    private static int GetClampedPilerCount(float value) {
        int maxStack = GetInteractionStationMaxStack();
        int newValue = Mathf.RoundToInt(value);
        return newValue > maxStack ? maxStack : newValue;
    }

    private static int GetInteractionStationMaxStack() {
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        return building.MaxProductOutputStack();
    }

    private static void RefreshInteractionStationPilerUI(StationComponent station, Slider minPilerSlider,
        Text minPilerValue, GameObject minPilerGroup, GameObject pilerTechGroup) {
        int maxStack = GetInteractionStationMaxStack();
        minPilerSlider.maxValue = maxStack;
        int pilerCount = station.pilerCount;
        int showValue = pilerCount == 0 ? maxStack : pilerCount;
        minPilerSlider.value = showValue;
        minPilerValue.text = showValue.ToString();
        if (maxStack > 1) {
            minPilerGroup.SetActive(true);
            pilerTechGroup.SetActive(true);
        }
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

    private static Transform FindStationModeButtonTransform(UIStationStorage storage,
        string buttonName) {

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

    private static Transform FindControlPanelModeButtonTransform(UIControlPanelStationStorage storage,
        string buttonName) {
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

    private static void RestoreInspectorRightGroup(RectTransform rightGroupRect) {
        if (rightGroupRect != null
            && inspectorOriginalRightGroupWidth.TryGetValue(rightGroupRect, out float originalWidth)) {
            rightGroupRect.sizeDelta = new Vector2(originalWidth, rightGroupRect.sizeDelta.y);
        }
    }

    private static void RestoreInspectorStorageWidths(UIControlPanelStationInspector inspector) {
        if (inspector?.storageUIs == null) {
            return;
        }

        foreach (UIControlPanelStationStorage storage in inspector.storageUIs) {
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

        foreach (UIControlPanelStationStorage storage in inspector.storageUIs) {
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

    private static void RestoreControlPanelInspectorLayout(UIControlPanelStationInspector inspector,
        RectTransform rightGroupRect) {
        if (inspector?.storageUIs != null) {
            foreach (UIControlPanelStationStorage storage in inspector.storageUIs) {
                RestoreControlPanelStorageSlot(storage);
            }
        }

        RestoreInspectorStorageWidths(inspector);

        RectTransform stateGroupTrans = inspector?.masterWindow?.filterPanel?.stateFilterGroupTrans;
        if (stateGroupTrans != null
            && filterOriginalStateGroupPosition.TryGetValue(stateGroupTrans, out float stateGroupX)) {
            stateGroupTrans.anchoredPosition = new Vector2(stateGroupX, stateGroupTrans.anchoredPosition.y);
        }

        RectTransform masterRect = inspector?.masterWindow != null
            ? inspector.masterWindow.transform as RectTransform
            : null;
        if (masterRect != null && inspectorOriginalMasterWidth.TryGetValue(masterRect, out float masterWidth)) {
            masterRect.sizeDelta = new Vector2(masterWidth, masterRect.sizeDelta.y);
        }

        RestoreInspectorRightGroup(rightGroupRect);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIControlPanelStationInspector), nameof(UIControlPanelStationInspector._OnUpdate))]
    public static void UIControlPanelStationInspector_OnUpdate_Postfix(UIControlPanelStationInspector __instance) {
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
        if (!IsInteractionStation(buildingID)
            || __instance.currentTabPanel != EUIControlPanelStationPanel.Info) {
            RestoreControlPanelInspectorLayout(__instance, rightGroupRect);
            return;
        }

        WidenInspectorStorageWidths(__instance);

        bool logisticShipWarpDrive = GameMain.history.logisticShipWarpDrive;
        RectTransform powerRect = __instance.powerGroupRect;
        if (powerRect != null) {
            float basePowerWidth = station.isStellar
                ? (logisticShipWarpDrive ? 320f : 380f)
                : 440f;
            powerRect.sizeDelta = new Vector2(basePowerWidth + spacingX, 40f);
        }

        if (__instance.energyText != null && station.energyMax > 0) {
            float energyRatio = (float)station.energy / station.energyMax;
            float energyTextBaseX = (station.isStellar ? (logisticShipWarpDrive ? 180f : 240f) : 300f) + spacingX;
            float energyTextX = energyRatio > 0.7f
                ? Mathf.Round(energyTextBaseX * energyRatio - 30f)
                : Mathf.Round(energyTextBaseX * energyRatio + 30f);
            __instance.energyText.rectTransform.anchoredPosition =
                new Vector2(energyTextX, __instance.energyText.rectTransform.anchoredPosition.y);
        }

        RectTransform masterRect = __instance.masterWindow != null
            ? __instance.masterWindow.transform as RectTransform
            : null;
        if (masterRect != null) {
            float originalMasterWidth =
                GetOrCacheOriginal(inspectorOriginalMasterWidth, masterRect, x => x.sizeDelta.x);
            masterRect.sizeDelta = new Vector2(originalMasterWidth + spacingX, masterRect.sizeDelta.y);
        }

        RectTransform stateGroupTrans = __instance.masterWindow?.filterPanel?.stateFilterGroupTrans;
        if (stateGroupTrans != null) {
            float originalStateGroupX = GetOrCacheOriginal(filterOriginalStateGroupPosition, stateGroupTrans,
                x => x.anchoredPosition.x);
            stateGroupTrans.anchoredPosition =
                new Vector2(originalStateGroupX + spacingX, stateGroupTrans.anchoredPosition.y);
        }

        if (rightGroupRect != null) {
            float originalRightWidth =
                GetOrCacheOriginal(inspectorOriginalRightGroupWidth, rightGroupRect, x => x.sizeDelta.x);
            rightGroupRect.sizeDelta = new Vector2(originalRightWidth + spacingX, rightGroupRect.sizeDelta.y);
        }
    }

    private static void SetStoragePopupShift(UIStationStorage storage, bool shiftRight) {
        RectTransform popupRect = storage?.popupBoxRect;
        if (popupRect == null) {
            return;
        }

        float originalX = GetOrCacheOriginal(storagePopupOriginalX, popupRect, x => x.anchoredPosition.x);
        float targetX = shiftRight ? originalX + spacingX : originalX;
        popupRect.anchoredPosition = new Vector2(targetX, popupRect.anchoredPosition.y);
        if (shiftRight) {
            storagePopup[storage] = true;
        } else {
            storagePopup.TryRemove(storage, out _);
        }
    }

    private static void SetControlPanelStoragePopupShift(UIControlPanelStationStorage storage, bool shiftLeft) {
        RectTransform popupRect = storage?.popupBoxRect;
        if (popupRect == null) {
            return;
        }

        float originalX = GetOrCacheOriginal(controlPanelStoragePopupOriginalX, popupRect,
            x => x.anchoredPosition.x);
        float targetX = shiftLeft ? originalX - spacingX : originalX;
        popupRect.anchoredPosition = new Vector2(targetX, popupRect.anchoredPosition.y);
        if (shiftLeft) {
            controlPanelStoragePopup[storage] = true;
        } else {
            controlPanelStoragePopup.TryRemove(storage, out _);
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

        SetControlPanelStoragePopupShift(storage, false);
        ClearPopupState(GetPopupStateKey(storage));

        if (controlPanelTransferGameObjects.TryGetValue(storage, out GameObject transferGO)) {
            transferGO.SetActive(false);
        }
        if (controlPanelCapacityGameObjects.TryGetValue(storage, out GameObject capacityGO)) {
            capacityGO.SetActive(false);
        }
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
                valueRect.anchoredPosition = new Vector2(originalValuePosition.x + delta,
                    originalValuePosition.y);
            }
        }
    }

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
}
