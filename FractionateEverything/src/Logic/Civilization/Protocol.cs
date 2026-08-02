using FE.Logic.Fractionation.Recipes;
using System.Collections.Generic;
using static FE.Utils.Utils;
using System;
using System.IO;
using FE.Utils;
using FE.Logic.Civilization.DarkFog;
using FE.Logic.DataCenter;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization.Analysis;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Logic.Items.ItemManager;

namespace FE.Logic.Civilization.Protocols;

#region ProtocolDefinition

/// <summary>
/// 描述一项远古协议；全局目录中由塔型与输入物品唯一标识。
/// </summary>
public sealed class ProtocolDefinition(RecipeKey recipeKey) {
    public RecipeKey RecipeKey { get; } = recipeKey;
}

#endregion // ProtocolDefinition

#region ProtocolCatalog

/// <summary>
/// 根据当前配方目录生成不可变的全局协议定义。
/// 仅收录复制、转化与培养塔返祖；同塔同输入遇重复键时确定性拒绝。
/// </summary>
public static class ProtocolCatalog {
    private static readonly List<ProtocolDefinition> definitions = [];
    private static readonly Dictionary<RecipeKey, ProtocolDefinition> definitionsByRecipe = [];

    public static IReadOnlyList<ProtocolDefinition> All => definitions;

    public static void Initialize() {
        definitions.Clear();
        definitionsByRecipe.Clear();

        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            if (!recipe.RequiresProtocolRecovery) {
                continue;
            }

            RecipeKey recipeKey = RecipeKey.FromRecipe(recipe);
            if (definitionsByRecipe.ContainsKey(recipeKey)) {
                LogError(
                    $"[ProtocolCatalog] Duplicate protocol key refused: {recipeKey.RecipeType}/{recipeKey.InputId}");
                continue;
            }

            var definition = new ProtocolDefinition(recipeKey);
            definitions.Add(definition);
            definitionsByRecipe[recipeKey] = definition;
        }
    }

    public static ProtocolDefinition Get(RecipeKey recipeKey) =>
        definitionsByRecipe.TryGetValue(recipeKey, out ProtocolDefinition definition) ? definition : null;

    public static int CountFormallyUnlocked() {
        int count = 0;
        foreach (ProtocolDefinition definition in definitions) {
            if (ProtocolProgressStore.IsFormallyUnlocked(definition.RecipeKey)) {
                count++;
            }
        }
        return count;
    }

    public static int CountDiscovered() {
        int count = 0;
        foreach (ProtocolDefinition definition in definitions) {
            if (ProtocolProgressStore.GetOrCreate(definition.RecipeKey).Discovered) {
                count++;
            }
        }
        return count;
    }

    public static int CountPurchaseEligible() {
        int count = 0;
        foreach (ProtocolDefinition definition in definitions) {
            if (ProtocolProgressStore.IsPurchaseEligible(definition.RecipeKey)) {
                count++;
            }
        }
        return count;
    }
}

#endregion // ProtocolCatalog

#region ProtocolProgressStore

/// <summary>
/// 保存协议发现、完整度、正式解锁与首个 100% 记忆奖励标记。
/// 旧 Protocols 块布局保持兼容；正式解锁使用独立 FormalUnlock 块。
/// </summary>
public static class ProtocolProgressStore {
    public sealed class ProtocolProgress {
        public bool Discovered;
        public int Completeness;
        public bool FormallyUnlocked;
    }

    private static readonly Dictionary<RecipeKey, ProtocolProgress> protocols = [];
    private static bool firstProtocolCompletenessRewardGranted;

    public static bool FirstProtocolCompletenessRewardGranted {
        get => firstProtocolCompletenessRewardGranted;
        set => firstProtocolCompletenessRewardGranted = value;
    }

    public static ProtocolProgress GetOrCreate(RecipeKey recipeKey) {
        if (!protocols.TryGetValue(recipeKey, out ProtocolProgress progress)) {
            progress = new ProtocolProgress();
            protocols[recipeKey] = progress;
        }
        return progress;
    }

    /// <summary>
    /// 完整度达到 100% 仅表示取得购买资格，不等于正式解锁。
    /// </summary>
    public static bool IsPurchaseEligible(RecipeKey recipeKey) =>
        GetOrCreate(recipeKey).Completeness >= 100;

    /// <summary>
    /// 兼容旧调用名：完整度达到 100%。
    /// </summary>
    public static bool IsComplete(RecipeKey recipeKey) => IsPurchaseEligible(recipeKey);

