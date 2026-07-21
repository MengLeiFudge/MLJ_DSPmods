using System;
using System.Collections.Generic;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization.Analysis;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.FracRecipes;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Protocols;

/// <summary>
/// 协议检索的货币策略。
/// </summary>
public enum ProtocolRetrievalMode {
    Broad,
    Directional,
    Anchored,
}

/// <summary>
/// 单次协议检索无法执行时的明确原因。
/// </summary>
public enum ProtocolRetrievalStopReason {
    None,
    InvalidRequest,
    NoOpportunity,
    NoCandidate,
    InsufficientFragments,
    InsufficientMemorySourcePoints,
    AwaitingHost,
}

/// <summary>
/// 单次协议检索的实际结果类型。
/// </summary>
public enum ProtocolRetrievalOutcome {
    None,
    Failed,
    Discovered,
    Progressed,
    Completed,
    DeepAnalysis,
}

/// <summary>
/// 声明一次检索的阶段、策略及其方向或锚定目标。
/// </summary>
public readonly struct ProtocolRetrievalRequest(
    string stageKey,
    ProtocolRetrievalMode mode = ProtocolRetrievalMode.Broad,
    ERecipe directionalRecipeType = default,
    RecipeKey anchoredRecipeKey = default,
    bool hasAnchoredRecipe = false) {
    public string StageKey { get; } = stageKey;
    public ProtocolRetrievalMode Mode { get; } = mode;
    public ERecipe DirectionalRecipeType { get; } = directionalRecipeType;
    public RecipeKey AnchoredRecipeKey { get; } = anchoredRecipeKey;
    public bool HasAnchoredRecipe { get; } = hasAnchoredRecipe;

    public static ProtocolRetrievalRequest Broad(string stageKey) => new(stageKey);

    public static ProtocolRetrievalRequest Directional(string stageKey, ERecipe recipeType) =>
        new(stageKey, ProtocolRetrievalMode.Directional, recipeType);

    public static ProtocolRetrievalRequest Anchored(string stageKey, RecipeKey recipeKey) =>
        new(stageKey, ProtocolRetrievalMode.Anchored, default, recipeKey, true);
}

/// <summary>
/// 描述一次协议检索的结果，供 UI 和日志读取。
/// </summary>
public readonly struct ProtocolRetrievalResult(
    ProtocolRetrievalOutcome outcome,
    RecipeKey recipeKey,
    int previousCompleteness,
    int currentCompleteness,
    bool awardedTechPoint,
    int spentFragments,
    int spentMemorySourcePoints,
    int awardedFragments,
    ProtocolRetrievalStopReason stopReason = ProtocolRetrievalStopReason.None) {
    public ProtocolRetrievalOutcome Outcome { get; } = outcome;
    public RecipeKey RecipeKey { get; } = recipeKey;
    public int PreviousCompleteness { get; } = previousCompleteness;
    public int CurrentCompleteness { get; } = currentCompleteness;
    public bool AwardedTechPoint { get; } = awardedTechPoint;
    public int SpentFragments { get; } = spentFragments;
    public int SpentMemorySourcePoints { get; } = spentMemorySourcePoints;
    public int AwardedFragments { get; } = awardedFragments;
    public ProtocolRetrievalStopReason StopReason { get; } = stopReason;
}

/// <summary>
/// 汇总连续单次检索的各结果数量、资源流动和停止原因。
/// </summary>
public readonly struct ProtocolRetrievalBatchResult(
    int executedCount,
    int failedCount,
    int discoveredCount,
    int progressedCount,
    int completedCount,
    int deepAnalysisCount,
    int awardedTechPoints,
    int spentFragments,
    int spentMemorySourcePoints,
    int awardedFragments,
    ProtocolRetrievalStopReason stopReason) {
    public int ExecutedCount { get; } = executedCount;
    public int FailedCount { get; } = failedCount;
    public int DiscoveredCount { get; } = discoveredCount;
    public int ProgressedCount { get; } = progressedCount;
    public int CompletedCount { get; } = completedCount;
    public int DeepAnalysisCount { get; } = deepAnalysisCount;
    public int AwardedTechPoints { get; } = awardedTechPoints;
    public int SpentFragments { get; } = spentFragments;
    public int SpentMemorySourcePoints { get; } = spentMemorySourcePoints;
    public int AwardedFragments { get; } = awardedFragments;
    public ProtocolRetrievalStopReason StopReason { get; } = stopReason;
}

