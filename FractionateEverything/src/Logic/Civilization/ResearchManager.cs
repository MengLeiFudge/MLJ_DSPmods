using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Recipes;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 一项可由解析池发现的配方或塔特质。
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
    bool eligible,
    int progress,
    int discoveredCount,
    int totalCount) {
    /// <summary>作为解析方向的物品 ID。</summary>
    public int ItemId { get; } = itemId;

    /// <summary>是否已经取得该方向的解析资格。</summary>
    public bool Eligible { get; } = eligible;

    /// <summary>当前内容发现进度，范围 0..9999。</summary>
    public int Progress { get; } = progress;

    /// <summary>该方向已经发现的内容数量。</summary>
    public int DiscoveredCount { get; } = discoveredCount;

    /// <summary>该方向的内容总数。</summary>
    public int TotalCount { get; } = totalCount;
}

/// <summary>
/// 随机配方研究、解析资格和高级塔取得状态。
/// </summary>
public static class ResearchManager {
    /// <summary>
    /// 供逐帧路径无锁读取的研究状态副本；研究状态每次变化后整体替换。
    /// </summary>
    private sealed class AnalysisRuntimeSnapshot(Dictionary<int, string[]> candidatesByItemId,
        HashSet<string> unlockedContents) {
        public readonly Dictionary<int, string[]> CandidatesByItemId = candidatesByItemId;
        public readonly HashSet<string> UnlockedContents = unlockedContents;
    }

    private sealed class SacrificeRateState {
        public readonly object Sync = new();
        public readonly long[] BucketSeconds = new long[SacrificeWindowSeconds];
        public readonly long[] BucketCounts = new long[SacrificeWindowSeconds];
        public float SuccessBoost;

        public SacrificeRateState() {
            for (int i = 0; i < BucketSeconds.Length; i++) {
                BucketSeconds[i] = long.MinValue;
            }
        }
    }

    /// <summary>高级塔库存达到此数量后开放对应解析方向。</summary>
    public const int AdvancedTowerEligibilityThreshold = 1000;

