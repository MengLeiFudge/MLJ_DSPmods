using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Recipes;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>点位所属玩法；静默点与剧情点不占动态队列。</summary>
public enum ExplorationMarkKind {
    FirstRelic = 1, DirectionEligibility = 2, ResourceCache = 3, PlanetDetector = 4,
    SilentCache = 5, Story = 6, Sacrifice = 7, Infrastructure = 8, Ultimate = 9,
}

/// <summary>存档内的点位身份、局部位置和固定质量。</summary>
public sealed class ExplorationMark {
    /// <summary>静默点为负数确定性身份，动态/剧情点为正数递增身份。</summary>
    public int Id;
    /// <summary>目标星球。</summary>
    public int PlanetId;
    /// <summary>相对星球中心的局部位置，单位米。</summary>
    public Vector3 LocalPosition;
    /// <summary>玩法类型。</summary>
    public ExplorationMarkKind Kind;
    /// <summary>剧情分支或研究内容身份，仅逻辑域使用。</summary>
    public string ContentId;
    /// <summary>实际奖励物品或剧情序号。</summary>
    public int ItemId;
    /// <summary>实际奖励数量。</summary>
    public int Count;
    /// <summary>生成后固定的质量层级，1～3。</summary>
    public int Quality = 1;
}

/// <summary>一次已经结算的回收记录，奖励不得在查看档案时重新抽取。</summary>
public sealed class ExplorationRecord {
    /// <summary>已回收的点位。</summary>
    public ExplorationMark Mark;
    /// <summary>回收时游戏刻。</summary>
    public long Tick;
    /// <summary>按查看者当前语言生成文明记录或奖励描述，不缓存成品文本。</summary>
    public string Text => ExplorationManager.GetRecoveryText(Mark);
}

/// <summary>管理固定遗迹、阶段广播、剧情任务与主机认领结算。</summary>
public static class ExplorationManager {
    private const int MaximumRecords = 1000000;
    private const long TicksPerMinute = 3600;
    private const int UltimateMemoryCost = 2400;
    private const float DetectionRange = 180f;
    private const float OrdinaryArrivalDistance = 18f;
    private const float KeyArrivalDistance = 30f;
    /// <summary>原版机甲默认悬浮高度，遗迹悬于其一半处，使步行与悬浮的触发范围相同。</summary>
    private const float DefaultHoverAltitude = 15f;
    /// <summary>首遗物回收前未接通亚空间时的引导提示。</summary>
    public const string FirstSignalHint = "也许我应该先去看看那个信号……";
    /// <summary>不能由普通矿脉替代的建筑奖励，按矩阵阶段开放。</summary>
    private static readonly (int ItemId, int MinStage)[] BuildingRewards = [
        (I大型采矿机, 0), (I物流配送器, 1), (I自动集装机, 2), (I星际物流运输站, 2), (I位面熔炉, 2),
        (I微型粒子对撞机, 2), (I量子化工厂, 3), (I重组式制造台, 3), (I轨道采集器, 3),
        (I自演化研究站, 4), (I能量枢纽, 4), (I人造恒星, 4), (I负熵熔炉, 5),
    ];
    private static readonly object Sync = new();
    private static readonly List<ExplorationMark> ActiveMarks = [];
    private static readonly Dictionary<int, ExplorationMark[]> SilentMarks = [];
    private static readonly HashSet<int> CollectedSilentMarks = [];
    private static readonly List<ExplorationRecord> Records = [];
    private static readonly int[] UltimateSteps = new int[2];
    private static readonly int[] UltimateInputs = new int[2];
    private static int nextMarkId = 1;
    private static int storyCompleted;
    private static int matrixMilestoneId = -1;
    private static long nextTimedGenerationTick;
    private static uint randomSeed = 0x8F3A51C7u;
    private static bool snapshotDirty;
    private static bool capacityHintShown;
    private static ExplorationMark[] broadcastSnapshot = [];
    private static bool broadcastDirty = true;
    private static ExplorationRecord[] recordsSnapshot;
    private static ExplorationMark[] nearbySnapshot = [];
    private static int nearbyPlanet;
    private static int nearbyQuality;
    private static bool nearbyDirty = true;

    /// <summary>首遗物是否已经回收。</summary>
    public static bool FirstRelicClaimed { get { lock (Sync) { return storyCompleted > 0; } } }
    /// <summary>首遗物回收后开放普通探测与 Frag 输入。</summary>
    public static bool PlanetDetectorUnlocked => FirstRelicClaimed;
    /// <summary>已经完成的主线节点数。</summary>
    public static int StoryCompleted { get { lock (Sync) { return storyCompleted; } } }
    /// <summary>静默层级随矩阵阶段开放，最高为三层。</summary>
    public static int DetectableQuality => ResearchManager.CurrentMatrixStage >= 3 ? 3
        : ResearchManager.CurrentMatrixStage >= 2 ? 2 : 1;

    /// <summary>是否为公开目标星球的剧情点。</summary>
    /// <param name="mark">目标点。</param>
    /// <returns>是否剧情点。</returns>
    public static bool IsKeySignal(ExplorationMark mark) => mark != null && mark.Kind is
        ExplorationMarkKind.FirstRelic or ExplorationMarkKind.PlanetDetector or ExplorationMarkKind.Story
        or ExplorationMarkKind.Sacrifice or ExplorationMarkKind.Infrastructure or ExplorationMarkKind.Ultimate;

    /// <summary>提供玩家可见名称，不泄露稳定身份或未解析结果。</summary>
    /// <param name="mark">目标点。</param>
    /// <returns>显示名称。</returns>
    public static string GetSignalName(ExplorationMark mark) {
        if (mark == null) { return string.Empty; }
        return mark.Kind switch {
            ExplorationMarkKind.FirstRelic => "最初的回声".Translate(),
            ExplorationMarkKind.PlanetDetector => "矩阵中的生命".Translate(),
            ExplorationMarkKind.Story => GetStoryTitle(mark.ItemId),
            ExplorationMarkKind.Sacrifice => "过剩的生命".Translate(),
            ExplorationMarkKind.Infrastructure => "远古物流接口".Translate(),
            ExplorationMarkKind.Ultimate => mark.ContentId == "exploration" ? "长夜中的回声".Translate() : "没有尽头的库房".Translate(),
            ExplorationMarkKind.SilentCache => string.Format("{0}遗迹".Translate(), new[] { "", "普通".Translate(), "精良".Translate(), "珍贵".Translate() }[mark.Quality]),
            _ => "远古广播".Translate(),
        };
    }

