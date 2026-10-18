using System;
using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Nebula;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>重要广播的任务类型；路线始终可作为现场替代方案。</summary>
public enum SignalChallengeKind { Route = 1, Combat = 2, Production = 3 }

/// <summary>挑战的持久事实；归探索锁保护，不直接暴露给窗口或工厂线程。</summary>
internal sealed class SignalChallenge {
    public int MarkId;
    public SignalChallengeKind Kind;
    public int Reward;
    public bool Started;
    public long StartTick;
    public int Owner = -1;
    public int Progress;
    public int Required;
    public int RecipeId;
    public int RequiredMachines;
    public int QualifiedMachines;
    public long OutsideSince;
    public Vector3[] Route = [];
    /// <summary>启动后新建的制造台及其累计合格产量，实体身份防止拆除重用组件号冒领。</summary>
    public readonly Dictionary<int, (int Entity, int Produced)> Machines = [];
}

/// <summary>交给页面和地表绘制的只读副本。</summary>
public sealed class SignalChallengeView {
    public int MarkId { get; internal set; }
    public SignalChallengeKind Kind { get; internal set; }
    public bool Started { get; internal set; }
    public int Progress { get; internal set; }
    public int Required { get; internal set; }
    public int Reward { get; internal set; }
    public int RecipeId { get; internal set; }
    public int Machines { get; internal set; }
    public int RequiredMachines { get; internal set; }
    public Vector3[] Route { get; internal set; }
}

public static partial class ExplorationManager {
    private static readonly Dictionary<int, SignalChallenge> Challenges = [];
    private const float RouteWidth = 18f;
    private const float ChallengeWorkRadius = 120f;

    /// <summary>仅新生成的高质量广播占用挑战名额，普通点及旧存档点保持原有结算。</summary>
    private static void TryCreateChallenge(ExplorationMark mark) {
        if (mark.Quality < 2 || Challenges.Count >= 2) { return; }
        PlanetData planet = GameMain.galaxy.PlanetById(mark.PlanetId);
        var challenge = new SignalChallenge {
            MarkId = mark.Id, Kind = (SignalChallengeKind)(1 + NextInt(3)),
            Reward = ResearchManager.GetChallengeMemoryReward(ResearchManager.CurrentMatrixStage, mark.Quality),
            RequiredMachines = mark.Quality == 3 ? 3 : 2,
        };
        if (planet.type == EPlanetType.Gas) { challenge.Kind = SignalChallengeKind.Route; }
        if (challenge.Kind == SignalChallengeKind.Combat) {
            PlanetFactory factory = planet.factory;
            int enemy = 0;
            if (factory?.enemyPool != null) {
                for (int i = 1; i < factory.enemyCursor; i++) {
                    if (factory.enemyPool[i].id == i && factory.enemyPool[i].dfGBaseId > 0) { enemy = i; break; }
                }
            }
            if (enemy == 0) { challenge.Kind = SignalChallengeKind.Route; }
            else {
                Vector3 direction = ((Vector3)factory.enemyPool[enemy].pos).normalized;
                Vector3 axis = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                mark.LocalPosition = Quaternion.AngleAxis(80f / planet.realRadius * Mathf.Rad2Deg, axis) * direction * MarkRadius(planet);
                challenge.Required = mark.Quality == 3 ? 24 : 12;
            }
        }
        if (challenge.Kind == SignalChallengeKind.Production) {
            int[] candidates = [I处理器, I电路板, I齿轮];
            challenge.RecipeId = candidates.Select(id => LDB.items.Select(id)?.maincraft)
                .FirstOrDefault(recipe => recipe != null && recipe.Type == ERecipeType.Assemble
                    && GameMain.history.RecipeUnlocked(recipe.ID))?.ID ?? 0;
            if (challenge.RecipeId == 0) { challenge.Kind = SignalChallengeKind.Route; }
            else { challenge.Required = mark.Quality == 3 ? 360 : 240; }
        }
        if (challenge.Kind == SignalChallengeKind.Route) { MakeRoute(mark, challenge); }
        Challenges.Add(mark.Id, challenge);
        MarkDirty();
    }

    /// <summary>按真实半径限制航迹总长；固定折线路径不会随玩家移动重生成。</summary>
    private static void MakeRoute(ExplorationMark mark, SignalChallenge challenge) {
        PlanetData planet = GameMain.galaxy.PlanetById(mark.PlanetId);
        int segments = mark.Quality == 3 ? 6 : 4;
        float length = Mathf.Min(mark.Quality == 3 ? 360f : 240f, 0.5f * Mathf.PI * planet.realRadius);
        challenge.Route = new Vector3[segments + 1];
        challenge.Route[0] = mark.LocalPosition.normalized * MarkRadius(planet);
        Vector3 normal = challenge.Route[0].normalized;
        Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        for (int i = 1; i <= segments; i++) {
            Vector3 direction = challenge.Route[i - 1].normalized;
            tangent = Vector3.ProjectOnPlane(tangent, direction).normalized;
            tangent = Quaternion.AngleAxis(i % 2 == 0 ? -35f : 35f, direction) * tangent;
            Vector3 axis = Vector3.Cross(direction, tangent).normalized;
            challenge.Route[i] = Quaternion.AngleAxis(length / segments / planet.realRadius * Mathf.Rad2Deg, axis) * direction * MarkRadius(planet);
        }
        challenge.Required = segments;
    }

