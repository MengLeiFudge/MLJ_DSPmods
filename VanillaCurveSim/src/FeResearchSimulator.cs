using System;
using System.Collections.Generic;
using System.Linq;
using static FE.Utils.Utils;

namespace VanillaCurveSim;

/// <summary>解析塔的样本共享槽、源点阶段恢复及完整分钟献祭；线索与资格由工况明确提供。</summary>
internal static class FeResearchSimulator {
    public static FeResearchResult RunSamples(string content, int[] budgets, double sampleValue,
        int stack, int seconds, uint seed) {
        var (successRatio, destroyRatio) = FeRules.SampleProbabilities(sampleValue);
        var pending = budgets.ToList();
        var result = new FeResearchResult {
            Content = content, Stack = stack, SampleValue = sampleValue,
            SuccessProbability = successRatio, DestroyProbability = destroyRatio,
            ExpectedSeconds = budgets.Sum() / (30d * stack * successRatio), ContentCount = budgets.Length,
        };
        double passing = 0;
        int progress = 0;
        for (int tick = 0; tick < seconds * 60 && pending.Count > 0; tick++) {
            result.ElapsedSeconds = (tick + 1) / 60d;
            passing += stack / 2d;
            int count = (int)passing;
            passing -= count;
            if (count == 0) { continue; }
            result.Attempts += count;
            double roll = GetRandDouble(ref seed);
            int successes = (int)(count * (double)successRatio + roll);
            int destroyed = (int)(count * ((double)successRatio + destroyRatio) + roll) - successes;
            int remaining = successes;
            while (remaining > 0) {
                if (pending.Count == 0) { progress = 0; break; }
                double perSuccess = 10000d / pending.Average();
                int applied = Math.Min(remaining, Math.Max(1, (int)Math.Ceiling((10000 - progress) / perSuccess)));
                progress += FeRules.Roll(ref seed, applied * perSuccess);
                remaining -= applied;
                if (progress < 10000) { break; }
                int index = Math.Min(pending.Count - 1, (int)(GetRandDouble(ref seed) * pending.Count));
                pending.RemoveAt(index);
                result.CompletedContents++;
                progress -= 10000;
            }
            int used = successes - remaining;
            if (remaining > 0 && pending.Count == 0) { destroyed = (int)((long)destroyed * used / successes); }
            result.ReturnedSamples += used;
            result.DestroyedSamples += destroyed;
        }
        result.Progress = pending.Count == 0 ? 0 : Math.Max(0, Math.Min(9999, progress));
        return result;
    }

    public static FeRecoveryResult RunRecovery(int stage, int stack, int seconds, uint seed) {
        int budget = FeRules.StageMemoryBudgets[stage - 1];
        var result = new FeRecoveryResult {
            Stage = stage, Stack = stack, RequiredMemory = budget, ExpectedSeconds = budget / (30d * stack * 0.02f),
        };
        double passing = 0;
        for (int tick = 0; tick < seconds * 60 && result.ConsumedMemory < budget; tick++) {
            result.ElapsedSeconds = (tick + 1) / 60d;
            passing += stack / 2d;
            int count = (int)passing;
            passing -= count;
            result.Passes += count;
            int successes = FeRules.Roll(ref seed, count * (double)0.02f);
            result.ConsumedMemory += Math.Min(budget - result.ConsumedMemory, successes);
        }
        return result;
    }

    public static List<FeOfferingMinute> RunOffering(FeScenarioConfig scenario, int seconds, uint seed) {
        var minutes = new List<FeOfferingMinute>();
        double passing = 0;
        float boost = 0;
        for (int minute = 0; minute < seconds / 60; minute++) {
            var result = new FeOfferingMinute { Minute = minute + 1, AppliedBoost = boost };
            for (int tick = 0; tick < 3600; tick++) {
                passing += scenario.MemoryPassesPerSecond / 60;
                int count = (int)passing;
                passing -= count;
                result.ConsumedMemory += FeRules.Roll(ref seed, count * 0.08d);
            }
            boost = FeRules.MemoryBoost(result.ConsumedMemory);
            result.NextBoost = boost;
            result.LegendaryRecoveryEligible = result.ConsumedMemory >= 50;
            minutes.Add(result);
        }
        return minutes;
    }

    /// <summary>通信已恢复，六档各由一座同集装解析塔持续供料；每档缓存共享，不按塔数复制。</summary>
    public static List<FeExpansionResult> RunExpansion(int stack, int seconds, uint seed) {
        var rows = Enumerable.Range(0, 6).Select(i => new FeExpansionResult { MatrixId = 6001 + i }).ToList();
        double passing = 0;
        for (int tick = 0; tick < seconds * 60; tick++) {
            passing += stack / 2d;
            int count = (int)passing;
            passing -= count;
            for (int i = 0; i < rows.Count; i++) {
                FeExpansionResult row = rows[i];
                int processed = FeRules.Roll(ref seed, count * 0.08d);
                int supplied = Math.Min(processed, 60 - row.Buffered);
                row.Processed += processed;
                row.Buffered += supplied;
                row.MemoryProduced += FeRules.Roll(ref seed,
                    (processed - supplied) * (double)(1f / FeRules.MatrixMemoryBudgets[i]));
                if ((tick + 1) % 60 != 0 || row.Buffered == 0) { continue; }
                row.Buffered--;
                row.ExpansionConsumed++;
                row.CapacityIncrement = Math.Sqrt(Math.Pow(row.CapacityIncrement + 11, 2) + (1 << i) / 30d) - 11;
            }
        }
        return rows;
    }
}
