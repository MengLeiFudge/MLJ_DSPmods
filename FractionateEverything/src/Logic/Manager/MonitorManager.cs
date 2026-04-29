using System;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class MonitorManager {
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(MonitorComponent), nameof(MonitorComponent.InternalUpdate))]
    public static Exception MonitorComponent_InternalUpdate_Finalizer(
        Exception __exception,
        ref MonitorComponent __instance,
        bool sandbox) {
        if (__exception is not IndexOutOfRangeException
            || !sandbox
            || __instance.spawnItemOperator != 1
            || __instance.cargoFilter <= 0) {
            return __exception;
        }

        // 原版在沙盒生成物品时会分两次读取同一段货物槽位；
        // 多线程货物流转下槽位可能在两次读取之间变化，导致 cargoPool 越界。
        __instance.SetSpawnOperator(0);
        LogWarning(
            $"MonitorComponent.InternalUpdate: 已关闭异常监控器的沙盒生成模式，避免 cargoPool 越界崩溃。"
            + $" monitorId={__instance.id}, entityId={__instance.entityId}, "
            + $"targetBeltId={__instance.targetBeltId}, cargoFilter={__instance.cargoFilter}");
        return null;
    }
}
