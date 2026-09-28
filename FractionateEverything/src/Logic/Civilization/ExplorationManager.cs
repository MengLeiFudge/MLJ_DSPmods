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

    /// <summary>资格物品或资源物品 ID。</summary>
    public int ItemId;

    /// <summary>资源数量。</summary>
    public int Count;
}

/// <summary>
/// FE 3.0 探索信号生成、导航、星球额度和抵达结算。
/// </summary>
public static class ExplorationManager {
    private const long TicksPerMinute = 3600L;
    private const float ArrivalDistance = 120f;
    private static readonly object Sync = new();
    private static readonly List<ExplorationMark> ActiveMarks = [];
    private static readonly Dictionary<int, int> GeneratedCountByPlanet = [];
    private static readonly int[] ResourceCandidates = [
        I铁矿, I铜矿, I硅石, I钛石, I石矿, I煤矿, I原油, I水, I氢, I重氢,
        IFE通用原胚,
    ];
    private static int nextMarkId = 1;
    private static long nextTimedGenerationTick;
    private static uint randomSeed = 0x8F3A51C7u;
    private static bool firstRelicClaimed;
    private static bool snapshotDirty;
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
        lock (Sync) {
            if (firstRelicClaimed || ActiveMarks.Any(mark => mark.Kind == ExplorationMarkKind.FirstRelic)) {
                return;
            }
            PlanetData birthPlanet = GameMain.galaxy.PlanetById(GameMain.galaxy.birthPlanetId);
            if (birthPlanet == null) {
                return;
            }
            ActiveMarks.Add(new ExplorationMark {
                Id = nextMarkId++,
                PlanetId = birthPlanet.id,
                LocalPosition = CreateSurfacePosition(birthPlanet),
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
    /// 主机依据连接对应玩家的权威位置结算探索点位。
    /// </summary>
    public static bool TryClaimFromClient(int markId, int planetId, float x, float y, float z) {
        if (NebulaMultiplayerModAPI.IsClient || !IsFinite(x) || !IsFinite(y) || !IsFinite(z)) {
            return false;
        }
        lock (Sync) {
            int index = ActiveMarks.FindIndex(mark => mark.Id == markId);
            if (index < 0) {
                return false;
            }
            ExplorationMark mark = ActiveMarks[index];
            if (mark.PlanetId != planetId
                || Vector3.Distance(new Vector3(x, y, z), mark.LocalPosition) > ArrivalDistance) {
                return false;
            }
            ActiveMarks.RemoveAt(index);
            ClaimMark(mark);
            snapshotDirty = true;
            return true;
        }
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
        lock (Sync) {
            r.ReadBlocks(
                ("Header", br => {
                    nextMarkId = Math.Max(1, br.ReadInt32());
                    nextTimedGenerationTick = br.ReadInt64();
                    randomSeed = br.ReadUInt32();
                    firstRelicClaimed = br.ReadBoolean();
                }),
                ("PlanetCounts", br => {
                    GeneratedCountByPlanet.Clear();
                    int count = br.ReadInt32();
                    for (int i = 0; i < count; i++) {
                        GeneratedCountByPlanet[br.ReadInt32()] = br.ReadInt32();
                    }
                }),
                ("Marks", br => {
                    ActiveMarks.Clear();
                    int count = br.ReadInt32();
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
                })
            );
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
        ExplorationMarkKind kind;
        if (eligibility) {
            int[] potentialDirections = ResearchManager.GetPotentialDirections()
                .Where(directionId => !ActiveMarks.Any(mark =>
                    mark.Kind == ExplorationMarkKind.DirectionEligibility && mark.ItemId == directionId))
                .ToArray();
            if (potentialDirections.Length > 0) {
                itemId = potentialDirections[NextInt(potentialDirections.Length)];
                kind = ExplorationMarkKind.DirectionEligibility;
            } else {
                kind = ExplorationMarkKind.ResourceCache;
            }
        } else {
            kind = ExplorationMarkKind.ResourceCache;
        }

        if (kind == ExplorationMarkKind.ResourceCache) {
            itemId = ResourceCandidates[NextInt(ResourceCandidates.Length)];
            ItemProto item = LDB.items.Select(itemId);
            int stackSize = Math.Max(1, item?.StackSize ?? 1);
            long resourceCount = 5L * stackSize * GetDistanceValueMultiplier(planet.id);
            count = resourceCount > int.MaxValue ? int.MaxValue : (int)resourceCount;
        }

        ActiveMarks.Add(new ExplorationMark {
            Id = nextMarkId++,
            PlanetId = planet.id,
            LocalPosition = CreateSurfacePosition(planet),
            Kind = kind,
            ItemId = itemId,
            Count = count,
        });
        GeneratedCountByPlanet[planet.id] = GetGeneratedCount(planet.id) + 1;
        snapshotDirty = true;
        return true;
    }

    private static void ResolveArrivals() {
        Player player = GameMain.mainPlayer;
        if (player.planetId <= 0) {
            return;
        }
        Vector3 playerPosition = player.position;
        for (int i = ActiveMarks.Count - 1; i >= 0; i--) {
            ExplorationMark mark = ActiveMarks[i];
            if (mark.PlanetId != player.planetId
                || Vector3.Distance(playerPosition, mark.LocalPosition) > ArrivalDistance) {
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
                ResearchManager.GrantDirectionEligibility(mark.ItemId);
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

    private static Vector3 CreateSurfacePosition(PlanetData planet) {
        double y = GetRandDouble(ref randomSeed) * 2d - 1d;
        double angle = GetRandDouble(ref randomSeed) * Math.PI * 2d;
        double horizontal = Math.Sqrt(Math.Max(0d, 1d - y * y));
        float radius = planet.realRadius + 1.5f;
        return new Vector3(
            (float)(Math.Cos(angle) * horizontal * radius),
            (float)(y * radius),
            (float)(Math.Sin(angle) * horizontal * radius));
    }

    private static int NextInt(int count) {
        if (count <= 1) {
            return 0;
        }
        return Math.Min(count - 1, (int)(GetRandDouble(ref randomSeed) * count));
    }

    private static void RequestNearbyClientClaims() {
        Player player = GameMain.mainPlayer;
        if (player.planetId <= 0) {
            return;
        }
        Vector3 position = player.position;
        ExplorationMark[] marks = GetActiveMarks();
        foreach (ExplorationMark mark in marks) {
            if (mark.PlanetId == player.planetId
                && Vector3.Distance(position, mark.LocalPosition) <= ArrivalDistance) {
                NebulaMultiplayerModAPI.RequestExplorationClaim(mark.Id);
            }
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
        ItemId = mark.ItemId,
        Count = mark.Count,
    };
}
