using System;
using UnityEngine;

namespace FE.Logic.Civilization;

/// <summary>玩家主动选择的探测量程；跨量程不重新选择信号。</summary>
public enum DetectorRange {
    StarSystem,
    Planet,
    Region,
    Fine,
}

/// <summary>三行仪表的只读文本投影，坐标和目标均由同一探测会话生成。</summary>
public readonly struct DetectorReading(string heading, string measurement, string position) {
    /// <summary>模式、量程及当前信号名称。</summary>
    public readonly string Heading = heading;
    /// <summary>当前量程允许公开的测量结果。</summary>
    public readonly string Measurement = measurement;
    /// <summary>区域或精细量程的玩家自身坐标，其他量程为空。</summary>
    public readonly string Position = position;
}

/// <summary>本地探测会话与四挡测量投影，不拥有世界点位、回收或联机结算。</summary>
public static class ExplorationDetector {
    private static readonly int[] TargetIds = new int[2];
    private static GalaxyData galaxy;
    private static bool exploration;
    private static DetectorRange range;

    /// <summary>循环切换量程，保留两种模式各自的跟踪目标。</summary>
    public static void CycleRange() {
        EnsureSession();
        range = (DetectorRange)(((int)range + 1) % 4);
    }

    /// <summary>切换测量对象；仿制探索模块在首遗物恢复后开放。</summary>
    public static void CycleMode() {
        EnsureSession();
        if (ExplorationManager.FirstRelicClaimed) { exploration = !exploration; }
    }

    /// <summary>真实切档时清空本地选择，普通世界快照更新不调用此入口。</summary>
    public static void Reset() {
        galaxy = null;
        exploration = false;
        range = DetectorRange.StarSystem;
        Array.Clear(TargetIds, 0, TargetIds.Length);
    }

    /// <summary>读取当前量程；只有目标消失或失去探测资格时才自动续接。</summary>
    /// <returns>可直接呈现的三行仪表文本，不包含内部点位身份。</returns>
    public static DetectorReading Read() {
        EnsureSession();
        Player player = GameMain.mainPlayer;
        if (!ExplorationManager.FirstRelicClaimed) { exploration = false; }
        int slot = exploration ? 1 : 0;
        ExplorationMark target = ExplorationManager.GetDetectorTarget(!exploration, TargetIds[slot]);
        TargetIds[slot] = target?.Id ?? 0;
        string mode = (exploration ? "FE.Detector.Exploration" : "FE.Detector.Civilization").Translate();
        string rangeName = (range switch {
            DetectorRange.StarSystem => "FE.Detector.StarRange",
            DetectorRange.Planet => "FE.Detector.PlanetRange",
            DetectorRange.Region => "FE.Detector.RegionRange",
            _ => "FE.Detector.FineRange",
        }).Translate();
        string heading = $"{mode} · {rangeName}";
        if (target != null) { heading += $" · {ExplorationManager.GetSignalName(target)}"; }
        string position = string.Empty;
        if (range is DetectorRange.Region or DetectorRange.Fine) {
            PlanetData local = GameMain.galaxy?.PlanetById(player?.planetId ?? 0);
            if (local != null) {
                Vector3 direction = (Vector3)Maths.QRotateLF(Quaternion.Inverse(local.runtimeRotation),
                    player.uPosition - local.uPosition);
                GetCoordinates(direction, out double latitude, out double longitude);
                position = string.Format("FE.Detector.Position".Translate(), latitude, longitude);
            }
        }
        PlanetData planet = GameMain.galaxy?.PlanetById(target?.PlanetId ?? 0);
        if (planet == null || player == null) {
            return new(heading, "FE.Detector.NoSignal".Translate(), position);
        }
        string measurement = "FE.Detector.Unresolved".Translate();
        switch (range) {
            case DetectorRange.StarSystem:
                measurement = string.Format("FE.Detector.StarResult".Translate(), planet.star.displayName);
                break;
            case DetectorRange.Planet:
                if (planet.star == GameMain.localStar) {
                    measurement = string.Format("FE.Detector.PlanetResult".Translate(), planet.displayName);
                }
                break;
            case DetectorRange.Region:
                if (player.planetId == planet.id) { measurement = GetRegion(target, planet); }
                break;
            case DetectorRange.Fine:
                if (ExplorationManager.TryGetDetectorStrengths(target, player.uPosition, out _, out float strength)) {
                    string state = ExplorationManager.IsDetectorTargetReached(target)
                        ? "FE.Detector.Located" : "FE.Detector.Receiving";
                    measurement = string.Format("FE.Detector.Strength".Translate(), strength * 100f, state.Translate());
                }
                break;
        }
        return new(heading, measurement, position);
    }

