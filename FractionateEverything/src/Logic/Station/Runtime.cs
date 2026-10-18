using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.MainPanel.Setting;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;

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
        if (time % updateTick != 3L || FE.Compatibility.Nebula.NebulaMultiplayerModAPI.IsClient) {
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
                                        stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy, capacityMode);
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
                                        stationComponent.SetTargetCount(i, store.count - transferCount, maxSlotEnergy, capacityMode);
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

    /// <summary>按可用电量裁剪槽位目标数量，并按实际搬运数量结算耗电。</summary>
    /// <param name="stationComponent">发起上传或下载的交互站。</param>
    /// <param name="index">物品槽位的零基索引。</param>
    /// <param name="targetCount">槽位期望保留的物品数量。</param>
    /// <param name="maxSlotEnergy">本轮分配给该槽位的电量上限，单位 J。</param>
    private static void SetTargetCount(this StationComponent stationComponent, int index, int targetCount,
        long maxSlotEnergy, ECapacityMode capacityMode = ECapacityMode.Limited) {
        ref StationStore store = ref stationComponent.storage[index];
        if (store.count == targetCount || itemValue[store.itemId] == float.MaxValue) {
            return;
        }

        ItemProto itemProto = LDB.items.Select(IFE行星内物流交互站);
        // 单件耗电（J）为物品价值的平方根乘以 10000 和交互耗电倍率。
        double efficiencyMultiplier = Math.Max(0.4d,
            1d - GrowthTechnologyManager.SubspaceEfficiencyLevel * 0.1d);
        double cost = Math.Sqrt(itemValue[store.itemId]) * 10000
                      * itemProto.InteractEnergyRatio() * efficiencyMultiplier;
        if (cost < 0 || double.IsNaN(cost) || double.IsInfinity(cost)) {
            return;
        }

        long budget = Math.Max(0L, Math.Min(maxSlotEnergy, stationComponent.energy));
        bool download = store.count < targetCount;
        int count = download ? targetCount - store.count : store.count - targetCount;
        if (cost > 0) {
            count = (int)Math.Min(count, Math.Floor(budget / cost));
        }
        if (count <= 0) {
            return;
        }

        if (download) {
            // 将数据中心的物品下载到交互站，按实际取出的数量计费。
            count = TakeItemFromModData(store.itemId, count, out int inc);
            store.count += count;
            store.inc += inc;
        } else {
            // 将交互站的物品上传到亚空间；容量竞争导致部分接收时把未接收部分放回槽位。
            int offeredCount = count;
            int offeredInc = store.count <= 0 ? 0 : split_inc(ref store.count, ref store.inc, offeredCount);
            StorageReceipt receipt = DataCenterUploadRouter.UploadFromStation(store.itemId, offeredCount, offeredInc,
                capacityMode != ECapacityMode.Limited, itemModSaveCount[store.itemId]);
            int acceptedCount = (int)receipt.Accepted;
            if (acceptedCount < offeredCount) {
                store.count += (int)receipt.Rejected;
                store.inc += (int)receipt.RejectedInc;
            }
            count = acceptedCount;
        }

        if (count <= 0) {
            return;
        }
        double spent = Math.Ceiling(cost * count);
        // 先限制预算再转 long，避免 long.MaxValue 转 double 后舍入到 2^63 导致溢出。
        stationComponent.energy -= spent >= budget ? budget : (long)spent;
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

        if (capacityMode != ECapacityMode.Limited) { return transferCount; }
        long target = Math.Min(GetItemCapacity(store.itemId), itemModSaveCount[store.itemId]);
        long remaining = Math.Max(0L, target - GetModDataItemCount(store.itemId));
        return (int)Math.Min(transferCount, remaining);
    }
}
