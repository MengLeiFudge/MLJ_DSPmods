using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FE.Logic.Civilization;

/// <summary>信号HUD的只读投影；首遗物不公开地点身份或精确指引。</summary>
public readonly struct DetectorReading(bool visible, float strength, string name, int markId = 0, bool showStrength = true, int level = 0) {
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
    /// <summary>主线或终极任务的阶段序号，普通地点为质量等级；从1起，0表示不显示。</summary>
    public readonly int Level = level;
}

/// <summary>拥有本端各信号槽的连续指引选择，只投影位置，不创建移动命令。</summary>
public static class ExplorationDetector {
    private static GalaxyData galaxy;
    // 槽键只用于本端会话；值为0时该槽仍开启指引，等待后续目标。
    private static readonly Dictionary<string, int> TrackedSlots = [];
    private static ExplorationMark[] navigationSnapshot;

    /// <summary>切档丢弃旧世界引用与本端指引。</summary>
    public static void Reset() {
        galaxy = null;
        TrackedSlots.Clear();
        navigationSnapshot = null;
    }

    /// <summary>查询地点是否为本端某个槽的当前选择，面板和HUD共用同一状态。</summary>
    public static bool IsTracking(int markId) {
        EnsureSession();
        return markId != 0 && TrackedSlots.ContainsValue(markId);
    }

