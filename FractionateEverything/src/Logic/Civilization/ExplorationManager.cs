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
    PlanetDetector = 4,
}

/// <summary>
/// 一个永久探索信号，抵达前没有时限。
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

    /// <summary>具体研究内容的稳定 ID；仅研究内容信号使用。</summary>
    public string ContentId;

    /// <summary>解析物品或资源物品 ID。</summary>
    public int ItemId;

    /// <summary>资源数量。</summary>
    public int Count;
}

/// <summary>
/// 探索信号生成、双尺度探测、星球额度和抵达结算。
/// </summary>
public static class ExplorationManager {
    private const long TicksPerMinute = 3600L;
    private const float TerrestrialArrivalDistance = 120f;
    private const float PlanetLocalDetectionAltitude = 900f;
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
    private static bool planetDetectorUnlocked;
    private static bool snapshotDirty;
    private static int electromagneticMatrixMilestoneId = int.MinValue;
    private static bool milestoneLookupWarningLogged;
    private static int cachedGalaxySeed = int.MinValue;
    private static int cachedGalaxyStarCount;
    private static double cachedGalaxyMaximumDistance;

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
    /// 判断遗物探测器是否已经由首次电磁矩阵脉冲解锁。
    /// </summary>
    public static bool RelicDetectorUnlocked => FirstRelicClaimed || IsElectromagneticMatrixMilestoneUnlocked();

    /// <summary>
    /// 判断星球探测器是否已经从第二个关键信号恢复。
    /// </summary>
    public static bool PlanetDetectorUnlocked {
        get {
            lock (Sync) {
                return planetDetectorUnlocked;
            }
        }
    }

    /// <summary>
    /// 判断点位是否属于始终固定显示的主线关键信号。
    /// </summary>
    public static bool IsKeySignal(ExplorationMark mark) =>
        mark?.Kind is ExplorationMarkKind.FirstRelic or ExplorationMarkKind.PlanetDetector;