    public static bool IsFormallyUnlocked(RecipeKey recipeKey) =>
        GetOrCreate(recipeKey).FormallyUnlocked;

    public static void MarkFormallyUnlocked(RecipeKey recipeKey) {
        ProtocolProgress progress = GetOrCreate(recipeKey);
        progress.Discovered = true;
        progress.Completeness = 100;
        progress.FormallyUnlocked = true;
    }

    public static void Import(BinaryReader r) {
        protocols.Clear();
        firstProtocolCompletenessRewardGranted = false;
        r.ReadBlocks(
            ("Protocols", br => {
                int count = Math.Max(0, br.ReadInt32());
                for (int i = 0; i < count; i++) {
                    RecipeKey recipeKey = new((ERecipe)br.ReadInt32(), br.ReadInt32());
                    protocols[recipeKey] = new ProtocolProgress {
                        Discovered = br.ReadBoolean(),
                        Completeness = Math.Max(0, Math.Min(100, br.ReadInt32())),
                    };
                }
            }),
            // 旧阶段 Retrieval 块直接忽略，不重解释。
            ("Retrieval", _ => { }),
            ("FormalUnlock", br => {
                int count = Math.Max(0, br.ReadInt32());
                for (int i = 0; i < count; i++) {
                    RecipeKey recipeKey = new((ERecipe)br.ReadInt32(), br.ReadInt32());
                    ProtocolProgress progress = GetOrCreate(recipeKey);
                    progress.FormallyUnlocked = br.ReadBoolean();
                    if (progress.FormallyUnlocked) {
                        progress.Discovered = true;
                        progress.Completeness = Math.Max(progress.Completeness, 100);
                    }
                }
            }),
            ("Flags", br => {
                firstProtocolCompletenessRewardGranted = br.ReadBoolean();
            })
        );

        // 旧块缺标记且已有 100% 时不得重复补发。
        if (!firstProtocolCompletenessRewardGranted) {
            foreach (KeyValuePair<RecipeKey, ProtocolProgress> pair in protocols) {
                if (pair.Value.Completeness >= 100) {
                    firstProtocolCompletenessRewardGranted = true;
                    break;
                }
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Protocols", bw => {
                bw.Write(protocols.Count);
                foreach (KeyValuePair<RecipeKey, ProtocolProgress> pair in protocols) {
                    bw.Write((int)pair.Key.RecipeType);
                    bw.Write(pair.Key.InputId);
                    bw.Write(pair.Value.Discovered);
                    bw.Write(pair.Value.Completeness);
                }
            }),
            ("FormalUnlock", bw => {
                int count = 0;
                foreach (KeyValuePair<RecipeKey, ProtocolProgress> pair in protocols) {
                    if (pair.Value.FormallyUnlocked) {
                        count++;
                    }
                }
                bw.Write(count);
                foreach (KeyValuePair<RecipeKey, ProtocolProgress> pair in protocols) {
                    if (!pair.Value.FormallyUnlocked) {
                        continue;
                    }
                    bw.Write((int)pair.Key.RecipeType);
                    bw.Write(pair.Key.InputId);
                    bw.Write(true);
                }
            }),
            ("Flags", bw => {
                bw.Write(firstProtocolCompletenessRewardGranted);
            })
        );
    }

    public static void IntoOtherSave() {
        protocols.Clear();
        firstProtocolCompletenessRewardGranted = false;
    }
}

#endregion // ProtocolProgressStore

#region ProtocolEligibilityService

/// <summary>
/// 集中判断协议能否进入全局检索池，不修改任何玩家状态。
/// </summary>
public static class ProtocolEligibilityService {
    public static bool IsEligible(ProtocolDefinition definition) {
        if (definition == null) {
            return false;
        }

        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(definition.RecipeKey.RecipeType,
            definition.RecipeKey.InputId);
        if (recipe == null || !recipe.IsProtocolEligible) {
            return false;
        }

        // 普通物品无 ItemUnlocked 前置；涉及黑雾物品时要求全部相关实物已接触。
        if (!HasContactedAllDarkFogItems(recipe)) {
            return false;
        }

        // 黑雾调查特殊前置（rank/样本）由冻结 API 判定；非黑雾配方返回 true。
        return DarkFogInvestigationService.MeetsProtocolPrerequisite(recipe);
    }