    /// <summary>将已结算的回收事实转换为本端语言的正文；不执行奖励或推进任务。</summary>
    /// <param name="mark">包含实际物品、数量或剧情身份的已回收点位。</param>
    /// <returns>本端语言的文明记录或奖励描述。</returns>
    public static string GetRecoveryText(ExplorationMark mark) {
        if (mark == null) { return string.Empty; }
        return mark.Kind switch {
            ExplorationMarkKind.FirstRelic or ExplorationMarkKind.PlanetDetector or ExplorationMarkKind.Story =>
                GetStoryNarrative(mark.ItemId),
            ExplorationMarkKind.Sacrifice => "FE.ExplorationManager.SacrificeRecovered".Translate(),
            ExplorationMarkKind.Infrastructure => "FE.ExplorationManager.InfrastructureRecovered".Translate(),
            ExplorationMarkKind.Ultimate => GetUltimateNarrative(mark.ContentId == "exploration" ? 0 : 1, mark.ItemId),
            _ when mark.Count == 0 && ResearchManager.IsRecoveredPageId(mark.ContentId) =>
                "FE.Encyclopedia.PageRecovered".Translate(),
            _ when mark.Count == 0 && RecipeManager.GetRecipe(mark.ContentId)?.UnlockTier == RecipeUnlockTier.Legendary =>
                string.Format("FE.Encyclopedia.LegendaryRecovered".Translate(), RecipeManager.GetRecipe(mark.ContentId).DisplayName),
            _ when mark.Count == 0 && !string.IsNullOrEmpty(mark.ContentId) =>
                string.Format("回收了以{0}为样本的未知记录。".Translate(), LDB.items.Select(mark.ItemId)?.name),
            _ => string.Format("探索取得：{0} × {1}".Translate(), LDB.items.Select(mark.ItemId)?.name, mark.Count),
        };
    }

    /// <summary>下一主线节点是否只在等待矩阵科技推进；不点名所需矩阵。</summary>
    public static bool StoryWaitingForTechnology {
        get {
            lock (Sync) {
                // 4～6 号节点由“第一枚矩阵”目标负责引导，这里只覆盖后续矩阵阶段。
                int next = storyCompleted + 1;
                if (next < 7 || next > 18 || ActiveMarks.Any(m => m.Kind is ExplorationMarkKind.Story
                        or ExplorationMarkKind.PlanetDetector)) { return false; }
                return ResearchManager.CurrentMatrixStage < (next - 1) / 3;
            }
        }
    }

    /// <summary>描述已收到信号的来历与抵达方式，不提前公开回收奖励。</summary>
    /// <param name="mark">玩家已经收到的广播。</param>
    /// <returns>该信号的叙事与行动提示。</returns>
    public static string GetSignalDescription(ExplorationMark mark) {
        if (mark == null) { return string.Empty; }
        PlanetData planet = GameMain.galaxy?.PlanetById(mark.PlanetId);
        string lead = mark.Kind switch {
            ExplorationMarkKind.FirstRelic => "FE.ExplorationManager.FirstRelicHint".Translate(),
            ExplorationMarkKind.PlanetDetector => "FE.ExplorationManager.DetectorHint".Translate(),
            ExplorationMarkKind.Sacrifice => "FE.ExplorationManager.SacrificeHint".Translate(),
            ExplorationMarkKind.Infrastructure => "FE.ExplorationManager.InfrastructureHint".Translate(),
            ExplorationMarkKind.Ultimate => mark.ItemId switch {
                0 => "FE.ExplorationManager.UltimateStartHint".Translate(),
                >= 1 and <= 3 => "FE.ExplorationManager.UltimateNodesHint".Translate(),
                4 => "FE.ExplorationManager.UltimateDeliveryHint".Translate(),
                _ => "FE.ExplorationManager.UltimateCoreHint".Translate(),
            },
            ExplorationMarkKind.Story => mark.ItemId switch {
                2 or 3 => "FE.ExplorationManager.StoryContainersHint".Translate(),
                <= 9 => "FE.ExplorationManager.StoryLivingHint".Translate(),
                <= 12 => "FE.ExplorationManager.StoryPlanetsHint".Translate(),
                _ => planet != null && planet.star.id != GameMain.galaxy.birthStarId
                    ? "FE.ExplorationManager.StoryInterstellarHint".Translate()
                    : "FE.ExplorationManager.StoryLocalHint".Translate(),
            },
            _ => "FE.ExplorationManager.BroadcastHint".Translate(),
        };
        return string.Format("FE.ExplorationManager.Destination".Translate(), lead, planet?.displayName);
    }

    /// <summary>获取已广播点的缓存快照；不会暴露未发现静默点。</summary>
    /// <returns>调用方只读的点位副本。</returns>
    public static ExplorationMark[] GetActiveMarks() {
        lock (Sync) {
            if (broadcastDirty) {
                broadcastSnapshot = ActiveMarks.Select(CloneMark).ToArray();
                broadcastDirty = false;
            }
            return broadcastSnapshot;
        }
    }

    /// <summary>获取回收档案副本。</summary>
    /// <returns>按回收时间排序的档案。</returns>
    public static ExplorationRecord[] GetRecords() {
        lock (Sync) {
            return recordsSnapshot ??= Records.Select(r => new ExplorationRecord {
                Mark = CloneMark(r.Mark), Tick = r.Tick,
            }).ToArray();
        }
    }

    /// <summary>返回当前终极任务步骤，0～5为进行中，6为完成。</summary>
    /// <param name="branch">0为探索，1为仓储。</param>
    /// <returns>任务步骤。</returns>
    public static int GetUltimateStep(int branch) { lock (Sync) { return UltimateSteps[branch == 0 ? 0 : 1]; } }

    /// <summary>获取终极任务已经提交的 Memory。</summary>
    /// <param name="branch">0为探索，1为仓储。</param>
    /// <returns>累计交付数量。</returns>
    public static int GetUltimateInput(int branch) { lock (Sync) { return UltimateInputs[branch == 0 ? 0 : 1]; } }

    /// <summary>统计星球当前可探测静默点，不包含剧情和广播。</summary>
    /// <param name="planetId">星球身份。</param>
    /// <param name="collected">已回收数量。</param>
    /// <param name="total">已开放总量。</param>
    public static void GetSilentProgress(int planetId, out int collected, out int total) {
        lock (Sync) {
            var marks = GetSilentMarks(planetId);
            int quality = DetectableQuality;
            total = marks.Count(m => m.Quality <= quality);
            collected = marks.Count(m => m.Quality <= quality && CollectedSilentMarks.Contains(m.Id));
        }
    }

    /// <summary>返回定时广播剩余游戏刻。</summary>
    /// <param name="gameTick">当前游戏刻。</param>
    /// <returns>剩余刻数，终极持续广播返回零。</returns>
    public static long GetTicksUntilNextSignal(long gameTick) {
        lock (Sync) { return GrowthTechnologyManager.UltimateExploration ? 0 : Math.Max(0, nextTimedGenerationTick - gameTick); }
    }

