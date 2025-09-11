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
        //1s更新一次
        if (time % 60L != 0L) {
            return;
        }
        try {
            // 避免同一帧内多次处理
            if (lastTickDic.TryGetValue(__instance.stationPool, out long lastTick) && lastTick == time) {
                return;
            }
            lastTickDic[__instance.stationPool] = time;
            // 打乱所有物流塔的顺序，尽可能的让每个物流塔都有机会拿到物品
            // TODO 要不加个开关，毕竟需要消耗性能
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
            stations.Sort((_, _) => (int)(GetRandDouble() * 100));
            foreach (StationComponent stationComponent in stations) {
                // 循环所有的栏位
                for (var i = 0; i < stationComponent.storage.Length; ++i) {
                    ref var store = ref stationComponent.storage[i];
                    // 暂存栏位数据
                    var storeCount = store.count;
                    var storeInc = store.inc;
                    // TODO 要不根据物品堆叠，消耗电量？
                    // 1堆叠10%的消耗
                    switch (store.localLogic) {
                        case ELogisticStorage.Supply: {
                            // 供应 = 从数据中心下载到塔里，然后提供出去
                            // 计算需求的数量
                            var count = store.max - storeCount;
                            // 下载物品
                            TakeItem(ref store, count);
                            break;
                        }
                        case ELogisticStorage.Demand: {
                            // 需求 = 需求物品到塔里，然后上传数据中心
                            // 如果存在物品，则上传
                            if (storeCount > 0) {
                                // 上传物品
                                AddItem(ref store, storeCount, storeInc);
                            }
                            break;
                        }
                        case ELogisticStorage.None: {
                            // 仓储 = 维持数目为上限的一半
                            // 计算上限一半的数量
                            var num = store.max / 2;
                            if (storeCount < num) {
                                // 如果数量小于一半
                                // 计算下载的数量
                                var count = num - storeCount;
                                // 下载物品
                                TakeItem(ref store, count);
                            } else if (storeCount > num) {
                                // 不是沙盒模式，如果keepMode大于0，并且数据中心已存在的数量大于设置的上限，就不上传了
                                if (!GameMain.sandboxToolsEnabled && store.keepMode > 0 && GetModDataItemCount(store.itemId) >= store.max) {
                                    continue;
                                }
                                // 如果数量大于一半
                                // 计算上传的数量
                                var count = storeCount - num;
                                // 计算上传的增产点
                                var inc = count / storeCount * storeInc;
                                // 上传物品
                                AddItem(ref store, count, inc);
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

    private static void AddItem(ref StationStore store, int count, int inc) {
        // todo: 考虑patch选择物品的界面，不让选择无价物品？
        if (itemValue[store.itemId] >= maxValue) {
            return;
        }
        // 上传物品到数据中心
        AddItemToModData(store.itemId, count, inc);
        // 移除行星内物流交互站中对应的数量
        store.count -= count;
        // 移除行星内物流交互站中对应的增产点
        store.inc -= inc;
    }

    private static void TakeItem(ref StationStore store, int count) {
        // 如果请求下载的数量小于0，则返回
        if (count <= 0) {
            return;
        }
        // todo: 考虑patch选择物品的界面，不让选择无价物品？
        if (itemValue[store.itemId] >= maxValue) {
            return;
        }
        // 从数据中心获取物品数量
        var modDataItemCount = GetModDataItemCount(store.itemId);
        // 如果数据中心没有物品，则返回
        if (modDataItemCount <= 0L) {
            return;
        }
        // 从数据中心下载的增产点
        // 从数据中心下载物品
        var itemFromModData = TakeItemFromModData(store.itemId, count, out var inc);
        // 添加物品到行星内物流交互站中
        store.count += itemFromModData;
        // 添加增产点到行星内物流交互站中
        store.inc += inc;
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
        // 如果是沙盒，不用处理，走默认逻辑
        if (GameMain.sandboxToolsEnabled) {
            return true;
        }

        int buildingID = __instance.stationWindow.factory.entityPool[__instance.station.entityId].protoId;
        // 如果不是自定义的塔，不处理
        if (buildingID != IFE行星内物流交互站 && buildingID != IFE星际物流交互站) {
            return true;
        }

        __instance.station.storage[__instance.index].keepMode = 
            __instance.station.storage[__instance.index].keepMode == 0 ? 1 : 0;
        // 原版有4个keepMode，需要点4下，用不上，这里只处理0和1
        return false;
    }
}
