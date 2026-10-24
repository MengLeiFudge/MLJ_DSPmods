using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace FE.Logic.Enhancements.Stacking;

public static partial class MonitorStackPatch {
    // 原版样本以十分之一件为单位，可包含回退修正；高堆叠必须保留完整有符号整数。
    // 数组身份区分工厂和监测器，原版数组被替换或释放后，关联缓存自动回收。
    private static readonly ConditionalWeakTable<sbyte[], int[]> wideSamples = new();
    private static int displayedMaxStack = -1;

    private static int[] GetWideSamples(sbyte[] samples) => wideSamples.GetValue(samples, CreateWideSamples);

    private static int[] CreateWideSamples(sbyte[] samples) {
        int[] values = new int[samples.Length];
        for (int i = 0; i < samples.Length; i++) { values[i] = samples[i]; }
        return values;
    }

    /// <summary>切换周期时，保留最近的完整采样，避免重新使用原版窄样本。</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MonitorComponent), nameof(MonitorComponent.SetPeriodTickCount))]
    private static void MonitorComponent_SetPeriodTickCount_Prefix(ref MonitorComponent __instance, out int[] __state) {
        __state = __instance.cargoBytesArray == null ? null : GetWideSamples(__instance.cargoBytesArray);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MonitorComponent), nameof(MonitorComponent.SetPeriodTickCount))]
    private static void MonitorComponent_SetPeriodTickCount_Postfix(ref MonitorComponent __instance, int[] __state) {
        int[] samples = GetWideSamples(__instance.cargoBytesArray);
        if (ReferenceEquals(samples, __state)) { return; }
        if (__state != null) {
            int count = Math.Min(__state.Length, samples.Length);
            Array.Copy(__state, __state.Length - count, samples, samples.Length - count, count);
        }
        __instance.cargoFlow = 0;
        foreach (int sample in samples) { __instance.cargoFlow += sample; }
    }

    /// <summary>原版存档无法容纳宽样本；读档后重建短时窗口，保留设置与累计货物计数。</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MonitorComponent), nameof(MonitorComponent.Import))]
    private static void MonitorComponent_Import_Postfix(ref MonitorComponent __instance) {
        Array.Clear(__instance.cargoBytesArray, 0, __instance.cargoBytesArray.Length);
        Array.Clear(__instance.periodCargoBytesArray, 0, __instance.periodCargoBytesArray.Length);
        __instance.cargoFlow = 0;
        __instance.prewarmSampleTick = 0;
    }

    /// <summary>替换采样数组及元素指令；累计流量、警报与数字信号继续执行原版算法。</summary>
    private static IEnumerable<CodeInstruction> WidenSamples(IEnumerable<CodeInstruction> instructions,
        int expectedArrayLoads, int expectedReads, int expectedWrites) {
        var field = AccessTools.Field(typeof(MonitorComponent), nameof(MonitorComponent.cargoBytesArray));
        var getter = AccessTools.Method(typeof(MonitorStackPatch), nameof(GetWideSamples));
        List<CodeInstruction> result = [];
        int arrays = 0;
        int reads = 0;
        int writes = 0;
        int conversions = 0;
        foreach (CodeInstruction original in instructions) {
            CodeInstruction instruction = new(original);
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, field)) {
                result.Add(instruction);
                result.Add(new CodeInstruction(OpCodes.Call, getter));
                arrays++;
                continue;
            }
            if (instruction.opcode == OpCodes.Ldelem_I1) {
                instruction.opcode = OpCodes.Ldelem_I4;
                reads++;
            } else if (instruction.opcode == OpCodes.Stelem_I1) {
                instruction.opcode = OpCodes.Stelem_I4;
                writes++;
            } else if (instruction.opcode == OpCodes.Conv_I1) {
                // 保留标签和异常块边界，只移除写入样本前的截断。
                instruction.opcode = OpCodes.Nop;
                conversions++;
            }
            result.Add(instruction);
        }
        int expectedConversions = expectedWrites > 0 ? 1 : 0;
        if (arrays != expectedArrayLoads || reads != expectedReads || writes != expectedWrites
            || conversions != expectedConversions) {
            throw new InvalidOperationException($"流速监测器采样指令不匹配：数组 {arrays}/{expectedArrayLoads}，"
                + $"读取 {reads}/{expectedReads}，写入 {writes}/{expectedWrites}，转换 {conversions}/{expectedConversions}。");
        }
        return result;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow.RefreshMonitorWindow))]
    private static void UIMonitorWindow_RefreshMonitorWindow_Postfix() {
        displayedMaxStack = GetMonitorMaxStack();
    }

    /// <summary>窗口打开期间修改堆叠上限时，同步滑块映射、输入范围及目标值。</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIMonitorWindow), nameof(UIMonitorWindow._OnUpdate))]
    private static void UIMonitorWindow_OnUpdate_Prefix(UIMonitorWindow __instance) {
        if (!__instance.monitorAvailable || __instance.monitorId == 0 || __instance.factory == null
            || __instance.event_lock || displayedMaxStack == GetMonitorMaxStack()) { return; }
        __instance.OnPeriodValueChange(__instance.periodSlider.value);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SingleMonitorStatPlan), nameof(SingleMonitorStatPlan.GetRawImageData))]
    private static IEnumerable<CodeInstruction> SingleMonitorStatPlan_GetRawImageData_Transpiler(
        IEnumerable<CodeInstruction> instructions) => ReplaceIntConstantWithCall(instructions, VanillaMonitorMaxStack,
        AccessTools.Method(typeof(MonitorStackPatch), nameof(GetMonitorMaxStack)), 2, "SingleMonitorStatPlan graph max stack");

    /// <summary>统计面板仍使用原版接口返回数组身份，复制到图表时读取同一份完整采样。</summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIChartSingleMonitor), nameof(UIChartSingleMonitor.Refresh))]
    private static IEnumerable<CodeInstruction> UIChartSingleMonitor_Refresh_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        Type[] intParameters = [typeof(Array), typeof(int), typeof(Array), typeof(int), typeof(int)];
        Type[] longParameters = [typeof(Array), typeof(long), typeof(Array), typeof(long), typeof(long)];
        var intCopy = AccessTools.Method(typeof(Array), nameof(Array.Copy), intParameters);
        var longCopy = AccessTools.Method(typeof(Array), nameof(Array.Copy), longParameters);
        List<CodeInstruction> result = [];
        int replaced = 0;
        foreach (CodeInstruction original in instructions) {
            CodeInstruction instruction = new(original);
            if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, intCopy)) {
                instruction.operand = AccessTools.Method(typeof(MonitorStackPatch), nameof(CopyMonitorSamples), intParameters);
                replaced++;
            } else if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, longCopy)) {
                instruction.operand = AccessTools.Method(typeof(MonitorStackPatch), nameof(CopyMonitorSamples), longParameters);
                replaced++;
            }
            result.Add(instruction);
        }
        if (replaced != 3) { throw new InvalidOperationException($"流速监测图表复制指令不匹配：{replaced}/3。"); }
        return result;
    }

    private static void CopyMonitorSamples(Array source, int sourceIndex, Array destination, int destinationIndex, int count) =>
        Array.Copy(source is sbyte[] samples ? GetWideSamples(samples) : source, sourceIndex, destination, destinationIndex, count);

    private static void CopyMonitorSamples(Array source, long sourceIndex, Array destination, long destinationIndex, long count) =>
        Array.Copy(source is sbyte[] samples ? GetWideSamples(samples) : source, sourceIndex, destination, destinationIndex, count);
}