    /// <summary>点击当前地点关闭本槽指引；同槽其他地点替换本槽目标，0用于关闭等待中的主线指引。</summary>
    public static void ToggleTracking(int markId) {
        EnsureSession();
        if (markId == 0) { TrackedSlots.Remove("mainline"); return; }
        ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Id == markId);
        if (mark == null || mark.Kind == ExplorationMarkKind.FirstRelic) { return; }
        string slot = GetSlot(mark);
        if (TrackedSlots.TryGetValue(slot, out int selected) && selected == mark.Id) {
            TrackedSlots.Remove(slot);
        } else {
            TrackedSlots[slot] = mark.Id;
        }
    }

    /// <summary>地点回收后只在其所属槽续接；没有候选时保持开启，等待同槽后续信号。</summary>
    public static void UpdateNavigation() {
        EnsureSession();
        Player player = GameMain.mainPlayer;
        if (player == null) { return; }
        ExplorationMark[] known = ExplorationManager.GetActiveMarks();
        if (ReferenceEquals(navigationSnapshot, known)) { return; }
        navigationSnapshot = known;
        foreach (string slot in TrackedSlots.Keys.ToArray()) {
            TrackedSlots[slot] = SelectTarget(slot, known, player)?.Id ?? 0;
        }
    }

    /// <summary>同星球指向地点或下一航迹节点，跨星球先指向目标星球。</summary>
    public static bool TryGetGuidePosition(int markId, out VectorLF3 position) {
        EnsureSession();
        position = default;
        if (markId == 0 || GameMain.mainPlayer == null) { return false; }
        ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Id == markId);
        if (mark == null || mark.Kind == ExplorationMarkKind.FirstRelic) { return false; }
        PlanetData planet = GameMain.galaxy?.PlanetById(mark.PlanetId);
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

    /// <summary>按主线、已固定独立任务、普通到珍贵三个质量槽的顺序填充HUD投影。</summary>
    /// <param name="readings">调用方复用的列表；每次先清空，只加入实际存在且允许显示的信号。</param>
    public static void Read(List<DetectorReading> readings) {
        EnsureSession();
        readings.Clear();
        Player player = GameMain.mainPlayer;
        if (player == null) { return; }
        ExplorationMark[] known = ExplorationManager.GetActiveMarks();
        foreach (ExplorationMark mark in known.Where(IsMainline).OrderBy(m => m.ItemId)) {
            DetectorReading reading = ReadMark(mark, player);
            if (reading.Visible) { readings.Add(reading); }
        }
        int pendingStory = ExplorationManager.PendingStoryNumber;
        if (readings.Count == 0 && ExplorationManager.FirstRelicClaimed && pendingStory > 0) {
            string status = ExplorationManager.StoryWaitingForTechnology ? "沉默的频段" : "等待新信号";
            readings.Add(new(true, 0f, status.Translate(), 0, false, pendingStory));
        }
        foreach (ExplorationMark mark in known.Where(m => ExplorationManager.IsKeySignal(m) && !IsMainline(m)
                     && IsTracking(m.Id)).OrderBy(m => m.Id)) {
            readings.Add(ReadMark(mark, player));
        }
        for (int quality = 1; quality <= 3; quality++) {
            ExplorationMark mark = SelectTarget($"quality:{quality}", known, player);
            if (mark != null) { readings.Add(ReadMark(mark, player)); }
        }
    }

    /// <summary>读取单个目标的实际强度或挑战进度；首遗物仍只在原感知范围内匿名显示。</summary>
    private static DetectorReading ReadMark(ExplorationMark target, Player player) {
        int actionId = target.Kind != ExplorationMarkKind.FirstRelic ? target.Id : 0;
        int level = target.Kind switch {
            ExplorationMarkKind.Story or ExplorationMarkKind.PlanetDetector => target.ItemId,
            ExplorationMarkKind.Ultimate => target.ItemId + 1,
            _ when !ExplorationManager.IsKeySignal(target) => target.Quality,
            _ => 0,
        };
        SignalChallengeView challenge = ExplorationManager.GetChallenge(target.Id);
        if (target.PlanetId == player.planetId && challenge is { Started: true }) {
            float progress = challenge.Progress / (float)challenge.Required;
            if (challenge.Kind == SignalChallengeKind.Production) {
                progress = Mathf.Min(progress, challenge.Machines / (float)challenge.RequiredMachines);
            }
            return new(true, progress, string.Format("FE.Challenge.Status".Translate(), challenge.Progress, challenge.Required),
                actionId, level: level);
        }
        bool sensed = ExplorationManager.TryGetSignalStrength(target, player.uPosition, player.planetId, out float strength);
        return new(sensed || target.Kind != ExplorationMarkKind.FirstRelic, strength,
            target.Kind == ExplorationMarkKind.FirstRelic ? string.Empty : ExplorationManager.GetSignalName(target),
            actionId, sensed, level);
    }

    /// <summary>槽内固定目标优先；其余候选同星球优先，再按真实空间距离选择，不公开未发现地点。</summary>
    private static ExplorationMark SelectTarget(string slot, ExplorationMark[] known, Player player) {
        if (TrackedSlots.TryGetValue(slot, out int selected)) {
            ExplorationMark current = known.FirstOrDefault(m => m.Id == selected && GameMain.galaxy.PlanetById(m.PlanetId) != null);
            if (current != null) { return current; }
        }
        return known.Where(m => GetSlot(m) == slot && GameMain.galaxy.PlanetById(m.PlanetId) != null)
            .OrderBy(m => m.PlanetId == player.planetId ? 0 : 1)
            .ThenBy(m => (GetWorldPosition(m) - player.uPosition).sqrMagnitude)
            .ThenBy(m => m.Id).FirstOrDefault();
    }

    /// <summary>主线与独立任务按任务链分槽，普通遗迹和广播只按质量分槽。</summary>
    private static string GetSlot(ExplorationMark mark) => mark.Kind switch {
        ExplorationMarkKind.FirstRelic or ExplorationMarkKind.Story or ExplorationMarkKind.PlanetDetector => "mainline",
        ExplorationMarkKind.Ultimate => $"ultimate:{mark.ContentId}",
        ExplorationMarkKind.Sacrifice => "sacrifice",
        ExplorationMarkKind.Infrastructure => $"infrastructure:{mark.ContentId}",
        _ => $"quality:{mark.Quality}",
    };

    /// <summary>首遗物与连续剧情属于主线，其他探索任务使用各自槽位。</summary>
    private static bool IsMainline(ExplorationMark mark) => mark.Kind is ExplorationMarkKind.FirstRelic
        or ExplorationMarkKind.Story or ExplorationMarkKind.PlanetDetector;

    /// <summary>跨世界丢弃旧选择；普通主机快照保持本地选择。</summary>
    private static void EnsureSession() {
        if (ReferenceEquals(galaxy, GameMain.galaxy)) { return; }
        Reset();
        galaxy = GameMain.galaxy;
    }
}
