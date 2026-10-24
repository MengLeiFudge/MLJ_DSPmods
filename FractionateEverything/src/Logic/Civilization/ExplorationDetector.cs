using System.Linq;
using UnityEngine;

namespace FE.Logic.Civilization;

/// <summary>信号HUD的只读投影；首遗物不公开地点身份或精确指引。</summary>
public readonly struct DetectorReading(bool visible, float strength, string name, int markId = 0, bool showStrength = true) {
    /// <summary>是否显示信号HUD，包含感知范围外的已选目标。</summary>
    public readonly bool Visible = visible;
    /// <summary>连续信号强度或挑战进度，不参与认领判定。</summary>
    public readonly float Strength = strength;
    /// <summary>首遗物为空，其他地点使用已公开的名称或挑战状态。</summary>
    public readonly string Name = name;
    /// <summary>可操作的已发现地点，0表示没有可选地点。</summary>
    public readonly int MarkId = markId;
    /// <summary>超出局部感知范围时保留取消入口，但不显示虚假的强度。</summary>
    public readonly bool ShowStrength = showStrength;
}

/// <summary>拥有本端连续指引的开启态与目标，只投影位置，不创建移动命令。</summary>
public static class ExplorationDetector {
    private static GalaxyData galaxy;
    // 0表示暂时没有目标；静默遗迹使用负数编号，剧情与动态广播使用正数编号。
    private static int targetId;
    private static bool trackingEnabled;

    public static bool TrackingEnabled {
        get { EnsureSession(); return trackingEnabled; }
    }

    /// <summary>切档丢弃旧世界引用与本端指引。</summary>
    public static void Reset() {
        galaxy = null;
        targetId = 0;
        trackingEnabled = false;
    }

    /// <summary>查询地点是否为本端当前选择，面板和HUD共用同一状态。</summary>
    public static bool IsTracking(int markId) {
        EnsureSession();
        return trackingEnabled && markId != 0 && targetId == markId;
    }

    /// <summary>同地点或等待状态下点击关闭；其他已发现地点替换当前目标。</summary>
    public static void ToggleTracking(int markId) {
        EnsureSession();
        if (trackingEnabled && (markId == 0 || targetId == markId)) {
            trackingEnabled = false;
            targetId = 0;
            return;
        }
        ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Id == markId);
        if (mark == null || mark.Kind == ExplorationMarkKind.FirstRelic) { return; }
        targetId = mark.Id;
        trackingEnabled = true;
    }

    /// <summary>当前地点回收后续接已发现目标，无候选时保持开启并等待。</summary>
    public static void UpdateNavigation() {
        EnsureSession();
        Player player = GameMain.mainPlayer;
        if (!trackingEnabled || player == null) { return; }
        ExplorationMark[] known = ExplorationManager.GetActiveMarks();
        if (known.Any(mark => mark.Id == targetId)) { return; }
        ExplorationMark next = known.Where(mark => mark.Kind != ExplorationMarkKind.FirstRelic
                && GameMain.galaxy.PlanetById(mark.PlanetId) != null)
            .OrderBy(mark => mark.PlanetId == player.planetId ? 0 : 1)
            .ThenBy(mark => (GetWorldPosition(mark) - player.uPosition).sqrMagnitude)
            .ThenBy(mark => mark.Id).FirstOrDefault();
        targetId = next?.Id ?? 0;
    }

    /// <summary>同星球指向地点或下一航迹节点，跨星球先指向目标星球。</summary>
    public static bool TryGetGuidePosition(out VectorLF3 position) {
        EnsureSession();
        position = default;
        if (!trackingEnabled || targetId == 0 || GameMain.mainPlayer == null) { return false; }
        ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Id == targetId);
        PlanetData planet = GameMain.galaxy?.PlanetById(mark?.PlanetId ?? 0);
        if (planet == null) { return false; }
        position = planet.uPosition;
        if (GameMain.mainPlayer.planetId == mark.PlanetId) { position = GetWorldPosition(mark); }
        return true;
    }

    private static VectorLF3 GetWorldPosition(ExplorationMark mark) {
        PlanetData planet = GameMain.galaxy.PlanetById(mark.PlanetId);
        SignalChallengeView challenge = ExplorationManager.GetChallenge(mark.Id);
        Vector3 point = challenge?.Kind == SignalChallengeKind.Route
            ? challenge.Route[Mathf.Clamp(challenge.Started ? challenge.Progress + 1 : 0, 0, challenge.Route.Length - 1)]
            : mark.LocalPosition;
        return planet.uPosition + Maths.QRotateLF(planet.runtimeRotation, point);
    }

    /// <summary>已选目标优先；等待新目标时仍提供关闭入口，未开启时展示附近信号。</summary>
    public static DetectorReading Read() {
        EnsureSession();
        Player player = GameMain.mainPlayer;
        if (player == null) { return default; }
        ExplorationMark[] known = ExplorationManager.GetActiveMarks();
        ExplorationMark target = known.FirstOrDefault(m => m.Id == targetId);
        if (target == null && trackingEnabled) { return new(true, 0f, "等待新信号".Translate(), 0, false); }
        if (target == null) {
            target = known.FirstOrDefault(m => m.PlanetId == player.planetId
                && ExplorationManager.GetChallenge(m.Id) is { Started: true } c
                && (c.Kind == SignalChallengeKind.Route || ((Vector3)player.position - m.LocalPosition).magnitude <= 180f))
                ?? ExplorationManager.GetNearbySignal(0);
        }
        if (target == null) { return default; }
        int actionId = target.Kind != ExplorationMarkKind.FirstRelic && known.Any(m => m.Id == target.Id) ? target.Id : 0;
        SignalChallengeView challenge = ExplorationManager.GetChallenge(target.Id);
        if (target.PlanetId == player.planetId && challenge is { Started: true }) {
            float progress = challenge.Progress / (float)challenge.Required;
            if (challenge.Kind == SignalChallengeKind.Production) {
                progress = Mathf.Min(progress, challenge.Machines / (float)challenge.RequiredMachines);
            }
            return new(true, progress, string.Format("FE.Challenge.Status".Translate(), challenge.Progress, challenge.Required), actionId);
        }
        bool sensed = ExplorationManager.TryGetSignalStrength(target, player.uPosition, player.planetId, out float strength);
        return new(sensed || target.Id == targetId, strength,
            target.Kind == ExplorationMarkKind.FirstRelic ? string.Empty : ExplorationManager.GetSignalName(target), actionId, sensed);
    }

    /// <summary>跨世界丢弃旧选择；普通主机快照保持本地选择。</summary>
    private static void EnsureSession() {
        if (ReferenceEquals(galaxy, GameMain.galaxy)) { return; }
        Reset();
        galaxy = GameMain.galaxy;
    }
}