/// <summary>
/// 提供恢复页面展示所需的单项协议只读投影。
/// </summary>
public readonly struct ProtocolRetrievalProtocolSnapshot(
    RecipeKey recipeKey,
    string displayName,
    bool discovered,
    int completeness,
    bool countsTowardStageCompletion,
    bool eligible,
    bool preferred) {
    public RecipeKey RecipeKey { get; } = recipeKey;
    public string DisplayName { get; } = displayName;
    public bool Discovered { get; } = discovered;
    public int Completeness { get; } = completeness;
    public bool CountsTowardStageCompletion { get; } = countsTowardStageCompletion;
    public bool Eligible { get; } = eligible;
    public bool Preferred { get; } = preferred;
    public bool IsActionable => Eligible && Completeness < 100;
}

/// <summary>
/// 提供恢复页面展示所需的阶段检索只读投影。
/// </summary>
public readonly struct ProtocolRetrievalStageSnapshot(
    int availableOpportunities,
    long pendingData,
    long nextOpportunityCost,
    int failureStreak,
    int discoveryStreak,
    bool stageComplete,
    bool hasRequiredProtocols,
    bool hasPriorityCandidate,
    long fragments,
    long memorySourcePoints,
    IReadOnlyList<ProtocolRetrievalProtocolSnapshot> protocols) {
    public int AvailableOpportunities { get; } = availableOpportunities;
    public long PendingData { get; } = pendingData;
    public long NextOpportunityCost { get; } = nextOpportunityCost;
    public int FailureStreak { get; } = failureStreak;
    public int DiscoveryStreak { get; } = discoveryStreak;
    public bool StageComplete { get; } = stageComplete;
    public bool HasRequiredProtocols { get; } = hasRequiredProtocols;
    public bool HasPriorityCandidate { get; } = hasPriorityCandidate;
    public long Fragments { get; } = fragments;
    public long MemorySourcePoints { get; } = memorySourcePoints;
    public IReadOnlyList<ProtocolRetrievalProtocolSnapshot> Protocols { get; } = protocols;
}

/// <summary>
/// 集中执行广域、方向和锚定协议检索，并维护概率、保底和策略货币基线。
/// </summary>
public static class ProtocolRetrievalService {
    public const int DirectionalFragmentCost = 8;
    public const int AnchoredMemorySourcePointCost = 1;
    public const int FailedRetrievalFragmentReward = 2;
    public const int DefaultBatchCount = 10;

    private static readonly Random random = new();

    /// <summary>
    /// 保持旧调用方兼容的广域单次检索入口。
    /// </summary>
    public static bool TryRetrieve(string stageKey, out ProtocolRetrievalResult result) =>
        TryRetrieve(ProtocolRetrievalRequest.Broad(stageKey), out result);

    /// <summary>
    /// 按请求执行一次检索；无候选或货币不足时不消费检索机会和货币。
    /// </summary>
    public static bool TryRetrieve(ProtocolRetrievalRequest request, out ProtocolRetrievalResult result) {
        if (NebulaMultiplayerModAPI.RequestProtocolRetrieval(request, 1)) {
            result = CannotExecute(ProtocolRetrievalStopReason.AwaitingHost);
            return false;
        }

        bool executed = TryRetrieveCore(request, out result);
        if (executed) {
            NebulaMultiplayerModAPI.BroadcastCivilizationState();
        }
        return executed;
    }

