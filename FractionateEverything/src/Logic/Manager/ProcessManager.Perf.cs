using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using FE.Logic.Building;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.View.ProgressTask;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class ProcessManager {
    private static bool EnableFractionatorPerfProbe = false;
    private const int FractionatorPerfBucketCount = 4;
    private const int FractionatorPerfLogIntervalTicks = 300;
    private const int FractionatorPerfUpdateFe = 0;
    private const int FractionatorPerfUpdateVanilla = 1;
    private const int FractionatorPerfSetPcFe = 2;
    private const int FractionatorPerfSetPcVanilla = 3;
    private const int FractionatorPerfStageCount = 7;
    private const int FractionatorPerfStagePrepare = 0;
    private const int FractionatorPerfStageProcess = 1;
    private const int FractionatorPerfStageFlushDeltas = 2;
    private const int FractionatorPerfStageZeroPressure = 3;
    private const int FractionatorPerfStageFluidBelts = 4;
    private const int FractionatorPerfStageProductBelt = 5;
    private const int FractionatorPerfStageFinalize = 6;
    private const int FractionatorPerfDetailCount = 7;
    private const int FractionatorPerfDetailPrepareStateRecipe = 0;
    private const int FractionatorPerfDetailPrepareSchema = 1;
    private const int FractionatorPerfDetailPrepareProduct = 2;
    private const int FractionatorPerfDetailPrepareConfig = 3;
    private const int FractionatorPerfDetailFlushDeltas = 4;
    private const int FractionatorPerfDetailProcessGetOutputs = 5;
    private const int FractionatorPerfDetailProcessMergeOutputs = 6;
    private static readonly long[] fractionatorPerfCalls = new long[FractionatorPerfBucketCount];
    private static readonly long[] fractionatorPerfTicks = new long[FractionatorPerfBucketCount];
    private static readonly long[] fractionatorPerfMaxTicks = new long[FractionatorPerfBucketCount];
    private static readonly long[] fractionatorPerfStageCalls = new long[FractionatorPerfStageCount];
    private static readonly long[] fractionatorPerfStageTicks = new long[FractionatorPerfStageCount];
    private static readonly long[] fractionatorPerfStageMaxTicks = new long[FractionatorPerfStageCount];
    private static readonly long[] fractionatorPerfDetailCalls = new long[FractionatorPerfDetailCount];
    private static readonly long[] fractionatorPerfDetailTicks = new long[FractionatorPerfDetailCount];
    private static readonly long[] fractionatorPerfDetailMaxTicks = new long[FractionatorPerfDetailCount];
    private static readonly long[] fractionatorPerfFeUpdateCallsByType = new long[updateHandlersByBuildingOffset.Length];
    private static readonly long[] fractionatorPerfFeUpdateTicksByType = new long[updateHandlersByBuildingOffset.Length];
    private static long fractionatorPerfWindowStartTick = -1;
    private static int fractionatorPerfLogging;
    private static void RecordFractionatorPerf(int bucket, int buildingID, long elapsedTicks) {
        if (!EnableFractionatorPerfProbe) {
            return;
        }
        fractionatorPerfCalls[bucket]++;
        fractionatorPerfTicks[bucket] += elapsedTicks;
        UpdateFractionatorPerfMax(fractionatorPerfMaxTicks, bucket, elapsedTicks);

        int handlerIndex = buildingID - IFE交互塔;
        if (bucket == FractionatorPerfUpdateFe
            && handlerIndex >= 0
            && handlerIndex < fractionatorPerfFeUpdateCallsByType.Length) {
            fractionatorPerfFeUpdateCallsByType[handlerIndex]++;
            fractionatorPerfFeUpdateTicksByType[handlerIndex] += elapsedTicks;
        }

        MaybeLogFractionatorPerf();
    }

    private static void RecordFractionatorPerfStage(int stage, long elapsedTicks) {
        if (!EnableFractionatorPerfProbe) {
            return;
        }
        fractionatorPerfStageCalls[stage]++;
        fractionatorPerfStageTicks[stage] += elapsedTicks;
        UpdateFractionatorPerfMax(fractionatorPerfStageMaxTicks, stage, elapsedTicks);
    }

    public static void RecordFractionatorPerfDetail(int detail, long elapsedTicks) {
        if (!EnableFractionatorPerfProbe) {
            return;
        }
        fractionatorPerfDetailCalls[detail]++;
        fractionatorPerfDetailTicks[detail] += elapsedTicks;
        UpdateFractionatorPerfMax(fractionatorPerfDetailMaxTicks, detail, elapsedTicks);
    }

    private static long GetFractionatorPerfTimestamp() {
        return EnableFractionatorPerfProbe ? Stopwatch.GetTimestamp() : 0L;
    }

    private static long GetFractionatorPerfElapsed(long startTicks) {
        return EnableFractionatorPerfProbe ? Stopwatch.GetTimestamp() - startTicks : 0L;
    }

    private static void UpdateFractionatorPerfMax(long[] maxTicks, int index, long elapsedTicks) {
        if (elapsedTicks > maxTicks[index]) {
            maxTicks[index] = elapsedTicks;
        }
    }

    private static void MaybeLogFractionatorPerf() {
        long gameTick = GameMain.gameTick;
        if (gameTick < 0) {
            return;
        }

        long windowStart = fractionatorPerfWindowStartTick;
        if (windowStart < 0) {
            fractionatorPerfWindowStartTick = gameTick;
            return;
        }
        if (gameTick - windowStart < FractionatorPerfLogIntervalTicks) {
            return;
        }
        if (fractionatorPerfLogging != 0) {
            return;
        }
        fractionatorPerfLogging = 1;

        try {
            windowStart = fractionatorPerfWindowStartTick;
            fractionatorPerfWindowStartTick = gameTick;
            long windowTicks = Math.Max(1, gameTick - windowStart);
            long[] calls = new long[FractionatorPerfBucketCount];
            long[] ticks = new long[FractionatorPerfBucketCount];
            long[] maxTicks = new long[FractionatorPerfBucketCount];
            for (int i = 0; i < FractionatorPerfBucketCount; i++) {
                calls[i] = fractionatorPerfCalls[i];
                ticks[i] = fractionatorPerfTicks[i];
                maxTicks[i] = fractionatorPerfMaxTicks[i];
                fractionatorPerfCalls[i] = 0;
                fractionatorPerfTicks[i] = 0;
                fractionatorPerfMaxTicks[i] = 0;
            }

            long[] feTypeCalls = new long[fractionatorPerfFeUpdateCallsByType.Length];
            long[] feTypeTicks = new long[fractionatorPerfFeUpdateTicksByType.Length];
            for (int i = 0; i < feTypeCalls.Length; i++) {
                feTypeCalls[i] = fractionatorPerfFeUpdateCallsByType[i];
                feTypeTicks[i] = fractionatorPerfFeUpdateTicksByType[i];
                fractionatorPerfFeUpdateCallsByType[i] = 0;
                fractionatorPerfFeUpdateTicksByType[i] = 0;
            }
            long[] stageCalls = new long[FractionatorPerfStageCount];
            long[] stageTicks = new long[FractionatorPerfStageCount];
            long[] stageMaxTicks = new long[FractionatorPerfStageCount];
            for (int i = 0; i < FractionatorPerfStageCount; i++) {
                stageCalls[i] = fractionatorPerfStageCalls[i];
                stageTicks[i] = fractionatorPerfStageTicks[i];
                stageMaxTicks[i] = fractionatorPerfStageMaxTicks[i];
                fractionatorPerfStageCalls[i] = 0;
                fractionatorPerfStageTicks[i] = 0;
                fractionatorPerfStageMaxTicks[i] = 0;
            }
            long[] detailCalls = new long[FractionatorPerfDetailCount];
            long[] detailTicks = new long[FractionatorPerfDetailCount];
            long[] detailMaxTicks = new long[FractionatorPerfDetailCount];
            for (int i = 0; i < FractionatorPerfDetailCount; i++) {
                detailCalls[i] = fractionatorPerfDetailCalls[i];
                detailTicks[i] = fractionatorPerfDetailTicks[i];
                detailMaxTicks[i] = fractionatorPerfDetailMaxTicks[i];
                fractionatorPerfDetailCalls[i] = 0;
                fractionatorPerfDetailTicks[i] = 0;
                fractionatorPerfDetailMaxTicks[i] = 0;
            }
            LogInfo("[FractionatorPerf] "
                    + $"window={windowTicks}gt "
                    + FormatPerfBucket("feUpdate", calls[FractionatorPerfUpdateFe],
                        ticks[FractionatorPerfUpdateFe], maxTicks[FractionatorPerfUpdateFe])
                    + " "
                    + FormatPerfBucket("vanillaUpdate", calls[FractionatorPerfUpdateVanilla],
                        ticks[FractionatorPerfUpdateVanilla], maxTicks[FractionatorPerfUpdateVanilla])
                    + " "
                    + FormatPerfBucket("feSetPC", calls[FractionatorPerfSetPcFe],
                        ticks[FractionatorPerfSetPcFe], maxTicks[FractionatorPerfSetPcFe])
                    + " "
                    + FormatPerfBucket("vanillaSetPC", calls[FractionatorPerfSetPcVanilla],
                        ticks[FractionatorPerfSetPcVanilla], maxTicks[FractionatorPerfSetPcVanilla])
                    + " "
                    + FormatStagePerf(stageCalls, stageTicks, stageMaxTicks)
                    + " "
                    + FormatDetailPerf(detailCalls, detailTicks, detailMaxTicks)
                    + " "
                    + FormatFeTypePerf(feTypeCalls, feTypeTicks));
        } finally {
            fractionatorPerfLogging = 0;
        }
    }

    private static string FormatPerfBucket(string name, long calls, long elapsedTicks, long maxTicks) {
        double totalMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
        double avgUs = calls > 0 ? elapsedTicks * 1000000.0 / Stopwatch.Frequency / calls : 0.0;
        double maxUs = maxTicks * 1000000.0 / Stopwatch.Frequency;
        return $"{name}:calls={calls},totalMs={totalMs:F3},avgUs={avgUs:F3},maxUs={maxUs:F3}";
    }

    private static string FormatFeTypePerf(long[] calls, long[] ticks) {
        return "feTypes="
               + FormatFeType(IFE交互塔, calls, ticks)
               + ";"
               + FormatFeType(IFE矿物复制塔, calls, ticks)
               + ";"
               + FormatFeType(IFE点数聚集塔, calls, ticks)
               + ";"
               + FormatFeType(IFE转化塔, calls, ticks)
               + ";"
               + FormatFeType(IFE精馏塔, calls, ticks);
    }

    private static string FormatStagePerf(long[] calls, long[] ticks, long[] maxTicks) {
        StringBuilder builder = new("feStages=");
        AppendStagePerf(builder, "prepare", FractionatorPerfStagePrepare, calls, ticks, maxTicks);
        AppendStagePerf(builder, "process", FractionatorPerfStageProcess, calls, ticks, maxTicks);
        AppendStagePerf(builder, "flush", FractionatorPerfStageFlushDeltas, calls, ticks, maxTicks);
        AppendStagePerf(builder, "zeroPressure", FractionatorPerfStageZeroPressure, calls, ticks, maxTicks);
        AppendStagePerf(builder, "fluidBelts", FractionatorPerfStageFluidBelts, calls, ticks, maxTicks);
        AppendStagePerf(builder, "productBelt", FractionatorPerfStageProductBelt, calls, ticks, maxTicks);
        AppendStagePerf(builder, "finalize", FractionatorPerfStageFinalize, calls, ticks, maxTicks);
        return builder.ToString();
    }

    private static string FormatDetailPerf(long[] calls, long[] ticks, long[] maxTicks) {
        StringBuilder builder = new("feDetails=");
        AppendStagePerf(builder, "stateRecipe", FractionatorPerfDetailPrepareStateRecipe, calls, ticks, maxTicks);
        AppendStagePerf(builder, "schema", FractionatorPerfDetailPrepareSchema, calls, ticks, maxTicks);
        AppendStagePerf(builder, "product", FractionatorPerfDetailPrepareProduct, calls, ticks, maxTicks);
        AppendStagePerf(builder, "config", FractionatorPerfDetailPrepareConfig, calls, ticks, maxTicks);
        AppendStagePerf(builder, "flushDeltas", FractionatorPerfDetailFlushDeltas, calls, ticks, maxTicks);
        AppendStagePerf(builder, "processGetOutputs", FractionatorPerfDetailProcessGetOutputs, calls, ticks,
            maxTicks);
        AppendStagePerf(builder, "processMergeOutputs", FractionatorPerfDetailProcessMergeOutputs, calls, ticks,
            maxTicks);
        return builder.ToString();
    }

    private static void AppendStagePerf(StringBuilder builder, string name, int stage, long[] calls, long[] ticks,
        long[] maxTicks) {
        if (stage > 0) {
            builder.Append('|');
        }
        builder.Append(name);
        builder.Append(':');
        builder.Append(calls[stage]);
        builder.Append('/');
        builder.Append((ticks[stage] * 1000.0 / Stopwatch.Frequency).ToString("F3"));
        builder.Append("ms/");
        builder.Append((calls[stage] > 0 ? ticks[stage] * 1000000.0 / Stopwatch.Frequency / calls[stage] : 0.0)
            .ToString("F3"));
        builder.Append("us/");
        builder.Append((maxTicks[stage] * 1000000.0 / Stopwatch.Frequency).ToString("F3"));
        builder.Append("maxUs");
    }

    private static string FormatFeType(int buildingID, long[] calls, long[] ticks) {
        int index = buildingID - IFE交互塔;
        long callCount = calls[index];
        double avgUs = callCount > 0 ? ticks[index] * 1000000.0 / Stopwatch.Frequency / callCount : 0.0;
        return $"{buildingID}:{callCount}/{avgUs:F3}us";
    }}
