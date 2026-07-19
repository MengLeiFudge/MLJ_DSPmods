using System;
using System.Collections.Generic;
using FE.Logic.Civilization.Analysis;
using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Civilization.Protocols;

public enum ProtocolRetrievalOutcome {
    None,
    Failed,
    Discovered,
    Progressed,
    Completed,
    DeepAnalysis,
}

/// <summary>
/// 描述一次协议检索的结果，供 UI 和日志读取。
/// </summary>
public readonly struct ProtocolRetrievalResult(
    ProtocolRetrievalOutcome outcome,
    RecipeKey recipeKey,
    int previousCompleteness,
    int currentCompleteness,
    bool awardedTechPoint) {
    public ProtocolRetrievalOutcome Outcome { get; } = outcome;
    public RecipeKey RecipeKey { get; } = recipeKey;
    public int PreviousCompleteness { get; } = previousCompleteness;
    public int CurrentCompleteness { get; } = currentCompleteness;
    public bool AwardedTechPoint { get; } = awardedTechPoint;
}

/// <summary>
/// 消费检索机会，执行失败、新发现、随机推进或阶段完成后的深层解析。
/// </summary>
public static class ProtocolRetrievalService {
    private static readonly Random random = new();

    public static bool TryRetrieve(string stageKey, out ProtocolRetrievalResult result) {
        result = default;
        IReadOnlyList<ProtocolDefinition> stageDefinitions = ProtocolCatalog.GetByStage(stageKey);

        List<ProtocolDefinition> eligible = [];
        List<ProtocolDefinition> undiscovered = [];
        List<ProtocolDefinition> discoveredIncomplete = [];
        foreach (ProtocolDefinition definition in stageDefinitions) {
            if (!ProtocolEligibilityService.IsEligible(definition)) {
                continue;
            }
            eligible.Add(definition);
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            if (!progress.Discovered) {
                undiscovered.Add(definition);
            } else if (progress.Completeness < 100) {
                discoveredIncomplete.Add(definition);
            }
        }

        bool hasActionableProtocol = undiscovered.Count > 0 || discoveredIncomplete.Count > 0;
        if (ProtocolCatalog.IsStageComplete(stageKey) && !hasActionableProtocol) {
            if (!AnalysisService.TryConsumeOpportunity(stageKey)) {
                return false;
            }
            DeepAnalysisService.SubmitOpportunity(out bool awardedPoint);
            result = new(ProtocolRetrievalOutcome.DeepAnalysis, default, 0, 0, awardedPoint);
            return true;
        }

        if (eligible.Count == 0 || !hasActionableProtocol || !AnalysisService.TryConsumeOpportunity(stageKey)) {
            return false;
        }

        ProtocolProgressStore.StageRetrievalProgress stageProgress = ProtocolProgressStore.GetStageProgress(stageKey);
        bool guaranteedEffective = stageProgress.FailureStreak >= 4;
        if (!guaranteedEffective && random.NextDouble() < 0.20d) {
            stageProgress.FailureStreak++;
            stageProgress.DiscoveryStreak++;
            result = new(ProtocolRetrievalOutcome.Failed, default, 0, 0, false);
            return true;
        }

        stageProgress.FailureStreak = 0;
        bool shouldDiscover = undiscovered.Count > 0
                              && (discoveredIncomplete.Count == 0
                                  || stageProgress.DiscoveryStreak >= 5
                                  || random.NextDouble() < 0.35d);
        if (shouldDiscover) {
            ProtocolDefinition definition = undiscovered[random.Next(undiscovered.Count)];
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            int previous = progress.Completeness;
            progress.Discovered = true;
            progress.Completeness = Math.Min(100, random.Next(20, 41));
            stageProgress.DiscoveryStreak = 0;
            result = new(progress.Completeness >= 100
                    ? ProtocolRetrievalOutcome.Completed
                    : ProtocolRetrievalOutcome.Discovered,
                definition.RecipeKey, previous, progress.Completeness, false);
            CivilizationRuntimeSync.Refresh();
            return true;
        }

        if (discoveredIncomplete.Count == 0 && undiscovered.Count > 0) {
            ProtocolDefinition definition = undiscovered[random.Next(undiscovered.Count)];
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            progress.Discovered = true;
            progress.Completeness = Math.Min(100, random.Next(20, 41));
            stageProgress.DiscoveryStreak = 0;
            result = new(ProtocolRetrievalOutcome.Discovered, definition.RecipeKey, 0, progress.Completeness, false);
            CivilizationRuntimeSync.Refresh();
            return true;
        }

        ProtocolDefinition target = SelectProgressTarget(discoveredIncomplete, stageProgress);
        ProtocolProgressStore.ProtocolProgress targetProgress = ProtocolProgressStore.GetOrCreate(target.RecipeKey);
        int oldCompleteness = targetProgress.Completeness;
        targetProgress.Completeness = Math.Min(100, oldCompleteness + random.Next(12, 26));
        stageProgress.DiscoveryStreak++;
        ProtocolRetrievalOutcome outcome = targetProgress.Completeness >= 100
            ? ProtocolRetrievalOutcome.Completed
            : ProtocolRetrievalOutcome.Progressed;
        result = new(outcome, target.RecipeKey, oldCompleteness, targetProgress.Completeness, false);
        CivilizationRuntimeSync.Refresh();
        return true;
    }

    public static bool CyclePreferredProtocol(string stageKey) {
        List<ProtocolDefinition> candidates = [];
        foreach (ProtocolDefinition definition in ProtocolCatalog.GetByStage(stageKey)) {
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            if (progress.Discovered && progress.Completeness < 100 && ProtocolEligibilityService.IsEligible(definition)) {
                candidates.Add(definition);
            }
        }

        ProtocolProgressStore.StageRetrievalProgress stageProgress = ProtocolProgressStore.GetStageProgress(stageKey);
        if (candidates.Count == 0) {
            stageProgress.HasPreferredRecipe = false;
            return false;
        }

        int currentIndex = -1;
        if (stageProgress.HasPreferredRecipe) {
            for (int i = 0; i < candidates.Count; i++) {
                if (candidates[i].RecipeKey.Equals(stageProgress.PreferredRecipe)) {
                    currentIndex = i;
                    break;
                }
            }
        }
        stageProgress.PreferredRecipe = candidates[(currentIndex + 1) % candidates.Count].RecipeKey;
        stageProgress.HasPreferredRecipe = true;
        return true;
    }

    private static ProtocolDefinition SelectProgressTarget(List<ProtocolDefinition> candidates,
        ProtocolProgressStore.StageRetrievalProgress stageProgress) {
        if (stageProgress.HasPreferredRecipe) {
            ProtocolDefinition preferred = null;
            foreach (ProtocolDefinition candidate in candidates) {
                if (candidate.RecipeKey.Equals(stageProgress.PreferredRecipe)) {
                    preferred = candidate;
                    break;
                }
            }
            if (preferred != null) {
                // 40% 直接命中优先目标，剩余 60% 进入全候选均匀随机；最终概率即 0.4 + 0.6 / N。
                if (random.NextDouble() < 0.4d) {
                    return preferred;
                }
            }
        }
        return candidates[random.Next(candidates.Count)];
    }
}
