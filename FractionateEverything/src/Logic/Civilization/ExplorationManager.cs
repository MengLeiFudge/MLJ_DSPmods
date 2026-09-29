using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 探索点位内容类型。
/// </summary>
public enum ExplorationMarkKind {
    FirstRelic = 1,
    DirectionEligibility = 2,
    ResourceCache = 3,
}

/// <summary>
/// 星球表面的一个永久探索信号，抵达前没有时限。
/// </summary>
public sealed class ExplorationMark {
    /// <summary>存档内稳定点位 ID。</summary>
    public int Id;

    /// <summary>目标星球 ID。</summary>
    public int PlanetId;

    /// <summary>星球局部坐标。</summary>
    public Vector3 LocalPosition;

    /// <summary>点位内容类型。</summary>
    public ExplorationMarkKind Kind;

    /// <summary>具体研究内容的稳定 ID；资源点和首遗物为空。</summary>
    public string ContentId;

    /// <summary>解析物品或资源物品 ID。</summary>
    public int ItemId;

    /// <summary>资源数量。</summary>
    public int Count;
}

/// <summary>
/// 探索信号生成、导航、星球额度和抵达结算。
/// </summary>
public static class ExplorationManager {
    private const long TicksPerMinute = 3600L;
    private const float TerrestrialArrivalDistance = 120f;
    private const int MaximumImportedCollectionCount = 4096;
    private const int MaximumImportedMarkId = 1000000000;
    private static readonly object Sync = new();
    private static readonly List<ExplorationMark> ActiveMarks = [];
    private static readonly Dictionary<int, int> GeneratedCountByPlanet = [];
    private static readonly int[] FallbackResourceCandidates = [
        I铁矿, I铜矿, I硅石, I钛石, I石矿, I煤矿, I原油, I水, I氢, I重氢,
    ];
    private static int nextMarkId = 1;
    private static long nextTimedGenerationTick;
    private static uint randomSeed = 0x8F3A51C7u;
    private static bool firstRelicClaimed;
    private static bool snapshotDirty;
    private static int electromagneticMatrixMilestoneId = int.MinValue;
    private static bool milestoneLookupWarningLogged;
    private static int guidedFirstRelicId;
    private static int guidedFirstRelicPlanetId;
    private static Vector3 guidedFirstRelicPosition;

    /// <summary>
    /// 判断首个遗物是否已经取得。
    /// </summary>
    public static bool FirstRelicClaimed {
        get {
            lock (Sync) {
                return firstRelicClaimed;
            }
        }
    }

    /// <summary>
    /// 获取当前全部未探索信号。
    /// </summary>
    public static ExplorationMark[] GetActiveMarks() {
        lock (Sync) {
            return ActiveMarks.Select(CloneMark).ToArray();
        }
    }

    /// <summary>
    /// 获取距离下一次定时信号的剩余游戏刻；尚未初始化时返回 -1。
    /// </summary>
    public static long GetTicksUntilNextSignal(long gameTick) {
        lock (Sync) {
            return nextTimedGenerationTick <= 0L ? -1L : Math.Max(0L, nextTimedGenerationTick - gameTick);
        }
    }

    /// <summary>
    /// 为新存档在母星生成固定首个遗物。
    /// </summary>
    public static void EnsureFirstRelicMark() {
        if (NebulaMultiplayerModAPI.IsClient || GameMain.galaxy == null) {
            return;
        }
        if (!IsElectromagneticMatrixMilestoneUnlocked()) {
            lock (Sync) {
                int removed = ActiveMarks.RemoveAll(mark => mark.Kind == ExplorationMarkKind.FirstRelic);
                if (removed > 0) {
                    snapshotDirty = true;
                }
            }
            return;
        }
        lock (Sync) {
            if (firstRelicClaimed || ActiveMarks.Any(mark => mark.Kind == ExplorationMarkKind.FirstRelic)) {
                return;
            }
            PlanetData birthPlanet = GameMain.galaxy.PlanetById(GameMain.galaxy.birthPlanetId);
            if (birthPlanet == null) {
                return;
            }
            if (!TryTakeNextMarkId(out int markId)) {
                return;
            }
            ActiveMarks.Add(new ExplorationMark {
                Id = markId,
                PlanetId = birthPlanet.id,
                LocalPosition = CreateMarkPosition(birthPlanet),
                Kind = ExplorationMarkKind.FirstRelic,
            });
            snapshotDirty = true;
            QueueTip("检测到母星上的古文明遗物信号".Translate());
        }
    }