    private static bool HasContactedAllDarkFogItems(BaseRecipe recipe) {
        if (!IsDarkFogItemContacted(recipe.InputID)) {
            return false;
        }
        foreach (OutputInfo output in recipe.OutputMain) {
            if (!IsDarkFogItemContacted(output.OutputID)) {
                return false;
            }
        }
        foreach (OutputInfo output in recipe.OutputAppend) {
            if (!IsDarkFogItemContacted(output.OutputID)) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 非黑雾物品视为已满足；黑雾物品接受 history 解锁或玩家/数据中心持有。
    /// </summary>
    private static bool IsDarkFogItemContacted(int itemId) {
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return true;
        }
        bool isDarkFog = item.Type == EItemType.DarkFog || item.UnlockKey == -2;
        if (!isDarkFog) {
            return true;
        }
        if (GameMain.history != null && GameMain.history.ItemUnlocked(itemId)) {
            return true;
        }
        return PlayerInventoryAccess.GetItemTotalCount(itemId) > 0;
    }
}

#endregion // ProtocolEligibilityService

#region ProtocolRetrievalService

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
    NoCandidate,
    InsufficientFragments,
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
/// 声明一次全局检索的策略及其方向或锚定目标。
/// </summary>
public readonly struct ProtocolRetrievalRequest(
    ProtocolRetrievalMode mode = ProtocolRetrievalMode.Broad,
    ERecipe directionalRecipeType = default,
    RecipeKey anchoredRecipeKey = default,
    bool hasAnchoredRecipe = false) {
    public ProtocolRetrievalMode Mode { get; } = mode;
    public ERecipe DirectionalRecipeType { get; } = directionalRecipeType;
    public RecipeKey AnchoredRecipeKey { get; } = anchoredRecipeKey;
    public bool HasAnchoredRecipe { get; } = hasAnchoredRecipe;

    public static ProtocolRetrievalRequest Broad() => new();

    public static ProtocolRetrievalRequest Directional(ERecipe recipeType) =>
        new(ProtocolRetrievalMode.Directional, recipeType);

    public static ProtocolRetrievalRequest Anchored(RecipeKey recipeKey) =>
        new(ProtocolRetrievalMode.Anchored, default, recipeKey, true);
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
    int awardedFragments,
    int baseCost = 0,
    ProtocolRetrievalStopReason stopReason = ProtocolRetrievalStopReason.None) {
    public ProtocolRetrievalOutcome Outcome { get; } = outcome;
    public RecipeKey RecipeKey { get; } = recipeKey;
    public int PreviousCompleteness { get; } = previousCompleteness;
    public int CurrentCompleteness { get; } = currentCompleteness;
    public bool AwardedTechPoint { get; } = awardedTechPoint;
    public int SpentFragments { get; } = spentFragments;
    public int AwardedFragments { get; } = awardedFragments;
    public int BaseCost { get; } = baseCost;
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
    bool formallyUnlocked,
    bool eligible,
    bool purchaseEligible) {
    public RecipeKey RecipeKey { get; } = recipeKey;
    public string DisplayName { get; } = displayName;
    public bool Discovered { get; } = discovered;
    public int Completeness { get; } = completeness;
    public bool FormallyUnlocked { get; } = formallyUnlocked;
    public bool Eligible { get; } = eligible;
    public bool PurchaseEligible { get; } = purchaseEligible;
    public bool IsActionable => Eligible && Completeness < 100;
}

/// <summary>
/// 提供恢复页面展示所需的全局检索只读投影。
/// </summary>
public readonly struct ProtocolRetrievalGlobalSnapshot(
    int totalRetrievalCount,
    int nextBaseCost,
    int failureStreak,
    int deepAnalysisProgress,
    int nextDeepPointCost,
    long fragments,
    long memory,
    IReadOnlyList<ProtocolRetrievalProtocolSnapshot> protocols) {
    public int TotalRetrievalCount { get; } = totalRetrievalCount;
    public int NextBaseCost { get; } = nextBaseCost;
    public int FailureStreak { get; } = failureStreak;
    public int DeepAnalysisProgress { get; } = deepAnalysisProgress;
    public int NextDeepPointCost { get; } = nextDeepPointCost;
    public long Fragments { get; } = fragments;
    public long Memory { get; } = memory;
    public IReadOnlyList<ProtocolRetrievalProtocolSnapshot> Protocols { get; } = protocols;
}

/// <summary>
/// 集中执行广域、方向和锚定协议检索，并维护全局残片成本、保底与深层解析。
/// </summary>
public static class ProtocolRetrievalService {
    public const int DefaultBatchCount = 10;
    public const int MaxBaseCost = 4000;
    public const int BaseCostOffset = 100;
    public const int BaseCostStep = 50;
    public const double InvalidResponseChance = 0.20d;
    public const int InvalidFailureStreakThreshold = 4;

