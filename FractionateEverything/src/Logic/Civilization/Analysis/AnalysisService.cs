using System;
using FE.Logic.Civilization.Configuration;

namespace FE.Logic.Civilization.Analysis;

/// <summary>
/// 消费实体解析数据，并按递增阈值生成由玩家主动使用的协议检索机会。
/// </summary>
public static class AnalysisService {
    private static readonly int[] baseOpportunityCosts = [8, 12, 18, 28, 42, 64];

    public static bool TrySubmitDataItem(int itemId, long count, out int generatedOpportunities) {
        generatedOpportunities = 0;
        MatrixStageDefinition stage = ProgressionProfileRegistry.Current?.GetStageByAnalysisDataItem(itemId);
        if (stage == null || count <= 0) {
            return false;
        }

        AnalysisProgressStore.StageProgress progress = AnalysisProgressStore.GetOrCreate(stage.StageKey);
        progress.PendingData = count > long.MaxValue - progress.PendingData
            ? long.MaxValue
            : progress.PendingData + count;
        while (progress.AvailableOpportunities < int.MaxValue
               && progress.PendingData >= GetNextOpportunityCost(stage.StageKey)) {
            long cost = GetNextOpportunityCost(stage.StageKey);
            if (cost == 1_000_000_000L) {
                long availableRoom = int.MaxValue - (long)progress.AvailableOpportunities;
                long batch = Math.Min(progress.PendingData / cost, availableRoom);
                if (batch <= 0) {
                    break;
                }
                progress.PendingData -= batch * cost;
                progress.GeneratedOpportunities += batch;
                progress.AvailableOpportunities += (int)batch;
                generatedOpportunities = batch >= int.MaxValue - generatedOpportunities
                    ? int.MaxValue
                    : generatedOpportunities + (int)batch;
                continue;
            }
            progress.PendingData -= cost;
            progress.GeneratedOpportunities++;
            progress.AvailableOpportunities++;
            if (generatedOpportunities < int.MaxValue) {
                generatedOpportunities++;
            }
        }
        return true;
    }

    public static bool TryConsumeOpportunity(string stageKey) {
        AnalysisProgressStore.StageProgress progress = AnalysisProgressStore.GetOrCreate(stageKey);
        if (progress.AvailableOpportunities <= 0) {
            return false;
        }
        progress.AvailableOpportunities--;
        return true;
    }

    public static long GetNextOpportunityCost(string stageKey) {
        MatrixStageDefinition stage = ProgressionProfileRegistry.Current?.GetStage(stageKey);
        if (stage == null) {
            return long.MaxValue;
        }
        AnalysisProgressStore.StageProgress progress = AnalysisProgressStore.GetOrCreate(stageKey);
        int baseCost = baseOpportunityCosts[Math.Min(stage.Order, baseOpportunityCosts.Length - 1)];
        double cost = baseCost * Math.Pow(1.32d, progress.GeneratedOpportunities);
        return cost >= 1_000_000_000d ? 1_000_000_000L : Math.Max(1L, (long)Math.Ceiling(cost));
    }

    public static AnalysisProgressStore.StageProgress GetProgress(string stageKey) =>
        AnalysisProgressStore.GetOrCreate(stageKey);
}