    public static SignalChallengeView GetChallenge(int markId) {
        lock (Sync) {
            if (!Challenges.TryGetValue(markId, out SignalChallenge c)) { return null; }
            return new SignalChallengeView {
                MarkId = markId, Kind = c.Kind, Started = c.Started, Progress = c.Progress, Required = c.Required,
                Reward = c.Reward, RecipeId = c.RecipeId, RequiredMachines = c.RequiredMachines,
                Machines = c.QualifiedMachines, Route = (Vector3[])c.Route.Clone(),
            };
        }
    }

    /// <summary>显示合格事实；不是仓储物品提交，也不采用全局历史产量。</summary>
    public static string GetChallengeDescription(int markId) {
        SignalChallengeView c = GetChallenge(markId);
        if (c == null) { return ""; }
        string progress = string.Format("FE.Challenge.Progress".Translate(), c.Progress, c.Required, c.Reward);
        string rules = c.Kind switch {
            SignalChallengeKind.Route => "FE.Challenge.Route".Translate(),
            SignalChallengeKind.Combat => "FE.Challenge.Combat".Translate(),
            _ => string.Format("FE.Challenge.Production".Translate(), LDB.recipes.Select(c.RecipeId)?.name,
                c.RequiredMachines, c.Machines),
        };
        return "\n\n" + rules + "\n" + progress + (c.Started ? "" : "\n" + "FE.Challenge.Dormant".Translate());
    }

    /// <summary>本地页面仅在真实抵达时提供替代操作，主机仍会独立复核。</summary>
    public static bool CanSwitchChallenge(int markId) {
        lock (Sync) {
            Player player = GameMain.mainPlayer;
            ExplorationMark mark = FindMark(markId);
            return player?.isAlive == true && mark != null && Challenges.TryGetValue(markId, out SignalChallenge c)
                && c.Kind != SignalChallengeKind.Route && IsWithinArrival(mark, player.uPosition, player.planetId);
        }
    }

    /// <summary>在已发现地点现场切换路线，清除原挑战的产量、击杀和参与者，不远程推进。</summary>
    public static void SwitchChallengeToRoute(int markId, VectorLF3 position, int planetId) {
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        lock (Sync) {
            if (!Challenges.TryGetValue(markId, out SignalChallenge c) || c.Kind == SignalChallengeKind.Route) { return; }
            ExplorationMark mark = FindMark(markId);
            if (mark == null || !DiscoveredMarks.ContainsKey(markId) || !IsWithinArrival(mark, position, planetId)) { return; }
            c.Kind = SignalChallengeKind.Route; c.Started = false; c.Progress = 0; c.Owner = -1; c.OutsideSince = 0; c.Machines.Clear();
            MakeRoute(mark, c); MarkDirty();
        }
    }