    private static Random random = new();

    /// <summary>
    /// 测试/探针可注入确定随机源；生产路径使用默认实例。
    /// </summary>
    public static void SetRandom(Random source) => random = source ?? new Random();

    public static void ResetRandom() => random = new Random();

    /// <summary>
    /// 全局第 n 次检索的基础成本；n 从 0 起。
    /// </summary>
    public static int GetBaseCost(int totalRetrievalCount) {
        long n = Math.Max(0L, totalRetrievalCount);
        long raw = BaseCostOffset + BaseCostStep * n;
        if (raw >= MaxBaseCost) {
            return MaxBaseCost;
        }
        return (int)raw;
    }

    public static int GetCurrentBaseCost() => GetBaseCost(AnalysisProgressStore.TotalRetrievalCount);

    /// <summary>
    /// 方向附加费精确为 ceil(base/4)。
    /// </summary>
    public static int GetDirectionalSurcharge(int baseCost) =>
        baseCost <= 0 ? 0 : (baseCost + 3) / 4;

    public static int GetTotalCost(ProtocolRetrievalMode mode, int baseCost) {
        if (baseCost <= 0) {
            return 0;
        }
        return mode switch {
            ProtocolRetrievalMode.Directional => baseCost + GetDirectionalSurcharge(baseCost),
            ProtocolRetrievalMode.Anchored => baseCost * 2,
            _ => baseCost,
        };
    }

    public static int GetCurrentTotalCost(ProtocolRetrievalMode mode) =>
        GetTotalCost(mode, GetCurrentBaseCost());

    /// <summary>
    /// 无效响应仅返还 floor(base/5) 且至少 1；不返策略附加费。
    /// </summary>
    public static int GetInvalidRefund(int baseCost) {
        if (baseCost <= 0) {
            return 0;
        }
        int refund = baseCost / 5;
        return refund < 1 ? 1 : refund;
    }

    public static bool TryRetrieve(out ProtocolRetrievalResult result) =>
        TryRetrieve(ProtocolRetrievalRequest.Broad(), out result);

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

    /// <summary>
    /// 主机/单人结算核心；不触发联机请求，可被批量与主机处理器复用。
    /// </summary>
    public static bool TryRetrieveCore(ProtocolRetrievalRequest request, out ProtocolRetrievalResult result) {
        result = default;
        if (!IsRequestValid(request)) {
            result = CannotExecute(ProtocolRetrievalStopReason.InvalidRequest);
            return false;
        }

        List<ProtocolDefinition> candidates = GetCandidates(request);
        bool deepAnalysis = request.Mode == ProtocolRetrievalMode.Broad
                            && !HasAnyGlobalActionableCandidate();
        if (!deepAnalysis && candidates.Count == 0) {
            result = CannotExecute(ProtocolRetrievalStopReason.NoCandidate);
            return false;
        }

        int baseCost = GetCurrentBaseCost();
        int totalCost = GetTotalCost(request.Mode, baseCost);
        if (GetModDataItemCount(IFE残片) < totalCost) {
            result = CannotExecute(ProtocolRetrievalStopReason.InsufficientFragments);
            return false;
        }

        lock (centerItemCount) {
            if (GetModDataItemCount(IFE残片) < totalCost
                || TakeItemFromModData(IFE残片, totalCost, out _) != totalCost) {
                result = CannotExecute(ProtocolRetrievalStopReason.InsufficientFragments);
                return false;
            }
        }

        if (AnalysisProgressStore.TotalRetrievalCount < int.MaxValue) {
            AnalysisProgressStore.TotalRetrievalCount++;
        }

        if (deepAnalysis) {
            DeepAnalysisService.SubmitBaseCost(baseCost, out bool awardedPoint);
            result = new(ProtocolRetrievalOutcome.DeepAnalysis, default, 0, 0, awardedPoint,
                totalCost, 0, baseCost);
            return true;
        }

        bool guaranteedEffective = request.Mode == ProtocolRetrievalMode.Anchored
                                   || AnalysisProgressStore.FailureStreak >= InvalidFailureStreakThreshold;
        if (!guaranteedEffective && random.NextDouble() < InvalidResponseChance) {
            AnalysisProgressStore.FailureStreak++;
            int refund = GetInvalidRefund(baseCost);
            if (refund > 0) {
                AddItemToModData(IFE残片, refund);
            }
            result = new(ProtocolRetrievalOutcome.Failed, default, 0, 0, false,
                totalCost, refund, baseCost);
            return true;
        }

        AnalysisProgressStore.FailureStreak = 0;
        result = ResolveEffectiveRetrieval(candidates, request, totalCost, baseCost);
        CivilizationRuntimeSync.Refresh();
        return true;
    }

