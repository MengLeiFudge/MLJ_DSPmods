using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FE.Logic.Buildings;
using FE.Logic.Buildings.Definitions;
using FE.UI.MainPanel.Setting;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;
using static FE.Logic.Station.ProliferatorPool;
using static FE.Logic.DataCenter.PlayerInventoryAccess;

namespace FE.Logic.Station;

/// <summary>
/// 物流交互站上传下载同步、目标数量与耗电运行逻辑。
/// </summary>
public static partial class StationManager {
    /// <summary>交互站逻辑更新周期（每30帧更新一次）</summary>
    private static int updateTick = 30;
    /// <summary>每种物品在数据中心"有限上传"模式下的目标存储数量</summary>
    private static readonly int[] itemModSaveCount = new int[12000];

    /// <summary>
    /// 预计算每种物品在数据中心的“有限上传”目标数量。
    /// </summary>
    public static void CalculateItemModSaveCount() {
        // 遍历所有物品，计算每种物品在数据中心的目标存储数量
        foreach (var item in LDB.items.dataArray) {
            if (item.BuildMode != 0) {
                // 建筑类物品：限制为10组（防止建筑占用过多空间）
                itemModSaveCount[item.ID] = item.StackSize * 10;
            } else {
                // 非建筑物品：按价值对数压低有限上传目标，避免高价值物品目标数过低。
                itemModSaveCount[item.ID] = CalculateLimitedUploadTarget(itemValue[item.ID]);
            }
        }
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

    private static void EnsureInteractionStationState(PlanetFactory factory, StationComponent station, int buildingID) {
        if (factory == null || station?.storage == null || station.priorityLocks == null) {
            return;
        }

        PrefabDesc prefabDesc = LDB.items.Select(buildingID)?.prefabDesc;
        if (prefabDesc == null) {
            return;
        }

        if (prefabDesc.stationMaxItemKinds > station.storage.Length) {
            // 旧存档和“先放小塔再升级”的路径可能保留旧数组长度，这里只扩容不收缩，避免丢槽位数据。
            Array.Resize(ref station.storage, prefabDesc.stationMaxItemKinds);
            Array.Resize(ref station.priorityLocks, prefabDesc.stationMaxItemKinds);
        }

        station.energyMax = prefabDesc.stationMaxEnergyAcc;
        if (station.energy > station.energyMax) {
            station.energy = station.energyMax;
        }

        if (station.pcId <= 0 || station.pcId >= factory.powerSystem.consumerPool.Length) {
            return;
        }

        long minChargePower = prefabDesc.workEnergyPerTick / 2;
        long maxChargePower = prefabDesc.workEnergyPerTick * 5;
        ref PowerConsumerComponent powerConsumer = ref factory.powerSystem.consumerPool[station.pcId];
        powerConsumer.workEnergyPerTick =
            Math.Max(minChargePower, Math.Min(maxChargePower, powerConsumer.workEnergyPerTick));
    }

    /// <summary>防止同一 stationPool 在同一 tick 被重复处理</summary>
    private static readonly ConcurrentDictionary<StationComponent[], long> lastTickDic = [];
    /// <summary>按 stationPool 复用交互站扫描缓冲，避免 30 帧热路径重复分配列表。</summary>
    private static readonly ConcurrentDictionary<StationComponent[], List<StationComponent>> stationBufferDic = [];

    /// <summary>在行星运输周期内按自定义规则处理交互站的上传下载</summary>
    /// <param name="__instance">PlanetTransport 实例</param>
    /// <param name="time">当前游戏 tick 时间</param>
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
            List<StationComponent> stations = stationBufferDic.GetOrAdd(__instance.stationPool, _ => []);
            lock (stations) {
                // 获取所有的交互站后，执行随机排序，让每个塔都有机会拿到物品
                stations.Clear();
                for (int index = 1; index < __instance.stationCursor; ++index) {
                    StationComponent stationComponent = __instance.stationPool[index];
                    if (stationComponent == null || stationComponent.id != index) {
                        continue;
                    }

                    int buildingID = __instance.factory.entityPool[stationComponent.entityId].protoId;
                    if (buildingID == IFE行星内物流交互站 || buildingID == IFE星际物流交互站) {
                        EnsureInteractionStationState(__instance.factory, stationComponent, buildingID);
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

                        // 获取该槽位的传输模式和容量模式
                        ETransferMode transferMode = transferDictionary.GetOrAdd(i, ETransferMode.Sync);
                        ECapacityMode capacityMode = capacityDictionary.GetOrAdd(i, ECapacityMode.Limited);

                        // 根据传输模式决定行为
                        switch (transferMode) {
                            case ETransferMode.Upload: {
                                // 仅上传模式：交互站 -> 数据中心
                                // 当交互站数量超过上传阈值时，将超出部分上传到数据中心
                                if (store.count > store.max * uploadThreshold) {
                                    int transferCount = GetUploadTransferCount(store, uploadThreshold, capacityMode);
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
                                    // 数量高于上传阈值，优先执行统一的上传裁剪逻辑。
                                    int transferCount = GetUploadTransferCount(store, uploadThreshold, capacityMode);
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
        }
        catch (Exception ex) {
            LogError(ex);
        }
    }

    /// <summary>设置交互站某槽位的目标物品数量并消耗对应电力</summary>
    /// <param name="stationComponent">操作的交互站组件</param>
    /// <param name="index">槽位索引</param>
    /// <param name="targetCount">目标数量</param>
    /// <param name="maxSlotEnergy">单槽位可用的最大电量</param>
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
                BuildingManager.AddBuildingExp(IFE行星内物流交互站, count);
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
                BuildingManager.AddBuildingExp(IFE行星内物流交互站, count);
            }
        }
        finally {
            if (PlanetaryInteractionStation.Level >= 3) {
                AddIncToItem(store.count, ref store.inc);
            }
        }
    }

    /// <summary>
    /// 计算上传模式下本槽位本轮允许搬运到数据中心的数量。
    /// </summary>
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
}
