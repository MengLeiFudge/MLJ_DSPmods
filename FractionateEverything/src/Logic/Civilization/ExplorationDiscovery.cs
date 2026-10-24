using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using UnityEngine;

namespace FE.Logic.Civilization;

/// <summary>探索的永久发现账本、活动范围及连续强度投影，状态归主机所有。</summary>
public static partial class ExplorationManager {
    private static readonly Dictionary<int, ExplorationMark> DiscoveredMarks = [];
    private static readonly HashSet<int> VisitedPlanets = [];

    /// <summary>从主机掌握的位置登记活动范围及邻近信号；重复调用不会改变已有坐标。</summary>
    /// <param name="position">真实宇宙位置。</param>
    /// <param name="planetId">玩家当前星球。</param>
    public static void DiscoverNearby(VectorLF3 position, int planetId) {
        if (NebulaMultiplayerModAPI.IsClient || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) { return; }
        PlanetData planet = GameMain.galaxy?.PlanetById(planetId);
        if (planet == null || Math.Abs((position - planet.uPosition).magnitude - planet.realRadius) > 70d) { return; }
        lock (Sync) {
            bool changed = VisitedPlanets.Add(planetId);
            foreach (ExplorationMark mark in NearbyMarks(planetId)) {
                if (IsKeySignal(mark) || DiscoveredMarks.ContainsKey(mark.Id)
                    || GetSignalDistance(mark, planet, position) > DetectionRange) { continue; }
                DiscoveredMarks.Add(mark.Id, CloneMark(mark));
                changed = true;
            }
            if (changed) { MarkDirty(); }
        }
    }

    /// <summary>取得当前可见的邻近目标；首遗物优先，其余只在局部感知范围内显示。</summary>
    /// <param name="preferredId">玩家主动标注的地点，可为零。</param>
    public static ExplorationMark GetNearbySignal(int preferredId) {
        Player player = GameMain.mainPlayer;
        if (player == null || player.planetId <= 0) { return null; }
        lock (Sync) {
            ExplorationMark best = null;
            float strongest = -1f;
            foreach (ExplorationMark mark in NearbyMarks(player.planetId)) {
                if (!TryGetSignalStrength(mark, player.uPosition, player.planetId, out float strength)) { continue; }
                if (mark.Kind == ExplorationMarkKind.FirstRelic || mark.Id == preferredId) { return CloneMark(mark); }
                if (strength > strongest) { best = mark; strongest = strength; }
            }
            return best == null ? null : CloneMark(best);
        }
    }

    /// <summary>首遗物覆盖所在星球的低空，其余信号使用局部空间距离；强度不是回收判据。</summary>
    /// <param name="mark">待测量地点。</param>
    /// <param name="position">真实宇宙位置。</param>
    /// <param name="planetId">玩家星球。</param>
    /// <param name="strength">连续强度，范围0～1。</param>
    public static bool TryGetSignalStrength(ExplorationMark mark, VectorLF3 position, int planetId, out float strength) {
        strength = 0f;
        PlanetData planet = GameMain.galaxy?.PlanetById(mark?.PlanetId ?? 0);
        if (planet == null || planet.id != planetId
            || Math.Abs((position - planet.uPosition).magnitude - planet.realRadius) > 70d) { return false; }
        double distance = GetSignalDistance(mark, planet, position);
        float range = mark.Kind == ExplorationMarkKind.FirstRelic ? 2f * MarkRadius(planet) + 70f : DetectionRange;
        if (distance > range) { return false; }
        strength = Mathf.Clamp01((range - (float)distance) / (range - GetArrivalDistance(mark)));
        return true;
    }

    /// <summary>活动范围从真实到访和已建工厂恢复，不用矩阵科技替玩家决定迁居。</summary>
    private static PlanetData SelectActivePlanet(int excludedPlanet = 0, bool preferNeighbor = false) {
        GalaxyData galaxy = GameMain.galaxy;
        PlanetData birth = galaxy.PlanetById(galaxy.birthPlanetId);
        PlanetData[] active = galaxy.stars.SelectMany(s => s.planets)
            .Where(p => p.type != EPlanetType.Gas && (p.id == birth.id || VisitedPlanets.Contains(p.id) || p.factory != null))
            .ToArray();
        int[] activeStars = active.Select(p => p.star.id).Distinct().ToArray();
        // 出生星系已有跨星球活动后，该星系其余岩石星成为主探索区；外星系仅投向已活动的星球。
        IEnumerable<PlanetData> candidates = active.Where(p => p.id != excludedPlanet);
        if (active.Any(p => p.star.id == birth.star.id && p.id != birth.id)) {
            candidates = candidates.Concat(birth.star.planets.Where(p => p.type != EPlanetType.Gas && p.id != excludedPlanet));
        }
        PlanetData[] pool = candidates.Distinct().ToArray();
        PlanetData[] other = pool.Where(p => p.star.id != birth.star.id && activeStars.Contains(p.star.id)).ToArray();
        if (preferNeighbor && other.Length > 0) { pool = other; }
        else if (pool.Any(p => p.star.id == birth.star.id && p.id != birth.id)) {
            pool = pool.Where(p => p.star.id == birth.star.id && p.id != birth.id).ToArray();
        }
        return pool.Length > 0 ? pool[NextInt(pool.Length)] : birth;
    }

    /// <summary>读取已发现地点与实际活动星球。</summary>
    private static void ImportDiscoveries(BinaryReader reader) {
        int count = ReadCount(reader);
        for (int i = 0; i < count; i++) {
            ExplorationMark mark = ReadLegacyMark(reader);
            if (mark.Id == 0 || mark.PlanetId <= 0 || DiscoveredMarks.ContainsKey(mark.Id)) {
                throw new InvalidDataException("探索发现账本身份无效。");
            }
            DiscoveredMarks.Add(mark.Id, mark);
        }
        count = ReadCount(reader);
        for (int i = 0; i < count; i++) {
            int planetId = reader.ReadInt32();
            if (planetId <= 0 || !VisitedPlanets.Add(planetId)) { throw new InvalidDataException("探索活动星球无效。"); }
        }
    }
}
