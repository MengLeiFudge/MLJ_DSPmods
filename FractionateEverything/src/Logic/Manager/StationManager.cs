using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class StationManager {
    private static readonly ConcurrentDictionary<StationComponent[], long> lastTickDic = [];

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.GameTick))]
    public static void PlanetTransportGameTickPostPatch(PlanetTransport __instance, long time) {
        // 10帧更新一次，取3作为特殊值
        if (time % 10L != 3L) {
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
                    // TODO 根据转移的物品数量，消耗电量
                    switch (store.localLogic) {
                        case ELogisticStorage.Supply: {
                            // 供应 = 从数据中心下载到塔里，然后提供出去
                            store.SetTargetCount(store.max);
                            break;
                        }
                        case ELogisticStorage.Demand: {
                            // 需求 = 需求物品到塔里，然后上传数据中心
                            store.SetTargetCount(0);
                            break;
                        }
                        case ELogisticStorage.None: {
                            // 仓储 = 维持数目为上限的一半；如果锁定，则维持数目为Min(仓储上限，(本格物品+Mod背包物品)/2)
                            if (!GameMain.sandboxToolsEnabled && store.keepMode > 0) {
                                int totalCount = (int)Math.Min(int.MaxValue,
                                    store.count + GetModDataItemCount(store.itemId));
                                // avgCount: 使交互站与Mod背包各持有一半物品的物品数目
                                int avgCount = totalCount / 2;
                                // +100以超过设定上限，从而能优先消耗数据中心的物品
                                store.SetTargetCount(Math.Min(store.max + 100, avgCount));
                            } else {
                                store.SetTargetCount(store.max / 2);
                            }
                            break;
                        }
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
        if (store.count == targetCount || itemValue[store.itemId] >= maxValue) {
            return;
        }
        if (store.count < targetCount) {
            // 将数据中心的物品下载到交互站
            int count = targetCount - store.count;
            count = TakeItemFromModData(store.itemId, count, out int inc);
            store.count += count;
            store.inc += inc;
        } else {
            // 将交互站的物品上传到数据中心
            int count = store.count - targetCount;
            int inc = store.count <= 0 ? 0 : split_inc(ref store.count, ref store.inc, count);
            AddItemToModData(store.itemId, count, inc);
        }
    }

    /// <summary>
    /// 在物流交互站中启用沙盒模式的keepModeButton
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIStationStorage), nameof(UIStationStorage.RefreshValues))]
    public static void UIStationStorage__RefreshValues_Postfix(UIStationStorage __instance) {
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
    public static bool UIStationStorage__OnKeepModeButtonClick_Prefix(UIStationStorage __instance) {
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