    private static bool TryRetrieveCore(ProtocolRetrievalRequest request, out ProtocolRetrievalResult result) {
        result = default;
        if (string.IsNullOrEmpty(request.StageKey)) {
            result = CannotExecute(ProtocolRetrievalStopReason.InvalidRequest);
            return false;
        }

        List<ProtocolDefinition> candidates = GetCandidates(request);
        bool deepAnalysis = request.Mode == ProtocolRetrievalMode.Broad
                            && candidates.Count == 0
                            && ProtocolCatalog.IsStageComplete(request.StageKey);
        if (!deepAnalysis && candidates.Count == 0) {
            result = CannotExecute(ProtocolRetrievalStopReason.NoCandidate);
            return false;
        }

        if (AnalysisService.GetProgress(request.StageKey).AvailableOpportunities <= 0) {
            result = CannotExecute(ProtocolRetrievalStopReason.NoOpportunity);
            return false;
        }

        if (!TrySpendStrategyCurrency(request.Mode, out int spentFragments, out int spentMemorySourcePoints,
                out ProtocolRetrievalStopReason stopReason)) {
            result = CannotExecute(stopReason);
            return false;
        }

        if (!AnalysisService.TryConsumeOpportunity(request.StageKey)) {
            RefundStrategyCurrency(spentFragments, spentMemorySourcePoints);
            result = CannotExecute(ProtocolRetrievalStopReason.NoOpportunity);
            return false;
        }

        if (deepAnalysis) {
            DeepAnalysisService.SubmitOpportunity(out bool awardedPoint);
            result = new(ProtocolRetrievalOutcome.DeepAnalysis, default, 0, 0, awardedPoint,
                spentFragments, spentMemorySourcePoints, 0);
            return true;
        }

        ProtocolProgressStore.StageRetrievalProgress stageProgress = ProtocolProgressStore.GetStageProgress(request.StageKey);
        bool guaranteedEffective = request.Mode == ProtocolRetrievalMode.Anchored
                                   || stageProgress.FailureStreak >= 4;
        if (!guaranteedEffective && random.NextDouble() < 0.20d) {
            stageProgress.FailureStreak++;
            stageProgress.DiscoveryStreak++;
            AddItemToModData(IFE残片, FailedRetrievalFragmentReward);
            result = new(ProtocolRetrievalOutcome.Failed, default, 0, 0, false,
                spentFragments, spentMemorySourcePoints, FailedRetrievalFragmentReward);
            return true;
        }

        stageProgress.FailureStreak = 0;
        result = ResolveEffectiveRetrieval(candidates, stageProgress, spentFragments, spentMemorySourcePoints);
        CivilizationRuntimeSync.Refresh();
        return true;
    }

    /// <summary>
    /// 连续执行指定次数的单次规则，每一步均独立更新保底和候选池。
    /// </summary>
    public static ProtocolRetrievalBatchResult RetrieveBatch(ProtocolRetrievalRequest request, int requestedCount) {
        int limit = Math.Max(0, requestedCount);
        if (limit > 0 && NebulaMultiplayerModAPI.RequestProtocolRetrieval(request, limit)) {
            return new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                ProtocolRetrievalStopReason.AwaitingHost);
        }

        int executed = 0;
        int failed = 0;
        int discovered = 0;
        int progressed = 0;
        int completed = 0;
        int deepAnalysis = 0;
        int techPoints = 0;
        int spentFragments = 0;
        int spentMemorySourcePoints = 0;
        int awardedFragments = 0;
        ProtocolRetrievalStopReason stopReason = ProtocolRetrievalStopReason.None;

