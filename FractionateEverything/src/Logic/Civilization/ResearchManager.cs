using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Items;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 一项可被探索点明确发现、再由对应物品共享解析槽解析的内容。
/// </summary>
public sealed class ResearchContentDefinition(string id, string displayName, int tier) {
    /// <summary>稳定内容 ID。</summary>
    public string Id { get; } = id;

    /// <summary>玩家可见名称。</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>同一方向内的开放层级。</summary>
    public int Tier { get; } = tier;
}

/// <summary>
/// 一个物品方向的只读研究界面快照。
/// </summary>
public sealed class ResearchDirectionSnapshot(
    int itemId,
    int pendingContentCount,
    bool hasRemainingContent,
    int progress) {
    /// <summary>作为解析方向的物品 ID。</summary>
    public int ItemId { get; } = itemId;

    /// <summary>当前已经探索并等待该物品解析的内容数量。</summary>
    public int PendingContentCount { get; } = pendingContentCount;

    /// <summary>是否已有等待该物品解析的具体内容。</summary>
    public bool HasPendingContent => PendingContentCount > 0;

    /// <summary>目录中是否仍有尚未解析的内容，不包含具体数量。</summary>
    public bool HasRemainingContent { get; } = hasRemainingContent;

    /// <summary>当前内容发现进度，范围 0..9999。</summary>
    public int Progress { get; } = progress;
}

/// <summary>
/// 具体内容探索、物品共享解析槽和高级塔取得状态。
/// </summary>
public static partial class ResearchManager {
    /// <summary>
    /// 供逐帧路径无锁读取的研究状态副本；研究状态每次变化后整体替换。
    /// </summary>
    private sealed class AnalysisRuntimeSnapshot(long version, Dictionary<int, string[]> candidatesByItemId,
        HashSet<string> unlockedContents) {
        /// <summary>本进程内递增的发布版本，切档和导入也会失效界面缓存。</summary>
        public readonly long Version = version;
        public readonly Dictionary<int, string[]> CandidatesByItemId = candidatesByItemId;
        public readonly HashSet<string> UnlockedContents = unlockedContents;
    }

    /// <summary>高级塔库存达到此数量后，对应特质可以进入探索候选。</summary>
    public const int AdvancedTowerEligibilityThreshold = 1000;

    private const int MaximumImportedCollectionCount = 12000;

    /// <summary>高级矿物塔特质内容 ID。</summary>
    public const string AdvancedMineralTraitId = "trait:advanced-mineral";

    /// <summary>高级交互塔特质内容 ID。</summary>
    public const string AdvancedInteractionTraitId = "trait:advanced-interaction";

    /// <summary>高级转化塔特质内容 ID。</summary>
    public const string AdvancedConversionTraitId = "trait:advanced-conversion";

    private static readonly object Sync = new();
    private static readonly Dictionary<string, ResearchContentDefinition> ContentDefinitions = [];
    private static readonly Dictionary<int, List<string>> DirectionPools = [];
    private static readonly Dictionary<int, List<string>> RemainingDirectionPools = [];
    private static readonly Dictionary<string, List<int>> ContentDirections = [];
    private static readonly HashSet<int> EligibleDirections = [];
    private static readonly HashSet<string> ExploredContents = [];
    private static readonly Dictionary<string, int> ExploredContentDirections = [];
    private static readonly HashSet<string> UnlockedContents = [];
    private static readonly Dictionary<int, int> DirectionProgress = [];
    private static readonly HashSet<int> BootstrapTowerGrants = [];
    private static readonly HashSet<int> AcquiredAdvancedTowers = [];
    private static readonly HashSet<int> AdvancedTowerInventoryMilestones = [];
    private static readonly Queue<string> PendingNotifications = [];
    private static AnalysisRuntimeSnapshot analysisRuntimeSnapshot = new(0, [], []);

    /// <summary>当前研究只读快照的版本，用于配方选项缓存失效。</summary>
    public static long AnalysisSnapshotVersion => Volatile.Read(ref analysisRuntimeSnapshot).Version;
    private static int discoveredContentCount;
    private static int pendingSignalAccelerations;
    private static bool belongsToGeneration3;
    /// <summary>当前存档失去跨档完成资格后保持为真，并随研究块保存。</summary>
    private static bool completionDisqualified;
    private static bool snapshotDirty;

    /// <summary>
    /// 获取已发现内容数量。
    /// </summary>
    public static int DiscoveredContentCount {
        get {
            lock (Sync) {
                return discoveredContentCount;
            }
        }
    }

    /// <summary>
    /// 获取当前存档是否使用当前研究格式。
    /// </summary>
    public static bool BelongsToGeneration3 {
        get {
            lock (Sync) {
                return belongsToGeneration3;
            }
        }
    }

    /// <summary>当前存档能否记录正式游玩的完成标记，沙盒使用记录不可撤销。</summary>
    public static bool CanRecordCompletion {
        get { lock (Sync) { return belongsToGeneration3 && !completionDisqualified; } }
    }

