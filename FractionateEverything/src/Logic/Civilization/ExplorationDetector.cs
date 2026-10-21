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
    /// <summary>可操作的已发现地点，0表示不提供指引按钮。</summary>
    public readonly int MarkId = markId;
    /// <summary>超出局部感知范围时保留取消入口，但不显示虚假的强度。</summary>
    public readonly bool ShowStrength = showStrength;
}

/// <summary>拥有本端选定地点，在天体指引和地表位置追踪之间切换，不创建移动命令。</summary>
public static class ExplorationDetector {
    private static GalaxyData galaxy;
    // 0表示未选择；静默遗迹使用负数编号，剧情与动态广播使用正数编号。
    private static int targetId;
    private static int indicatorPlanetId;
    private static int locationPlanetId;
    private static int warningId;
    private static Vector3 lastRouteTarget;

    /// <summary>切档丢弃旧世界引用，不操作新世界的原版导航。</summary>
    public static void Reset() {
        galaxy = null;
        targetId = indicatorPlanetId = locationPlanetId = warningId = 0;
        lastRouteTarget = default;
    }

    /// <summary>查询地点是否为本端当前选择，面板和HUD共用同一状态。</summary>
    /// <param name="markId">地点身份。</param>
    /// <returns>该地点正在接受指引时为true。</returns>
    public static bool IsTracking(int markId) {
        EnsureSession();
        return markId != 0 && targetId == markId;
    }

    /// <summary>同地点取消，其他已发现地点替换当前指引；首遗物不可标注。</summary>
    /// <param name="markId">页面或HUD当前显示的地点身份。</param>
    public static void ToggleTracking(int markId) {
        EnsureSession();
        if (markId == 0) { return; }
        if (targetId == markId) { CancelTracking(); return; }
        ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Id == markId);
        if (mark == null || mark.Kind == ExplorationMarkKind.FirstRelic || GameMain.mainPlayer?.navigation == null
            || GameMain.data?.warningSystem == null) { return; }
        CancelTracking();
        // 显式选择新地点取代原版指引；自动刷新和取消只处理自己仍持有的连线。
        GameMain.mainPlayer.navigation.indicatorAstroId = 0;
        GameMain.data.warningSystem.RemoveAllLocationTracking();
        targetId = mark.Id;
        UpdateNavigation();
    }

    /// <summary>与HUD显隐独立刷新，进出目标星球切换连线，回收后清理选择。</summary>
    public static void UpdateNavigation() {
        EnsureSession();
        Player player = GameMain.mainPlayer;
        if (targetId == 0 || player?.navigation == null || GameMain.data?.warningSystem == null) { return; }
        ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Id == targetId);
        if (mark == null) { CancelTracking(); return; }
        if (player.planetId != mark.PlanetId) {
            if (locationPlanetId != 0) { ClearNavigation(); }
            if (indicatorPlanetId == 0) {
                indicatorPlanetId = mark.PlanetId;
                player.navigation.indicatorAstroId = indicatorPlanetId;
            } else if (player.navigation.indicatorAstroId != indicatorPlanetId) {
                // 玩家从原版入口取消或选择了其他天体，不抢回导航。
                CancelTracking();
            }
            return;
        }

        SignalChallengeView challenge = ExplorationManager.GetChallenge(mark.Id);
        Vector3 point = challenge?.Kind == SignalChallengeKind.Route
            ? challenge.Route[Mathf.Clamp(challenge.Started ? challenge.Progress + 1 : 0, 0, challenge.Route.Length - 1)]
            : mark.LocalPosition;
        if (locationPlanetId == mark.PlanetId) {
            bool replaced = player.navigation.indicatorAstroId > 0
                || !HasLocationTracking() && GameMain.data.warningSystem.warningPool
                    .Any(w => w.id > 0 && w.factoryId == -6 && w.signalId == 519);
            if (replaced || !HasLocationTracking() && !IsAtLocation(player)) {
                // 原版右键取消或其他标注替换了地表线，同步取消FE选择。
                CancelTracking();
                return;
            }
            if (point == lastRouteTarget) { return; }
        }
        ClearNavigation();
        // 同星球不保留旧版FE或原版指向脚下星球的天体线。
        if (player.navigation.indicatorAstroId == mark.PlanetId) { player.navigation.indicatorAstroId = 0; }
        WarningSystem warnings = GameMain.data.warningSystem;
        // 沿用原版SetLocationTracking的标记结构，并保存本次标记身份以便定向清理。
        warnings.RemoveAllLocationTracking();
        ref WarningData warning = ref warnings.NewWarningData(-6, 0, 519);
        warning.state = 1;
        warning.astroId = locationPlanetId = mark.PlanetId;
        warning.localPos = lastRouteTarget = point;
        warning.detailId1 = 519;
        warningId = warning.id;
    }

    /// <summary>确认池中仍是自己创建的标记，避免删除已回收编号上的其他标记。</summary>
    /// <returns>身份、目标星球与坐标均匹配时为true。</returns>
    private static bool HasLocationTracking() {
        WarningSystem warnings = GameMain.data?.warningSystem;
        if (warnings == null || warningId <= 0 || warningId >= warnings.warningCursor) { return false; }
        ref WarningData warning = ref warnings.warningPool[warningId];
        return warning.id == warningId && warning.factoryId == -6 && warning.signalId == 519
            && warning.astroId == locationPlanetId && warning.localPos == lastRouteTarget;
    }

    /// <summary>原版到达后会自动移除地表线；此时保留航迹选择，等待下一个节点。</summary>
    /// <param name="player">本端玩家。</param>
    /// <returns>是否处于原版自动清线的表面投影范围内。</returns>
    private static bool IsAtLocation(Player player) {
        float radius = player.planetData?.realRadius ?? 0f;
        float distance = Mathf.Max(5f, player.speed * (player.movementState >= EMovementState.Fly ? 0.15f : 0.1f));
        return radius > 0f && (((Vector3)player.position).normalized * radius
            - lastRouteTarget.normalized * radius).sqrMagnitude < distance * distance;
    }

    /// <summary>仅释放FE仍持有的连线，不清除玩家后来设置的其他目标。</summary>
    private static void ClearNavigation() {
        PlayerNavigation navigation = GameMain.mainPlayer?.navigation;
        if (indicatorPlanetId != 0 && navigation?.indicatorAstroId == indicatorPlanetId) { navigation.indicatorAstroId = 0; }
        if (HasLocationTracking()) { GameMain.data.warningSystem.RemoveWarningData(warningId); }
        indicatorPlanetId = locationPlanetId = warningId = 0;
        lastRouteTarget = default;
    }

    /// <summary>取消目标与显示，后续航迹节点变化也不会重新创建指引。</summary>
    private static void CancelTracking() {
        ClearNavigation();
        targetId = 0;
    }

    /// <summary>已选目标优先显示；超出感知范围仍可取消，未选择时展示附近信号。</summary>
    /// <returns>标题、连续强度及可操作的已发现地点身份。</returns>
    public static DetectorReading Read() {
        EnsureSession();
        Player player = GameMain.mainPlayer;
        if (player == null) { return default; }
        ExplorationMark[] known = ExplorationManager.GetActiveMarks();
        ExplorationMark target = known.FirstOrDefault(m => m.Id == targetId);
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
