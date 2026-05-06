using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using FE.Logic.Buildings.Definitions;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 流速监测器上限和货物容量扩展逻辑。
/// </summary>
public static class MonitorManager {
    private const int VanillaMonitorMaxStack = 4;
    private const int VanillaMonitorMaxCargoPerSecond = 120;
    private const int VanillaMonitorMaxTargetCargoBytes = 72000;

    public static int GetMonitorMaxStack() {
        int maxStack = InteractionTower.MaxStack;
        maxStack = Math.Max(maxStack, MineralReplicationTower.MaxStack);
        maxStack = Math.Max(maxStack, PointAggregateTower.MaxStack);
        maxStack = Math.Max(maxStack, ConversionTower.MaxStack);
        maxStack = Math.Max(maxStack, RectificationTower.MaxStack);
        maxStack = Math.Max(maxStack, PlanetaryInteractionStation.MaxStack);
        maxStack = Math.Max(maxStack, InterstellarInteractionStation.MaxStack);
        return Math.Max(VanillaMonitorMaxStack, maxStack);
    }

    public static int GetMonitorMaxCargoPerSecond() => VanillaMonitorMaxCargoPerSecond
                                                       / VanillaMonitorMaxStack
                                                       * GetMonitorMaxStack();

    public static float GetMonitorMaxCargoPerSecondFloat() => GetMonitorMaxCargoPerSecond();

    public static int GetMonitorMaxTargetCargoBytes() => VanillaMonitorMaxTargetCargoBytes
                                                         / VanillaMonitorMaxStack
                                                         * GetMonitorMaxStack();

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

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MonitorComponent), nameof(MonitorComponent.InternalUpdate))]
    public static IEnumerable<CodeInstruction> MonitorComponent_InternalUpdate_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        return ReplaceIntConstantWithCall(
            instructions,
            VanillaMonitorMaxStack,
            AccessTools.Method(typeof(MonitorManager), nameof(GetMonitorMaxStack)),
            3,
            "MonitorComponent.InternalUpdate max stack");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MonitorComponent), nameof(MonitorComponent.SetTargetCargoBytes))]
    public static IEnumerable<CodeInstruction> MonitorComponent_SetTargetCargoBytes_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        return ReplaceIntConstantWithCall(
            instructions,
            VanillaMonitorMaxTargetCargoBytes,
            AccessTools.Method(typeof(MonitorManager), nameof(GetMonitorMaxTargetCargoBytes)),
            2,
            "MonitorComponent.SetTargetCargoBytes max target");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow.RefreshMonitorWindow))]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow.OnPeriodValueChange))]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow.OnCargoFlowValueChange))]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow.OnCargoFlowInputFieldChange))]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow.OnCargoFlowInputFieldEndEdit))]
    public static IEnumerable<CodeInstruction> UIMonitorWindow_CargoFlow_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        return ReplaceFloatConstantWithCall(
            instructions,
            VanillaMonitorMaxCargoPerSecond,
            AccessTools.Method(typeof(MonitorManager), nameof(GetMonitorMaxCargoPerSecondFloat)),
            1,
            "UIMonitorWindow cargo flow max");
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow._OnUpdate))]
    public static IEnumerable<CodeInstruction> UIMonitorWindow_OnUpdate_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        return ReplaceIntConstantWithCall(
            instructions,
            VanillaMonitorMaxStack,
            AccessTools.Method(typeof(MonitorManager), nameof(GetMonitorMaxStack)),
            2,
            "UIMonitorWindow graph max stack");
    }

    private static IEnumerable<CodeInstruction> ReplaceIntConstantWithCall(
        IEnumerable<CodeInstruction> instructions,
        int constant,
        MethodInfo replacement,
        int expectedCount,
        string context) {
        var result = new List<CodeInstruction>();
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions) {
            if (IsIntConstant(instruction, constant)) {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced++;
            }
            result.Add(instruction);
        }

        if (replaced != expectedCount) {
            LogWarning($"{context}: expected {expectedCount} replacements, actual {replaced}.");
        }
        return result;
    }

    private static IEnumerable<CodeInstruction> ReplaceFloatConstantWithCall(
        IEnumerable<CodeInstruction> instructions,
        int constant,
        MethodInfo replacement,
        int expectedCount,
        string context) {
        var result = new List<CodeInstruction>();
        int replaced = 0;
        foreach (CodeInstruction instruction in instructions) {
            if (instruction.opcode == OpCodes.Ldc_R4
                && instruction.operand is float value
                && Math.Abs(value - constant) < 0.001f) {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced++;
            }
            result.Add(instruction);
        }

        if (replaced != expectedCount) {
            LogWarning($"{context}: expected {expectedCount} replacements, actual {replaced}.");
        }
        return result;
    }

    private static bool IsIntConstant(CodeInstruction instruction, int value) {
        if (value == 4 && instruction.opcode == OpCodes.Ldc_I4_4) return true;
        if (value == 72000 && instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int operand) {
            return operand == value;
        }
        return false;
    }
}
