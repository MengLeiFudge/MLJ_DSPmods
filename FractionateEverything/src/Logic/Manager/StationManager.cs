using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private enum ETransferMode { Sync = 0, Supply = 1, Demand = 2 }
    private enum ECapacityMode { Limited = 0, Infinite = 1 }
    private enum EPopupType { None = 0, Transfer = 1, Capacity = 2 }

    private struct PopupInfo {
        public EPopupType Type;
        public ETransferMode[] TransferOptions; // for Transfer popup: options presented
        public ECapacityMode[] CapacityOptions; // for Capacity popup
    }

    private static readonly ConcurrentDictionary<long, ETransferMode> slotTransferMode = new();
    private static readonly ConcurrentDictionary<long, ECapacityMode> slotCapacityMode = new();
    private static readonly ConcurrentDictionary<long, PopupInfo> slotPopupInfo = new();

    // store base positions to avoid repeatedly adding spacing
    private static readonly ConcurrentDictionary<UIStationWindow, float> windowBaseWidth = new();
    private static readonly ConcurrentDictionary<UIStationStorage, Vector2> popupBasePos = new();

    private const float ExtraSpacing = 12f;
    private const float BtnHeight = 26f;
    private const float BtnYOffset = 14f;

    private static long GetSlotKey(UIStationStorage s) {
        if (s?.station == null) return 0L;
        return ((long)s.station.id << 32) | (uint)s.index;
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
            float uploadThreshold2 = 1 - uploadThreshold;
            foreach (StationComponent stationComponent in stations) {
                // 单个槽位可用最大电量
                long maxSlotEnergy = stationComponent.energy / stationComponent.storage.Length;
                // 循环交互站的所有的栏位
                for (int i = 0; i < stationComponent.storage.Length; i++) {
                    // StationStore为struct，必须使用引用修改内容
                    ref StationStore store = ref stationComponent.storage[i];
                    if (store.itemId <= 0 || itemValue[store.itemId] >= maxValue) {
                        continue;
                    }
                    bool storeLocked = !GameMain.sandboxToolsEnabled && store.keepMode > 0;
                    bool station2Mod;
                    bool mod2Station;
                    if (storeLocked) {
                        station2Mod = store.remoteLogic == ELogisticStorage.Supply
                                      || store.localLogic == ELogisticStorage.Supply;
                        mod2Station = store.remoteLogic == ELogisticStorage.Demand
                                      || store.localLogic == ELogisticStorage.Demand;
                    } else {
                        station2Mod = store.remoteLogic == ELogisticStorage.Demand
                                      || store.localLogic == ELogisticStorage.Demand;
                        mod2Station = store.remoteLogic == ELogisticStorage.Supply
                                      || store.localLogic == ELogisticStorage.Supply;
                    }
                    if (mod2Station) {
                        // 产线/Mod背包（背包仅在指定比例之下启用） -> 自身 -> 其他塔
                        if (store.totalSupplyCount < store.max * downloadThreshold) {
                            stationComponent.SetTargetCount(i,
                                Math.Max(store.count, (int)(store.max * downloadThreshold - store.totalOrdered)),
                                maxSlotEnergy);
                        }
                    }
                    if (station2Mod) {
                        // 其他塔 -> 自身 -> 产线/Mod背包（背包仅在指定比例之上启用）
                        if (store.count > store.max * uploadThreshold
                            && store.totalSupplyCount > store.max * uploadThreshold2) {
                            int modTargetCount = itemModSaveCount[store.itemId];
                            long modCurrCount = GetModDataItemCount(store.itemId);
                            if (modCurrCount < modTargetCount) {
                                int transferCount = Math.Min(store.count - (int)(store.max * uploadThreshold),
                                    modTargetCount - (int)modCurrCount);
                                stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy);
                            }
                        }
                    }
                    if (store.localLogic == ELogisticStorage.None) {
                        if (storeLocked) {
                            // 仓储锁定：维持数目为Min(仓储上限，(本格物品+Mod背包物品)/2)
                            int totalCount = (int)Math.Min(int.MaxValue,
                                store.count + GetModDataItemCount(store.itemId));
                            // avgCount: 使交互站与Mod背包各持有一半物品的物品数目
                            int avgCount = totalCount / 2;
                            stationComponent.SetTargetCount(i, Math.Min(store.max, avgCount), maxSlotEnergy);
                        } else {
                            // 仓储解锁：维持数目为上限的一半，可以无限投入/取出
                            stationComponent.SetTargetCount(i, store.max / 2, maxSlotEnergy);
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

    /// <summary>
    /// 在物流交互站中启用沙盒模式的keepModeButton
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
    public static void UIStationStorage_RefreshValues_Postfix(UIStationStorage __instance) {
        // 如果是沙盒，不用处理，会自动启用按钮
        if (GameMain.sandboxToolsEnabled) {
            return;
        }
        int buildingID = __instance.stationWindow.factory.entityPool[__instance.station.entityId].protoId;
        // 如果不是自定义的塔，不处理
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            return;
        }
        StationStore stationStore = new();
        if (__instance.station != null && __instance.index < __instance.station.storage.Length)
            stationStore = __instance.station.storage[__instance.index];
        ItemProto itemProto1 = LDB.items.Select(stationStore.itemId);
        ItemProto itemProto2 = LDB.items.Select(buildingID);
        // 如果物品或者塔不是游戏中存在的物品，不处理
        if (itemProto1 == null || itemProto2 == null) {
            return;
        }
        // 原版逻辑会先禁用这个按钮，所以在切换成供应或需求的时候不需要手动禁用
        // 启用keepModeButton
        __instance.keepModeButton.gameObject.SetActive(true);

        // --- 新增两个模式按钮（Transfer / Capacity） ---
        try {
            // spacing based on reference button width
            var refRect = __instance.localSdButton.GetComponent<RectTransform>();
            float btnWidth = refRect.sizeDelta.x;
            float spacingX = btnWidth + ExtraSpacing;

            // ensure window base width saved once
            if (__instance.stationWindow != null && !windowBaseWidth.ContainsKey(__instance.stationWindow)) {
                windowBaseWidth[__instance.stationWindow] = __instance.stationWindow.windowTrans.sizeDelta.x;
            }
            // set window width = base + spacingX
            if (__instance.stationWindow != null && windowBaseWidth.TryGetValue(__instance.stationWindow, out float baseW)) {
                __instance.stationWindow.windowTrans.sizeDelta = new Vector2(baseW + spacingX, __instance.stationWindow.windowTrans.sizeDelta.y);
            }

            // popup base pos store
            if (!popupBasePos.ContainsKey(__instance)) {
                popupBasePos[__instance] = __instance.popupBoxRect.anchoredPosition;
            }

            // compute vertical positions depending on tower type
            var localImgRT = __instance.localSdImage?.rectTransform;
            var remoteImgRT = __instance.remoteSdImage?.rectTransform;
            float topY, bottomY;
            if (__instance.station != null && __instance.station.isStellar && localImgRT != null && remoteImgRT != null) {
                // stellar: use the same Y as small images (14 / -14)
                topY = localImgRT.anchoredPosition.y;
                bottomY = remoteImgRT.anchoredPosition.y;
            }
            else if (localImgRT != null) {
                // planetary: center within the larger image
                float centerY = localImgRT.anchoredPosition.y;
                // 默认把按钮垂直居中于大图像区域
                float halfGap = (localImgRT.sizeDelta.y - BtnHeight) / 2f;
                // 行星塔中间额外增加 2f 的空隙（上下各 +1f）
                const float PlanetExtraGap = 4f;
                halfGap += PlanetExtraGap / 2f;
                topY = centerY + halfGap;
                bottomY = centerY - halfGap;
            } else {
                topY = BtnYOffset; bottomY = -BtnYOffset;
            }

            // create or find Transfer button
            string tName = "FE_transferModeButton_" + __instance.index;
            Transform tTrans = __instance.transform.Find(tName);
            Button transferBtn;
            Text transferText;
            if (tTrans == null) {
                // 克隆原版 localSdButton（保留原始样式：image + text）
                GameObject template = __instance.localSdButton.gameObject;
                GameObject go = GameObject.Instantiate(template, __instance.localSdButton.transform.parent, false);
                go.name = tName;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = refRect.anchorMin; rt.anchorMax = refRect.anchorMax; rt.pivot = refRect.pivot;
                rt.sizeDelta = new Vector2(btnWidth, BtnHeight);
                rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, topY);
                transferBtn = go.GetComponent<Button>();
                // remove original listeners and bind our popup
                try { transferBtn.onClick.RemoveAllListeners(); } catch { }
                transferBtn.onClick.AddListener(() => ShowTransferPopup(__instance));
                // find text child (original localSd uses an Image + Text structure)
                transferText = go.GetComponentInChildren<Text>();
            } else {
                transferBtn = tTrans.GetComponent<Button>();
                transferText = tTrans.GetComponentInChildren<Text>();
                // update position in case ref moved
                var rt = transferBtn.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, topY);
                rt.sizeDelta = new Vector2(btnWidth, BtnHeight);
            }

            // create or find Capacity button
            string cName = "FE_capacityModeButton_" + __instance.index;
            Transform cTrans = __instance.transform.Find(cName);
            Button capBtn;
            Text capText;
            if (cTrans == null) {
                // 克隆原版 localSdButton 作为样式模板
                GameObject template = __instance.localSdButton.gameObject;
                GameObject go = GameObject.Instantiate(template, __instance.localSdButton.transform.parent, false);
                go.name = cName;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = refRect.anchorMin; rt.anchorMax = refRect.anchorMax; rt.pivot = refRect.pivot;
                rt.sizeDelta = new Vector2(btnWidth, BtnHeight);
                rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, bottomY);
                capBtn = go.GetComponent<Button>();
                try { capBtn.onClick.RemoveAllListeners(); } catch { }
                capBtn.onClick.AddListener(() => ShowCapacityPopup(__instance));
                capText = go.GetComponentInChildren<Text>();
            } else {
                capBtn = cTrans.GetComponent<Button>();
                capText = cTrans.GetComponentInChildren<Text>();
                var rt = capBtn.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(refRect.anchoredPosition.x + spacingX, bottomY);
                rt.sizeDelta = new Vector2(btnWidth, BtnHeight);
            }

            // display/hide follow original localSdButton visible state
            transferBtn.gameObject.SetActive(__instance.localSdButton.gameObject.activeSelf);
            capBtn.gameObject.SetActive(__instance.localSdButton.gameObject.activeSelf);

            // ensure default modes exist
            long key = GetSlotKey(__instance);
            slotTransferMode.TryAdd(key, ETransferMode.Sync);
            slotCapacityMode.TryAdd(key, ECapacityMode.Limited);

            // update texts based on current mode
            if (transferText != null && slotTransferMode.TryGetValue(key, out var tm)) {
                transferText.text = tm switch { ETransferMode.Sync => "双向同步", ETransferMode.Supply => "供应", ETransferMode.Demand => "需求", _ => "双向同步" };
            }
            if (capText != null && slotCapacityMode.TryGetValue(key, out var cm)) {
                capText.text = cm == ECapacityMode.Infinite ? "无限" : "有限";
            }
        }
        catch (Exception ex) {
            // swallow to avoid breaking UI; log for debug
            LogError($"FE StationManager: create mode buttons failed: {ex}");
        }
    }

    /// <summary>
    /// 点击keepModeButton按钮
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnKeepModeButtonClick))]
    public static bool UIStationStorage_OnKeepModeButtonClick_Prefix(UIStationStorage __instance) {
        // 沙盒使用默认逻辑
        if (GameMain.sandboxToolsEnabled) {
            return true;
        }
        // 只处理物流交互站
        int buildingID = __instance.stationWindow.factory.entityPool[__instance.station.entityId].protoId;
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            return true;
        }
        // 原版有4个keepMode，需要点4下，用不上，这里只处理0和1
        __instance.station.storage[__instance.index].keepMode =
            __instance.station.storage[__instance.index].keepMode == 0 ? 1 : 0;
        return false;
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
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxStack = building.MaxStack();
        if (maxStack <= 1) {
            // 没解锁堆叠，不调整
            return;
        }
        if (station.isStellar) {
            __instance.windowTrans.sizeDelta = new Vector2(600f, (float)(360 + 76 * station.storage.Length + 36));
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 186f);
        } else {
            __instance.windowTrans.sizeDelta = new Vector2(600f, (float)(300 + 76 * station.storage.Length + 36));
            __instance.panelDownTrans.anchoredPosition =
                new Vector2(__instance.panelDownTrans.anchoredPosition.x, 126f);
        }
    }

    // Show transfer-mode popup shifted to the right
    private static void ShowTransferPopup(UIStationStorage __instance) {
        try {
            long key = GetSlotKey(__instance);
            // current mode
            slotTransferMode.TryGetValue(key, out var current);
            // compute remaining two options
            var all = new[] { ETransferMode.Sync, ETransferMode.Supply, ETransferMode.Demand };
            var opts = new List<ETransferMode>();
            foreach (var m in all) if (m != current) opts.Add(m);

            var info = new PopupInfo { Type = EPopupType.Transfer, TransferOptions = opts.ToArray() };
            slotPopupInfo[key] = info;

            // set popup texts and buttons
            __instance.optionButton2.gameObject.SetActive(false);
            if (opts.Count > 0) { __instance.optionText0.text = opts[0] switch { ETransferMode.Sync => "双向同步", ETransferMode.Supply => "供应", ETransferMode.Demand => "需求", _ => "双向同步" }; __instance.optionButton0.gameObject.SetActive(true); }
            if (opts.Count > 1) { __instance.optionText1.text = opts[1] switch { ETransferMode.Sync => "双向同步", ETransferMode.Supply => "供应", ETransferMode.Demand => "需求", _ => "双向同步" }; __instance.optionButton1.gameObject.SetActive(true); }

            // shift popup X
            if (popupBasePos.TryGetValue(__instance, out var basePos)) {
                var refRect = __instance.localSdButton.GetComponent<RectTransform>();
                float spacingX = refRect.sizeDelta.x + ExtraSpacing;
                __instance.popupBoxRect.anchoredPosition = new Vector2(basePos.x + spacingX, basePos.y);
                __instance.collectionPopupRect.anchoredPosition = new Vector2(basePos.x + spacingX, __instance.collectionPopupRect.anchoredPosition.y);
            }
            __instance.popupBoxRect.gameObject.SetActive(true);
        }
        catch (Exception ex) { LogError($"FE ShowTransferPopup error: {ex}"); }
    }

    // Show capacity-mode popup shifted to the right
    private static void ShowCapacityPopup(UIStationStorage __instance) {
        try {
            long key = GetSlotKey(__instance);
            var opts = new[] { ECapacityMode.Infinite, ECapacityMode.Limited };
            var info = new PopupInfo { Type = EPopupType.Capacity, CapacityOptions = opts };
            slotPopupInfo[key] = info;

            __instance.optionButton2.gameObject.SetActive(false);
            __instance.optionText0.text = "无限";
            __instance.optionButton0.gameObject.SetActive(true);
            __instance.optionText1.text = "有限";
            __instance.optionButton1.gameObject.SetActive(true);

            if (popupBasePos.TryGetValue(__instance, out var basePos)) {
                var refRect = __instance.localSdButton.GetComponent<RectTransform>();
                float spacingX = refRect.sizeDelta.x + ExtraSpacing;
                __instance.popupBoxRect.anchoredPosition = new Vector2(basePos.x + spacingX, basePos.y);
                __instance.collectionPopupRect.anchoredPosition = new Vector2(basePos.x + spacingX, __instance.collectionPopupRect.anchoredPosition.y);
            }
            __instance.popupBoxRect.gameObject.SetActive(true);
        }
        catch (Exception ex) { LogError($"FE ShowCapacityPopup error: {ex}"); }
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

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.OnOptionButton2Click))]
    public static bool UIStationStorage_OnOptionButton2Click_Prefix(UIStationStorage __instance) {
        return HandleOptionClick(__instance, 2);
    }

    private static bool HandleOptionClick(UIStationStorage __instance, int idx) {
        try {
            long key = GetSlotKey(__instance);
            if (!slotPopupInfo.TryRemove(key, out var info)) {
                // not our popup, let original handler run
                return true;
            }
            // apply selection according to popup type
            if (info.Type == EPopupType.Transfer && info.TransferOptions != null) {
                if (idx >= 0 && idx < info.TransferOptions.Length) {
                    slotTransferMode[key] = info.TransferOptions[idx];
                }
            }
            else if (info.Type == EPopupType.Capacity && info.CapacityOptions != null) {
                if (idx >= 0 && idx < info.CapacityOptions.Length) {
                    slotCapacityMode[key] = info.CapacityOptions[idx];
                }
            }
            // hide popup
            __instance.popupBoxRect.gameObject.SetActive(false);
            __instance.collectionPopupRect.gameObject.SetActive(false);
            return false; // prevent original OnOptionButton* handlers
        }
        catch (Exception ex) {
            LogError($"FE HandleOptionClick error: {ex}");
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
}