    /// <summary>
    /// 为尚未解锁面板的玩家设置一次首遗物导航；普通探索点不会调用该入口。
    /// </summary>
    public static void EnsureFirstRelicGuidance() {
        if (!IsElectromagneticMatrixMilestoneUnlocked()) {
            return;
        }
        ExplorationMark relic;
        lock (Sync) {
            if (firstRelicClaimed) {
                return;
            }
            relic = ActiveMarks.FirstOrDefault(mark => mark.Kind == ExplorationMarkKind.FirstRelic);
            if (relic == null || relic.Id == guidedFirstRelicId) {
                return;
            }
            relic = CloneMark(relic);
        }

        Player player = GameMain.mainPlayer;
        if (player?.navigation == null) {
            return;
        }
        player.navigation.Navigate(relic.PlanetId,
            new VectorLF3(relic.LocalPosition.x, relic.LocalPosition.y, relic.LocalPosition.z));
        player.navigation.indicatorAstroId = relic.PlanetId;
        lock (Sync) {
            guidedFirstRelicId = relic.Id;
            guidedFirstRelicPlanetId = relic.PlanetId;
            guidedFirstRelicPosition = relic.LocalPosition;
        }
    }

    /// <summary>
    /// Frag 解析或其他来源增加探索点位；每点立即尝试生成一个永久信号。
    /// </summary>
    public static void AddExplorationPoints(int count, string source) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        int generated = 0;
        lock (Sync) {
            for (int i = 0; i < count; i++) {
                if (TryGenerateMark()) {
                    generated++;
                }
            }
        }
        if (generated > 0) {
            QueueTip(string.Format("{0}发现 {1} 个探索信号".Translate(), source.Translate(), generated));
        }
    }

    /// <summary>
    /// 每发现一项研究内容，将下一次定时信号提前三分钟，最低仍保留十分钟间隔。
    /// </summary>
    public static void AccelerateNextSignal(int discoveredCount) {
        if (discoveredCount <= 0 || NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        lock (Sync) {
            if (nextTimedGenerationTick <= 0L) {
                return;
            }
            long earliestTick = GameMain.gameTick + 10L * TicksPerMinute;
            long reduction = (long)discoveredCount * 3L * TicksPerMinute;
            nextTimedGenerationTick = Math.Max(earliestTick,
                reduction >= nextTimedGenerationTick ? earliestTick : nextTimedGenerationTick - reduction);
            snapshotDirty = true;
        }
    }

    /// <summary>
    /// 主机每秒处理定时保底与抵达判定。
    /// </summary>
    public static void GameTick(long gameTick) {
        if (GameMain.mainPlayer == null || GameMain.galaxy == null) {
            return;
        }
        if (gameTick % 60L != 0L) {
            return;
        }
        if (NebulaMultiplayerModAPI.IsClient) {
            RequestNearbyClientClaims();
            return;
        }

        lock (Sync) {
            if (nextTimedGenerationTick <= 0L) {
                nextTimedGenerationTick = gameTick + GetGenerationIntervalTicks();
            }
            if (gameTick >= nextTimedGenerationTick && TryGenerateMark()) {
                QueueTip("古文明网络发现了新的探索信号".Translate());
                nextTimedGenerationTick = gameTick + GetGenerationIntervalTicks();
                snapshotDirty = true;
            }
            ResolveArrivals();
        }
    }

    /// <summary>
    /// 主机依据连接对应玩家的宇宙坐标结算探索点位。
    /// </summary>
    public static bool TryClaimFromClient(int markId, double x, double y, double z) {
        if (NebulaMultiplayerModAPI.IsClient || !IsFinite(x) || !IsFinite(y) || !IsFinite(z)) {
            return false;
        }
        VectorLF3 playerPosition = new(x, y, z);
        lock (Sync) {
            int index = ActiveMarks.FindIndex(mark => mark.Id == markId);
            if (index < 0) {
                return false;
            }
            ExplorationMark mark = ActiveMarks[index];
            if (!IsWithinArrivalSphere(mark, playerPosition)) {
                return false;
            }
            ActiveMarks.RemoveAt(index);
            ClaimMark(mark);
            snapshotDirty = true;
            return true;
        }
    }

    /// <summary>
    /// 在玩家接近目标星球后计算现场信号强度，不暴露目标方向。
    /// </summary>
    public static bool TryGetSignalStrength(ExplorationMark mark, VectorLF3 playerPosition,
        out float strength) {
        strength = 0f;
        if (mark == null || GameMain.galaxy?.PlanetById(mark.PlanetId) is not PlanetData planet) {
            return false;
        }
        float markRadius = GetMarkRadius(planet);
        float arrivalDistance = GetArrivalDistance(planet);
        double centerDistance = (playerPosition - planet.uPosition).magnitude;
        if (centerDistance > markRadius + arrivalDistance) {
            return false;
        }
        double distance = (playerPosition - GetMarkUniversalPosition(mark, planet)).magnitude;
        double maximumDistance = Math.Max(arrivalDistance + 1d, markRadius * 2d);
        strength = 1f - Mathf.Clamp01((float)((distance - arrivalDistance)
                                               / (maximumDistance - arrivalDistance)));
        return true;
    }

    /// <summary>
    /// 返回并清除联机快照脏标记。
    /// </summary>
    public static bool TakeSnapshotDirty() {
        lock (Sync) {
            bool dirty = snapshotDirty;
            snapshotDirty = false;
            return dirty;
        }
    }

    /// <summary>
    /// 读取探索状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        bool claimed;
        var importedMarkContents = new Dictionary<int, string>();
        lock (Sync) {
            ActiveMarks.Clear();
            GeneratedCountByPlanet.Clear();
            nextMarkId = 1;
            nextTimedGenerationTick = 0L;
            randomSeed = 0x8F3A51C7u;
            firstRelicClaimed = false;
            r.ReadBlocks(
                ("Header", br => {
                    nextMarkId = Math.Max(1, Math.Min(MaximumImportedMarkId, br.ReadInt32()));
                    nextTimedGenerationTick = Math.Max(0L, br.ReadInt64());
                    randomSeed = br.ReadUInt32();
                    if (randomSeed == 0u) {
                        randomSeed = 0x8F3A51C7u;
                    }
                    firstRelicClaimed = br.ReadBoolean();
                }),
                ("PlanetCounts", br => {
                    GeneratedCountByPlanet.Clear();
                    int count = ReadImportedCollectionCount(br, "PlanetCounts");
                    for (int i = 0; i < count; i++) {
                        GeneratedCountByPlanet[br.ReadInt32()] = br.ReadInt32();
                    }
                }),
                ("Marks", br => {
                    ActiveMarks.Clear();
                    int count = ReadImportedCollectionCount(br, "Marks");
                    for (int i = 0; i < count; i++) {
                        ActiveMarks.Add(new ExplorationMark {
                            Id = br.ReadInt32(),
                            PlanetId = br.ReadInt32(),
                            LocalPosition = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                            Kind = (ExplorationMarkKind)br.ReadInt32(),
                            ItemId = br.ReadInt32(),
                            Count = br.ReadInt32(),
                        });
                    }
                }),
                ("MarkContents", br => {
                    importedMarkContents.Clear();
                    int count = ReadImportedCollectionCount(br, "MarkContents");
                    for (int i = 0; i < count; i++) {
                        importedMarkContents[br.ReadInt32()] = br.ReadString();
                    }
                })
            );
            SanitizeImportedStateUnsafe(importedMarkContents);
            snapshotDirty = true;
            claimed = firstRelicClaimed;
        }
        if (claimed) {
            ClearFirstRelicGuidance();
        }
    }

    /// <summary>
    /// 写入探索状态。
    /// </summary>
    public static void Export(BinaryWriter w) {
        lock (Sync) {
            w.WriteBlocks(
                ("Header", bw => {
                    bw.Write(nextMarkId);
                    bw.Write(nextTimedGenerationTick);
                    bw.Write(randomSeed);
                    bw.Write(firstRelicClaimed);
                }),
                ("PlanetCounts", bw => {
                    bw.Write(GeneratedCountByPlanet.Count);
                    foreach (KeyValuePair<int, int> pair in GeneratedCountByPlanet.OrderBy(pair => pair.Key)) {
                        bw.Write(pair.Key);
                        bw.Write(pair.Value);
                    }
                }),
                ("Marks", bw => {
                    bw.Write(ActiveMarks.Count);
                    foreach (ExplorationMark mark in ActiveMarks) {
                        bw.Write(mark.Id);
                        bw.Write(mark.PlanetId);
                        bw.Write(mark.LocalPosition.x);
                        bw.Write(mark.LocalPosition.y);
                        bw.Write(mark.LocalPosition.z);
                        bw.Write((int)mark.Kind);
                        bw.Write(mark.ItemId);
                        bw.Write(mark.Count);
                    }
                }),
                ("MarkContents", bw => {
                    ExplorationMark[] contentMarks = ActiveMarks
                        .Where(mark => mark.Kind == ExplorationMarkKind.DirectionEligibility
                                       && !string.IsNullOrEmpty(mark.ContentId))
                        .ToArray();
                    bw.Write(contentMarks.Length);
                    foreach (ExplorationMark mark in contentMarks) {
                        bw.Write(mark.Id);
                        bw.Write(mark.ContentId);
                    }
                })
            );
        }
    }

    /// <summary>
    /// 切档时清除探索状态。
    /// </summary>
    public static void IntoOtherSave() {
        lock (Sync) {
            ActiveMarks.Clear();
            GeneratedCountByPlanet.Clear();
            nextMarkId = 1;
            nextTimedGenerationTick = 0L;
            randomSeed = 0x8F3A51C7u;
            firstRelicClaimed = false;
            snapshotDirty = false;
            guidedFirstRelicId = 0;
            guidedFirstRelicPlanetId = 0;
            guidedFirstRelicPosition = Vector3.zero;
        }
    }

    private static void SanitizeImportedStateUnsafe(Dictionary<int, string> importedMarkContents) {
        GalaxyData galaxy = GameMain.galaxy;
        long currentTick = Math.Max(0L, GameMain.gameTick);
        long maximumInterval = GetGenerationIntervalTicks();
        long latestGenerationTick = currentTick > long.MaxValue - maximumInterval
            ? long.MaxValue
            : currentTick + maximumInterval;
        nextTimedGenerationTick = Math.Min(nextTimedGenerationTick, latestGenerationTick);
        foreach (int planetId in GeneratedCountByPlanet.Keys.ToArray()) {
            PlanetData planet = galaxy?.PlanetById(planetId);
            if (galaxy != null && planet == null) {
                GeneratedCountByPlanet.Remove(planetId);
                continue;
            }
            int maximumCount = planet == null ? int.MaxValue : GetPlanetQuota(planetId);
            GeneratedCountByPlanet[planetId] = Math.Max(0,
                Math.Min(maximumCount, GeneratedCountByPlanet[planetId]));
        }

        ActiveMarks.Sort((left, right) => left.Id.CompareTo(right.Id));
        var markIds = new HashSet<int>();
        var reservedContentIds = new HashSet<string>();
        bool hasFirstRelic = false;
        for (int index = 0; index < ActiveMarks.Count; index++) {
            ExplorationMark mark = ActiveMarks[index];
            PlanetData planet = galaxy?.PlanetById(mark.PlanetId);
            if (mark.Id <= 0 || mark.Id >= MaximumImportedMarkId || !markIds.Add(mark.Id)
                || galaxy != null && planet == null) {
                ActiveMarks.RemoveAt(index--);
                continue;
            }
            float markRadius = planet == null ? 0f : GetMarkRadius(planet);
            float localDistance = mark.LocalPosition.magnitude;
            bool invalidPosition = !IsFinite(mark.LocalPosition.x)
                                   || !IsFinite(mark.LocalPosition.y)
                                   || !IsFinite(mark.LocalPosition.z)
                                   || !IsFinite(localDistance)
                                   || planet != null && (localDistance < markRadius * 0.5f
                                                         || localDistance > markRadius * 1.5f);
            if (invalidPosition) {
                if (planet == null) {
                    ActiveMarks.RemoveAt(index--);
                    continue;
                }
                mark.LocalPosition = CreateMarkPosition(planet);
            }

            switch (mark.Kind) {
                case ExplorationMarkKind.FirstRelic:
                    if (firstRelicClaimed || hasFirstRelic) {
                        ActiveMarks.RemoveAt(index--);
                        continue;
                    }
                    hasFirstRelic = true;
                    mark.ContentId = null;
                    mark.ItemId = 0;
                    mark.Count = 0;
                    break;
                case ExplorationMarkKind.DirectionEligibility:
                    bool hasValidImportedContent = importedMarkContents.TryGetValue(mark.Id,
                            out string contentId)
                        && !string.IsNullOrEmpty(contentId)
                        && !reservedContentIds.Contains(contentId)
                        && ResearchManager.CanDiscoverContent(contentId, mark.ItemId);
                    if (!hasValidImportedContent) {
                        contentId = ResearchManager.GetLegacyExplorationContentId(mark.ItemId,
                            reservedContentIds);
                    }
                    mark.ContentId = contentId;
                    if (!string.IsNullOrEmpty(contentId)) {
                        reservedContentIds.Add(contentId);
                    } else if (planet != null) {
                        ConfigureResourceMark(mark, planet);
                    } else {
                        ActiveMarks.RemoveAt(index--);
                    }
                    break;
                case ExplorationMarkKind.ResourceCache:
                    mark.ContentId = null;
                    if (mark.Count <= 0 || !LDB.items.Exist(mark.ItemId)) {
                        if (planet != null) {
                            ConfigureResourceMark(mark, planet);
                        } else {
                            ActiveMarks.RemoveAt(index--);
                        }
                    } else if (planet != null) {
                        mark.Count = Math.Min(mark.Count, GetResourceRewardCount(mark.ItemId, planet));
                    }
                    break;
                default:
                    if (planet != null) {
                        ConfigureResourceMark(mark, planet);
                    } else {
                        ActiveMarks.RemoveAt(index--);
                    }
                    break;
            }
        }

        int maximumMarkId = ActiveMarks.Select(mark => mark.Id).DefaultIfEmpty(0).Max();
        nextMarkId = Math.Max(nextMarkId, maximumMarkId + 1);
    }

    private static int ReadImportedCollectionCount(BinaryReader reader, string collectionName) {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumImportedCollectionCount) {
            throw new InvalidDataException(
                $"探索存档集合 {collectionName} 的元素数量无效：{count}。");
        }
        return count;
    }

    private static bool TryGenerateMark() {
        List<PlanetData> candidates = GetReachablePlanets()
            .Where(planet => GetGeneratedCount(planet.id) < GetPlanetQuota(planet.id))
            .ToList();
        if (candidates.Count == 0) {
            return false;
        }

        PlanetData planet = candidates[NextInt(candidates.Count)];
        bool eligibility = GetRandDouble(ref randomSeed) < GetEligibilityProbability();
        int itemId = 0;
        int count = 0;
        string contentId = null;
        ExplorationMarkKind kind;
        if (eligibility) {
            string[] potentialContents = ResearchManager.GetPotentialContentIds()
                .Where(candidateId => !ActiveMarks.Any(mark =>
                    mark.Kind == ExplorationMarkKind.DirectionEligibility
                    && mark.ContentId == candidateId))
                .ToArray();
            if (potentialContents.Length > 0) {
                contentId = potentialContents[NextInt(potentialContents.Length)];
                int[] directionItems = ResearchManager.GetContentDirectionItemIds(contentId);
                if (directionItems.Length > 0) {
                    itemId = directionItems[NextInt(directionItems.Length)];
                    kind = ExplorationMarkKind.DirectionEligibility;
                } else {
                    contentId = null;
                    kind = ExplorationMarkKind.ResourceCache;
                }
            } else {
                kind = ExplorationMarkKind.ResourceCache;
            }
        } else {
            kind = ExplorationMarkKind.ResourceCache;
        }

        if (!TryTakeNextMarkId(out int markId)) {
            return false;
        }
        var mark = new ExplorationMark {
            Id = markId,
            PlanetId = planet.id,
            LocalPosition = CreateMarkPosition(planet),
            Kind = kind,
            ContentId = contentId,
            ItemId = itemId,
            Count = count,
        };
        if (kind == ExplorationMarkKind.ResourceCache) {
            ConfigureResourceMark(mark, planet);
        }
        ActiveMarks.Add(mark);
        GeneratedCountByPlanet[planet.id] = GetGeneratedCount(planet.id) + 1;
        snapshotDirty = true;
        return true;
    }

    private static void ResolveArrivals() {
        Player player = GameMain.mainPlayer;
        VectorLF3 playerPosition = player.uPosition;
        for (int i = ActiveMarks.Count - 1; i >= 0; i--) {
            ExplorationMark mark = ActiveMarks[i];
            if (!IsWithinArrivalSphere(mark, playerPosition)) {
                continue;
            }

            ActiveMarks.RemoveAt(i);
            ClaimMark(mark);
            snapshotDirty = true;
        }
    }

    private static void ClaimMark(ExplorationMark mark) {
        switch (mark.Kind) {
            case ExplorationMarkKind.FirstRelic:
                firstRelicClaimed = true;
                ClearFirstRelicGuidance();
                CivilizationRecoveryManager.CompleteFirstRelicRecovery();
                QueueTip("已接入古文明亚空间网络".Translate());
                break;
            case ExplorationMarkKind.DirectionEligibility:
                ResearchManager.DiscoverContent(mark.ContentId, mark.ItemId);
                break;
            case ExplorationMarkKind.ResourceCache:
                DataCenterInventory.AddExplorationReward(mark.ItemId, mark.Count);
                QueueTip(string.Format("探索取得：{0} × {1}".Translate(),
                    LDB.items.Select(mark.ItemId)?.name ?? mark.ItemId.ToString(), mark.Count));
                break;
        }
    }

    private static List<PlanetData> GetReachablePlanets() {
        GalaxyData galaxy = GameMain.galaxy;
        int thrusterLevel = GameMain.mainPlayer?.mecha?.thrusterLevel ?? 0;
        if (thrusterLevel < 2) {
            PlanetData birthPlanet = galaxy.PlanetById(galaxy.birthPlanetId);
            return birthPlanet == null ? [] : [birthPlanet];
        }
        if (thrusterLevel < 3) {
            StarData birthStar = galaxy.StarById(galaxy.birthStarId);
            return birthStar?.planets?.Where(planet => planet != null).ToList() ?? [];
        }
        List<PlanetData> planets = [];
        foreach (StarData star in galaxy.stars) {
            if (star?.planets == null) {
                continue;
            }
            planets.AddRange(star.planets.Where(planet => planet != null));
        }
        return planets;
    }

    private static int GetPlanetQuota(int planetId) {
        GalaxyData galaxy = GameMain.galaxy;
        if (planetId == galaxy.birthPlanetId) {
            return 8;
        }
        PlanetData planet = galaxy.PlanetById(planetId);
        return planet?.star?.id == galaxy.birthStarId ? 6 : 4;
    }

    private static int GetDistanceValueMultiplier(int planetId) {
        GalaxyData galaxy = GameMain.galaxy;
        if (planetId == galaxy.birthPlanetId) {
            return 1;
        }
        PlanetData planet = galaxy.PlanetById(planetId);
        return planet?.star?.id == galaxy.birthStarId ? 3 : 10;
    }

    private static double GetEligibilityProbability() {
        int thrusterLevel = GameMain.mainPlayer?.mecha?.thrusterLevel ?? 0;
        return thrusterLevel switch {
            < 2 => 0.70d,
            < 3 => 0.50d,
            _ => 0.30d,
        };
    }

    private static long GetGenerationIntervalTicks() {
        int growthLevel = GrowthTechnologyManager.ExplorationLevel;
        double multiplier = Math.Pow(0.9d, growthLevel);
        return Math.Max(10L * TicksPerMinute, (long)(60L * TicksPerMinute * multiplier));
    }

    private static int GetGeneratedCount(int planetId) =>
        GeneratedCountByPlanet.TryGetValue(planetId, out int count) ? count : 0;

    private static void ConfigureResourceMark(ExplorationMark mark, PlanetData planet) {
        mark.Kind = ExplorationMarkKind.ResourceCache;
        mark.ContentId = null;
        mark.ItemId = SelectResourceCandidate(planet);
        mark.Count = GetResourceRewardCount(mark.ItemId, planet);
    }

    private static int GetResourceRewardCount(int itemId, PlanetData planet) {
        ItemProto item = LDB.items.Select(itemId);
        int stackSize = Math.Max(1, item?.StackSize ?? 1);
        long resourceCount = 5L * stackSize * GetDistanceValueMultiplier(planet.id);
        return resourceCount > int.MaxValue ? int.MaxValue : (int)resourceCount;
    }

    private static bool TryTakeNextMarkId(out int markId) {
        if (nextMarkId <= 0 || nextMarkId >= MaximumImportedMarkId) {
            markId = 0;
            return false;
        }
        markId = nextMarkId++;
        return true;
    }

    private static int SelectResourceCandidate(PlanetData planet) {
        if (NextInt(100) < 10) {
            return IFE通用原胚;
        }

        HashSet<int> veinItems = [];
        HashSet<int> fluidItems = [];
        if (planet.type == EPlanetType.Gas && planet.gasItems != null) {
            foreach (int itemId in planet.gasItems) {
                if (itemId > 0 && LDB.items.Exist(itemId)) {
                    fluidItems.Add(itemId);
                }
            }
        } else if (planet.waterItemId > 0 && LDB.items.Exist(planet.waterItemId)) {
            fluidItems.Add(planet.waterItemId);
        }
        lock (planet.veinGroupsLock) {
            if (planet.veinGroups != null) {
                foreach (VeinGroup group in planet.veinGroups) {
                    if (group.type == EVeinType.None || group.count <= 0 || group.amount <= 0) {
                        continue;
                    }
                    int miningItemId = LDB.veins.Select((int)group.type)?.MiningItem ?? 0;
                    if (miningItemId > 0 && LDB.items.Exist(miningItemId)) {
                        veinItems.Add(miningItemId);
                    }
                }
            }
        }

        (int veinWeight, int fluidWeight) = planet.type switch {
            EPlanetType.Ocean => (55, 35),
            EPlanetType.Ice => (60, 30),
            EPlanetType.Gas => (0, 90),
            EPlanetType.Vocano or EPlanetType.Desert => (80, 10),
            _ => (80, 10),
        };
        if (veinItems.Count == 0 && fluidItems.Count == 0) {
            return FallbackResourceCandidates[NextInt(FallbackResourceCandidates.Length)];
        }
        if (veinItems.Count == 0) {
            fluidWeight += veinWeight;
            veinWeight = 0;
        } else if (fluidItems.Count == 0) {
            veinWeight += fluidWeight;
            fluidWeight = 0;
        }

        bool selectVein = NextInt(veinWeight + fluidWeight) < veinWeight;
        int[] candidates = (selectVein ? veinItems : fluidItems).OrderBy(itemId => itemId).ToArray();
        return candidates[NextInt(candidates.Length)];
    }

    private static Vector3 CreateMarkPosition(PlanetData planet) {
        double y = GetRandDouble(ref randomSeed) * 2d - 1d;
        double angle = GetRandDouble(ref randomSeed) * Math.PI * 2d;
        double horizontal = Math.Sqrt(Math.Max(0d, 1d - y * y));
        float radius = GetMarkRadius(planet);
        return new Vector3(
            (float)(Math.Cos(angle) * horizontal * radius),
            (float)(y * radius),
            (float)(Math.Sin(angle) * horizontal * radius));
    }

    private static float GetMarkRadius(PlanetData planet) => planet.type == EPlanetType.Gas
        ? planet.realRadius + Math.Max(40f, planet.realRadius * 0.25f)
        : planet.realRadius + 1.5f;

    private static float GetArrivalDistance(PlanetData planet) => planet.type == EPlanetType.Gas
        ? Math.Max(TerrestrialArrivalDistance, planet.realRadius * 0.25f)
        : TerrestrialArrivalDistance;

    private static VectorLF3 GetMarkUniversalPosition(ExplorationMark mark, PlanetData planet) =>
        planet.uPosition + Maths.QRotateLF(planet.runtimeRotation, mark.LocalPosition);

    private static bool IsWithinArrivalSphere(ExplorationMark mark, VectorLF3 playerPosition) {
        PlanetData planet = GameMain.galaxy?.PlanetById(mark.PlanetId);
        if (planet == null) {
            return false;
        }
        double distance = (playerPosition - GetMarkUniversalPosition(mark, planet)).magnitude;
        return distance <= GetArrivalDistance(planet);
    }

    private static bool IsElectromagneticMatrixMilestoneUnlocked() {
        MilestoneSystem milestoneSystem = GameMain.data?.milestoneSystem;
        if (milestoneSystem == null) {
            return false;
        }
        int milestoneId = ResolveElectromagneticMatrixMilestoneId();
        return milestoneId > 0 && milestoneSystem.MilestoneUnlocked(milestoneId);
    }

    private static int ResolveElectromagneticMatrixMilestoneId() {
        if (electromagneticMatrixMilestoneId != int.MinValue) {
            return electromagneticMatrixMilestoneId;
        }
        electromagneticMatrixMilestoneId = 0;
        foreach (MilestoneProto milestone in LDB.milestones.dataArray) {
            if (milestone?.DeterminatorParams == null || milestone.DeterminatorParams.Length == 0
                || milestone.DeterminatorParams[0] != I电磁矩阵
                || !string.Equals(milestone.DeterminatorName, nameof(MST_ItemFirstProduction),
                    StringComparison.Ordinal)) {
                continue;
            }
            electromagneticMatrixMilestoneId = milestone.ID;
            break;
        }
        if (electromagneticMatrixMilestoneId <= 0 && !milestoneLookupWarningLogged) {
            milestoneLookupWarningLogged = true;
            LogWarning("未找到首次生产电磁矩阵的里程碑，首遗物信号暂不生成。");
        }
        return electromagneticMatrixMilestoneId;
    }

    private static int NextInt(int count) {
        if (count <= 1) {
            return 0;
        }
        return Math.Min(count - 1, (int)(GetRandDouble(ref randomSeed) * count));
    }

    private static void RequestNearbyClientClaims() {
        VectorLF3 position = GameMain.mainPlayer.uPosition;
        ExplorationMark[] marks = GetActiveMarks();
        foreach (ExplorationMark mark in marks) {
            if (IsWithinArrivalSphere(mark, position)) {
                NebulaMultiplayerModAPI.RequestExplorationClaim(mark.Id);
            }
        }
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static void ClearFirstRelicGuidance() {
        int planetId;
        Vector3 position;
        lock (Sync) {
            if (guidedFirstRelicId == 0) {
                return;
            }
            planetId = guidedFirstRelicPlanetId;
            position = guidedFirstRelicPosition;
            guidedFirstRelicId = 0;
            guidedFirstRelicPlanetId = 0;
            guidedFirstRelicPosition = Vector3.zero;
        }

        PlayerNavigation navigation = GameMain.mainPlayer?.navigation;
        if (navigation == null) {
            return;
        }
        VectorLF3 target = navigation.naviTarget;
        bool stillGuidingRelic = navigation.naviAstroId == planetId
            && Math.Abs(target.x - position.x) < 0.01d
            && Math.Abs(target.y - position.y) < 0.01d
            && Math.Abs(target.z - position.z) < 0.01d;
        if (stillGuidingRelic) {
            navigation.Arrive();
            if (navigation.indicatorAstroId == planetId) {
                navigation.indicatorAstroId = 0;
            }
        }
    }

    private static void QueueTip(string text) {
        CivilizationModule.QueueNotification(text);
    }

    private static ExplorationMark CloneMark(ExplorationMark mark) => new() {
        Id = mark.Id,
        PlanetId = mark.PlanetId,
        LocalPosition = mark.LocalPosition,
        Kind = mark.Kind,
        ContentId = mark.ContentId,
        ItemId = mark.ItemId,
        Count = mark.Count,
    };
}