    /// <summary>开局保证母星首遗物存在。</summary>
    public static void EnsureFirstRelicMark() {
        if (NebulaMultiplayerModAPI.IsClient || GameMain.galaxy == null) { return; }
        lock (Sync) {
            if (storyCompleted == 0 && !ActiveMarks.Any(m => m.Kind == ExplorationMarkKind.FirstRelic)) {
                PlanetData planet = GameMain.galaxy.PlanetById(GameMain.galaxy.birthPlanetId);
                if (TryFindFirstRelicPosition(planet, out Vector3 position)) {
                    AddMark(planet, ExplorationMarkKind.FirstRelic, 1, fixedPosition: position);
                }
            }
        }
    }

    /// <summary>Frag 结算尝试生成动态补给，统一受星球容量和冷却约束。</summary>
    /// <param name="count">需要生成的点数。</param>
    /// <param name="source">提示来源。</param>
    public static void AddExplorationPoints(int count, string source) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) { return; }
        int generated = 0;
        lock (Sync) {
            while (generated < count && GenerateDynamicMark()) { generated++; }
        }
        if (generated > 0) {
            CivilizationModule.QueueNotification(string.Format("{0}发现了新的远古广播。".Translate(), source));
        }
    }

    /// <summary>研究结果适度提前尚未到期的广播，不延长既有倒计时。</summary>
    /// <param name="discoveredCount">新完成内容数。</param>
    public static void AccelerateNextSignal(int discoveredCount) {
        if (discoveredCount <= 0 || NebulaMultiplayerModAPI.IsClient) { return; }
        lock (Sync) {
            long now = GameMain.gameTick;
            nextTimedGenerationTick = Math.Min(nextTimedGenerationTick,
                Math.Max(now + 30 * 60, nextTimedGenerationTick - discoveredCount * 60L * 60L));
            MarkDirty();
        }
    }

    /// <summary>主机按秒推进任务和广播，每 10 刻（六分之一秒）处理短距离认领。</summary>
    /// <param name="gameTick">当前游戏刻。</param>
    /// <param name="secondBoundary">本次调用是否进入了新的游戏秒；由调用方按秒序号变化判断。</param>
    /// <remarks>调用方保证每个 10 刻区间最多调用一次，低帧率跳刻时也不会漏掉。</remarks>
    public static void GameTick(long gameTick, bool secondBoundary) {
        if (GameMain.mainPlayer == null || GameMain.galaxy == null) { return; }
        if (NebulaMultiplayerModAPI.IsClient) {
            foreach (ExplorationMark mark in NearbyMarks(GameMain.mainPlayer.planetId)) {
                if (!IsPaymentMark(mark) && IsWithinArrival(mark, GameMain.mainPlayer.uPosition,
                        GameMain.mainPlayer.planetId)) {
                    NebulaMultiplayerModAPI.RequestExplorationClaim(mark.Id);
                }
            }
            return;
        }
        lock (Sync) {
            if (secondBoundary) {
                EnsureFirstRelicMark();
                EnsureStory();
                EnsureMilestones();
                EnsureUltimate(0);
                EnsureUltimate(1);
                if (FirstRelicClaimed) {
                    if (GrowthTechnologyManager.UltimateExploration) {
                        ReplenishUltimateSignals();
                    } else if (nextTimedGenerationTick == 0) {
                        nextTimedGenerationTick = gameTick + GetGenerationIntervalTicks();
                        MarkDirty();
                    } else if (gameTick >= nextTimedGenerationTick) {
                        GenerateDynamicMark();
                        nextTimedGenerationTick = gameTick + GetGenerationIntervalTicks();
                    }
                }
            }
            foreach (ExplorationMark mark in NearbyMarks(GameMain.mainPlayer.planetId)) {
                if (!IsPaymentMark(mark) && IsWithinArrival(mark, GameMain.mainPlayer.uPosition,
                        GameMain.mainPlayer.planetId)) {
                    Claim(mark);
                }
            }
        }
    }

    /// <summary>验证连接玩家位置后结算认领，任务交付必须显式请求。</summary>
    /// <param name="markId">点位身份。</param>
    /// <param name="position">连接玩家的宇宙坐标。</param>
    /// <param name="planetId">连接玩家所在星球。</param>
    /// <returns>是否成功认领。</returns>
    public static bool TryClaimFromClient(int markId, VectorLF3 position, int planetId) {
        if (NebulaMultiplayerModAPI.IsClient || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) { return false; }
        lock (Sync) {
            ExplorationMark mark = FindMark(markId);
            if (mark == null || IsPaymentMark(mark) || !IsWithinArrival(mark, position, planetId)) { return false; }
            Claim(mark);
            return true;
        }
    }

    /// <summary>在交付遗迹附近显式提交 Memory，可分批并严格限制到剩余数量。</summary>
    /// <param name="markId">交付节点身份。</param>
    /// <param name="position">主机验证过的玩家宇宙位置。</param>
    /// <param name="planetId">玩家所在星球。</param>
    /// <returns>本次提交数量。</returns>
    public static int SubmitTaskMemory(int markId, VectorLF3 position, int planetId) {
        if (NebulaMultiplayerModAPI.IsClient || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) { return 0; }
        lock (Sync) {
            ExplorationMark mark = ActiveMarks.FirstOrDefault(m => m.Id == markId);
            if (mark == null || !IsPaymentMark(mark) || !IsWithinArrival(mark, position, planetId)) { return 0; }
            int branch = mark.ContentId == "exploration" ? 0 : 1;
            int count = DataCenterInventory.TakeItemFromModData(IFE记忆源点, UltimateMemoryCost - UltimateInputs[branch], out int _);
            UltimateInputs[branch] += count;
            if (UltimateInputs[branch] >= UltimateMemoryCost) { Claim(mark); }
            MarkDirty();
            return count;
        }
    }

    /// <summary>读取当前信号的精细量程；量化百分比不参与回收判定。</summary>
    /// <param name="mark">当前跟踪目标。</param>
    /// <param name="playerPosition">玩家宇宙位置。</param>
    /// <param name="planetStrength">兼容输出，等于精细读数，不是星球量程。</param>
    /// <param name="relicStrength">就近取档后的0～1强度。</param>
    /// <returns>是否处于目标星球低空及180m量程内。</returns>
    public static bool TryGetDetectorStrengths(ExplorationMark mark, VectorLF3 playerPosition,
        out float planetStrength, out float relicStrength) {
        planetStrength = relicStrength = 0f;
        PlanetData planet = GameMain.galaxy?.PlanetById(mark?.PlanetId ?? 0);
        if (planet == null || GameMain.mainPlayer?.planetId != planet.id
            || mark.Quality > DetectableQuality && !IsKeySignal(mark)) { return false; }
        if (Math.Abs((playerPosition - planet.uPosition).magnitude - planet.realRadius) > 70) { return false; }
        double distance = GetSignalDistance(mark, planet, playerPosition);
        if (distance > DetectionRange) { return false; }
        float arrival = GetArrivalDistance(mark);
        float raw = Mathf.Clamp01((DetectionRange - (float)distance) / (DetectionRange - arrival));
        float step = IsKeySignal(mark) ? 0.01f : GrowthTechnologyManager.ExplorationSignalPrecisionPercent / 100f;
        relicStrength = Mathf.Clamp01(Mathf.Floor(raw / step + 0.5f) * step);
        planetStrength = relicStrength;
        return true;
    }

    /// <summary>保持模式内有效目标，失效后依次优先本星球、本星系、距离和稳定身份。</summary>
    /// <param name="keySignals">是否为文明信标模式。</param>
    /// <param name="currentId">当前目标身份，0表示尚未选择。</param>
    /// <returns>目标副本；无候选为null，不公开远处静默点。</returns>
    public static ExplorationMark GetDetectorTarget(bool keySignals, int currentId) {
        Player player = GameMain.mainPlayer;
        if (player == null || GameMain.galaxy == null || !keySignals && !FirstRelicClaimed) { return null; }
        lock (Sync) {
            IEnumerable<ExplorationMark> candidates = ActiveMarks;
            if (!keySignals && player.planetId > 0) {
                candidates = candidates.Concat(NearbyMarks(player.planetId).Where(m => m.Kind == ExplorationMarkKind.SilentCache));
            }
            ExplorationMark best = null;
            int bestPriority = int.MaxValue;
            double bestDistance = double.MaxValue;
            foreach (ExplorationMark mark in candidates) {
                if (IsKeySignal(mark) != keySignals || !keySignals && mark.Quality > DetectableQuality) { continue; }
                PlanetData planet = GameMain.galaxy.PlanetById(mark.PlanetId);
                if (planet == null) { continue; }
                if (mark.Kind == ExplorationMarkKind.SilentCache
                    && !TryGetDetectorStrengths(mark, player.uPosition, out _, out _)) { continue; }
                if (mark.Id == currentId) { return CloneMark(mark); }
                int priority = planet.id == player.planetId ? 0 : planet.star == GameMain.localStar ? 1 : 2;
                double distance = priority == 0 ? GetSignalDistance(mark, planet, player.uPosition)
                    : (planet.uPosition - player.uPosition).sqrMagnitude;
                if (priority < bestPriority || priority == bestPriority
                    && (distance < bestDistance || distance == bestDistance && (best == null || mark.Id < best.Id))) {
                    best = mark;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }
            return best == null ? null : CloneMark(best);
        }
    }

    /// <summary>用真实星球和空间距离判定本地玩家到达，不从百分比推断。</summary>
    /// <param name="mark">当前目标。</param>
    /// <returns>本地玩家是否实际到达。</returns>
    public static bool IsDetectorTargetReached(ExplorationMark mark) => mark != null
        && GameMain.mainPlayer is Player player && IsWithinArrival(mark, player.uPosition, player.planetId);

    /// <summary>读取并清除主机快照脏标记。</summary>
    /// <returns>是否有待同步变化。</returns>
    public static bool TakeSnapshotDirty() { lock (Sync) { bool dirty = snapshotDirty; snapshotDirty = false; return dirty; } }

    /// <summary>保存任务和已结算点位，不保存本地化文本；静默几何可由种子重建。</summary>
    /// <param name="writer">输出流。</param>
    public static void Export(BinaryWriter writer) {
        lock (Sync) {
            writer.Write(3);
            writer.Write(nextMarkId); writer.Write(storyCompleted); writer.Write(nextTimedGenerationTick); writer.Write(randomSeed);
            writer.Write(capacityHintShown);
            for (int i = 0; i < 2; i++) { writer.Write(UltimateSteps[i]); writer.Write(UltimateInputs[i]); }
            writer.Write(ActiveMarks.Count);
            foreach (ExplorationMark mark in ActiveMarks) { WriteMark(writer, mark); }
            writer.Write(Records.Count);
            foreach (ExplorationRecord record in Records) { WriteMark(writer, record.Mark); writer.Write(record.Tick); }
            writer.Write(LastBroadcastTicks.Count);
            foreach (var pair in LastBroadcastTicks) { writer.Write(pair.Key); writer.Write(pair.Value); }
        }
    }

    /// <summary>恢复当前格式状态，兼容读取带成品文本的版本1，重建静默认领集合。</summary>
    /// <param name="reader">存档或主机快照流。</param>
    public static void Import(BinaryReader reader) {
        lock (Sync) {
            IntoOtherSave();
            int version = reader.ReadInt32();
            if (version is < 1 or > 3) { throw new InvalidDataException("不支持的探索存档格式。"); }
            nextMarkId = reader.ReadInt32(); storyCompleted = reader.ReadInt32(); nextTimedGenerationTick = reader.ReadInt64(); randomSeed = reader.ReadUInt32();
            capacityHintShown = reader.ReadBoolean();
            for (int i = 0; i < 2; i++) { UltimateSteps[i] = Math.Max(0, Math.Min(6, reader.ReadInt32())); UltimateInputs[i] = Math.Max(0, Math.Min(UltimateMemoryCost, reader.ReadInt32())); }
            int count = ReadCount(reader);
            for (int i = 0; i < count; i++) { ActiveMarks.Add(ReadMark(reader)); }
            count = ReadCount(reader);
            for (int i = 0; i < count; i++) {
                var record = new ExplorationRecord { Mark = ReadMark(reader), Tick = reader.ReadInt64() };
                // 版本1已经保存完整的结算事实，旧成品文本只需消费，不作为显示来源。
                if (version == 1) { reader.ReadString(); }
                Records.Add(record);
                if (record.Mark.Kind == ExplorationMarkKind.SilentCache) { CollectedSilentMarks.Add(record.Mark.Id); }
            }
            if (version >= 3) {
                count = ReadCount(reader);
                for (int i = 0; i < count; i++) {
                    int planetId = reader.ReadInt32();
                    long tick = reader.ReadInt64();
                    if (planetId <= 0 || tick < 0 || LastBroadcastTicks.ContainsKey(planetId)) {
                        throw new InvalidDataException("广播冷却数据无效。");
                    }
                    LastBroadcastTicks.Add(planetId, tick);
                }
            }
            if (nextMarkId <= 0 || storyCompleted < 0 || storyCompleted > 18) { throw new InvalidDataException("探索进度无效。"); }
            MarkDirty();
        }
    }

    /// <summary>切档时清除所有快照、几何和任务状态。</summary>
    public static void IntoOtherSave() {
        lock (Sync) {
            ActiveMarks.Clear(); SilentMarks.Clear(); CollectedSilentMarks.Clear(); Records.Clear();
            LastBroadcastTicks.Clear();
            Array.Clear(UltimateSteps, 0, 2); Array.Clear(UltimateInputs, 0, 2);
            nextMarkId = 1; storyCompleted = 0; nextTimedGenerationTick = 0; randomSeed = 0x8F3A51C7u;
            capacityHintShown = false; matrixMilestoneId = -1; broadcastSnapshot = []; broadcastDirty = true; snapshotDirty = false;
            recordsSnapshot = null; nearbySnapshot = []; nearbyPlanet = 0; nearbyQuality = 0; nearbyDirty = true;
        }
    }

    /// <summary>判断首个电磁矩阵实际生产里程碑，不把科技解锁当成已经生产。</summary>
    /// <returns>是否已生产首个电磁矩阵。</returns>
    public static bool HasProducedFirstMatrix() {
        if (matrixMilestoneId == -1) {
            matrixMilestoneId = LDB.milestones.dataArray.FirstOrDefault(m =>
                m?.DeterminatorName == nameof(MST_ItemFirstProduction)
                && m.DeterminatorParams?.Length > 0 && m.DeterminatorParams[0] == I电磁矩阵)?.ID ?? 0;
        }
        return matrixMilestoneId > 0 && GameMain.data?.milestoneSystem?.MilestoneUnlocked(matrixMilestoneId) == true;
    }

    private static void EnsureStory() {
        int next = storyCompleted + 1;
        if (next <= 1 || next > 18 || ActiveMarks.Any(m => m.Kind is ExplorationMarkKind.FirstRelic or ExplorationMarkKind.Story or ExplorationMarkKind.PlanetDetector)) { return; }
        int stage = (next - 1) / 3;
        if (ResearchManager.CurrentMatrixStage < stage || stage >= 1 && !HasProducedFirstMatrix()) { return; }
        if (next == 3 && ResearchManager.DiscoveredContentCount < 1) { return; }
        PlanetData planet = SelectReachablePlanet(stage, stage == 3 ? GameMain.galaxy.birthPlanetId : 0, next >= 13);
        AddMark(planet, next == 4 ? ExplorationMarkKind.PlanetDetector : ExplorationMarkKind.Story, next);
    }

    private static void EnsureMilestones() {
        if (!FirstRelicClaimed) { return; }
        if (!capacityHintShown && DataCenterInventory.GetItemCapacity(IFE高级交互塔) >= 1000) {
            capacityHintShown = true;
            CivilizationModule.QueueNotification("FE.ExplorationManager.CapacityHint".Translate());
            MarkDirty();
        }
        if (ResearchManager.HasTowerInventoryMilestone && !ResearchManager.SacrificeUnlocked
            && !ActiveMarks.Any(m => m.Kind == ExplorationMarkKind.Sacrifice)) {
            AddMark(SelectReachablePlanet(3), ExplorationMarkKind.Sacrifice);
        }
        foreach (var pair in new[] {
            (T行星物流系统, GrowthTechnologyManager.PlanetaryInteractionTechnologyContentId),
            (T星际物流系统, GrowthTechnologyManager.InterstellarInteractionTechnologyContentId) }) {
            if (GameMain.history.TechUnlocked(pair.Item1) && !ResearchManager.IsContentUnlocked(pair.Item2)
                && !ActiveMarks.Any(m => m.ContentId == pair.Item2)) {
                AddMark(SelectReachablePlanet(ResearchManager.CurrentMatrixStage), ExplorationMarkKind.Infrastructure, 0, pair.Item2);
            }
        }
    }

    private static void EnsureUltimate(int branch) {
        bool ready = branch == 0 ? GrowthTechnologyManager.ExplorationLevel >= 10 : SubspaceExpansion.UltimateQuestReady;
        string id = branch == 0 ? "exploration" : "capacity";
        if ((!ready && UltimateSteps[branch] == 0) || UltimateSteps[branch] >= 6
            || ActiveMarks.Any(m => m.Kind == ExplorationMarkKind.Ultimate && m.ContentId == id)) { return; }
        ExplorationRecord previous = Records.LastOrDefault(r => r.Mark.Kind == ExplorationMarkKind.Ultimate && r.Mark.ContentId == id);
        PlanetData planet = SelectReachablePlanet(6, previous?.Mark.PlanetId ?? 0, UltimateSteps[branch] % 2 == 1);
        AddMark(planet, ExplorationMarkKind.Ultimate, UltimateSteps[branch], id);
    }

    private static PlanetData SelectReachablePlanet(int stage, int excludedPlanet = 0, bool preferNeighbor = false) {
        GalaxyData galaxy = GameMain.galaxy;
        if (stage < 3) { return galaxy.PlanetById(galaxy.birthPlanetId); }
        StarData star = galaxy.StarById(galaxy.birthStarId);
        if (stage >= 4 && GameMain.mainPlayer.mecha.thrusterLevel >= 3) {
            // 限出生星系与邻近可达星系，避免随机把任务抛到星区另一端。
            StarData other = galaxy.stars.Where(s => s.id != galaxy.birthStarId)
                .OrderBy(s => (s.uPosition - star.uPosition).sqrMagnitude).FirstOrDefault();
            if (other != null && (preferNeighbor || NextInt(2) == 1)) { star = other; }
        }
        PlanetData[] planets = star.planets.Where(p => p.type != EPlanetType.Gas && p.id != excludedPlanet).ToArray();
        return planets.Length > 0 ? planets[NextInt(planets.Length)] : galaxy.PlanetById(galaxy.birthPlanetId);
    }

    /// <summary>终极奖励生效后按星球上限及冷却补充普通广播，剧情与静默点不占名额。</summary>
    private static void ReplenishUltimateSignals() {
        if (!GrowthTechnologyManager.UltimateExploration) { return; }
        int missing = Math.Max(0, 5 - ActiveMarks.Count(m => !IsKeySignal(m)));
        for (int i = 0; i < missing; i++) { GenerateDynamicMark(); }
    }

    /// <summary>各星球最近一次普通广播生成刻，随存档及主机快照保存。</summary>
    private static readonly Dictionary<int, long> LastBroadcastTicks = [];

    /// <summary>尝试生成一个普通广播，剧情点不占容量；失败不产生积欠。</summary>
    /// <returns>本次是否实际生成。</returns>
    private static bool GenerateDynamicMark() {
        PlanetData planet = SelectReachablePlanet(ResearchManager.CurrentMatrixStage);
        if (planet == null || ActiveMarks.Count(m => m.PlanetId == planet.id && !IsKeySignal(m)) >= 5
            || LastBroadcastTicks.TryGetValue(planet.id, out long tick) && GameMain.gameTick - tick < 3600) {
            return false;
        }
        LastBroadcastTicks[planet.id] = GameMain.gameTick;
        ExplorationMark mark = AddMark(planet, ExplorationMarkKind.ResourceCache);
        int roll = NextInt(100);
        mark.Quality = ResearchManager.CurrentMatrixStage switch {
            <= 1 => 1,
            2 => roll < 70 ? 1 : 2,
            3 => roll < 50 ? 1 : roll < 85 ? 2 : 3,
            4 => roll < 30 ? 1 : roll < 75 ? 2 : 3,
            _ => roll < 15 ? 1 : roll < 60 ? 2 : 3,
        };
        return true;
    }

    /// <summary>在出生半球搜索首遗物，使用弧长约束探索距离和黑雾基地安全圈。</summary>
    /// <param name="planet">母星，工厂及基地数据必须已经就绪。</param>
    /// <param name="position">安全点的星球局部位置。</param>
    /// <returns>本次有界搜索是否找到安全位置。</returns>
    private static bool TryFindFirstRelicPosition(PlanetData planet, out Vector3 position) {
        position = Vector3.zero;
        PlanetFactory factory = planet?.factory;
        if (factory?.enemySystem?.bases == null || planet.birthPoint.sqrMagnitude < 1f) { return false; }
        float radius = planet.realRadius;
        float minAngle = 100f / radius;
        float maxAngle = Mathf.Min(160f / radius, Mathf.PI / 2f - 0.01f);
        if (minAngle > maxAngle) { return false; }
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, planet.birthPoint.normalized);
        float phase = (float)(GetRandDouble(ref randomSeed) * Math.PI * 2d);
        var bases = factory.enemySystem.bases;
        for (int attempt = 0; attempt < 512; attempt++) {
            float y = Mathf.Lerp(Mathf.Cos(minAngle), Mathf.Cos(maxAngle), (attempt + 0.5f) / 512f);
            float ring = Mathf.Sqrt(1f - y * y);
            float angle = phase + attempt * 2.39996323f;
            Vector3 direction = rotation * new Vector3(ring * Mathf.Cos(angle), y, ring * Mathf.Sin(angle));
            bool safe = true;
            for (int i = 1; i < bases.cursor; i++) {
                DFGBaseComponent b = bases[i];
                if (b == null || b.id != i || b.enemyId <= 0 || b.enemyId >= factory.enemyPool.Length) { continue; }
                ref EnemyData enemy = ref factory.enemyPool[b.enemyId];
                if (enemy.id != b.enemyId) { continue; }
                float dot = Vector3.Dot(direction, ((Vector3)enemy.pos).normalized);
                if (Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * radius < 200f) { safe = false; break; }
            }
            if (!safe) { continue; }
            position = direction * MarkRadius(planet);
            return true;
        }
        return false;
    }

    /// <summary>添加主机生成的动态或剧情点，首遗物使用预先验证的安全位置。</summary>
    /// <param name="planet">目标星球。</param>
    /// <param name="kind">点位类型。</param>
    /// <param name="number">剧情序号。</param>
    /// <param name="content">研究内容身份。</param>
    /// <param name="fixedPosition">已经通过专用规则验证的位置；为空时按普通点规则随机生成。</param>
    /// <returns>已经加入活动集合的点位。</returns>
    private static ExplorationMark AddMark(PlanetData planet, ExplorationMarkKind kind, int number = 0,
        string content = null, Vector3? fixedPosition = null) {
        if (nextMarkId == int.MaxValue) { throw new InvalidOperationException("探索点位身份已耗尽。"); }
        Vector3 position = Vector3.up;
        for (int attempt = 0; !fixedPosition.HasValue && attempt < 32; attempt++) {
            position = RandomDirection(ref randomSeed) * MarkRadius(planet);
            if (!ActiveMarks.Any(m => m.PlanetId == planet.id && (m.LocalPosition - position).magnitude < 65)
                && (GameMain.mainPlayer?.planetId != planet.id || ((Vector3)GameMain.mainPlayer.position - position).magnitude > 65)) { break; }
        }
        if (fixedPosition.HasValue) {
            position = fixedPosition.Value;
        } else if (GameMain.mainPlayer?.planetId == planet.id
                   && ((Vector3)GameMain.mainPlayer.position - position).magnitude <= 65) {
            position = -((Vector3)GameMain.mainPlayer.position).normalized * MarkRadius(planet);
        }
        var mark = new ExplorationMark { Id = nextMarkId++, PlanetId = planet.id, LocalPosition = position,
            Kind = kind, ItemId = number, ContentId = content };
        ActiveMarks.Add(mark); MarkDirty(); return mark;
    }

    private static ExplorationMark[] GetSilentMarks(int planetId) {
        if (SilentMarks.TryGetValue(planetId, out ExplorationMark[] cached)) { return cached; }
        PlanetData planet = GameMain.galaxy?.PlanetById(planetId);
        if (planet == null) { return []; }
        int[] counts = planet.type == EPlanetType.Gas ? [1, 1, 1]
            : planetId == GameMain.galaxy.birthPlanetId ? [20, 12, 8]
            : planet.star.id == GameMain.galaxy.birthStarId ? [12, 10, 8] : [6, 8, 10];
        int total = counts.Sum();
        uint seed = unchecked((uint)(GameMain.galaxy.seed * 397 ^ planetId * 7919)) | 1u;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, RandomDirection(ref seed));
        var marks = new ExplorationMark[total];
        for (int i = 0; i < total; i++) {
            // 黄金角均匀铺点，旋转由星球种子固定；层级交错分布以免集中在一侧。
            int spatialIndex = i * 17 % total;
            double y = 1d - 2d * (spatialIndex + 0.5d) / total;
            double radius = Math.Sqrt(1d - y * y);
            double angle = spatialIndex * 2.399963229728653d;
            Vector3 direction = rotation * new Vector3((float)(radius * Math.Cos(angle)), (float)y, (float)(radius * Math.Sin(angle)));
            marks[i] = new ExplorationMark { Id = -(planetId * 100 + i + 1), PlanetId = planetId,
                LocalPosition = direction * MarkRadius(planet), Kind = ExplorationMarkKind.SilentCache,
                Quality = i < counts[0] ? 1 : i < counts[0] + counts[1] ? 2 : 3 };
        }
        // 保留原网格、稳定身份和已回收事实，只移除多余低级点，避免旧档已探索地点移位。
        int lowCount = planet.type == EPlanetType.Gas ? 1
            : planetId == GameMain.galaxy.birthPlanetId ? 8
            : planet.star.id == GameMain.galaxy.birthStarId ? 7 : 6;
        marks = marks.Where((mark, index) => mark.Quality > 1 || index < lowCount).ToArray();
        SilentMarks[planetId] = marks;
        return marks;
    }

    private static ExplorationMark[] NearbyMarks(int planetId) {
        lock (Sync) {
            int quality = DetectableQuality;
            if (nearbyDirty || nearbyPlanet != planetId || nearbyQuality != quality) {
                nearbySnapshot = ActiveMarks.Where(m => m.PlanetId == planetId)
                    .Concat(FirstRelicClaimed ? GetSilentMarks(planetId).Where(m => m.Quality <= quality && !CollectedSilentMarks.Contains(m.Id)) : [])
                    .ToArray();
                nearbyPlanet = planetId; nearbyQuality = quality; nearbyDirty = false;
            }
            return nearbySnapshot;
        }
    }

    private static ExplorationMark FindMark(int id) {
        if (id >= 0) { return ActiveMarks.FirstOrDefault(m => m.Id == id); }
        int planetId = (-id - 1) / 100;
        return FirstRelicClaimed && !CollectedSilentMarks.Contains(id)
            ? GetSilentMarks(planetId).FirstOrDefault(m => m.Id == id && m.Quality <= DetectableQuality) : null;
    }

    /// <summary>以星球身份和空间距离判断到达，步行与默认悬浮高度的触发范围一致。</summary>
    /// <param name="mark">目标遗迹。</param>
    /// <param name="position">真实宇宙位置。</param>
    /// <param name="planetId">玩家所在星球。</param>
    /// <returns>是否可以回收或交付。</returns>
    private static bool IsWithinArrival(ExplorationMark mark, VectorLF3 position, int planetId) {
        PlanetData planet = GameMain.galaxy?.PlanetById(mark.PlanetId);
        if (planet == null || planetId != mark.PlanetId) { return false; }
        return GetSignalDistance(mark, planet, position) <= GetArrivalDistance(mark);
    }

    /// <summary>按悬空遗迹的真实空间距离定位，岩石星球与气态巨星共用，单位米。</summary>
    /// <param name="mark">目标遗迹。</param>
    /// <param name="planet">目标星球。</param>
    /// <param name="position">玩家宇宙位置。</param>
    /// <returns>与目标的距离，单位米。</returns>
    private static double GetSignalDistance(ExplorationMark mark, PlanetData planet, VectorLF3 position) {
        Vector3 local = (Vector3)Maths.QRotateLF(Quaternion.Inverse(planet.runtimeRotation), position - planet.uPosition);
        // 按当前高度规则重算遗迹位置，旧存档中贴地或高悬的点位同样适用。
        return (local - mark.LocalPosition.normalized * MarkRadius(planet)).magnitude;
    }

    /// <summary>返回空间命中半径，单位米；水平命中半径在地面与默认悬浮高度上相同。</summary>
    /// <param name="mark">目标遗迹。</param>
    /// <returns>命中半径，单位米。</returns>
    private static float GetArrivalDistance(ExplorationMark mark) {
        float horizontal = IsKeySignal(mark) ? KeyArrivalDistance : OrdinaryArrivalDistance;
        float vertical = DefaultHoverAltitude / 2f;
        return Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
    }

    private static bool IsPaymentMark(ExplorationMark mark) => mark.Kind == ExplorationMarkKind.Ultimate && mark.ItemId == 4;

    /// <summary>主机认领点位并结算一次奖励，将事实副本交给档案与提示。</summary>
    /// <param name="mark">已经通过认领范围与任务条件检查的点位。</param>
    private static void Claim(ExplorationMark mark) {
        if (mark.Kind == ExplorationMarkKind.SilentCache) {
            if (!CollectedSilentMarks.Add(mark.Id)) { return; }
        } else if (!ActiveMarks.Remove(mark)) { return; }
        if (mark.Kind is ExplorationMarkKind.FirstRelic or ExplorationMarkKind.PlanetDetector or ExplorationMarkKind.Story) {
            storyCompleted = Math.Max(storyCompleted, mark.ItemId);
            CompleteStory(mark.ItemId);
        } else if (mark.Kind == ExplorationMarkKind.Sacrifice) {
            ResearchManager.UnlockSacrifice();
        } else if (mark.Kind == ExplorationMarkKind.Infrastructure) {
            ResearchManager.RecoverInfrastructure(mark.ContentId);
        } else if (mark.Kind == ExplorationMarkKind.Ultimate) {
            int branch = mark.ContentId == "exploration" ? 0 : 1;
            UltimateSteps[branch] = mark.ItemId + 1;
            if (UltimateSteps[branch] == 6) {
                GameMain.history.UnlockTechUnlimited(branch == 0 ? TFE终极探索 : TFE终极仓储, true);
            }
        } else { AwardCache(mark); }
        var record = new ExplorationRecord { Mark = CloneMark(mark), Tick = GameMain.gameTick };
        Records.Add(record);
        recordsSnapshot = null;
        CivilizationModule.QueueRecoveryPopup(record.Mark);
        NebulaMultiplayerModAPI.BroadcastExplorationRecovery(record.Mark);
        MarkDirty();
        ReplenishUltimateSignals();
    }

    /// <summary>结算主线节点的固定奖励和研究线索。</summary>
    /// <param name="number">从1起的已完成主线序号。</param>
    private static void CompleteStory(int number) {
        if (number == 1) { CivilizationRecoveryManager.CompleteFirstRelicRecovery(); }
        if (number == 4) {
            DataCenterInventory.AddExplorationReward(IFE高级交互塔, 2);
            DataCenterInventory.AddExplorationReward(IFE通用原胚, 60);
            DataCenterInventory.AddExplorationReward(IFE记忆源点, 30);
        }
        if (number is >= 7 and <= 9) { DataCenterInventory.AddExplorationReward(IFE记忆源点, 12); }
    }

    /// <summary>抽取并发放普通遗迹奖励，将实际结果写回点位以供存档和本地化显示。</summary>
    /// <param name="mark">待结算的普通遗迹。</param>
    private static void AwardCache(ExplorationMark mark) {
        if (mark.Quality >= 2 && NextInt(100) < 25 && ResearchManager.TryRecoverPage(ref randomSeed, out string page)) {
            mark.ContentId = page;
            mark.ItemId = IFE残片;
            mark.Count = 0;
            return;
        }
        bool silent = mark.Kind == ExplorationMarkKind.SilentCache;
        double memoryChance = silent ? mark.Quality == 3 ? 0.25 : 0 : mark.Quality switch { 1 => 0.05, 2 => 0.30, _ => 0.60 };
        if (GetRandDouble(ref randomSeed) < memoryChance) {
            mark.ItemId = IFE记忆源点;
            mark.Count = silent ? 10 + NextInt(11) : mark.Quality switch { 1 => 1 + NextInt(2), 2 => 5 + NextInt(6), _ => 20 + NextInt(21) };
        } else if (ResearchManager.GetDirectionSnapshots().Sum(s => s.PendingContentCount) < 8 && NextInt(100) < 30) {
            string[] candidates = ResearchManager.GetPotentialContentIds();
            if (candidates.Length > 0) {
                string id = candidates[NextInt(candidates.Length)];
                int[] inputs = ResearchManager.GetContentDirectionItemIds(id);
                if (inputs.Length > 0 && ResearchManager.DiscoverContent(id, inputs[0], false)) {
                    mark.ContentId = id; mark.ItemId = inputs[0]; mark.Count = 0;
                    return;
                }
            }
        }
        if (mark.ItemId <= 0) {
            int[] scarce = HasProducedFirstMatrix()
                ? new[] { IFE通用原胚, IFE高级交互塔 }.Where(id => DataCenterInventory.GetModDataItemCount(id) < 300).ToArray()
                : [];
            if (scarce.Length > 0) {
                // 紧缺时必定补给，数量随机浮动，层级越高越多。
                mark.ItemId = scarce[NextInt(scarce.Length)];
                mark.Count = mark.ItemId == IFE通用原胚 ? 50 + NextInt(51) : mark.Quality * (2 + NextInt(3));
            } else {
                mark.ItemId = SelectBuildingReward(); mark.Count = mark.Quality;
            }
        }
        DataCenterInventory.AddExplorationReward(mark.ItemId, mark.Count);
    }

    /// <summary>优先选择当前阶段已开放、但玩家尚不能制造的建筑；全部可造时从已开放建筑中挑选。</summary>
    /// <returns>奖励建筑物品。</returns>
    private static int SelectBuildingReward() {
        int stage = ResearchManager.CurrentMatrixStage;
        int[] open = BuildingRewards.Where(r => r.MinStage <= stage).Select(r => r.ItemId).ToArray();
        int[] locked = open.Where(id => {
            RecipeProto recipe = LDB.items.Select(id)?.maincraft;
            return recipe == null || !GameMain.history.RecipeUnlocked(recipe.ID);
        }).ToArray();
        int[] pool = locked.Length > 0 ? locked : open;
        return pool[NextInt(pool.Length)];
    }

    private static long GetGenerationIntervalTicks() => (long)(10 * TicksPerMinute * Math.Pow(0.85, GrowthTechnologyManager.ExplorationLevel));
    /// <summary>遗迹位于星球半径加默认悬浮高度一半处，岩石星球与气态巨星通用。</summary>
    private static float MarkRadius(PlanetData planet) => planet.realRadius + DefaultHoverAltitude / 2f;
    private static Vector3 RandomDirection(ref uint seed) {
        double y = GetRandDouble(ref seed) * 2 - 1, angle = GetRandDouble(ref seed) * Math.PI * 2;
        double radius = Math.Sqrt(Math.Max(0, 1 - y * y));
        return new Vector3((float)(radius * Math.Cos(angle)), (float)y, (float)(radius * Math.Sin(angle)));
    }
    private static int NextInt(int count) => count <= 1 ? 0 : Math.Min(count - 1, (int)(GetRandDouble(ref randomSeed) * count));
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static void MarkDirty() { snapshotDirty = true; broadcastDirty = true; nearbyDirty = true; }
    private static int ReadCount(BinaryReader reader) {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumRecords) { throw new InvalidDataException("探索集合数量无效。"); }
        return count;
    }
    private static void WriteMark(BinaryWriter w, ExplorationMark m) {
        w.Write(m.Id); w.Write(m.PlanetId); w.Write(m.LocalPosition.x); w.Write(m.LocalPosition.y); w.Write(m.LocalPosition.z);
        w.Write((int)m.Kind); w.Write(m.ContentId ?? ""); w.Write(m.ItemId); w.Write(m.Count); w.Write(m.Quality);
    }
    private static ExplorationMark ReadMark(BinaryReader r) {
        var mark = new ExplorationMark { Id = r.ReadInt32(), PlanetId = r.ReadInt32(), LocalPosition = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            Kind = (ExplorationMarkKind)r.ReadInt32(), ContentId = r.ReadString(), ItemId = r.ReadInt32(), Count = r.ReadInt32(), Quality = r.ReadInt32() };
        if (!Finite(mark.LocalPosition.x) || !Finite(mark.LocalPosition.y) || !Finite(mark.LocalPosition.z)
            || mark.Quality < 1 || mark.Quality > 3 || mark.Count < 0 || !Enum.IsDefined(typeof(ExplorationMarkKind), mark.Kind)) { throw new InvalidDataException("探索点位数据无效。"); }
        return mark;
    }
    private static ExplorationMark CloneMark(ExplorationMark m) => new() { Id = m.Id, PlanetId = m.PlanetId, LocalPosition = m.LocalPosition,
        Kind = m.Kind, ContentId = m.ContentId, ItemId = m.ItemId, Count = m.Count, Quality = m.Quality };

    /// <summary>取得阶段主线标题。</summary>
    /// <param name="number">从1起的主线序号。</param>
    /// <returns>剧情标题。</returns>
    public static string GetStoryTitle(int number) => number switch {
        1 => "最初的回声".Translate(), 2 => "留下的容器".Translate(), 3 => "物质的另一种可能".Translate(), 4 => "矩阵中的生命".Translate(),
        5 => "维持".Translate(), 6 => "第一座温室".Translate(), 7 => "记忆的重量".Translate(), 8 => "不同的形态".Translate(), 9 => "尚未熄灭".Translate(),
        10 => "另一颗行星".Translate(), 11 => "远古航线".Translate(), 12 => "生命的规模".Translate(), 13 => "星际回声".Translate(), 14 => "同行者".Translate(),
        15 => "没有归途的记录".Translate(), 16 => "星图的边缘".Translate(), 17 => "漫长的守候".Translate(), 18 => "留给后来者".Translate(), _ => "远古记录".Translate(),
    };

    /// <summary>取得主线节点对应的文明片段与可行动提示。</summary>
    /// <param name="number">主线序号。</param>
    /// <returns>文明记录。</returns>
    public static string GetStoryNarrative(int number) => number switch {
        1 => "FE.ExplorationManager.Story1".Translate(),
        2 => "FE.ExplorationManager.Story2".Translate(),
        3 => "FE.ExplorationManager.Story3".Translate(),
        4 => "FE.ExplorationManager.Story4".Translate(),
        5 => "FE.ExplorationManager.Story5".Translate(),
        6 => "FE.ExplorationManager.Story6".Translate(),
        7 => "FE.ExplorationManager.Story7".Translate(),
        8 => "FE.ExplorationManager.Story8".Translate(),
        9 => "FE.ExplorationManager.Story9".Translate(),
        10 => "FE.ExplorationManager.Story10".Translate(),
        11 => "FE.ExplorationManager.Story11".Translate(),
        12 => "FE.ExplorationManager.Story12".Translate(),
        13 => "FE.ExplorationManager.Story13".Translate(),
        14 => "FE.ExplorationManager.Story14".Translate(),
        15 => "FE.ExplorationManager.Story15".Translate(),
        16 => "FE.ExplorationManager.Story16".Translate(),
        17 => "FE.ExplorationManager.Story17".Translate(),
        18 => "FE.ExplorationManager.Story18".Translate(),
        _ => "FE.ExplorationManager.StoryFallback".Translate(),
    };

    private static string GetUltimateNarrative(int branch, int step) => step switch {
        0 => "FE.ExplorationManager.UltimateStart".Translate(),
        1 or 2 or 3 => string.Format("FE.ExplorationManager.UltimateNode".Translate(), step),
        4 => "FE.ExplorationManager.UltimateDelivery".Translate(),
        _ => branch == 0 ? "FE.ExplorationManager.UltimateExploration".Translate() : "FE.ExplorationManager.UltimateCapacity".Translate(),
    };
}