    public static ProtocolRetrievalBatchResult RetrieveBatch(ProtocolRetrievalRequest request, int requestedCount) {
        int limit = Math.Max(0, requestedCount);
        if (limit > 0 && NebulaMultiplayerModAPI.RequestProtocolRetrieval(request, limit)) {
            return new(0, 0, 0, 0, 0, 0, 0, 0, 0, ProtocolRetrievalStopReason.AwaitingHost);
        }

        int executed = 0;
        int failed = 0;
        int discovered = 0;
        int progressed = 0;
        int completed = 0;
        int deepAnalysis = 0;
        int techPoints = 0;
        int spentFragments = 0;
        int awardedFragments = 0;
        ProtocolRetrievalStopReason stopReason = ProtocolRetrievalStopReason.None;

        for (int i = 0; i < limit; i++) {
            if (!TryRetrieveCore(request, out ProtocolRetrievalResult result)) {
                stopReason = result.StopReason;
                break;
            }

            executed++;
            spentFragments += result.SpentFragments;
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
            spentFragments, awardedFragments, stopReason);
    }

    public static ProtocolRetrievalGlobalSnapshot GetGlobalSnapshot() {
        List<ProtocolRetrievalProtocolSnapshot> protocols = [];
        foreach (ProtocolDefinition definition in ProtocolCatalog.All) {
            ProtocolProgressStore.ProtocolProgress progress =
                ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            bool eligible = ProtocolEligibilityService.IsEligible(definition);
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(definition.RecipeKey.RecipeType,
                definition.RecipeKey.InputId);
            protocols.Add(new(
                definition.RecipeKey,
                recipe?.TypeName ?? $"{definition.RecipeKey.RecipeType}/{definition.RecipeKey.InputId}",
                progress.Discovered,
                progress.Completeness,
                progress.FormallyUnlocked,
                eligible,
                progress.Completeness >= 100));
        }

        return new(
            AnalysisProgressStore.TotalRetrievalCount,
            GetCurrentBaseCost(),
            AnalysisProgressStore.FailureStreak,
            AnalysisProgressStore.DeepAnalysisProgress,
            DeepAnalysisService.GetNextPointCost(),
            GetModDataItemCount(IFE残片),
            GetModDataItemCount(IFE记忆源点),
            protocols);
    }

    public static List<ERecipe> GetDirectionalRecipeTypes() {
        List<ERecipe> recipeTypes = [];
        foreach (ProtocolRetrievalProtocolSnapshot protocol in GetGlobalSnapshot().Protocols) {
            if (protocol.IsActionable && !recipeTypes.Contains(protocol.RecipeKey.RecipeType)) {
                recipeTypes.Add(protocol.RecipeKey.RecipeType);
            }
        }
        return recipeTypes;
    }

    public static List<ProtocolRetrievalProtocolSnapshot> GetAnchoredCandidates() {
        List<ProtocolRetrievalProtocolSnapshot> candidates = [];
        foreach (ProtocolRetrievalProtocolSnapshot protocol in GetGlobalSnapshot().Protocols) {
            if (protocol.IsActionable) {
                candidates.Add(protocol);
            }
        }
        return candidates;
    }

    public static bool CanRetrieve(ProtocolRetrievalRequest request, out ProtocolRetrievalStopReason stopReason) {
        if (!IsRequestValid(request)) {
            stopReason = ProtocolRetrievalStopReason.InvalidRequest;
            return false;
        }
        List<ProtocolDefinition> candidates = GetCandidates(request);
        bool deepAnalysis = request.Mode == ProtocolRetrievalMode.Broad
                            && !HasAnyGlobalActionableCandidate();
        if (!deepAnalysis && candidates.Count == 0) {
            stopReason = ProtocolRetrievalStopReason.NoCandidate;
            return false;
        }
        int totalCost = GetCurrentTotalCost(request.Mode);
        if (GetModDataItemCount(IFE残片) < totalCost) {
            stopReason = ProtocolRetrievalStopReason.InsufficientFragments;
            return false;
        }
        stopReason = ProtocolRetrievalStopReason.None;
        return true;
    }