    /// <summary>主线程读取已验证的玩家位置；同一路线只接受一名参与者连续完成。</summary>
    private static void TickChallenges(long tick) {
        var players = ChallengeNetwork.GetPlayers();
        foreach (SignalChallenge c in Challenges.Values.ToArray()) {
            ExplorationMark mark = FindMark(c.MarkId);
            if (mark == null) { Challenges.Remove(c.MarkId); continue; }
            PlanetData planet = GameMain.galaxy.PlanetById(mark.PlanetId);
            if (planet == null) { continue; }
            if (!c.Started) {
                foreach (var player in players) {
                    if (!IsWithinArrival(mark, player.Position, player.Planet)) { continue; }
                    c.Started = true; c.StartTick = tick; c.Owner = player.Id; c.OutsideSince = 0;
                    MarkDirty(); break;
                }
            }
            if (c.Kind == SignalChallengeKind.Production) {
                int qualified = CountWorkingMachines(c);
                if (qualified != c.QualifiedMachines) { c.QualifiedMachines = qualified; MarkDirty(); }
                if (c.Progress >= c.Required && qualified >= c.RequiredMachines) { CompleteChallenge(mark, c); }
            }
            if (!c.Started || c.Kind != SignalChallengeKind.Route) { continue; }
            var owner = players.FirstOrDefault(p => p.Id == c.Owner);
            bool inPlanet = players.Any(p => p.Id == c.Owner) && owner.Planet == mark.PlanetId;
            Vector3 local = inPlanet ? (Vector3)Maths.QRotateLF(Quaternion.Inverse(planet.runtimeRotation), owner.Position - planet.uPosition) : Vector3.zero;
            Vector3 point = local.normalized * MarkRadius(planet);
            Vector3 start = c.Route[c.Progress], end = c.Route[c.Progress + 1];
            Vector3 segment = end - start;
            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude);
            float deviation = Vector3.Distance(point, Vector3.Lerp(start, end, t).normalized * MarkRadius(planet));
            if (!inPlanet || Mathf.Abs(local.magnitude - planet.realRadius) > 70f || deviation > RouteWidth) {
                if (c.OutsideSince == 0) { c.OutsideSince = tick; }
                if (tick - c.OutsideSince >= 180) {
                    c.Started = false; c.Progress = 0; c.Owner = -1; c.OutsideSince = 0; MarkDirty();
                }
                continue;
            }
            c.OutsideSince = 0;
            if (Vector3.Distance(point, end) <= RouteWidth) {
                c.Progress++; MarkDirty();
                if (c.Progress == c.Required) { CompleteChallenge(mark, c); }
            }
        }
        DrainChallengeFacts();
        ExplorationChallengeFacts.SetTargets(Challenges.Values.Where(c => c.Started && c.Kind == SignalChallengeKind.Production)
            .Select(c => (FindMark(c.MarkId)?.PlanetId ?? 0, c.RecipeId,
                c.Machines.Select(m => (m.Key, m.Value.Entity)).ToArray())).ToArray(),
            Challenges.Values.Any(c => c.Started && c.Kind == SignalChallengeKind.Combat));
    }

    private static int CountWorkingMachines(SignalChallenge challenge) {
        PlanetFactory factory = GameMain.galaxy?.PlanetById(FindMark(challenge.MarkId)?.PlanetId ?? 0)?.factory;
        if (factory?.factorySystem == null) { return 0; }
        AssemblerComponent[] pool = factory.factorySystem.assemblerPool;
        return challenge.Machines.Count(pair => pair.Value.Produced > 0 && pair.Key > 0 && pair.Key < pool.Length
            && pool[pair.Key].id == pair.Key && pool[pair.Key].entityId == pair.Value.Entity && pool[pair.Key].recipeId == challenge.RecipeId);
    }

    private static void CompleteChallenge(ExplorationMark mark, SignalChallenge challenge) {
        mark.ItemId = IFE记忆源点; mark.Count = challenge.Reward;
        // 移除挑战后才进入常规认领，固定奖励不再经过普通点随机表。
        Challenges.Remove(mark.Id);
        Claim(mark);
    }

    /// <summary>消费工厂线程上传的事实；只统计开启后、地点范围内的新设备和真实击杀。</summary>
    private static void DrainChallengeFacts() {
        while (ExplorationChallengeFacts.TryDequeue(out ChallengeFact fact)) {
            if (!ReferenceEquals(fact.World, GameMain.data)) { continue; }
            foreach (SignalChallenge c in Challenges.Values.ToArray()) {
                ExplorationMark mark = FindMark(c.MarkId);
                if (!c.Started || fact.Tick < c.StartTick || mark?.PlanetId != fact.PlanetId) { continue; }
                PlanetFactory factory = GameMain.galaxy.PlanetById(mark.PlanetId)?.factory;
                if (c.Kind == SignalChallengeKind.Combat && fact.Kind == ChallengeFactKind.Kill
                    && Vector3.Distance(fact.Position, mark.LocalPosition) <= ChallengeWorkRadius) {
                    c.Progress = Math.Min(c.Required, c.Progress + 1); MarkDirty();
                } else if (c.Kind == SignalChallengeKind.Production && factory != null && fact.EntityId > 0
                    && fact.EntityId < factory.entityPool.Length && factory.entityPool[fact.EntityId].id == fact.EntityId
                    && Vector3.Distance(factory.entityPool[fact.EntityId].pos, mark.LocalPosition) <= ChallengeWorkRadius) {
                    if (fact.Kind == ChallengeFactKind.Build) { c.Machines[fact.AssemblerId] = (fact.EntityId, 0); MarkDirty(); }
                    else if (fact.Kind == ChallengeFactKind.Produce && fact.RecipeId == c.RecipeId
                        && c.Machines.TryGetValue(fact.AssemblerId, out var machine) && machine.Entity == fact.EntityId) {
                        c.Machines[fact.AssemblerId] = (machine.Entity, (int)Math.Min(c.Required, (long)machine.Produced + fact.Count));
                        c.Progress = (int)Math.Min(c.Required, (long)c.Progress + fact.Count); MarkDirty();
                    }
                }
                if (c.Progress >= c.Required && (c.Kind != SignalChallengeKind.Production || CountWorkingMachines(c) >= c.RequiredMachines)) {
                    CompleteChallenge(mark, c);
                }
            }
        }
    }
}
