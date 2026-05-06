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

public static partial class StationManager {
    /// <summary>在总控面板修改集装滑块时同步强化上限</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    /// <param name="value">新的滑块数值</param>
    /// <returns>是否让原始逻辑继续执行</returns>
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

    /// <summary>切换总控面板集装自动选项并刷新 UI</summary>
    /// <param name="__instance">当前的 UIControlPanelStationInspector 实例</param>
    /// <returns>是否让原始逻辑继续执行</returns>
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

    /// <summary>
    /// 实际处理时，物流交互站的集装上限使用强化上限
    /// </summary>
}