    private static ProtocolRetrievalResult ResolveEffectiveRetrieval(List<ProtocolDefinition> candidates,
        ProtocolRetrievalRequest request, int spentFragments, int baseCost) {
        if (request.Mode == ProtocolRetrievalMode.Anchored && request.HasAnchoredRecipe) {
            foreach (ProtocolDefinition candidate in candidates) {
                if (candidate.RecipeKey.Equals(request.AnchoredRecipeKey)) {
                    return ApplyProgress(candidate, spentFragments, baseCost);
                }
            }
        }

        List<ProtocolDefinition> undiscovered = [];
        List<ProtocolDefinition> discoveredIncomplete = [];
        foreach (ProtocolDefinition candidate in candidates) {
            ProtocolProgressStore.ProtocolProgress progress =
                ProtocolProgressStore.GetOrCreate(candidate.RecipeKey);
            if (!progress.Discovered) {
                undiscovered.Add(candidate);
            } else {
                discoveredIncomplete.Add(candidate);
            }
        }

        bool shouldDiscover = undiscovered.Count > 0
                              && (discoveredIncomplete.Count == 0 || random.NextDouble() < 0.35d);
        if (shouldDiscover) {
            ProtocolDefinition definition = undiscovered[random.Next(undiscovered.Count)];
            return ApplyProgress(definition, spentFragments, baseCost);
        }

        ProtocolDefinition target = discoveredIncomplete.Count > 0
            ? discoveredIncomplete[random.Next(discoveredIncomplete.Count)]
            : candidates[random.Next(candidates.Count)];
        return ApplyProgress(target, spentFragments, baseCost);
    }

    private static ProtocolRetrievalResult ApplyProgress(ProtocolDefinition definition, int spentFragments,
        int baseCost) {
        ProtocolProgressStore.ProtocolProgress progress =
            ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
        int previous = progress.Completeness;
        bool wasDiscovered = progress.Discovered;
        if (!progress.Discovered) {
            progress.Discovered = true;
        }

        // 首次发现 20..40；后续推进 12..25。
        int gain = wasDiscovered ? random.Next(12, 26) : random.Next(20, 41);
        progress.Completeness = Math.Min(100, previous + gain);
        MaybeGrantFirstCompletenessMemoryReward(definition, previous, progress.Completeness);

        ProtocolRetrievalOutcome outcome;
        if (!wasDiscovered) {
            outcome = progress.Completeness >= 100
                ? ProtocolRetrievalOutcome.Completed
                : ProtocolRetrievalOutcome.Discovered;
        } else {
            outcome = progress.Completeness >= 100
                ? ProtocolRetrievalOutcome.Completed
                : ProtocolRetrievalOutcome.Progressed;
        }

        return new(outcome, definition.RecipeKey, previous, progress.Completeness, false,
            spentFragments, 0, baseCost);
    }

    /// <summary>
    /// 当前存档首个协议首次从 &lt;100 到 100 时立即奖励完整 Memory 成本。
    /// </summary>
    internal static void MaybeGrantFirstCompletenessMemoryReward(ProtocolDefinition definition,
        int previousCompleteness, int currentCompleteness) {
        if (ProtocolProgressStore.FirstProtocolCompletenessRewardGranted
            || previousCompleteness >= 100
            || currentCompleteness < 100
            || definition == null) {
            return;
        }

        int memoryReward = ProtocolUnlockService.GetMemoryCost(definition);
        if (memoryReward > 0) {
            AddItemToModData(IFE记忆源点, memoryReward);
        }
        ProtocolProgressStore.FirstProtocolCompletenessRewardGranted = true;
    }