    private const int SacrificeWindowSeconds = 60;
    private const float SacrificeBoostStep = 0.05f;
    private const float MaximumSacrificeBoost = 0.75f;

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
    private static readonly HashSet<string> UnlockedContents = [];
    private static readonly Dictionary<int, int> DirectionProgress = [];
    private static readonly HashSet<int> BootstrapTowerGrants = [];
    private static readonly HashSet<int> AcquiredAdvancedTowers = [];
    private static readonly HashSet<int> AdvancedTowerInventoryMilestones = [];
    private static readonly SacrificeRateState AdvancedMineralSacrificeRate = new();
    private static readonly SacrificeRateState AdvancedInteractionSacrificeRate = new();
    private static readonly SacrificeRateState AdvancedConversionSacrificeRate = new();
    private static readonly Queue<string> PendingNotifications = [];
    private static AnalysisRuntimeSnapshot analysisRuntimeSnapshot = new([], []);
    private static int discoveredContentCount;
    private static int pendingSignalAccelerations;
    private static bool belongsToGeneration3;
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
                HashSet<int> directions = [];
                foreach (WorldviewRecipeInput input in recipe.Inputs) {
                    directions.Add(input.ItemId);
                }
                foreach (WorldviewRecipeOutput output in recipe.Products) {
                    directions.Add(output.ItemId);
                }
                if (recipe.Kind == WorldviewRecipeKind.BasicMineral) {
                    int relatedDirection = GetRelatedCommonResource(recipe.InputID);
                    if (relatedDirection > 0) {
                        directions.Add(relatedDirection);
                    }
                }
                foreach (int direction in directions) {
                    AddToPool(direction, recipe.StableId);
                }
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
            RebuildRemainingDirectionPoolsUnsafe();
            PublishAnalysisRuntimeSnapshotUnsafe();
        }
    }

    /// <summary>
    /// 获取需要建立基础解析入口的物品。Frag 和三类高级塔始终包含在内。
    /// </summary>
    public static int[] GetAnalyzableItemIds() {
        lock (Sync) {
            HashSet<int> itemIds = [.. DirectionPools.Keys,
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
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 首次取得遗物时授予初始解析资格与两张基础矿物配方。
    /// </summary>
    public static void GrantStarterResearchState() {
        lock (Sync) {
            EligibleDirections.Add(I石矿);
            EligibleDirections.Add(I铁矿);
            EligibleDirections.Add(IFE通用原胚);
            UnlockWithoutReward($"mineral-basic:{I石矿}");
            UnlockWithoutReward($"mineral-basic:{I铁矿}");
            PublishAnalysisRuntimeSnapshotUnsafe();
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 标记存档来自旧格式；这类存档不会写入跨存档完成标记。
    /// </summary>
    public static void MarkLegacyGeneration() {
        lock (Sync) {
            GrowthTechnologyManager.SetStackProgressionMode(false);
            belongsToGeneration3 = false;
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 判断指定物品是否已取得解析资格且池内仍有内容。Frag 始终可解析。
    /// </summary>
    public static bool CanAnalyzeDirection(int itemId) {
        if (itemId == IFE残片 || FractionatorTowerCatalog.IsAdvancedTowerItem(itemId)) {
            return true;
        }
        return Volatile.Read(ref analysisRuntimeSnapshot).CandidatesByItemId.ContainsKey(itemId);
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
    /// 获取全部已取得资格的解析方向。
    /// </summary>
    public static int[] GetEligibleDirections() {
        lock (Sync) {
            return EligibleDirections.OrderBy(id => id).ToArray();
        }
    }

    /// <summary>
    /// 获取仍有未发现内容的候选解析方向，供探索点生成资格时使用。
    /// </summary>
    public static int[] GetPotentialDirections() {
        lock (Sync) {
            int currentTier = UnlockedContents
                .Where(ContentDefinitions.ContainsKey)
                .Select(contentId => ContentDefinitions[contentId].Tier)
                .DefaultIfEmpty(0)
                .Max();
            return DirectionPools.Keys
                .Where(itemId => !FractionatorTowerCatalog.IsAdvancedTowerItem(itemId))
                .Where(itemId => !EligibleDirections.Contains(itemId))
                .Where(itemId => {
                    string[] candidates = GetCandidateIdsUnsafe(itemId);
                    return candidates.Length > 0
                           && candidates.Min(contentId => ContentDefinitions[contentId].Tier) <= currentTier;
                })
                .OrderBy(itemId => itemId)
                .ToArray();
        }
    }

    /// <summary>
    /// 取得一个物品方向的解析资格。
    /// </summary>
    public static bool GrantDirectionEligibility(int itemId, bool notify = true) {
        lock (Sync) {
            if (FractionatorTowerCatalog.IsAdvancedTowerItem(itemId)
                && !AdvancedTowerInventoryMilestones.Contains(itemId)) {
                return false;
            }
            if (itemId <= 0 || !DirectionPools.ContainsKey(itemId) || !EligibleDirections.Add(itemId)) {
                return false;
            }
            snapshotDirty = true;
            PublishAnalysisRuntimeSnapshotUnsafe();
            if (notify) {
                QueueNotification(string.Format("已取得解析资格：{0}".Translate(), GetItemName(itemId)));
            }
            return true;
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
                .Select(pair => new ResearchDirectionSnapshot(
                    pair.Key,
                    true,
                    GetDirectionProgressUnsafe(pair.Key),
                    pair.Value.Count - GetRemainingDirectionCountUnsafe(pair.Key),
                    pair.Value.Count))
                .ToArray();
        }
    }

    /// <summary>
    /// 获取当前已发现内容定义。
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
    /// 结算一批基础解析塔输入。高级塔在没有可研究内容时转为献祭，并返回 Memory 产量。
    /// </summary>
    /// <param name="itemId">输入物品 ID。</param>
    /// <param name="count">本批输入数量。</param>
    /// <param name="seed">分馏随机种子。</param>
    /// <param name="sacrificeOutputCount">高级塔献祭产生的 Memory 数量。</param>
    /// <returns>该物品是否应由解析塔消耗；false 表示原样流出。</returns>
    public static bool TryAnalyzeDirection(int itemId, int count, ref uint seed,
        out int sacrificeOutputCount) {
        sacrificeOutputCount = 0;
        if (count <= 0) {
            return false;
        }

        if (itemId == IFE残片) {
            int pointCount = WorldviewRecipe.RollExpected(ref seed, count * 0.02d);
            if (pointCount > 0) {
                ExplorationManager.AddExplorationPoints(pointCount, "Frag 解析");
            }
            return true;
        }

        bool advancedTower = FractionatorTowerCatalog.IsAdvancedTowerItem(itemId);
        AnalysisRuntimeSnapshot runtimeSnapshot = Volatile.Read(ref analysisRuntimeSnapshot);
        if (!runtimeSnapshot.CandidatesByItemId.ContainsKey(itemId)) {
            if (!advancedTower) {
                return false;
            }
            RecordAdvancedTowerSacrifice(itemId, count);
            sacrificeOutputCount = WorldviewRecipe.RollExpected(ref seed, count * 0.02d);
            return true;
        }

        int successCount = WorldviewRecipe.RollExpected(ref seed, count * 0.02d);
        if (successCount <= 0) {
            return true;
        }

        lock (Sync) {
            string[] candidates = GetCandidateIdsUnsafe(itemId);
            if (!EligibleDirections.Contains(itemId) || candidates.Length == 0) {
                if (advancedTower) {
                    RecordAdvancedTowerSacrifice(itemId, count);
                    sacrificeOutputCount = successCount;
                }
                return true;
            }

            int progress = GetDirectionProgressUnsafe(itemId);
            int remainingSuccesses = successCount;
            while (remainingSuccesses > 0) {
                candidates = GetCandidateIdsUnsafe(itemId);
                if (candidates.Length == 0) {
                    progress = 0;
                    break;
                }

                int progressPerSuccess = AreAllTraitContents(candidates) ? 100 : 500;
                int successesToComplete = Math.Max(1,
                    (10000 - progress + progressPerSuccess - 1) / progressPerSuccess);
                int appliedSuccesses = Math.Min(remainingSuccesses, successesToComplete);
                progress += appliedSuccesses * progressPerSuccess;
                remainingSuccesses -= appliedSuccesses;
                if (progress < 10000) {
                    break;
                }

                int selectedIndex = Math.Min(candidates.Length - 1,
                    (int)(GetRandDouble(ref seed) * candidates.Length));
                UnlockContent(candidates[selectedIndex]);
                progress -= 10000;
            }
            DirectionProgress[itemId] = GetCandidateIdsUnsafe(itemId).Length == 0
                ? 0
                : Math.Max(0, Math.Min(9999, progress));
            snapshotDirty = true;
            return true;
        }
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
    /// 在亚空间中的同类高级塔首次达到门槛时，永久开放该塔的解析方向。
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
                if (DirectionPools.ContainsKey(itemId) && EligibleDirections.Add(itemId)) {
                    QueueNotification(string.Format(
                        "已取得解析资格：{0}".Translate(), GetItemName(itemId)));
                }
            }
            if (changed) {
                snapshotDirty = true;
                PublishAnalysisRuntimeSnapshotUnsafe();
            }
        }
    }

    /// <summary>
    /// 载入库存后补齐已经达到门槛的高级塔解析方向。
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
    /// 获取最近一分钟献祭为同种高级塔提供的成功率加成。
    /// </summary>
    public static float GetAdvancedTowerSacrificeBoost(int towerItemId) {
        SacrificeRateState state = GetSacrificeRateState(towerItemId);
        return state == null ? 0f : Volatile.Read(ref state.SuccessBoost);
    }

    /// <summary>
    /// 每秒按最近 60 秒实际献祭数量更新三类高级塔的成功率加成。
    /// </summary>
    public static void UpdateSacrificeBoosts(long gameTick) {
        if (NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        long currentSecond = Math.Max(0L, gameTick / 60L);
        bool changed = UpdateSacrificeBoost(AdvancedMineralSacrificeRate, currentSecond);
        changed |= UpdateSacrificeBoost(AdvancedInteractionSacrificeRate, currentSecond);
        changed |= UpdateSacrificeBoost(AdvancedConversionSacrificeRate, currentSecond);
        if (changed) {
            lock (Sync) {
                snapshotDirty = true;
            }
        }
    }

    /// <summary>
    /// 判断指定成长科技是否已经取得对应塔特质资格。
    /// </summary>
    public static bool CanResearchTraitTech(int techId) {
        if (techId > TFE高级矿物特质 && techId <= TFE高级矿物特质 + 5) {
            return IsContentUnlocked(AdvancedMineralTraitId);
        }
        if (techId > TFE高级交互特质 && techId <= TFE高级交互特质 + 5) {
            return IsContentUnlocked(AdvancedInteractionTraitId);
        }
        if (techId > TFE高级转化特质 && techId <= TFE高级转化特质 + 5) {
            return IsContentUnlocked(AdvancedConversionTraitId);
        }
        return true;
    }

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
            AdvancedTowerInventoryMilestones.Clear();
            ResetSacrificeRates();
            r.ReadBlocks(
                ("Generation", br => belongsToGeneration3 = br.ReadBoolean()),
                ("StackProgressionMode", br =>
                    GrowthTechnologyManager.SetStackProgressionMode(br.ReadBoolean())),
                ("EligibleDirections", br => ReadIntSet(br, EligibleDirections)),
                ("UnlockedContents", br => ReadStringSet(br, UnlockedContents)),
                ("DirectionProgress", br => ReadIntDictionary(br, DirectionProgress)),
                ("BootstrapTowerGrants", br => ReadIntSet(br, BootstrapTowerGrants)),
                ("AcquiredAdvancedTowers", br => ReadIntSet(br, AcquiredAdvancedTowers)),
                ("AdvancedTowerInventoryMilestones", br =>
                    ReadIntSet(br, AdvancedTowerInventoryMilestones)),
                ("SacrificeRates", ReadSacrificeRates),
                ("DiscoveredContentCount", br => discoveredContentCount = Math.Max(0, br.ReadInt32()))
            );
            foreach (string contentId in UnlockedContents.ToArray()) {
                if (!ContentDefinitions.ContainsKey(contentId)) {
                    UnlockedContents.Remove(contentId);
                }
            }
            AdvancedTowerInventoryMilestones.RemoveWhere(
                itemId => !FractionatorTowerCatalog.IsAdvancedTowerItem(itemId));
            foreach (int towerItemId in new[] { IFE高级矿物塔, IFE高级交互塔, IFE高级转化塔 }) {
                if (AdvancedTowerInventoryMilestones.Contains(towerItemId)) {
                    EligibleDirections.Add(towerItemId);
                } else {
                    EligibleDirections.Remove(towerItemId);
                }
            }
            discoveredContentCount = Math.Max(discoveredContentCount, UnlockedContents.Count);
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
                ("StackProgressionMode", bw =>
                    bw.Write(GrowthTechnologyManager.UsesExperiencedStackProgression)),
                ("EligibleDirections", bw => WriteIntSet(bw, EligibleDirections)),
                ("UnlockedContents", bw => WriteStringSet(bw, UnlockedContents)),
                ("DirectionProgress", bw => WriteIntDictionary(bw, DirectionProgress)),
                ("BootstrapTowerGrants", bw => WriteIntSet(bw, BootstrapTowerGrants)),
                ("AcquiredAdvancedTowers", bw => WriteIntSet(bw, AcquiredAdvancedTowers)),
                ("AdvancedTowerInventoryMilestones", bw =>
                    WriteIntSet(bw, AdvancedTowerInventoryMilestones)),
                ("SacrificeRates", WriteSacrificeRates),
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
            UnlockedContents.Clear();
            DirectionProgress.Clear();
            BootstrapTowerGrants.Clear();
            AcquiredAdvancedTowers.Clear();
            AdvancedTowerInventoryMilestones.Clear();
            ResetSacrificeRates();
            PendingNotifications.Clear();
            discoveredContentCount = 0;
            pendingSignalAccelerations = 0;
            belongsToGeneration3 = false;
            snapshotDirty = false;
            RebuildRemainingDirectionPoolsUnsafe();
            PublishAnalysisRuntimeSnapshotUnsafe();
            RebuildRecipeAvailability();
        }
    }

    /// <summary>
    /// 阻止未发现塔特质时将其成长科技加入队列。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.CanEnqueueTech))]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.CanEnqueueTechIgnoreFull))]
    private static void GameHistoryData_CanEnqueueTraitTech_Postfix(int techId, ref bool __result) {
        if (__result && (!CanResearchTraitTech(techId)
                         || !GrowthTechnologyManager.CanResearchStackTech(techId))) {
            __result = false;
        }
    }

    private static void UnlockContent(string contentId) {
        if (!UnlockedContents.Add(contentId)) {
            return;
        }

        discoveredContentCount++;
        pendingSignalAccelerations++;
        RemoveUnlockedContentFromPoolsUnsafe(contentId);
        ClearCompletedDirectionProgress();
        ResearchContentDefinition definition = ContentDefinitions[contentId];
        QueueNotification(string.Format("解析发现：{0}".Translate(), definition.DisplayName));
        WorldviewRecipe recipe = RecipeManager.GetRecipe(contentId);
        if (recipe != null) {
            foreach (WorldviewRecipeOutput output in recipe.Products) {
                if (!FractionatorTowerCatalog.IsAdvancedTowerItem(output.ItemId)) {
                    GrantDirectionEligibility(output.ItemId, false);
                }
            }
            GrantBootstrapTower(recipe);
        }
        RebuildRecipeAvailability();
        PublishAnalysisRuntimeSnapshotUnsafe();
        snapshotDirty = true;
    }

    private static void GrantBootstrapTower(WorldviewRecipe recipe) {
        if (!recipe.IsAdvanced) {
            return;
        }
        int towerId = recipe.RecipeType switch {
            ERecipe.AdvancedMineral => IFE高级矿物塔,
            ERecipe.AdvancedInteraction => IFE高级交互塔,
            ERecipe.AdvancedConversion => IFE高级转化塔,
            _ => 0,
        };
        if (towerId <= 0 || !BootstrapTowerGrants.Add(towerId)) {
            return;
        }

        DataCenterInventory.AddExplorationReward(towerId, 1);
        NotifyAdvancedTowerObtained(towerId);
        QueueNotification(string.Format("已恢复一座原型：{0}".Translate(), GetItemName(towerId)));
    }

    private static void UnlockWithoutReward(string contentId) {
        if (ContentDefinitions.ContainsKey(contentId) && UnlockedContents.Add(contentId)) {
            discoveredContentCount++;
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
        int minimumTier = int.MaxValue;
        foreach (string contentId in remaining) {
            minimumTier = Math.Min(minimumTier, ContentDefinitions[contentId].Tier);
        }
        List<string> candidates = [];
        foreach (string contentId in remaining) {
            if (ContentDefinitions[contentId].Tier == minimumTier) {
                candidates.Add(contentId);
            }
        }
        return candidates.ToArray();
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
            new AnalysisRuntimeSnapshot(candidatesByItemId, [.. UnlockedContents]));
    }

    private static int GetRecipeTier(WorldviewRecipe recipe) {
        if (recipe.Kind == WorldviewRecipeKind.BasicMineral) {
            return 0;
        }
        if (recipe.StableId == $"interaction-culture:{IFE高级交互塔}") {
            return 1;
        }
        if (recipe.Kind is WorldviewRecipeKind.TowerCultivation or WorldviewRecipeKind.RareCatalysis
            or WorldviewRecipeKind.Replacement) {
            return 2;
        }
        return 3;
    }

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

    private static SacrificeRateState GetSacrificeRateState(int towerItemId) => towerItemId switch {
        IFE高级矿物塔 => AdvancedMineralSacrificeRate,
        IFE高级交互塔 => AdvancedInteractionSacrificeRate,
        IFE高级转化塔 => AdvancedConversionSacrificeRate,
        _ => null,
    };

    private static void RecordAdvancedTowerSacrifice(int towerItemId, int count) {
        if (NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        SacrificeRateState state = GetSacrificeRateState(towerItemId);
        if (state == null || count <= 0) {
            return;
        }
        long currentSecond = Math.Max(0L, GameMain.gameTick / 60L);
        int bucketIndex = (int)(currentSecond % SacrificeWindowSeconds);
        lock (state.Sync) {
            if (state.BucketSeconds[bucketIndex] != currentSecond) {
                state.BucketSeconds[bucketIndex] = currentSecond;
                state.BucketCounts[bucketIndex] = 0L;
            }
            long currentCount = state.BucketCounts[bucketIndex];
            state.BucketCounts[bucketIndex] = currentCount > long.MaxValue - count
                ? long.MaxValue
                : currentCount + count;
        }
    }

    private static bool UpdateSacrificeBoost(SacrificeRateState state, long currentSecond) {
        long windowStart = Math.Max(0L, currentSecond - SacrificeWindowSeconds + 1L);
        long totalCount = 0L;
        lock (state.Sync) {
            for (int i = 0; i < SacrificeWindowSeconds; i++) {
                long bucketSecond = state.BucketSeconds[i];
                if (bucketSecond < windowStart || bucketSecond > currentSecond) {
                    continue;
                }
                long bucketCount = Math.Max(0L, state.BucketCounts[i]);
                totalCount = totalCount > long.MaxValue - bucketCount
                    ? long.MaxValue
                    : totalCount + bucketCount;
            }
        }
        double sacrificesPerSecond = totalCount / (double)SacrificeWindowSeconds;
        double steps = Math.Floor(Math.Sqrt(sacrificesPerSecond) * 2d + 1e-9d);
        float updatedBoost = Math.Min(MaximumSacrificeBoost, (float)(steps * SacrificeBoostStep));
        float previousBoost = Volatile.Read(ref state.SuccessBoost);
        Volatile.Write(ref state.SuccessBoost, updatedBoost);
        return Math.Abs(previousBoost - updatedBoost) > 0.0001f;
    }

    private static void WriteSacrificeRates(BinaryWriter writer) {
        WriteSacrificeRate(writer, AdvancedMineralSacrificeRate);
        WriteSacrificeRate(writer, AdvancedInteractionSacrificeRate);
        WriteSacrificeRate(writer, AdvancedConversionSacrificeRate);
    }

    private static void WriteSacrificeRate(BinaryWriter writer, SacrificeRateState state) {
        lock (state.Sync) {
            writer.Write(Volatile.Read(ref state.SuccessBoost));
            for (int i = 0; i < SacrificeWindowSeconds; i++) {
                writer.Write(state.BucketSeconds[i]);
                writer.Write(state.BucketCounts[i]);
            }
        }
    }

    private static void ReadSacrificeRates(BinaryReader reader) {
        ReadSacrificeRate(reader, AdvancedMineralSacrificeRate);
        ReadSacrificeRate(reader, AdvancedInteractionSacrificeRate);
        ReadSacrificeRate(reader, AdvancedConversionSacrificeRate);
    }

    private static void ReadSacrificeRate(BinaryReader reader, SacrificeRateState state) {
        lock (state.Sync) {
            float successBoost = reader.ReadSingle();
            state.SuccessBoost = float.IsNaN(successBoost) || float.IsInfinity(successBoost)
                ? 0f
                : Math.Max(0f, Math.Min(MaximumSacrificeBoost, successBoost));
            for (int i = 0; i < SacrificeWindowSeconds; i++) {
                long bucketSecond = reader.ReadInt64();
                long bucketCount = reader.ReadInt64();
                state.BucketSeconds[i] = bucketSecond < 0L ? long.MinValue : bucketSecond;
                state.BucketCounts[i] = Math.Max(0L, bucketCount);
            }
        }
    }

    private static void ResetSacrificeRates() {
        ResetSacrificeRate(AdvancedMineralSacrificeRate);
        ResetSacrificeRate(AdvancedInteractionSacrificeRate);
        ResetSacrificeRate(AdvancedConversionSacrificeRate);
    }

    private static void ResetSacrificeRate(SacrificeRateState state) {
        lock (state.Sync) {
            for (int i = 0; i < SacrificeWindowSeconds; i++) {
                state.BucketSeconds[i] = long.MinValue;
                state.BucketCounts[i] = 0L;
            }
            Volatile.Write(ref state.SuccessBoost, 0f);
        }
    }

    private static string GetItemName(int itemId) => LDB.items.Select(itemId)?.name ?? itemId.ToString();

    private static void QueueNotification(string message) {
        PendingNotifications.Enqueue(message);
    }

    private static void ReadIntSet(BinaryReader reader, HashSet<int> target) {
        target.Clear();
        int count = reader.ReadInt32();
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
        int count = reader.ReadInt32();
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

    private static void ReadIntDictionary(BinaryReader reader, Dictionary<int, int> target) {
        target.Clear();
        int count = reader.ReadInt32();
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