    /// <summary>
    /// 获取由稳定点位 ID 构成的信号名称。
    /// </summary>
    public static string GetSignalName(ExplorationMark mark) {
        if (mark == null) {
            return string.Empty;
        }
        string format = (IsKeySignal(mark) ? "关键信号{0}" : "信号{0}").Translate();
        return string.Format(format, mark.Id);
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
            var mark = new ExplorationMark {
                Id = markId,
                PlanetId = birthPlanet.id,
                LocalPosition = CreateMarkPosition(birthPlanet),
                Kind = ExplorationMarkKind.FirstRelic,
            };
            ActiveMarks.Add(mark);
            snapshotDirty = true;
            QueueTip(string.Format("强烈脉冲：遗物探测器已启用，{0}位于{1}".Translate(),
                GetSignalName(mark), birthPlanet.displayName));
        }
    }

    /// <summary>
    /// 清理由旧版本为首遗物启动的自动导航；不会触碰玩家自行设置的其他目标。
    /// </summary>
    public static void ClearLegacyFirstRelicNavigation() {
        ExplorationMark relic;
        lock (Sync) {
            relic = ActiveMarks.FirstOrDefault(mark => mark.Kind == ExplorationMarkKind.FirstRelic);
            if (relic == null) {
                return;
            }
            relic = CloneMark(relic);
        }

        PlayerNavigation navigation = GameMain.mainPlayer?.navigation;
        if (navigation == null || !navigation.navigating || navigation.naviAstroId != relic.PlanetId) {
            return;
        }
        VectorLF3 target = navigation.naviTarget;
        bool targetsRelic = Math.Abs(target.x - relic.LocalPosition.x) < 0.01d
                            && Math.Abs(target.y - relic.LocalPosition.y) < 0.01d
                            && Math.Abs(target.z - relic.LocalPosition.z) < 0.01d;
        if (!targetsRelic) {
            return;
        }

        navigation.Abort();
        if (navigation.indicatorAstroId == relic.PlanetId) {
            navigation.indicatorAstroId = 0;
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
            if (!planetDetectorUnlocked) {
                return;
            }
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
            EnsurePlanetDetectorMarkUnsafe();
            if (planetDetectorUnlocked) {
                if (nextTimedGenerationTick <= 0L) {
                    nextTimedGenerationTick = gameTick + GetGenerationIntervalTicks();
                }
                if (gameTick >= nextTimedGenerationTick && TryGenerateMark()) {
                    QueueTip("古文明网络发现了新的探索信号".Translate());
                    nextTimedGenerationTick = gameTick + GetGenerationIntervalTicks();
                    snapshotDirty = true;
                }
            } else {
                nextTimedGenerationTick = 0L;
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
    /// 计算当前点位的星球级和遗物级量化强度；两个读数都不包含方向或真实距离。
    /// </summary>
    public static bool TryGetDetectorStrengths(ExplorationMark mark, VectorLF3 playerPosition,
        out float planetStrength, out float relicStrength) {
        planetStrength = 0f;
        relicStrength = 0f;
        if (mark == null || GameMain.galaxy?.PlanetById(mark.PlanetId) is not PlanetData planet) {
            return false;
        }

        Player player = GameMain.mainPlayer;
        bool onTargetPlanet = player?.planetId == mark.PlanetId;
        if (PlanetDetectorUnlocked) {
            double minimumPlanetDistance = planet.realRadius + PlanetLocalDetectionAltitude;
            double maximumPlanetDistance = GetGalaxyMaximumDistance();
            double centerDistance = (playerPosition - planet.uPosition).magnitude;
            float rawPlanetStrength = onTargetPlanet
                ? 1f
                : NormalizeLogStrength(centerDistance, minimumPlanetDistance, maximumPlanetDistance);
            planetStrength = QuantizeStrength(rawPlanetStrength);
        }

        if (onTargetPlanet) {
            float arrivalDistance = GetArrivalDistance(planet);
            double maximumRelicDistance = Math.Max(arrivalDistance + 1d, GetMarkRadius(planet) * 2d);
            double relicDistance = (playerPosition - GetMarkUniversalPosition(mark, planet)).magnitude;
            relicStrength = QuantizeStrength(
                NormalizeLogStrength(relicDistance, arrivalDistance, maximumRelicDistance));
        }
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
        var importedMarkContents = new Dictionary<int, string>();
        lock (Sync) {
            ActiveMarks.Clear();
            GeneratedCountByPlanet.Clear();
            nextMarkId = 1;
            nextTimedGenerationTick = 0L;
            randomSeed = 0x8F3A51C7u;
            firstRelicClaimed = false;
            planetDetectorUnlocked = false;
            ResetGalaxyDistanceCache();
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
                }),
                ("DetectorState", br => planetDetectorUnlocked = br.ReadBoolean())
            );
            SanitizeImportedStateUnsafe(importedMarkContents);
            snapshotDirty = true;
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
                }),
                ("DetectorState", bw => bw.Write(planetDetectorUnlocked))
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
            planetDetectorUnlocked = false;
            snapshotDirty = false;
            ResetGalaxyDistanceCache();
        }
    }

    private static void SanitizeImportedStateUnsafe(Dictionary<int, string> importedMarkContents) {
        GalaxyData galaxy = GameMain.galaxy;
        if (!firstRelicClaimed) {
            planetDetectorUnlocked = false;
        }
        long currentTick = Math.Max(0L, GameMain.gameTick);
        long maximumInterval = GetGenerationIntervalTicks();
        long latestGenerationTick = currentTick > long.MaxValue - maximumInterval
            ? long.MaxValue
            : currentTick + maximumInterval;
        nextTimedGenerationTick = planetDetectorUnlocked
            ? Math.Min(nextTimedGenerationTick, latestGenerationTick)
            : 0L;
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
        bool hasPlanetDetectorMark = false;
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
                case ExplorationMarkKind.PlanetDetector:
                    if (!firstRelicClaimed || planetDetectorUnlocked || hasPlanetDetectorMark) {
                        ActiveMarks.RemoveAt(index--);
                        continue;
                    }
                    hasPlanetDetectorMark = true;
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

    private static void EnsurePlanetDetectorMarkUnsafe() {
        if (!firstRelicClaimed || planetDetectorUnlocked
            || (GameMain.mainPlayer?.mecha?.thrusterLevel ?? 0) < 3
            || ActiveMarks.Any(mark => mark.Kind == ExplorationMarkKind.PlanetDetector)) {
            return;
        }

        GalaxyData galaxy = GameMain.galaxy;
        StarData birthStar = galaxy?.StarById(galaxy.birthStarId);
        PlanetData target = birthStar?.planets?.Where(planet =>
                planet != null && planet.id != galaxy.birthPlanetId)
            .OrderBy(planet => planet.type == EPlanetType.Gas ? 1 : 0)
            .ThenByDescending(planet => planet.sunDistance)
            .FirstOrDefault()
            ?? galaxy?.PlanetById(galaxy.birthPlanetId);
        if (target == null || !TryTakeNextMarkId(out int markId)) {
            return;
        }

        var mark = new ExplorationMark {
            Id = markId,
            PlanetId = target.id,
            LocalPosition = CreateMarkPosition(target),
            Kind = ExplorationMarkKind.PlanetDetector,
        };
        ActiveMarks.Add(mark);
        snapshotDirty = true;
        QueueTip(string.Format("检测到{0}：{1}".Translate(),
            GetSignalName(mark), target.displayName));
    }

    private static bool TryGenerateMark() {
        List<PlanetData> candidates = GetOrdinarySignalCandidatePlanets()
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
                CivilizationRecoveryManager.CompleteFirstRelicRecovery();
                QueueTip("已接入古文明亚空间网络".Translate());
                break;
            case ExplorationMarkKind.PlanetDetector:
                planetDetectorUnlocked = true;
                nextTimedGenerationTick = GameMain.gameTick + GetGenerationIntervalTicks();
                QueueTip("星球探测器已恢复".Translate());
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

    private static List<PlanetData> GetOrdinarySignalCandidatePlanets() {
        List<PlanetData> planets = [];
        foreach (StarData star in GameMain.galaxy.stars) {
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

    private static double GetEligibilityProbability() => 0.30d;

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

    private static double GetGalaxyMaximumDistance() {
        GalaxyData galaxy = GameMain.galaxy;
        if (galaxy?.stars == null || galaxy.stars.Length == 0) {
            return 1d;
        }
        if (cachedGalaxySeed == galaxy.seed && cachedGalaxyStarCount == galaxy.starCount
            && cachedGalaxyMaximumDistance > 0d) {
            return cachedGalaxyMaximumDistance;
        }

        double maximumDistance = 1d;
        for (int i = 0; i < galaxy.starCount; i++) {
            StarData left = galaxy.stars[i];
            if (left == null) {
                continue;
            }
            double leftSystemRadius = left.systemRadius * GalaxyData.AU;
            maximumDistance = Math.Max(maximumDistance, leftSystemRadius * 2d);
            for (int j = i + 1; j < galaxy.starCount; j++) {
                StarData right = galaxy.stars[j];
                if (right == null) {
                    continue;
                }
                double systemMargins = leftSystemRadius + right.systemRadius * GalaxyData.AU;
                maximumDistance = Math.Max(maximumDistance,
                    (left.uPosition - right.uPosition).magnitude + systemMargins);
            }
        }

        cachedGalaxySeed = galaxy.seed;
        cachedGalaxyStarCount = galaxy.starCount;
        cachedGalaxyMaximumDistance = maximumDistance;
        return maximumDistance;
    }

    private static float NormalizeLogStrength(double distance, double minimumDistance,
        double maximumDistance) {
        if (distance <= minimumDistance) {
            return 1f;
        }
        if (distance >= maximumDistance || maximumDistance <= minimumDistance) {
            return 0f;
        }
        double range = Math.Log(maximumDistance / minimumDistance);
        return Mathf.Clamp01((float)(Math.Log(maximumDistance / distance) / range));
    }

    private static float QuantizeStrength(float strength) {
        strength = Mathf.Clamp01(strength);
        if (strength >= 1f) {
            return 1f;
        }
        float step = GrowthTechnologyManager.ExplorationSignalPrecisionPercent / 100f;
        return Mathf.Floor(strength / step) * step;
    }

    private static void ResetGalaxyDistanceCache() {
        cachedGalaxySeed = int.MinValue;
        cachedGalaxyStarCount = 0;
        cachedGalaxyMaximumDistance = 0d;
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
