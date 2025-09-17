using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class StationManager {
    private static int updateTick = 30;
    private static readonly int[] itemModSaveCount = new int[12000];
    private static int maxUploadCount;
    private static int maxDownloadCount;

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
        //上传速率至多12满带4堆叠
        maxUploadCount = ProcessManager.MaxBeltSpeed * updateTick * 4 / 60 * 12;
        //下载速率至多3满带4堆叠
        maxDownloadCount = ProcessManager.MaxBeltSpeed * updateTick * 4 / 60 * 3;
    }

    private static readonly ConcurrentDictionary<StationComponent[], long> lastTickDic = [];

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
            foreach (StationComponent stationComponent in stations) {
                // 循环交互站的所有的栏位
                for (int i = 0; i < stationComponent.storage.Length; i++) {
                    // StationStore为struct，必须使用引用修改内容
                    ref StationStore store = ref stationComponent.storage[i];
                    if (store.itemId <= 0 || itemValue[store.itemId] >= maxValue) {
                        continue;
                    }
                    // TODO 根据转移的物品数量，消耗电量
                    switch (store.localLogic) {
                        case ELogisticStorage.Supply:
                            // 供应：自身 -> 其他塔/Mod背包
                            if (store.count > store.max * 0.9 && store.totalSupplyCount > store.max * 0.1) {
                                int modTargetCount = itemModSaveCount[store.itemId];
                                long modCurrCount = GetModDataItemCount(store.itemId);
                                if (modCurrCount < modTargetCount) {
                                    int transferCount = Math.Min(store.count - (int)(store.max * 0.9),
                                        modTargetCount - (int)modCurrCount);
                                    store.SetTargetCount(store.count - transferCount);
                                }
                            }
                            break;
                        case ELogisticStorage.Demand:
                            // 需求：其他塔/Mod背包 -> 自身
                            if (store.totalSupplyCount < store.max * 0.1) {
                                store.SetTargetCount(Math.Max(store.count,
                                    (int)(store.max * 0.1 - store.totalOrdered)));
                            }
                            break;
                        case ELogisticStorage.None:
                            // 仓储解锁 = 维持数目为上限的一半，可以无限投入/取出
                            // 仓储锁定 = 维持数目为Min(仓储上限，(本格物品+Mod背包物品)/2)
                            if (!GameMain.sandboxToolsEnabled && store.keepMode > 0) {
                                int totalCount = (int)Math.Min(int.MaxValue,
                                    store.count + GetModDataItemCount(store.itemId));
                                // avgCount: 使交互站与Mod背包各持有一半物品的物品数目
                                int avgCount = totalCount / 2;
                                // +100以超过设定上限，从而能优先消耗数据中心的物品
                                // store.SetTargetCount(Math.Min(store.max + 100, avgCount));
                                store.SetTargetCount(Math.Min(store.max, avgCount));
                            } else {
                                store.SetTargetCount(store.max / 2);
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
        catch (Exception ex) {
            LogError(ex);
        }
    }

    private static void SetTargetCount(this ref StationStore store, int targetCount) {
        // todo: 考虑patch选择物品的界面，不让选择无价物品？
        if (store.count == targetCount) {
            return;
        }
        if (store.count < targetCount) {
            // 将数据中心的物品下载到交互站
            int count = Math.Min(maxDownloadCount, targetCount - store.count);
            count = TakeItemFromModData(store.itemId, count, out int inc);
            store.count += count;
            store.inc += inc;
        } else {
            // 将交互站的物品上传到数据中心
            int count = Math.Min(maxUploadCount, store.count - targetCount);
            int inc = store.count <= 0 ? 0 : split_inc(ref store.count, ref store.inc, count);
            AddItemToModData(store.itemId, count, inc);
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
        // 如果不是仓储，不处理
        if (stationStore.localLogic != ELogisticStorage.None) {
            return;
        }
        // 原版逻辑会先禁用这个按钮，所以在切换成供应或需求的时候不需要手动禁用
        // 启用keepModeButton
        __instance.keepModeButton.gameObject.SetActive(true);
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
}
