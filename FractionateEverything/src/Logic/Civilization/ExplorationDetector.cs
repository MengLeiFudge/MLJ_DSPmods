using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FE.Logic.Civilization;

/// <summary>地表信号条的本地投影；不包含坐标、百分比或需要玩家切换的量程。</summary>
public readonly struct DetectorReading(bool visible, float strength, string name) {
    /// <summary>当前位置是否存在可感知信号。</summary>
    public readonly bool Visible = visible;
    /// <summary>连续信号强度，不参与认领判定。</summary>
    public readonly float Strength = strength;
    /// <summary>首遗物不显示文字，其他地点使用已发现名称。</summary>
    public readonly string Name = name;
}

/// <summary>读取邻近信号并响应玩家的原版导航操作，不创建移动命令。</summary>
public static class ExplorationDetector {
    // 原版非公开接口通过既有反射路径访问，兼容不同的publicizer可见性。
    private static readonly MethodInfo TrackMethod = AccessTools.Method(typeof(UIGlobemap), "SetLocationTracking",
        [typeof(int), typeof(Vector3), typeof(int)]);
    private static GalaxyData galaxy;
    private static int targetId;
    private static Vector3 lastRouteTarget;

    /// <summary>真实切档清空本地目标；普通联机快照不重置选择。</summary>
    public static void Reset() {
        galaxy = null;
        targetId = 0;
        lastRouteTarget = default;
    }

    /// <summary>导航只接受当前公开的地点；首遗物始终保留模糊感应。</summary>
    /// <param name="mark">页面正在展示的地点副本。</param>
    public static void Track(ExplorationMark mark) {
        if (mark == null || mark.Kind == ExplorationMarkKind.FirstRelic) { return; }
        ExplorationMark known = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Id == mark.Id);
        Player player = GameMain.mainPlayer;
        if (known == null || player?.navigation == null) { return; }
        EnsureSession();
        targetId = known.Id;
        player.navigation.indicatorAstroId = known.PlanetId;
        SignalChallengeView challenge = ExplorationManager.GetChallenge(known.Id);
        Vector3 point = challenge?.Kind == SignalChallengeKind.Route
            ? challenge.Route[challenge.Started ? challenge.Progress + 1 : 0] : known.LocalPosition;
        lastRouteTarget = point;
        TrackMethod?.Invoke(UIRoot.instance?.uiGame?.globemap, [known.PlanetId, point, 519]);
    }

    /// <summary>自动呈现本地最近信号；已选择的目标在感知范围内优先。</summary>
    public static DetectorReading Read() {
        EnsureSession();
        Player player = GameMain.mainPlayer;
        foreach (ExplorationMark mark in ExplorationManager.GetActiveMarks().Where(m => m.PlanetId == player?.planetId)) {
            SignalChallengeView challenge = ExplorationManager.GetChallenge(mark.Id);
            if (challenge == null) { continue; }
            if (!challenge.Started) {
                if (challenge.Kind == SignalChallengeKind.Route && targetId == mark.Id && lastRouteTarget != challenge.Route[0]) {
                    lastRouteTarget = challenge.Route[0];
                    TrackMethod?.Invoke(UIRoot.instance?.uiGame?.globemap, [mark.PlanetId, lastRouteTarget, 519]);
                }
                continue;
            }
            if (challenge.Kind == SignalChallengeKind.Route) {
                Vector3 next = challenge.Route[challenge.Progress + 1];
                if (targetId == mark.Id && lastRouteTarget != next) {
                    lastRouteTarget = next;
                    TrackMethod?.Invoke(UIRoot.instance?.uiGame?.globemap, [mark.PlanetId, next, 519]);
                }
            } else if (((Vector3)player.position - mark.LocalPosition).magnitude > 180f) { continue; }
            float progress = challenge.Progress / (float)challenge.Required;
            if (challenge.Kind == SignalChallengeKind.Production) {
                progress = Mathf.Min(progress, challenge.Machines / (float)challenge.RequiredMachines);
            }
            return new(true, progress,
                string.Format("FE.Challenge.Status".Translate(), challenge.Progress, challenge.Required));
        }
        ExplorationMark target = ExplorationManager.GetNearbySignal(targetId);
        if (target == null || player == null
            || !ExplorationManager.TryGetSignalStrength(target, player.uPosition, player.planetId, out float strength)) {
            return new(false, 0f, string.Empty);
        }
        return new(true, strength, target.Kind == ExplorationMarkKind.FirstRelic
            ? string.Empty : ExplorationManager.GetSignalName(target));
    }

    /// <summary>跨世界时丢弃旧点位选择，普通读取保持选择。</summary>
    private static void EnsureSession() {
        if (ReferenceEquals(galaxy, GameMain.galaxy)) { return; }
        Reset();
        galaxy = GameMain.galaxy;
    }
}