    /// <summary>新银河实例清本地状态，防止未经过窗口切档入口时复用旧点位身份。</summary>
    private static void EnsureSession() {
        if (ReferenceEquals(galaxy, GameMain.galaxy)) { return; }
        Reset();
        galaxy = GameMain.galaxy;
    }

    /// <summary>原版地表经纬方向：北纬为正，经度由atan2(x,-z)得到，规范为[-180,180)。</summary>
    /// <param name="position">随星球自转的局部坐标或方向，不使用宇宙坐标。</param>
    /// <param name="latitude">纬度，单位度。</param>
    /// <param name="longitude">经度，单位度；极点取0，仅作当前位置的表示。</param>
    private static void GetCoordinates(Vector3 position, out double latitude, out double longitude) {
        Vector3 direction = position.normalized;
        latitude = Math.Asin(Math.Max(-1d, Math.Min(1d, direction.y))) * 180d / Math.PI;
        longitude = direction.x * direction.x + direction.z * direction.z < 1e-12f
            ? 0d : Math.Atan2(direction.x, -direction.z) * 180d / Math.PI;
        if (longitude >= 180d) { longitude -= 360d; }
    }

    /// <summary>按真实半径划固定网格；目标决定所在纬带，玩家移动不会改变区间。</summary>
    /// <param name="target">当前目标的局部位置。</param>
    /// <param name="planet">目标星球，使用其真实半径兼容大型星球。</param>
    /// <returns>向外保留三位小数的区间，极区不限制经度。</returns>
    private static string GetRegion(ExplorationMark target, PlanetData planet) {
        double cellSize = !exploration ? 60d : GrowthTechnologyManager.ExplorationLevel switch {
            <= 0 => 180d, 1 => 140d, 2 => 100d, 3 => 80d, 4 => 60d, _ => 40d,
        };
        double radius = planet.realRadius;
        if (radius <= 0d || double.IsNaN(radius) || double.IsInfinity(radius)) {
            return "FE.Detector.Unresolved".Translate();
        }
        GetCoordinates(target.LocalPosition, out double latitude, out double longitude);
        double rows = Math.Max(1d, Math.Ceiling(Math.PI * radius / cellSize));
        double height = 180d / rows;
        double row = Math.Min(rows - 1d, Math.Floor((latitude + 90d) / height));
        double minLatitude = -90d + row * height;
        double maxLatitude = Math.Min(90d, minLatitude + height);
        double lower = Math.Floor(minLatitude * 1000d) / 1000d;
        double upper = Math.Ceiling(maxLatitude * 1000d) / 1000d;
        if (row == 0d || row == rows - 1d) {
            return string.Format("FE.Detector.PolarRegion".Translate(), lower, upper);
        }
        double centerLatitude = (minLatitude + maxLatitude) * Math.PI / 360d;
        double columns = Math.Max(1d, Math.Ceiling(2d * Math.PI * radius * Math.Cos(centerLatitude) / cellSize));
        double width = 360d / columns;
        double column = Math.Min(columns - 1d, Math.Floor((longitude + 180d) / width));
        double minLongitude = -180d + column * width;
        double maxLongitude = Math.Min(180d, minLongitude + width);
        return string.Format("FE.Detector.Region".Translate(), lower, upper,
            Math.Floor(minLongitude * 1000d) / 1000d, Math.Ceiling(maxLongitude * 1000d) / 1000d);
    }
}