    private static List<ProtocolDefinition> GetCandidates(ProtocolRetrievalRequest request) {
        List<ProtocolDefinition> candidates = [];
        if (request.Mode == ProtocolRetrievalMode.Anchored && !request.HasAnchoredRecipe) {
            return candidates;
        }

        foreach (ProtocolDefinition definition in ProtocolCatalog.All) {
            if (!ProtocolEligibilityService.IsEligible(definition)
                || ProtocolProgressStore.IsPurchaseEligible(definition.RecipeKey)) {
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

    private static bool HasAnyGlobalActionableCandidate() {
        foreach (ProtocolDefinition definition in ProtocolCatalog.All) {
            if (ProtocolEligibilityService.IsEligible(definition)
                && !ProtocolProgressStore.IsPurchaseEligible(definition.RecipeKey)) {
                return true;
            }
        }
        return false;
    }

    private static bool IsRequestValid(ProtocolRetrievalRequest request) {
        if (request.Mode is not (ProtocolRetrievalMode.Broad
            or ProtocolRetrievalMode.Directional
            or ProtocolRetrievalMode.Anchored)) {
            return false;
        }
        if (request.Mode == ProtocolRetrievalMode.Anchored && !request.HasAnchoredRecipe) {
            return false;
        }
        return true;
    }

    private static ProtocolRetrievalResult CannotExecute(ProtocolRetrievalStopReason stopReason) =>
        new(ProtocolRetrievalOutcome.None, default, 0, 0, false, 0, 0, 0, stopReason);
}

#endregion // ProtocolRetrievalService

#region ProtocolUnlockService

/// <summary>
/// 正式解锁无法执行时的明确原因。
/// </summary>
public enum ProtocolUnlockStopReason {
    None,
    InvalidRequest,
    NotFound,
    NotPurchaseEligible,
    AlreadyUnlocked,
    PrerequisiteNotMet,
    InsufficientMemory,
    InsufficientSamples,
    AwaitingHost,
}

/// <summary>
/// 主机/单人唯一正式解锁结算：重验资格后在同一把中心库存锁内原子扣减并标记。
/// </summary>
public static class ProtocolUnlockService {
    public static int GetMemoryCost(ProtocolDefinition definition) {
        BaseRecipe recipe = GetRecipe(definition);
        if (recipe == null) {
            return 0;
        }
        float maxValue = GetMaxRelatedItemValue(recipe);
        if (maxValue < 100f) {
            return 1;
        }
        if (maxValue < 1000f) {
            return 5;
        }
        if (maxValue < 10000f) {
            return 20;
        }
        return 100;
    }

    public static List<(int itemId, long count)> GetPhysicalCosts(ProtocolDefinition definition) {
        List<(int itemId, long count)> costs = [];
        BaseRecipe recipe = GetRecipe(definition);
        if (recipe == null) {
            return costs;
        }

        HashSet<int> itemIds = [];
        itemIds.Add(recipe.InputID);

        bool inputIsBuilding = IsBuildingItem(recipe.InputID);
        bool onlyInput = recipe.RecipeType == ERecipe.Replication
                         || recipe.RecipeType == ERecipe.Cultivation
                         || (recipe.RecipeType == ERecipe.Conversion && inputIsBuilding);

        if (!onlyInput) {
            foreach (OutputInfo output in recipe.OutputMain) {
                itemIds.Add(output.OutputID);
            }
            foreach (OutputInfo output in recipe.OutputAppend) {
                itemIds.Add(output.OutputID);
            }
        }

        foreach (int itemId in itemIds) {
            ItemProto item = LDB.items.Select(itemId);
            if (item == null) {
                continue;
            }
            int stack = Math.Max(1, item.StackSize);
            costs.Add((itemId, stack));
        }
        return costs;
    }

    public static List<(int itemId, long count)> BuildAtomicCosts(ProtocolDefinition definition) {
        List<(int itemId, long count)> costs = [];
        int memory = GetMemoryCost(definition);
        if (memory > 0) {
            costs.Add((IFE记忆源点, memory));
        }
        costs.AddRange(GetPhysicalCosts(definition));
        return costs;
    }

    public static bool CanUnlock(RecipeKey recipeKey, out ProtocolUnlockStopReason stopReason) {
        ProtocolDefinition definition = ProtocolCatalog.Get(recipeKey);
        if (definition == null) {
            stopReason = ProtocolUnlockStopReason.NotFound;
            return false;
        }
        return CanUnlock(definition, out stopReason);
    }

    public static bool CanUnlock(ProtocolDefinition definition, out ProtocolUnlockStopReason stopReason) {
        if (definition == null) {
            stopReason = ProtocolUnlockStopReason.InvalidRequest;
            return false;
        }

        BaseRecipe recipe = GetRecipe(definition);
        if (recipe == null) {
            stopReason = ProtocolUnlockStopReason.NotFound;
            return false;
        }

        ProtocolProgressStore.ProtocolProgress progress =
            ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
        if (progress.FormallyUnlocked) {
            stopReason = ProtocolUnlockStopReason.AlreadyUnlocked;
            return false;
        }
        if (progress.Completeness < 100) {
            stopReason = ProtocolUnlockStopReason.NotPurchaseEligible;
            return false;
        }
        // 锁外/锁内统一走完整资格服务：黑雾接触 + 冻结调查前置 + 返祖门槛。
        if (!ProtocolEligibilityService.IsEligible(definition)) {
            stopReason = ProtocolUnlockStopReason.PrerequisiteNotMet;
            return false;
        }

        List<(int itemId, long count)> costs = BuildAtomicCosts(definition);
        long memoryNeed = 0;
        foreach ((int itemId, long count) in costs) {
            if (itemId == IFE记忆源点) {
                memoryNeed += count;
            }
        }
        if (GetModDataItemCount(IFE记忆源点) < memoryNeed) {
            stopReason = ProtocolUnlockStopReason.InsufficientMemory;
            return false;
        }
        foreach ((int itemId, long count) in costs) {
            if (itemId == IFE记忆源点) {
                continue;
            }
            if (GetModDataItemCount(itemId) < count) {
                stopReason = ProtocolUnlockStopReason.InsufficientSamples;
                return false;
            }
        }

        stopReason = ProtocolUnlockStopReason.None;
        return true;
    }

    public static bool TryUnlock(RecipeKey recipeKey, out ProtocolUnlockStopReason stopReason) {
        if (NebulaMultiplayerModAPI.RequestProtocolFormalUnlock(recipeKey)) {
            stopReason = ProtocolUnlockStopReason.AwaitingHost;
            return false;
        }

        bool unlocked = TryUnlockCore(recipeKey, out stopReason);
        if (unlocked) {
            NebulaMultiplayerModAPI.BroadcastCivilizationState();
        }
        return unlocked;
    }

    /// <summary>
    /// 主机/单人唯一结算核心：在 centerItemCount 外层锁内原子扣减并标记正式解锁。
    /// </summary>
    public static bool TryUnlockCore(RecipeKey recipeKey, out ProtocolUnlockStopReason stopReason) {
        ProtocolDefinition definition = ProtocolCatalog.Get(recipeKey);
        if (definition == null) {
            stopReason = ProtocolUnlockStopReason.NotFound;
            return false;
        }

        // 锁外预检，锁内仍完整重验，避免竞态窗口造成部分扣减。
        if (!CanUnlock(definition, out stopReason)) {
            return false;
        }

        List<(int itemId, long count)> costs = BuildAtomicCosts(definition);
        lock (centerItemCount) {
            if (!CanUnlock(definition, out stopReason)) {
                return false;
            }

            if (!TryConsumeItemsAtomic(costs)) {
                // 再区分 Memory / 实物不足，便于 UI 诊断。
                long memoryNeed = 0;
                foreach ((int itemId, long count) in costs) {
                    if (itemId == IFE记忆源点) {
                        memoryNeed += count;
                    }
                }
                if (GetModDataItemCount(IFE记忆源点) < memoryNeed) {
                    stopReason = ProtocolUnlockStopReason.InsufficientMemory;
                } else {
                    stopReason = ProtocolUnlockStopReason.InsufficientSamples;
                }
                return false;
            }

            ProtocolProgressStore.MarkFormallyUnlocked(definition.RecipeKey);
        }

        CivilizationRuntimeSync.Refresh();
        stopReason = ProtocolUnlockStopReason.None;
        return true;
    }

    private static BaseRecipe GetRecipe(ProtocolDefinition definition) {
        if (definition == null) {
            return null;
        }
        return RecipeManager.GetRecipe<BaseRecipe>(definition.RecipeKey.RecipeType,
            definition.RecipeKey.InputId);
    }

    private static float GetMaxRelatedItemValue(BaseRecipe recipe) {
        float max = GetEffectiveItemValue(recipe.InputID);
        foreach (OutputInfo output in recipe.OutputMain) {
            max = Math.Max(max, GetEffectiveItemValue(output.OutputID));
        }
        foreach (OutputInfo output in recipe.OutputAppend) {
            max = Math.Max(max, GetEffectiveItemValue(output.OutputID));
        }
        return max;
    }

    private static float GetEffectiveItemValue(int itemId) {
        if (itemId <= 0 || itemId >= itemValue.Length) {
            return 0f;
        }
        float value = itemValue[itemId];
        if (float.IsNaN(value) || float.IsInfinity(value) || value >= maxValue) {
            return 0f;
        }
        return Math.Max(0f, value);
    }

    private static bool IsBuildingItem(int itemId) {
        ItemProto item = LDB.items.Select(itemId);
        return item != null && item.BuildMode != 0;
    }
}

#endregion // ProtocolUnlockService
