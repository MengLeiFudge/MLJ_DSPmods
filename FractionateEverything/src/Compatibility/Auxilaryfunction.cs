using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace FE.Compatibility;

public static class Auxilaryfunction {
    public const string GUID = "cn.blacksnipe.dsp.Auxilaryfunction";
    public static bool Enable;
    private static readonly HashSet<int> warnedModelIds = [];

    public static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.Auxilaryfunction");
        harmony.PatchAll(typeof(Auxilaryfunction));
        CheckPlugins.LogInfo("Auxilaryfunction Compat finish.");
    }

    /// <summary>
    /// Aux 的研究室隐藏补丁会让部分 LabRenderer 继续走到这里。
    /// 原版这里既没有 modelId 边界保护，还错误地只清前 31 个槽位。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PerformanceMonitor), nameof(PerformanceMonitor.RecordGpuWorkByModel))]
    private static bool PerformanceMonitor_RecordGpuWorkByModel_Prefix(int modelId, int objCount, int vertCount) {
        if (PerformanceMonitor.objectCounts == null
            || PerformanceMonitor.vertexCounts == null
            || PerformanceMonitor.objectCountsByModel == null
            || PerformanceMonitor.vertexCountsByModel == null) {
            return false;
        }
        if (Time.frameCount > PerformanceMonitor.lastGPUSampleFrame) {
            Array.Clear(PerformanceMonitor.objectCounts, 0, PerformanceMonitor.objectCounts.Length);
            Array.Clear(PerformanceMonitor.vertexCounts, 0, PerformanceMonitor.vertexCounts.Length);
            Array.Clear(PerformanceMonitor.objectCountsByModel, 0, PerformanceMonitor.objectCountsByModel.Length);
            Array.Clear(PerformanceMonitor.vertexCountsByModel, 0, PerformanceMonitor.vertexCountsByModel.Length);
            PerformanceMonitor.gpuCounter = 0;
            PerformanceMonitor.lastGPUSampleFrame = Time.frameCount;
        }
        if (modelId <= 0
            || modelId >= PerformanceMonitor.objectCountsByModel.Length
            || modelId >= PerformanceMonitor.vertexCountsByModel.Length) {
            // 只对非法模型 ID 记一次日志，避免渲染热路径刷屏。
            if (warnedModelIds.Add(modelId)) {
                CheckPlugins.LogWarning(
                    $"Auxilaryfunction compat: 跳过非法 GPU 模型统计 modelId={modelId}, byModelLength={PerformanceMonitor.objectCountsByModel.Length}");
            }
            return false;
        }
        PerformanceMonitor.objectCountsByModel[modelId] += objCount;
        PerformanceMonitor.vertexCountsByModel[modelId] += vertCount;
        PerformanceMonitor.objectCountsByModel[0] += objCount;
        PerformanceMonitor.vertexCountsByModel[0] += vertCount;
        PerformanceMonitor.gpuCounter++;
        return false;
    }
}