        for (int i = 0; i < limit; i++) {
            if (!TryRetrieveCore(request, out ProtocolRetrievalResult result)) {
                stopReason = result.StopReason;
                break;
            }

            executed++;
            spentFragments += result.SpentFragments;
            spentMemorySourcePoints += result.SpentMemorySourcePoints;
            awardedFragments += result.AwardedFragments;
            if (result.AwardedTechPoint) {
                techPoints++;
            }
            switch (result.Outcome) {
                case ProtocolRetrievalOutcome.Failed:
                    failed++;
                    break;
                case ProtocolRetrievalOutcome.Discovered:
                    discovered++;
                    break;
                case ProtocolRetrievalOutcome.Progressed:
                    progressed++;
                    break;
                case ProtocolRetrievalOutcome.Completed:
                    completed++;
                    break;
                case ProtocolRetrievalOutcome.DeepAnalysis:
                    deepAnalysis++;
                    break;
            }
        }

        if (executed > 0) {
            NebulaMultiplayerModAPI.BroadcastCivilizationState();
        }
        return new(executed, failed, discovered, progressed, completed, deepAnalysis, techPoints,
            spentFragments, spentMemorySourcePoints, awardedFragments, stopReason);
    }

    /// <summary>
    /// 构建页面所需的阶段、协议和两种策略货币余额快照。
    /// </summary>
    public static ProtocolRetrievalStageSnapshot GetStageSnapshot(string stageKey) {
        AnalysisProgressStore.StageProgress analysis = AnalysisService.GetProgress(stageKey);
        ProtocolProgressStore.StageRetrievalProgress retrieval = ProtocolProgressStore.GetStageProgress(stageKey);
        List<ProtocolRetrievalProtocolSnapshot> protocols = [];
        bool hasPriorityCandidate = false;
        foreach (ProtocolDefinition definition in ProtocolCatalog.GetByStage(stageKey)) {
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            bool eligible = ProtocolEligibilityService.IsEligible(definition);
            bool preferred = retrieval.HasPreferredRecipe && retrieval.PreferredRecipe.Equals(definition.RecipeKey);
            bool actionable = eligible && progress.Completeness < 100;
            hasPriorityCandidate |= actionable && progress.Discovered;
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(definition.RecipeKey.RecipeType,
                definition.RecipeKey.InputId);
            protocols.Add(new(definition.RecipeKey, recipe?.TypeName ?? $"{definition.RecipeKey.RecipeType}/{definition.RecipeKey.InputId}",
                progress.Discovered, progress.Completeness, definition.CountsTowardStageCompletion, eligible, preferred));
        }

        return new(analysis.AvailableOpportunities, analysis.PendingData, AnalysisService.GetNextOpportunityCost(stageKey),
            retrieval.FailureStreak, retrieval.DiscoveryStreak, ProtocolCatalog.IsStageComplete(stageKey),
            ProtocolCatalog.HasRequiredProtocols(stageKey), hasPriorityCandidate, GetModDataItemCount(IFE残片),
            GetModDataItemCount(IFE记忆源点), protocols);
    }

    /// <summary>
    /// 返回当前阶段可执行方向检索的配方类型。
    /// </summary>
    public static List<ERecipe> GetDirectionalRecipeTypes(string stageKey) {
        List<ERecipe> recipeTypes = [];
        foreach (ProtocolRetrievalProtocolSnapshot protocol in GetStageSnapshot(stageKey).Protocols) {
            if (protocol.IsActionable && !recipeTypes.Contains(protocol.RecipeKey.RecipeType)) {
                recipeTypes.Add(protocol.RecipeKey.RecipeType);
            }
        }
        return recipeTypes;
    }

    /// <summary>
    /// 返回当前阶段可锚定的未完成协议。
    /// </summary>
    public static List<ProtocolRetrievalProtocolSnapshot> GetAnchoredCandidates(string stageKey) {
        List<ProtocolRetrievalProtocolSnapshot> candidates = [];
        foreach (ProtocolRetrievalProtocolSnapshot protocol in GetStageSnapshot(stageKey).Protocols) {
            if (protocol.IsActionable) {
                candidates.Add(protocol);
            }
        }
        return candidates;
    }

    /// <summary>
    /// 判断给定请求是否满足候选、机会和策略货币的前置条件。
    /// </summary>
    public static bool CanRetrieve(ProtocolRetrievalRequest request, out ProtocolRetrievalStopReason stopReason) {
        if (string.IsNullOrEmpty(request.StageKey)) {
            stopReason = ProtocolRetrievalStopReason.InvalidRequest;
            return false;
        }
        List<ProtocolDefinition> candidates = GetCandidates(request);
        bool deepAnalysis = request.Mode == ProtocolRetrievalMode.Broad
                            && candidates.Count == 0
                            && ProtocolCatalog.IsStageComplete(request.StageKey);
        if (!deepAnalysis && candidates.Count == 0) {
            stopReason = ProtocolRetrievalStopReason.NoCandidate;
            return false;
        }
        if (AnalysisService.GetProgress(request.StageKey).AvailableOpportunities <= 0) {
            stopReason = ProtocolRetrievalStopReason.NoOpportunity;
            return false;
        }
        stopReason = GetCurrencyStopReason(request.Mode);
        return stopReason == ProtocolRetrievalStopReason.None;
    }

    /// <summary>
    /// 在当前阶段的已发现未完成协议间循环优先推进目标。
    /// </summary>
    public static bool CyclePreferredProtocol(string stageKey) {
        if (NebulaMultiplayerModAPI.RequestPreferredProtocolCycle(stageKey)) {
            return true;
        }

        List<ProtocolDefinition> candidates = [];
        foreach (ProtocolDefinition definition in ProtocolCatalog.GetByStage(stageKey)) {
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            if (progress.Discovered && progress.Completeness < 100 && ProtocolEligibilityService.IsEligible(definition)) {
                candidates.Add(definition);
            }
        }

        ProtocolProgressStore.StageRetrievalProgress stageProgress = ProtocolProgressStore.GetStageProgress(stageKey);
        if (candidates.Count == 0) {
            if (stageProgress.HasPreferredRecipe) {
                stageProgress.HasPreferredRecipe = false;
                NebulaMultiplayerModAPI.BroadcastCivilizationState();
            }
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
        NebulaMultiplayerModAPI.BroadcastCivilizationState();
        return true;
    }

    private static ProtocolRetrievalResult ResolveEffectiveRetrieval(List<ProtocolDefinition> candidates,
        ProtocolProgressStore.StageRetrievalProgress stageProgress, int spentFragments, int spentMemorySourcePoints) {
        List<ProtocolDefinition> undiscovered = [];
        List<ProtocolDefinition> discoveredIncomplete = [];
        foreach (ProtocolDefinition candidate in candidates) {
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(candidate.RecipeKey);
            if (!progress.Discovered) {
                undiscovered.Add(candidate);
            } else {
                discoveredIncomplete.Add(candidate);
            }
        }

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
            return new(progress.Completeness >= 100 ? ProtocolRetrievalOutcome.Completed : ProtocolRetrievalOutcome.Discovered,
                definition.RecipeKey, previous, progress.Completeness, false, spentFragments, spentMemorySourcePoints, 0);
        }

        ProtocolDefinition target = SelectProgressTarget(discoveredIncomplete, stageProgress);
        ProtocolProgressStore.ProtocolProgress targetProgress = ProtocolProgressStore.GetOrCreate(target.RecipeKey);
        int oldCompleteness = targetProgress.Completeness;
        targetProgress.Completeness = Math.Min(100, oldCompleteness + random.Next(12, 26));
        stageProgress.DiscoveryStreak++;
        ProtocolRetrievalOutcome outcome = targetProgress.Completeness >= 100
            ? ProtocolRetrievalOutcome.Completed
            : ProtocolRetrievalOutcome.Progressed;
        return new(outcome, target.RecipeKey, oldCompleteness, targetProgress.Completeness, false,
            spentFragments, spentMemorySourcePoints, 0);
    }

    private static List<ProtocolDefinition> GetCandidates(ProtocolRetrievalRequest request) {
        List<ProtocolDefinition> candidates = [];
        if (request.Mode == ProtocolRetrievalMode.Anchored && !request.HasAnchoredRecipe) {
            return candidates;
        }

        foreach (ProtocolDefinition definition in ProtocolCatalog.GetByStage(request.StageKey)) {
            if (!ProtocolEligibilityService.IsEligible(definition) || ProtocolProgressStore.IsComplete(definition.RecipeKey)) {
                continue;
            }
            if (request.Mode == ProtocolRetrievalMode.Directional
                && definition.RecipeKey.RecipeType != request.DirectionalRecipeType) {
                continue;
            }
            if (request.Mode == ProtocolRetrievalMode.Anchored
                && !definition.RecipeKey.Equals(request.AnchoredRecipeKey)) {
                continue;
            }
            candidates.Add(definition);
        }
        return candidates;
    }

    private static ProtocolRetrievalResult CannotExecute(ProtocolRetrievalStopReason stopReason) =>
        new(ProtocolRetrievalOutcome.None, default, 0, 0, false, 0, 0, 0, stopReason);

    private static ProtocolRetrievalStopReason GetCurrencyStopReason(ProtocolRetrievalMode mode) {
        return mode switch {
            ProtocolRetrievalMode.Directional when GetModDataItemCount(IFE残片) < DirectionalFragmentCost =>
                ProtocolRetrievalStopReason.InsufficientFragments,
            ProtocolRetrievalMode.Anchored when GetModDataItemCount(IFE记忆源点) < AnchoredMemorySourcePointCost =>
                ProtocolRetrievalStopReason.InsufficientMemorySourcePoints,
            _ => ProtocolRetrievalStopReason.None,
        };
    }

    private static bool TrySpendStrategyCurrency(ProtocolRetrievalMode mode, out int spentFragments,
        out int spentMemorySourcePoints, out ProtocolRetrievalStopReason stopReason) {
        spentFragments = 0;
        spentMemorySourcePoints = 0;
        stopReason = GetCurrencyStopReason(mode);
        if (stopReason != ProtocolRetrievalStopReason.None) {
            return false;
        }

        switch (mode) {
            case ProtocolRetrievalMode.Directional:
                lock (centerItemCount) {
                    if (GetModDataItemCount(IFE残片) < DirectionalFragmentCost
                        || TakeItemFromModData(IFE残片, DirectionalFragmentCost, out _) != DirectionalFragmentCost) {
                        stopReason = ProtocolRetrievalStopReason.InsufficientFragments;
                        return false;
                    }
                }
                spentFragments = DirectionalFragmentCost;
                return true;
            case ProtocolRetrievalMode.Anchored:
                lock (centerItemCount) {
                    if (GetModDataItemCount(IFE记忆源点) < AnchoredMemorySourcePointCost
                        || TakeItemFromModData(IFE记忆源点, AnchoredMemorySourcePointCost, out _)
                        != AnchoredMemorySourcePointCost) {
                        stopReason = ProtocolRetrievalStopReason.InsufficientMemorySourcePoints;
                        return false;
                    }
                }
                spentMemorySourcePoints = AnchoredMemorySourcePointCost;
                return true;
            default:
                return true;
        }
    }

    private static void RefundStrategyCurrency(int spentFragments, int spentMemorySourcePoints) {
        if (spentFragments > 0) {
            AddItemToModData(IFE残片, spentFragments);
        }
        if (spentMemorySourcePoints > 0) {
            AddItemToModData(IFE记忆源点, spentMemorySourcePoints);
        }
    }

    private static ProtocolDefinition SelectProgressTarget(List<ProtocolDefinition> candidates,
        ProtocolProgressStore.StageRetrievalProgress stageProgress) {
        if (stageProgress.HasPreferredRecipe) {
            foreach (ProtocolDefinition candidate in candidates) {
                if (candidate.RecipeKey.Equals(stageProgress.PreferredRecipe) && random.NextDouble() < 0.4d) {
                    return candidate;
                }
            }
        }
        return candidates[random.Next(candidates.Count)];
    }
}