    /// <summary>永久取消当前存档的完成标记资格，不改写已经取得的本地档案。</summary>
    public static void DisqualifyCompletion() {
        lock (Sync) {
            if (completionDisqualified) { return; }
            completionDisqualified = true;
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 判断三类高级塔是否都已在当前格式存档中取得过。
    /// </summary>
    public static bool HasAcquiredAllAdvancedTowers {
        get {
            lock (Sync) {
                return AcquiredAdvancedTowers.Contains(IFE高级矿物塔)
                       && AcquiredAdvancedTowers.Contains(IFE高级交互塔)
                       && AcquiredAdvancedTowers.Contains(IFE高级转化塔);
            }
        }
    }

    /// <summary>
    /// 依据当前配方目录建立多对多解析池。该方法只建立定义，不改写存档状态。
    /// </summary>
    public static void BuildResearchCatalog() {
        lock (Sync) {
            ContentDefinitions.Clear();
            DirectionPools.Clear();
            RemainingDirectionPools.Clear();
            ContentDirections.Clear();

            foreach (WorldviewRecipe recipe in RecipeManager.WorldviewRecipes) {
                if (recipe.Kind == WorldviewRecipeKind.BasicAnalysis) {
                    continue;
                }

                int tier = GetRecipeTier(recipe);
                AddContentDefinition(new ResearchContentDefinition(recipe.StableId, recipe.DisplayName, tier));
                AddToPool(recipe.AnalysisItemId, recipe.StableId);
            }

            AddContentDefinition(new ResearchContentDefinition(AdvancedMineralTraitId,
                "FE高级矿物特质".Translate(), 4));
            AddContentDefinition(new ResearchContentDefinition(AdvancedInteractionTraitId,
                "FE高级交互特质".Translate(), 4));
            AddContentDefinition(new ResearchContentDefinition(AdvancedConversionTraitId,
                "FE高级转化特质".Translate(), 4));
            AddToPool(IFE高级矿物塔, AdvancedMineralTraitId);
            AddToPool(IFE高级交互塔, AdvancedInteractionTraitId);
            AddToPool(IFE高级转化塔, AdvancedConversionTraitId);
            AddStageContentDefinitions();
            RebuildRemainingDirectionPoolsUnsafe();
            PublishAnalysisRuntimeSnapshotUnsafe();
        }
    }

    /// <summary>
    /// 获取需要建立基础解析入口的物品，包含扩容矩阵、Frag 和三类高级塔。
    /// </summary>
    public static int[] GetAnalyzableItemIds() {
        lock (Sync) {
            HashSet<int> itemIds = [.. DirectionPools.Keys, .. ItemManager.MainProgressMatrixIds,
                IFE残片, IFE高级矿物塔, IFE高级交互塔, IFE高级转化塔];
            return itemIds.OrderBy(itemId => itemId).ToArray();
        }
    }

    /// <summary>
    /// 标记新建存档使用当前研究格式；首批研究内容仍由首个遗物授予。
    /// </summary>
    public static void InitializeGeneration3StarterState() {
        lock (Sync) {
            GrowthTechnologyManager.SetStackProgressionMode(PermanentProfileStore.HasPlayedFractionation);
            belongsToGeneration3 = true;
            completionDisqualified = GameMain.sandboxToolsEnabled || GameMain.data?.gameDesc?.isSandboxMode == true;
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 首遗物提供四种矿物与正向培育的样本线索，配方仍需解析。
    /// </summary>
    public static void GrantStarterResearchState() {
        foreach (int itemId in new[] { I铁矿, I铜矿, I石矿, I煤矿 }) {
            DiscoverContent($"mineral-basic:{itemId}", itemId, false);
        }
        DiscoverContent($"interaction-culture:{IFE高级交互塔}", IFE通用原胚, false);
    }

    /// <summary>
    /// 标记存档来自旧格式；这类存档不会写入跨存档完成标记。
    /// </summary>
    public static void MarkLegacyGeneration() {
        lock (Sync) {
            GrowthTechnologyManager.SetStackProgressionMode(false);
            belongsToGeneration3 = false;
            completionDisqualified = true;
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 判断指定物品是否已有待解析具体内容。Frag 和高级塔仍保留各自的特殊消耗路径。
    /// </summary>
    public static bool CanAnalyzeDirection(int itemId) {
        if (itemId == IFE残片 || itemId == IFE记忆源点
            || SacrificeUnlocked && FractionatorTowerCatalog.IsAdvancedTowerItem(itemId)) {
            return true;
        }
        return Volatile.Read(ref analysisRuntimeSnapshot).CandidatesByItemId.ContainsKey(itemId);
    }

    /// <summary>返回解析塔对当前输入的工作模式，不公开未知内容。</summary>
    /// <param name="itemId">当前输入。</param>
    /// <returns>模式文案。</returns>
    public static string GetAnalysisMode(int itemId) {
        if (Array.IndexOf(ItemManager.MainProgressMatrixIds, itemId) >= 0) { return "FE.Storage.ExpansionMode"; }
        if (itemId == IFE残片) { return "广播解析"; }
        if (Volatile.Read(ref analysisRuntimeSnapshot).CandidatesByItemId.ContainsKey(itemId)) { return "解析"; }
        if (SacrificeUnlocked && (itemId == IFE记忆源点 || FractionatorTowerCatalog.IsAdvancedTowerItem(itemId))) {
            return "献祭";
        }
        return "待机";
    }

    /// <summary>
    /// 获取解析方向当前进度，范围 0..9999，10000 表示一项内容。
    /// </summary>
    public static int GetDirectionProgress(int itemId) {
        lock (Sync) {
            return DirectionProgress.TryGetValue(itemId, out int progress) ? progress : 0;
        }
    }

    /// <summary>
    /// 获取全部已经进入研究记录的物品方向。
    /// </summary>
    public static int[] GetEligibleDirections() {
        lock (Sync) {
            return EligibleDirections.OrderBy(id => id).ToArray();
        }
    }

    /// <summary>
    /// 获取尚未发现、且当前层级允许生成探索点的具体内容。
    /// </summary>
    public static string[] GetPotentialContentIds() {
        lock (Sync) {
            return ContentDefinitions.Values
                .Where(definition => !definition.Id.StartsWith("technology:", StringComparison.Ordinal)
                                     && !definition.Id.StartsWith("stage:", StringComparison.Ordinal)
                                     && !UnlockedContents.Contains(definition.Id)
                                     && !ExploredContents.Contains(definition.Id)
                                     && definition.Tier <= CurrentMatrixStage
                                     && IsContentExplorableUnsafe(definition.Id))
                .OrderBy(definition => definition.Tier)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal)
                .Select(definition => definition.Id)
                .ToArray();
        }
    }

    /// <summary>
    /// 获取一项内容可使用的解析物品。
    /// </summary>
    public static int[] GetContentDirectionItemIds(string contentId) {
        lock (Sync) {
            return contentId != null
                   && ContentDirections.TryGetValue(contentId, out List<int> directions)
                ? directions
                    .Where(itemId => IsExplorationDirectionUnsafe(contentId, itemId))
                    .OrderBy(itemId => itemId)
                    .ToArray()
                : [];
        }
    }

    /// <summary>
    /// 判断具体内容当前能否由指定物品作为唯一承载方向进入研究记录。
    /// </summary>
    public static bool CanDiscoverContent(string contentId, int itemId) {
        lock (Sync) {
            return CanDiscoverContentUnsafe(contentId, itemId);
        }
    }

    /// <summary>
    /// 将探索点携带的具体内容加入对应物品的共享解析槽。
    /// </summary>
    public static bool DiscoverContent(string contentId, int itemId, bool notify = true) {
        lock (Sync) {
            if (!CanDiscoverContentUnsafe(contentId, itemId)) {
                return false;
            }

            ResearchContentDefinition definition = ContentDefinitions[contentId];
            ExploredContents.Add(contentId);
            ExploredContentDirections[contentId] = itemId;
            EligibleDirections.Add(itemId);
            snapshotDirty = true;
            PublishAnalysisRuntimeSnapshotUnsafe();
            if (notify) {
                QueueNotification(string.Format(
                    "发现了以{0}为样本的记录".Translate(), GetItemName(itemId)));
            }
            return true;
        }
    }

    /// <summary>
    /// 为旧探索点选择该物品尚未发现的一项具体内容。
    /// </summary>
    public static string GetLegacyExplorationContentId(int itemId, ISet<string> excludedContentIds = null) {
        lock (Sync) {
            if (!DirectionPools.TryGetValue(itemId, out List<string> pool)) {
                return null;
            }
            return pool
                .Where(contentId => !UnlockedContents.Contains(contentId)
                                    && !ExploredContents.Contains(contentId)
                                    && (excludedContentIds == null || !excludedContentIds.Contains(contentId))
                                    && IsExplorationDirectionUnsafe(contentId, itemId)
                                    && IsContentExplorableUnsafe(contentId))
                .OrderBy(contentId => ContentDefinitions[contentId].Tier)
                .ThenBy(contentId => contentId, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// 判断一项稳定内容是否已经发现。
    /// </summary>
    public static bool IsContentUnlocked(string contentId) {
        // 高级塔每帧都会查询，读无锁副本，不进全局锁。
        return contentId != null
               && Volatile.Read(ref analysisRuntimeSnapshot).UnlockedContents.Contains(contentId);
    }

    /// <summary>
    /// 获取全部研究方向的界面快照。
    /// </summary>
    public static ResearchDirectionSnapshot[] GetDirectionSnapshots() {
        lock (Sync) {
            return DirectionPools
                .Where(pair => EligibleDirections.Contains(pair.Key))
                .OrderBy(pair => pair.Key)
                .Select(pair => {
                    string[] candidates = GetCandidateIdsUnsafe(pair.Key);
                    return new ResearchDirectionSnapshot(
                        pair.Key,
                        candidates.Length,
                        GetRemainingDirectionCountUnsafe(pair.Key) > 0,
                        GetDirectionProgressUnsafe(pair.Key));
                })
                .ToArray();
        }
    }

    /// <summary>
    /// 获取完整研究目录定义；界面必须自行隐藏尚未探索条目的名称。
    /// </summary>
    public static ResearchContentDefinition[] GetCatalogContentDefinitions() {
        lock (Sync) {
            return ContentDefinitions.Values
                .OrderBy(definition => definition.Tier)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    /// <summary>
    /// 判断一项内容是否已经探索；已经解析的内容也视为已探索。
    /// </summary>
    public static bool IsContentKnown(string contentId) {
        lock (Sync) {
            return contentId != null
                   && (ExploredContents.Contains(contentId) || UnlockedContents.Contains(contentId));
        }
    }

    /// <summary>
    /// 获取全部已探索内容定义；包含等待解析和已经解析的内容。
    /// </summary>
    public static ResearchContentDefinition[] GetKnownContentDefinitions() {
        lock (Sync) {
            return ExploredContents
                .Concat(UnlockedContents)
                .Distinct()
                .Select(id => ContentDefinitions.TryGetValue(id, out ResearchContentDefinition definition)
                    ? definition
                    : null)
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Tier)
                .ThenBy(definition => definition.DisplayName)
                .ToArray();
        }
    }

    /// <summary>
    /// 获取当前已经解析的内容定义。
    /// </summary>
    public static ResearchContentDefinition[] GetUnlockedContentDefinitions() {
        lock (Sync) {
            return UnlockedContents
                .Select(id => ContentDefinitions.TryGetValue(id, out ResearchContentDefinition definition)
                    ? definition
                    : null)
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Tier)
                .ThenBy(definition => definition.DisplayName)
                .ToArray();
        }
    }

    /// <summary>
    /// 结算基础解析塔输入，返回实际消耗数；普通样本仅在解析成功判定后消耗。
    /// </summary>
    /// <param name="itemId">输入物品 ID。</param>
    /// <param name="count">本批输入数量。</param>
    /// <param name="seed">分馏随机种子。</param>
    /// <param name="sacrificeOutputCount">塔体献祭产出的源点数量。</param>
    /// <returns>实际消耗数量；其余输入应保留增产点并回流。</returns>
    public static int ProcessAnalysisBatch(int itemId, int count, ref uint seed,
        out int sacrificeOutputCount) {
        sacrificeOutputCount = 0;
        if (count <= 0) { return 0; }

        if (Array.IndexOf(ItemManager.MainProgressMatrixIds, itemId) >= 0) {
            return SubspaceExpansion.SupplyMatrix(itemId, count);
        }
        if (itemId == IFE残片) {
            if (!ExplorationManager.PlanetDetectorUnlocked) { return 0; }
            int pointCount = WorldviewRecipe.RollExpected(ref seed, count * 0.02d);
            if (pointCount > 0) {
                ExplorationManager.AddExplorationPoints(pointCount, "残片解析".Translate());
            }
            return count;
        }
        if (itemId == IFE记忆源点) { return ProcessMemory(count) ? count : 0; }

        bool advancedTower = FractionatorTowerCatalog.IsAdvancedTowerItem(itemId);
        AnalysisRuntimeSnapshot runtimeSnapshot = Volatile.Read(ref analysisRuntimeSnapshot);
        if (!runtimeSnapshot.CandidatesByItemId.ContainsKey(itemId)) {
            if (!advancedTower || !SacrificeUnlocked) { return 0; }
            sacrificeOutputCount = WorldviewRecipe.RollExpected(ref seed, count * 0.02d);
            return count;
        }

        int successCount = WorldviewRecipe.RollExpected(ref seed, count * 0.02d);
        if (successCount <= 0) { return 0; }
        List<int> bootstrapTowers = null;
        int consumed;
        lock (Sync) {
            string[] candidates = GetCandidateIdsUnsafe(itemId);
            if (!EligibleDirections.Contains(itemId) || candidates.Length == 0) { return 0; }

            int progress = GetDirectionProgressUnsafe(itemId);
            int remainingSuccesses = successCount;
            while (remainingSuccesses > 0) {
                candidates = GetCandidateIdsUnsafe(itemId);
                if (candidates.Length == 0) { progress = 0; break; }

                // 预算按真正消耗的样本计；2% 判定只决定推进时机，不再吞掉失败输入。
                double progressPerSuccess = 10000d / candidates.Average(GetExpectedSampleCount);
                int successesToComplete = Math.Max(1,
                    (int)Math.Ceiling((10000 - progress) / progressPerSuccess));
                int appliedSuccesses = Math.Min(remainingSuccesses, successesToComplete);
                progress += WorldviewRecipe.RollExpected(ref seed, appliedSuccesses * progressPerSuccess);
                remainingSuccesses -= appliedSuccesses;
                if (progress < 10000) { break; }

                int selectedIndex = Math.Min(candidates.Length - 1,
                    (int)(GetRandDouble(ref seed) * candidates.Length));
                int towerId = UnlockContent(candidates[selectedIndex]);
                if (towerId > 0) {
                    bootstrapTowers ??= [];
                    bootstrapTowers.Add(towerId);
                }
                progress -= 10000;
            }
            DirectionProgress[itemId] = GetCandidateIdsUnsafe(itemId).Length == 0
                ? 0 : Math.Max(0, Math.Min(9999, progress));
            snapshotDirty = true;
            consumed = successCount - remainingSuccesses;
        }
        // 先释放研究锁，再写库存；上传通知会反向进入研究域。
        if (bootstrapTowers != null) {
            foreach (int towerId in bootstrapTowers) {
                DataCenterInventory.AddExplorationReward(towerId, 1);
            }
        }
        return consumed;
    }

    /// <summary>
    /// 记录高级塔在当前存档中已经取得。
    /// </summary>
    public static void NotifyAdvancedTowerObtained(int itemId) {
        if (!FractionatorTowerCatalog.IsAdvancedTowerItem(itemId)) {
            return;
        }
        lock (Sync) {
            if (AcquiredAdvancedTowers.Add(itemId)) {
                snapshotDirty = true;
            }
        }
    }

    /// <summary>
    /// 在亚空间中的同类高级塔首次达到门槛时，永久开放对应特质的探索候选。
    /// </summary>
    public static void NotifyAdvancedTowerInventoryCount(int itemId, long inventoryCount) {
        if (NebulaMultiplayerModAPI.IsClient || inventoryCount < AdvancedTowerEligibilityThreshold
            || !FractionatorTowerCatalog.IsAdvancedTowerItem(itemId)) {
            return;
        }
        lock (Sync) {
            bool changed = AcquiredAdvancedTowers.Add(itemId);
            if (AdvancedTowerInventoryMilestones.Add(itemId)) {
                changed = true;
                QueueNotification(string.Format(
                    "高级塔库存门槛".Translate(), GetItemName(itemId)));
            }
            if (changed) {
                snapshotDirty = true;
                PublishAnalysisRuntimeSnapshotUnsafe();
            }
        }
    }

    /// <summary>
    /// 载入库存后补齐已经达到门槛的高级塔探索条件。
    /// </summary>
    public static void RefreshAdvancedTowerInventoryMilestones() {
        if (NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        NotifyAdvancedTowerInventoryCount(IFE高级矿物塔,
            DataCenterInventory.GetModDataItemCount(IFE高级矿物塔));
        NotifyAdvancedTowerInventoryCount(IFE高级交互塔,
            DataCenterInventory.GetModDataItemCount(IFE高级交互塔));
        NotifyAdvancedTowerInventoryCount(IFE高级转化塔,
            DataCenterInventory.GetModDataItemCount(IFE高级转化塔));
    }

    /// <summary>
    /// 获取上一完整分钟Memory献祭为指定高级塔提供的成功率增益。
    /// </summary>
    /// <param name="towerItemId">高级塔物品身份。</param>
    /// <returns>三类高级塔共享的相对增益，其他物品为零。</returns>
    public static float GetAdvancedTowerSacrificeBoost(int towerItemId) =>
        FractionatorTowerCatalog.IsAdvancedTowerItem(towerItemId) ? MemorySuccessBoost : 0f;

    /// <summary>
    /// 每秒维护阶段资格，在完整分钟边界发布上一窗口的Memory献祭增益。
    /// </summary>
    public static void UpdateSacrificeBoosts(long gameTick) {
        if (NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        RefreshStageResearch();
        lock (Sync) {
            AdvanceMemoryMinute(gameTick / 3600L);
        }
    }

    /// <summary>
    /// 判断指定 FE 科技是否已经完成对应方向的探索与解析。
    /// </summary>
    public static bool CanResearchTechnology(int techId) =>
        !GrowthTechnologyManager.TryGetTechnologyContentId(techId, out _)
        || GrowthTechnologyManager.IsTechnologyDiscovered(techId);

    /// <summary>
    /// 在主线程显示解析和探索结果，并返回是否需要广播联机快照。
    /// </summary>
    public static bool FlushMainThreadNotifications() {
        Queue<string> notifications = [];
        int signalAccelerations;
        bool dirty;
        lock (Sync) {
            while (PendingNotifications.Count > 0) {
                notifications.Enqueue(PendingNotifications.Dequeue());
            }
            signalAccelerations = pendingSignalAccelerations;
            pendingSignalAccelerations = 0;
            dirty = snapshotDirty;
            snapshotDirty = false;
        }
        if (signalAccelerations > 0) {
            ExplorationManager.AccelerateNextSignal(signalAccelerations);
        }
        while (notifications.Count > 0) {
            UIRealtimeTip.Popup(notifications.Dequeue(), true, 2);
        }
        return dirty;
    }

    /// <summary>
    /// 读取研究状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        lock (Sync) {
            belongsToGeneration3 = false;
            bool localDisqualified = NebulaMultiplayerModAPI.IsClient && completionDisqualified;
            // 没有资格记录的同格式存档不能证明未使用沙盒；仅保留玩法数据，不补授本地完成标记。
            completionDisqualified = true;
            EligibleDirections.Clear();
            ExploredContents.Clear();
            ExploredContentDirections.Clear();
            UnlockedContents.Clear();
            DirectionProgress.Clear();
            BootstrapTowerGrants.Clear();
            AcquiredAdvancedTowers.Clear();
            AdvancedTowerInventoryMilestones.Clear();
            discoveredContentCount = 0;
            pendingSignalAccelerations = 0;
            ResetProgression();
            r.ReadBlocks(
                ("Generation", br => belongsToGeneration3 = br.ReadBoolean()),
                ("CompletionDisqualified", br => completionDisqualified = br.ReadBoolean() || localDisqualified),
                ("StackProgressionMode", br =>
                    GrowthTechnologyManager.SetStackProgressionMode(br.ReadBoolean())),
                ("EligibleDirections", br => ReadIntSet(br, EligibleDirections)),
                ("ExploredContents", br => {
                    ReadStringIntDictionary(br, ExploredContentDirections);
                    ExploredContents.Clear();
                    ExploredContents.UnionWith(ExploredContentDirections.Keys);
                }),
                ("UnlockedContents", br => ReadStringSet(br, UnlockedContents)),
                ("DirectionProgress", br => ReadIntDictionary(br, DirectionProgress)),
                ("BootstrapTowerGrants", br => ReadIntSet(br, BootstrapTowerGrants)),
                ("AcquiredAdvancedTowers", br => ReadIntSet(br, AcquiredAdvancedTowers)),
                ("AdvancedTowerInventoryMilestones", br =>
                    ReadIntSet(br, AdvancedTowerInventoryMilestones)),
                ("Progression", ReadProgression),
                ("DiscoveredContentCount", br => discoveredContentCount = Math.Max(0, br.ReadInt32()))
            );
            foreach (string contentId in UnlockedContents.ToArray()) {
                if (!ContentDefinitions.ContainsKey(contentId)) {
                    UnlockedContents.Remove(contentId);
                }
            }
            foreach (string contentId in ExploredContents.ToArray()) {
                if (!ContentDefinitions.ContainsKey(contentId)
                    || UnlockedContents.Contains(contentId)
                    || !ExploredContentDirections.TryGetValue(contentId, out int itemId)
                    || !ContentDirections.TryGetValue(contentId, out List<int> directions)
                    || !directions.Contains(itemId)
                    || !IsExplorationDirectionUnsafe(contentId, itemId)) {
                    ExploredContents.Remove(contentId);
                    ExploredContentDirections.Remove(contentId);
                }
            }
            AdvancedTowerInventoryMilestones.RemoveWhere(
                itemId => !FractionatorTowerCatalog.IsAdvancedTowerItem(itemId));
            EligibleDirections.RemoveWhere(itemId => !DirectionPools.ContainsKey(itemId));
            BootstrapTowerGrants.RemoveWhere(
                itemId => !FractionatorTowerCatalog.IsAdvancedTowerItem(itemId));
            AcquiredAdvancedTowers.RemoveWhere(
                itemId => !FractionatorTowerCatalog.IsAdvancedTowerItem(itemId));
            foreach (int itemId in DirectionProgress.Keys.ToArray()) {
                if (!DirectionPools.ContainsKey(itemId)) {
                    DirectionProgress.Remove(itemId);
                    continue;
                }
                DirectionProgress[itemId] = Math.Max(0, Math.Min(9999, DirectionProgress[itemId]));
            }
            foreach (KeyValuePair<string, int> pair in ExploredContentDirections) {
                EligibleDirections.Add(pair.Value);
            }
            discoveredContentCount = Math.Min(ContentDefinitions.Count,
                Math.Max(discoveredContentCount, UnlockedContents.Count));
            RebuildRemainingDirectionPoolsUnsafe();
            ClearCompletedDirectionProgress();
            PublishAnalysisRuntimeSnapshotUnsafe();
            RebuildRecipeAvailability();
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 写入研究状态。
    /// </summary>
    public static void Export(BinaryWriter w) {
        lock (Sync) {
            w.WriteBlocks(
                ("Generation", bw => bw.Write(belongsToGeneration3)),
                ("CompletionDisqualified", bw => bw.Write(completionDisqualified)),
                ("StackProgressionMode", bw =>
                    bw.Write(GrowthTechnologyManager.UsesExperiencedStackProgression)),
                ("EligibleDirections", bw => WriteIntSet(bw, EligibleDirections)),
                ("ExploredContents", bw =>
                    WriteStringIntDictionary(bw, ExploredContentDirections)),
                ("UnlockedContents", bw => WriteStringSet(bw, UnlockedContents)),
                ("DirectionProgress", bw => WriteIntDictionary(bw, DirectionProgress)),
                ("BootstrapTowerGrants", bw => WriteIntSet(bw, BootstrapTowerGrants)),
                ("AcquiredAdvancedTowers", bw => WriteIntSet(bw, AcquiredAdvancedTowers)),
                ("AdvancedTowerInventoryMilestones", bw =>
                    WriteIntSet(bw, AdvancedTowerInventoryMilestones)),
                ("Progression", WriteProgression),
                ("DiscoveredContentCount", bw => bw.Write(discoveredContentCount))
            );
        }
    }

    /// <summary>
    /// 切档时清除当前存档研究状态。
    /// </summary>
    public static void IntoOtherSave() {
        lock (Sync) {
            GrowthTechnologyManager.SetStackProgressionMode(false);
            EligibleDirections.Clear();
            ExploredContents.Clear();
            ExploredContentDirections.Clear();
            UnlockedContents.Clear();
            DirectionProgress.Clear();
            BootstrapTowerGrants.Clear();
            AcquiredAdvancedTowers.Clear();
            AdvancedTowerInventoryMilestones.Clear();
            ResetProgression();
            PendingNotifications.Clear();
            discoveredContentCount = 0;
            pendingSignalAccelerations = 0;
            belongsToGeneration3 = false;
            completionDisqualified = false;
            snapshotDirty = false;
            RebuildRemainingDirectionPoolsUnsafe();
            PublishAnalysisRuntimeSnapshotUnsafe();
            RebuildRecipeAvailability();
        }
    }

    /// <summary>
    /// 阻止尚未完成探索与解析的 FE 科技加入研究队列。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.CanEnqueueTech))]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.CanEnqueueTechIgnoreFull))]
    private static void GameHistoryData_CanEnqueueTechnology_Postfix(int techId, ref bool __result) {
        if (__result && !CanResearchTechnology(techId)) {
            __result = false;
        }
    }

    /// <summary>记录内容解析完成，返回需要在研究锁外发放的原型塔。</summary>
    /// <param name="contentId">本次解析完成的稳定内容身份。</param>
    /// <returns>首次恢复的高级塔物品 ID；没有奖励时为 0。</returns>
    private static int UnlockContent(string contentId) {
        if (!UnlockedContents.Add(contentId)) {
            return 0;
        }

        discoveredContentCount++;
        pendingSignalAccelerations++;
        ExploredContents.Remove(contentId);
        ExploredContentDirections.Remove(contentId);
        RemoveUnlockedContentFromPoolsUnsafe(contentId);
        ClearCompletedDirectionProgress();
        ResearchContentDefinition definition = ContentDefinitions[contentId];
        QueueNotification(string.Format("解析发现：{0}".Translate(), definition.DisplayName));
        WorldviewRecipe recipe = RecipeManager.GetRecipe(contentId);
        int towerId = recipe == null ? 0 : RecordBootstrapTowerGrant(recipe);
        RebuildRecipeAvailability();
        PublishAnalysisRuntimeSnapshotUnsafe();
        snapshotDirty = true;
        return towerId;
    }

    /// <summary>在研究锁内登记首次原型奖励；实际入库由解析批次在锁外完成。</summary>
    /// <param name="recipe">刚刚解析完成的配方。</param>
    /// <returns>待发放的塔物品 ID；已领取或非高级配方时为 0。</returns>
    private static int RecordBootstrapTowerGrant(WorldviewRecipe recipe) {
        if (!recipe.IsAdvanced) {
            return 0;
        }
        int towerId = recipe.RecipeType switch {
            ERecipe.AdvancedMineral => IFE高级矿物塔,
            ERecipe.AdvancedInteraction => IFE高级交互塔,
            ERecipe.AdvancedConversion => IFE高级转化塔,
            _ => 0,
        };
        if (towerId <= 0 || !BootstrapTowerGrants.Add(towerId)) {
            return 0;
        }

        QueueNotification(string.Format("已恢复一座原型：{0}".Translate(), GetItemName(towerId)));
        return towerId;
    }

    private static void UnlockWithoutReward(string contentId) {
        if (ContentDefinitions.ContainsKey(contentId) && UnlockedContents.Add(contentId)) {
            discoveredContentCount++;
            ExploredContents.Remove(contentId);
            ExploredContentDirections.Remove(contentId);
            RemoveUnlockedContentFromPoolsUnsafe(contentId);
            ClearCompletedDirectionProgress();
            PublishAnalysisRuntimeSnapshotUnsafe();
        }
        RebuildRecipeAvailability();
    }

    private static void ClearCompletedDirectionProgress() {
        foreach (int itemId in DirectionProgress.Keys
                     .Where(itemId => GetRemainingDirectionCountUnsafe(itemId) == 0)
                     .ToArray()) {
            DirectionProgress.Remove(itemId);
        }
    }

    private static void RebuildRecipeAvailability() {
        List<KeyValuePair<RecipeKey, bool>> availability = [];
        foreach (WorldviewRecipe recipe in RecipeManager.WorldviewRecipes) {
            if (recipe.IsAdvanced || recipe.Kind == WorldviewRecipeKind.BasicAnalysis) {
                continue;
            }
            availability.Add(new KeyValuePair<RecipeKey, bool>(
                RecipeKey.FromRecipe(recipe), UnlockedContents.Contains(recipe.StableId)));
        }
        RecipeAvailabilityStore.ReplaceManaged(availability);
    }

    private static string[] GetCandidateIdsUnsafe(int itemId) {
        if (!RemainingDirectionPools.TryGetValue(itemId, out List<string> remaining)
            || remaining.Count == 0) {
            return Array.Empty<string>();
        }
        return remaining
            .Where(contentId => ExploredContentDirections.TryGetValue(contentId, out int directionItemId)
                                && directionItemId == itemId)
            .OrderBy(contentId => contentId, StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetRemainingDirectionCountUnsafe(int itemId) =>
        RemainingDirectionPools.TryGetValue(itemId, out List<string> remaining) ? remaining.Count : 0;

    private static bool AreAllTraitContents(string[] contentIds) {
        foreach (string contentId in contentIds) {
            if (!IsTraitContent(contentId)) {
                return false;
            }
        }
        return contentIds.Length > 0;
    }

    private static bool IsTraitContent(string contentId) =>
        contentId is AdvancedMineralTraitId or AdvancedInteractionTraitId or AdvancedConversionTraitId;

    private static int GetDirectionProgressUnsafe(int itemId) =>
        DirectionProgress.TryGetValue(itemId, out int progress) ? progress : 0;

    private static void AddContentDefinition(ResearchContentDefinition definition) {
        ContentDefinitions[definition.Id] = definition;
    }

    private static void AddTechnologyContentDefinitions() {
        AddTechnologyContent(GrowthTechnologyManager.StackTechnologyContentId,
            "FE分馏堆叠成长".Translate(), 0);
        AddTechnologyContent(GrowthTechnologyManager.ExplorationTechnologyContentId,
            "FE探索信号增幅".Translate(), 1);
        AddTechnologyContent(GrowthTechnologyManager.PlanetaryInteractionTechnologyContentId,
            "T行星内物流交互".Translate(), 2);
        AddTechnologyContent(GrowthTechnologyManager.SubspaceEfficiencyTechnologyContentId,
            "FE亚空间能效".Translate(), 2);
        AddTechnologyContent(GrowthTechnologyManager.InterstellarInteractionTechnologyContentId,
            "T星际物流交互".Translate(), 3);
    }

    private static void AddTechnologyContent(string contentId, string displayName, int tier) {
        AddContentDefinition(new ResearchContentDefinition(contentId, displayName, tier));
        AddToPool(IFE记忆源点, contentId);
    }

    private static bool CanDiscoverContentUnsafe(string contentId, int itemId) =>
        contentId != null
        && ContentDefinitions.ContainsKey(contentId)
        && ContentDirections.TryGetValue(contentId, out List<int> directions)
        && directions.Contains(itemId)
        && IsExplorationDirectionUnsafe(contentId, itemId)
        && !UnlockedContents.Contains(contentId)
        && !ExploredContents.Contains(contentId)
        && IsContentExplorableUnsafe(contentId);

    private static bool IsContentExplorableUnsafe(string contentId) {
        if (!ContentDirections.ContainsKey(contentId)) {
            return false;
        }
        return contentId switch {
            AdvancedMineralTraitId =>
                SacrificeUnlocked && AdvancedTowerInventoryMilestones.Contains(IFE高级矿物塔),
            AdvancedInteractionTraitId =>
                SacrificeUnlocked && AdvancedTowerInventoryMilestones.Contains(IFE高级交互塔),
            AdvancedConversionTraitId =>
                SacrificeUnlocked && AdvancedTowerInventoryMilestones.Contains(IFE高级转化塔),
            _ => ContentDirections[contentId].Any(itemId =>
                IsExplorationDirectionUnsafe(contentId, itemId)),
        };
    }

    private static bool IsExplorationDirectionUnsafe(string contentId, int itemId) =>
        ContentDirections.TryGetValue(contentId, out List<int> directions) && directions.Contains(itemId);

    private static void AddToPool(int itemId, string contentId) {
        if (itemId <= 0) {
            return;
        }
        if (!DirectionPools.TryGetValue(itemId, out List<string> pool)) {
            pool = [];
            DirectionPools[itemId] = pool;
        }
        if (pool.Contains(contentId)) {
            return;
        }
        pool.Add(contentId);
        if (!ContentDirections.TryGetValue(contentId, out List<int> directions)) {
            directions = [];
            ContentDirections[contentId] = directions;
        }
        directions.Add(itemId);
    }

    private static void RebuildRemainingDirectionPoolsUnsafe() {
        RemainingDirectionPools.Clear();
        foreach (KeyValuePair<int, List<string>> pair in DirectionPools) {
            List<string> remaining = [];
            foreach (string contentId in pair.Value) {
                if (!UnlockedContents.Contains(contentId)) {
                    remaining.Add(contentId);
                }
            }
            RemainingDirectionPools[pair.Key] = remaining;
        }
    }

    private static void RemoveUnlockedContentFromPoolsUnsafe(string contentId) {
        if (!ContentDirections.TryGetValue(contentId, out List<int> directions)) {
            return;
        }
        foreach (int itemId in directions) {
            if (RemainingDirectionPools.TryGetValue(itemId, out List<string> remaining)) {
                remaining.Remove(contentId);
            }
        }
    }

    private static void PublishAnalysisRuntimeSnapshotUnsafe() {
        Dictionary<int, string[]> candidatesByItemId = [];
        foreach (int itemId in EligibleDirections) {
            string[] candidates = GetCandidateIdsUnsafe(itemId);
            if (candidates.Length > 0) {
                candidatesByItemId[itemId] = candidates;
            }
        }
        Volatile.Write(ref analysisRuntimeSnapshot,
            new AnalysisRuntimeSnapshot(unchecked(analysisRuntimeSnapshot.Version + 1),
                candidatesByItemId, [.. UnlockedContents]));
    }

    private static int GetRecipeTier(WorldviewRecipe recipe) => recipe.ResearchStage;

    private static int GetRelatedCommonResource(int itemId) {
        return itemId switch {
            I铁矿 => I石矿,
            I石矿 => I铁矿,
            I铜矿 => I铁矿,
            I硅石 => I石矿,
            I钛石 => I硅石,
            I煤矿 => I石矿,
            I水 => I原油,
            I原油 => I水,
            I氢 => I重氢,
            I重氢 => I氢,
            _ => I石矿,
        };
    }

    private static string GetItemName(int itemId) => LDB.items.Select(itemId)?.name ?? itemId.ToString();

    private static void QueueNotification(string message) {
        PendingNotifications.Enqueue(message);
    }

    private static int ReadImportedCollectionCount(BinaryReader reader, string collectionName) {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumImportedCollectionCount) {
            throw new InvalidDataException(
                $"研究存档集合 {collectionName} 的元素数量无效：{count}。");
        }
        return count;
    }

    private static void ReadIntSet(BinaryReader reader, HashSet<int> target) {
        target.Clear();
        int count = ReadImportedCollectionCount(reader, nameof(HashSet<int>));
        for (int i = 0; i < count; i++) {
            target.Add(reader.ReadInt32());
        }
    }

    private static void WriteIntSet(BinaryWriter writer, HashSet<int> source) {
        writer.Write(source.Count);
        foreach (int value in source.OrderBy(value => value)) {
            writer.Write(value);
        }
    }

    private static void ReadStringSet(BinaryReader reader, HashSet<string> target) {
        target.Clear();
        int count = ReadImportedCollectionCount(reader, nameof(HashSet<string>));
        for (int i = 0; i < count; i++) {
            target.Add(reader.ReadString());
        }
    }

    private static void WriteStringSet(BinaryWriter writer, HashSet<string> source) {
        writer.Write(source.Count);
        foreach (string value in source.OrderBy(value => value)) {
            writer.Write(value);
        }
    }

    private static void ReadStringIntDictionary(BinaryReader reader, Dictionary<string, int> target) {
        target.Clear();
        int count = ReadImportedCollectionCount(reader, nameof(Dictionary<string, int>));
        for (int i = 0; i < count; i++) {
            target[reader.ReadString()] = reader.ReadInt32();
        }
    }

    private static void WriteStringIntDictionary(BinaryWriter writer, Dictionary<string, int> source) {
        writer.Write(source.Count);
        foreach (KeyValuePair<string, int> pair in source.OrderBy(pair => pair.Key)) {
            writer.Write(pair.Key);
            writer.Write(pair.Value);
        }
    }

    private static void ReadIntDictionary(BinaryReader reader, Dictionary<int, int> target) {
        target.Clear();
        int count = ReadImportedCollectionCount(reader, nameof(Dictionary<int, int>));
        for (int i = 0; i < count; i++) {
            target[reader.ReadInt32()] = reader.ReadInt32();
        }
    }

    private static void WriteIntDictionary(BinaryWriter writer, Dictionary<int, int> source) {
        writer.Write(source.Count);
        foreach (KeyValuePair<int, int> pair in source.OrderBy(pair => pair.Key)) {
            writer.Write(pair.Key);
            writer.Write(pair.Value);
        }
    }
}
