using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        SetMaxCount();
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
        int maxProductOutputStack = building.MaxProductOutputStack();
        // 获取集装输出的当前上限
        __instance.minPilerSlider.maxValue = maxProductOutputStack;
        int pilerCount = station.pilerCount;
        if (pilerCount == 0) {
            // 自动，设置为上限
            __instance.minPilerSlider.value = maxProductOutputStack;
            __instance.minPilerValue.text = maxProductOutputStack.ToString();
        } else {
            // 手动，设置为当前值
            __instance.minPilerSlider.value = pilerCount;
            __instance.minPilerValue.text = pilerCount.ToString();
        }
        if (maxProductOutputStack > 1) {
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
        int maxProductOutputStack = building.MaxProductOutputStack();
        if (maxProductOutputStack <= 1) {
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
        int maxProductOutputStack = building.MaxProductOutputStack();
        // 获取集装输出的当前上限
        __instance.minPilerSlider.maxValue = maxProductOutputStack;
        int pilerCount = station.pilerCount;
        if (pilerCount == 0) {
            // 自动，设置为上限
            __instance.minPilerSlider.value = maxProductOutputStack;
            __instance.minPilerValue.text = maxProductOutputStack.ToString();
        } else {
            // 手动，设置为当前值
            __instance.minPilerSlider.value = pilerCount;
            __instance.minPilerValue.text = pilerCount.ToString();
        }
        if (maxProductOutputStack > 1) {
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
            int maxProductOutputStack = building.MaxProductOutputStack();
            int newVal = Mathf.RoundToInt(value);
            // 如果修改之后的值超过上限，设为上限
            if (newVal > maxProductOutputStack) {
                newVal = maxProductOutputStack;
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
            int maxProductOutputStack = building.MaxProductOutputStack();
            int newVal = Mathf.RoundToInt(value);
            // 如果修改之后的值超过上限，设为上限
            if (newVal > maxProductOutputStack) {
                newVal = maxProductOutputStack;
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
        int maxProductOutputStack = building.MaxProductOutputStack();
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
        ItemProto building = LDB.items.Select(IFE行星内物流交互站);
        int maxProductOutputStack = building.MaxProductOutputStack();
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
}
