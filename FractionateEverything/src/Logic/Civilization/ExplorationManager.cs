using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
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
    /// <summary>文明记录或奖励描述。</summary>
    public string Text;
}

/// <summary>管理固定遗迹、阶段广播、剧情任务与主机认领结算。</summary>
public static class ExplorationManager {
    private const int MaximumRecords = 1000000;
    private const long TicksPerMinute = 3600;
    private const int UltimateMemoryCost = 2400;
    private const float DetectionRange = 180f;
    private const float OrdinaryArrivalDistance = 18f;
    private const float KeyArrivalDistance = 30f;
    private const float GasArrivalDistance = 40f;
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
    /// <summary>开局即可探测主线，首遗物后可使用普通探测。</summary>
    public static bool RelicDetectorUnlocked => GameMain.galaxy != null;
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
            ExplorationMarkKind.FirstRelic => "最初的回声",
            ExplorationMarkKind.PlanetDetector => "矩阵中的生命",
            ExplorationMarkKind.Story => GetStoryTitle(mark.ItemId),
            ExplorationMarkKind.Sacrifice => "过剩的生命",
            ExplorationMarkKind.Infrastructure => "远古物流接口",
            ExplorationMarkKind.Ultimate => mark.ContentId == "exploration" ? "长夜中的回声" : "没有尽头的库房",
            ExplorationMarkKind.SilentCache => $"{new[] { "", "普通", "精良", "珍贵" }[mark.Quality]}遗迹",
            _ => "远古广播",
        };
    }

    /// <summary>本地机甲已处于步行状态并接触地面，下降或跳跃均不算落地。</summary>
    public static bool LocalPlayerGrounded => GameMain.mainPlayer?.movementState == EMovementState.Walk
        && GameMain.mainPlayer.controller?.actionWalk?.isGrounded == true;

    /// <summary>描述已收到信号的来历与抵达方式，不提前公开回收奖励。</summary>
    /// <param name="mark">玩家已经收到的广播。</param>
    /// <returns>该信号的叙事与行动提示。</returns>
    public static string GetSignalDescription(ExplorationMark mark) {
        if (mark == null) { return string.Empty; }
        PlanetData planet = GameMain.galaxy?.PlanetById(mark.PlanetId);
        string lead = mark.Kind switch {
            ExplorationMarkKind.FirstRelic => "落地之后，伊卡洛斯收到了不属于任务通讯的脉冲。它从地表某处反复传来，强烈，却没有可以辨认的语言。也许有人把什么东西留在了这里。",
            ExplorationMarkKind.PlanetDetector => "第一枚电磁矩阵开始运转时，探测器忽然捕捉到了一段新的回应。那段脉冲与矩阵的闪光几乎重合，仿佛有什么沉睡已久的东西，正在试着醒来。",
            ExplorationMarkKind.Sacrifice => "成群的生命塔开始彼此呼应，一段此前无法辨认的广播变得清晰。它的末尾只有一个反复出现的符号，像是在指引某种归途。",
            ExplorationMarkKind.Infrastructure => "新建的物流网络收到了一次陌生的握手请求。对方使用着古老却完整的协议，地址指向一处远古接口。伊卡洛斯决定亲自去看看是谁在应答。",
            ExplorationMarkKind.Ultimate => mark.ItemId switch {
                0 => "常规信号逐渐安静下来，一段更深处的回声却穿过了噪声。它沿着古老的网络一遍遍寻找应答，似乎已经等了很久。伊卡洛斯记下了它指向的星球。",
                >= 1 and <= 3 => "刚刚恢复的记录指向另一处失联节点。广播里的间隔与上次一模一样，微弱的回应仍在继续。找到它，也许就能把这条断开的联系再接上一段。",
                4 => "几处节点已经重新连通，中央记录却留下了大片空白。接口请求更多源点，用来拼合失落的记忆。需要把它们带到遗迹附近，亲手完成交付。",
                _ => "最后一段坐标终于完整。远处的核心第一次发来了清晰的应答，声音里没有催促，只是重复着同一个地址。这一次，伊卡洛斯已经知道该去哪里。",
            },
            ExplorationMarkKind.Story => mark.ItemId switch {
                2 or 3 => "回收的容器中还留着另一段坐标。沿着同样的脉冲寻找，也许能找到这些装置曾经的使用者留下的记录。",
                <= 9 => "生命塔运转之后，一段新的记录从杂音里浮现。它似乎提到了同伴、供养，以及这些陌生生命曾经共同经历的日子。",
                <= 12 => "这一次，广播来自母星之外。熟悉的脉冲穿过星系传来，另一颗行星上，也许还留着相同的装置。",
                _ => "微弱的广播越过了星际空间。记录里断开的航线，终于又有了一处可以抵达的落点。出发前，先为伊卡洛斯备好足够的燃料。",
            },
            _ => "探测器收到了一段断断续续的远古广播。记录里没有说明那里还剩下什么，只有反复传来的脉冲。去信号源看看，或许能带回有用的东西。",
        };
        string arrival = planet?.type == EPlanetType.Gas
            ? "这颗星球没有可落脚的地面。沿着信号增强的方向，在低空接近目标。"
            : "到达后先让伊卡洛斯落地，再沿着信号增强的方向寻找。读数达到100%时，就已经进入遗迹的回收范围。";
        return $"{lead}\n\n目标星球：{planet?.displayName}\n\n{arrival}";
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
                Mark = CloneMark(r.Mark), Tick = r.Tick, Text = r.Text,
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
                AddMark(GameMain.galaxy.PlanetById(GameMain.galaxy.birthPlanetId), ExplorationMarkKind.FirstRelic, 1);
            }
        }
    }

    /// <summary>保留旧调用入口，探索不会创建或中断玩家导航。</summary>
    public static void ClearLegacyFirstRelicNavigation() { }

    /// <summary>Frag 结算立即生成动态补给，不受累计星球额度限制。</summary>
    /// <param name="count">需要生成的点数。</param>
    /// <param name="source">提示来源。</param>
    public static void AddExplorationPoints(int count, string source) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) { return; }
        lock (Sync) {
            for (int i = 0; i < count; i++) { GenerateDynamicMark(); }
        }
        CivilizationModule.QueueNotification($"{source}发现了新的远古广播。");
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

    /// <summary>主机按秒推进任务和广播，每六分之一秒处理短距离认领。</summary>
    /// <param name="gameTick">当前游戏刻。</param>
    public static void GameTick(long gameTick) {
        if (GameMain.mainPlayer == null || GameMain.galaxy == null || gameTick % 10 != 0) { return; }
        if (NebulaMultiplayerModAPI.IsClient) {
            foreach (ExplorationMark mark in NearbyMarks(GameMain.mainPlayer.planetId)) {
                if (!IsPaymentMark(mark) && IsWithinArrival(mark, GameMain.mainPlayer.uPosition,
                        GameMain.mainPlayer.planetId, LocalPlayerGrounded)) {
                    NebulaMultiplayerModAPI.RequestExplorationClaim(mark.Id);
                }
            }
            return;
        }
        lock (Sync) {
            if (gameTick % 60 == 0) {
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
                        GameMain.mainPlayer.planetId, LocalPlayerGrounded)) {
                    Claim(mark);
                }
            }
        }
    }

    /// <summary>验证连接玩家位置后结算认领，任务交付必须显式请求。</summary>
    /// <param name="markId">点位身份。</param>
    /// <param name="position">连接玩家的宇宙坐标。</param>
    /// <param name="planetId">连接玩家所在星球。</param>
    /// <param name="grounded">主机接收的最近移动状态是否已落地。</param>
    /// <returns>是否成功认领。</returns>
    public static bool TryClaimFromClient(int markId, VectorLF3 position, int planetId, bool grounded) {
        if (NebulaMultiplayerModAPI.IsClient || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) { return false; }
        lock (Sync) {
            ExplorationMark mark = FindMark(markId);
            if (mark == null || IsPaymentMark(mark) || !IsWithinArrival(mark, position, planetId, grounded)) { return false; }
            Claim(mark);
            return true;
        }
    }

    /// <summary>在交付遗迹附近显式提交 Memory，可分批并严格限制到剩余数量。</summary>
    /// <param name="markId">交付节点身份。</param>
    /// <param name="position">主机验证过的玩家宇宙位置。</param>
    /// <param name="planetId">玩家所在星球。</param>
    /// <param name="grounded">玩家是否已经落地。</param>
    /// <returns>本次提交数量。</returns>
    public static int SubmitTaskMemory(int markId, VectorLF3 position, int planetId, bool grounded) {
        if (NebulaMultiplayerModAPI.IsClient || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) { return 0; }
        lock (Sync) {
            ExplorationMark mark = ActiveMarks.FirstOrDefault(m => m.Id == markId);
            if (mark == null || !IsPaymentMark(mark) || !IsWithinArrival(mark, position, planetId, grounded)) { return 0; }
            int branch = mark.ContentId == "exploration" ? 0 : 1;
            int count = DataCenterInventory.TakeItemFromModData(IFE记忆源点, UltimateMemoryCost - UltimateInputs[branch], out int _);
            UltimateInputs[branch] += count;
            if (UltimateInputs[branch] >= UltimateMemoryCost) { Claim(mark); }
            MarkDirty();
            return count;
        }
    }

    /// <summary>计算地表距离强度，离开目标星球附近时不显示读数。</summary>
    /// <param name="mark">目标点。</param>
    /// <param name="playerPosition">玩家宇宙位置。</param>
    /// <param name="planetStrength">兼容页面入口，等于当前局部强度。</param>
    /// <param name="relicStrength">0～1最高可见局部强度。</param>
    /// <returns>目标是否处于探测范围。</returns>
    public static bool TryGetDetectorStrengths(ExplorationMark mark, VectorLF3 playerPosition,
        out float planetStrength, out float relicStrength) {
        planetStrength = relicStrength = 0f;
        PlanetData planet = GameMain.galaxy?.PlanetById(mark?.PlanetId ?? 0);
        if (planet == null || mark.Quality > DetectableQuality && !IsKeySignal(mark)) { return false; }
        VectorLF3 relative = playerPosition - planet.uPosition;
        if (Math.Abs(relative.magnitude - planet.realRadius) > 70) { return false; }
        double distance = GetSignalDistance(mark, planet, playerPosition);
        if (distance > DetectionRange) { return false; }
        float arrival = GetArrivalDistance(mark, planet);
        float raw = Mathf.Clamp01((DetectionRange - (float)distance) / (DetectionRange - arrival));
        float step = GrowthTechnologyManager.ExplorationSignalPrecisionPercent / 100f;
        relicStrength = distance <= arrival ? 1f : Math.Min(0.99f, Mathf.Floor(raw / step) * step);
        planetStrength = relicStrength;
        return true;
    }

    /// <summary>只取当前星球可探测点中的最高强度。</summary>
    /// <returns>0～1强度。</returns>
    public static float GetHighestStrength() {
        Player player = GameMain.mainPlayer;
        if (player == null) { return 0; }
        float strongest = 0;
        foreach (ExplorationMark mark in NearbyMarks(player.planetId)) {
            if (TryGetDetectorStrengths(mark, player.uPosition, out _, out float strength)) {
                strongest = Math.Max(strongest, strength);
            }
        }
        return strongest;
    }

    /// <summary>读取并清除主机快照脏标记。</summary>
    /// <returns>是否有待同步变化。</returns>
    public static bool TakeSnapshotDirty() { lock (Sync) { bool dirty = snapshotDirty; snapshotDirty = false; return dirty; } }

    /// <summary>保存当前格式的任务、实际奖励和动态点；静默几何可由种子重建。</summary>
    /// <param name="writer">输出流。</param>
    public static void Export(BinaryWriter writer) {
        lock (Sync) {
            writer.Write(1);
            writer.Write(nextMarkId); writer.Write(storyCompleted); writer.Write(nextTimedGenerationTick); writer.Write(randomSeed);
            writer.Write(capacityHintShown);
            for (int i = 0; i < 2; i++) { writer.Write(UltimateSteps[i]); writer.Write(UltimateInputs[i]); }
            writer.Write(ActiveMarks.Count);
            foreach (ExplorationMark mark in ActiveMarks) { WriteMark(writer, mark); }
            writer.Write(Records.Count);
            foreach (ExplorationRecord record in Records) { WriteMark(writer, record.Mark); writer.Write(record.Tick); writer.Write(record.Text ?? ""); }
        }
    }

    /// <summary>恢复当前格式状态，检查集合范围并重建静默认领集合。</summary>
    /// <param name="reader">存档或主机快照流。</param>
    public static void Import(BinaryReader reader) {
        lock (Sync) {
            IntoOtherSave();
            if (reader.ReadInt32() != 1) { throw new InvalidDataException("不支持的探索存档格式。"); }
            nextMarkId = reader.ReadInt32(); storyCompleted = reader.ReadInt32(); nextTimedGenerationTick = reader.ReadInt64(); randomSeed = reader.ReadUInt32();
            capacityHintShown = reader.ReadBoolean();
            for (int i = 0; i < 2; i++) { UltimateSteps[i] = Math.Max(0, Math.Min(6, reader.ReadInt32())); UltimateInputs[i] = Math.Max(0, Math.Min(UltimateMemoryCost, reader.ReadInt32())); }
            int count = ReadCount(reader);
            for (int i = 0; i < count; i++) { ActiveMarks.Add(ReadMark(reader)); }
            count = ReadCount(reader);
            for (int i = 0; i < count; i++) {
                var record = new ExplorationRecord { Mark = ReadMark(reader), Tick = reader.ReadInt64(), Text = reader.ReadString() };
                Records.Add(record);
                if (record.Mark.Kind == ExplorationMarkKind.SilentCache) { CollectedSilentMarks.Add(record.Mark.Id); }
            }
            if (nextMarkId <= 0 || storyCompleted < 0 || storyCompleted > 18) { throw new InvalidDataException("探索进度无效。"); }
            MarkDirty();
        }
    }

    /// <summary>切档时清除所有快照、几何和任务状态。</summary>
    public static void IntoOtherSave() {
        lock (Sync) {
            ActiveMarks.Clear(); SilentMarks.Clear(); CollectedSilentMarks.Clear(); Records.Clear();
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
        PlanetData planet = SelectReachablePlanet(stage, stage == 3 ? GameMain.galaxy.birthPlanetId : 0);
        AddMark(planet, next == 4 ? ExplorationMarkKind.PlanetDetector : ExplorationMarkKind.Story, next);
    }

    private static void EnsureMilestones() {
        if (!FirstRelicClaimed) { return; }
        if (!capacityHintShown && DataCenterInventory.GetItemCapacity(IFE高级交互塔) >= 1000) {
            capacityHintShown = true;
            CivilizationModule.QueueNotification("亚空间已经足够宽广。成群的生命塔，似乎正在彼此呼应。");
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
        int level = branch == 0 ? GrowthTechnologyManager.ExplorationLevel : GrowthTechnologyManager.SubspaceCapacityLevel;
        string id = branch == 0 ? "exploration" : "capacity";
        if (level < 10 || UltimateSteps[branch] >= 6 || ActiveMarks.Any(m => m.Kind == ExplorationMarkKind.Ultimate && m.ContentId == id)) { return; }
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

    /// <summary>终极奖励生效后立即补足普通广播，剧情与静默点不占名额。</summary>
    private static void ReplenishUltimateSignals() {
        if (!GrowthTechnologyManager.UltimateExploration) { return; }
        int missing = Math.Max(0, 5 - ActiveMarks.Count(m => !IsKeySignal(m)));
        for (int i = 0; i < missing; i++) { GenerateDynamicMark(); }
    }

    private static void GenerateDynamicMark() {
        PlanetData planet = SelectReachablePlanet(ResearchManager.CurrentMatrixStage);
        ExplorationMark mark = AddMark(planet, ExplorationMarkKind.ResourceCache);
        int roll = NextInt(100);
        mark.Quality = ResearchManager.CurrentMatrixStage switch {
            <= 1 => 1,
            2 => roll < 70 ? 1 : 2,
            3 => roll < 50 ? 1 : roll < 85 ? 2 : 3,
            4 => roll < 30 ? 1 : roll < 75 ? 2 : 3,
            _ => roll < 15 ? 1 : roll < 60 ? 2 : 3,
        };
    }

    private static ExplorationMark AddMark(PlanetData planet, ExplorationMarkKind kind, int number = 0, string content = null) {
        if (nextMarkId == int.MaxValue) { throw new InvalidOperationException("探索点位身份已耗尽。"); }
        Vector3 position = Vector3.up;
        for (int attempt = 0; attempt < 32; attempt++) {
            position = RandomDirection(ref randomSeed) * MarkRadius(planet);
            if (!ActiveMarks.Any(m => m.PlanetId == planet.id && (m.LocalPosition - position).magnitude < 65)
                && (GameMain.mainPlayer?.planetId != planet.id || ((Vector3)GameMain.mainPlayer.position - position).magnitude > 65)) { break; }
        }
        if (GameMain.mainPlayer?.planetId == planet.id
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

    /// <summary>用同一距离基准判断到达；岩石星球还须验证星球身份与实际落地状态。</summary>
    /// <param name="mark">目标遗迹。</param>
    /// <param name="position">真实宇宙位置。</param>
    /// <param name="planetId">玩家所在星球。</param>
    /// <param name="grounded">是否已接触地面。</param>
    /// <returns>是否可以回收或交付。</returns>
    private static bool IsWithinArrival(ExplorationMark mark, VectorLF3 position, int planetId, bool grounded) {
        PlanetData planet = GameMain.galaxy?.PlanetById(mark.PlanetId);
        if (planet == null || planetId != mark.PlanetId) { return false; }
        if (planet.type != EPlanetType.Gas
            && (!grounded || Math.Abs((position - planet.uPosition).magnitude - planet.realRadius) > 70)) { return false; }
        return GetSignalDistance(mark, planet, position) <= GetArrivalDistance(mark, planet);
    }

    /// <summary>岩石星球按地表弧长定位，气态巨星按悬空点的真实空间距离定位，单位米。</summary>
    /// <param name="mark">目标遗迹。</param>
    /// <param name="planet">目标星球。</param>
    /// <param name="position">玩家宇宙位置。</param>
    /// <returns>与目标的距离，单位米。</returns>
    private static double GetSignalDistance(ExplorationMark mark, PlanetData planet, VectorLF3 position) {
        Vector3 local = (Vector3)Maths.QRotateLF(Quaternion.Inverse(planet.runtimeRotation), position - planet.uPosition);
        return planet.type == EPlanetType.Gas ? (local - mark.LocalPosition).magnitude
            : Vector3.Angle(local, mark.LocalPosition) * Math.PI / 180d * planet.realRadius;
    }

    /// <summary>返回各类遗迹的命中半径，单位米；强度满值与认领共用此边界。</summary>
    /// <param name="mark">目标遗迹。</param>
    /// <param name="planet">目标星球。</param>
    /// <returns>命中半径，单位米。</returns>
    private static float GetArrivalDistance(ExplorationMark mark, PlanetData planet) =>
        planet.type == EPlanetType.Gas ? GasArrivalDistance : IsKeySignal(mark) ? KeyArrivalDistance : OrdinaryArrivalDistance;

    private static bool IsPaymentMark(ExplorationMark mark) => mark.Kind == ExplorationMarkKind.Ultimate && mark.ItemId == 4;

    private static void Claim(ExplorationMark mark) {
        if (mark.Kind == ExplorationMarkKind.SilentCache) {
            if (!CollectedSilentMarks.Add(mark.Id)) { return; }
        } else if (!ActiveMarks.Remove(mark)) { return; }
        string text;
        if (mark.Kind is ExplorationMarkKind.FirstRelic or ExplorationMarkKind.PlanetDetector or ExplorationMarkKind.Story) {
            storyCompleted = Math.Max(storyCompleted, mark.ItemId);
            text = CompleteStory(mark.ItemId);
        } else if (mark.Kind == ExplorationMarkKind.Sacrifice) {
            ResearchManager.UnlockSacrifice();
            text = "记录者没有称它们为机器。过剩的生命将化作记忆，为仍在工作的同伴留下力量。塔体献祭已恢复。";
        } else if (mark.Kind == ExplorationMarkKind.Infrastructure) {
            ResearchManager.RecoverInfrastructure(mark.ContentId);
            text = "古老的接口仍在等待接入。物流交互科技现已显现。";
        } else if (mark.Kind == ExplorationMarkKind.Ultimate) {
            int branch = mark.ContentId == "exploration" ? 0 : 1;
            UltimateSteps[branch] = mark.ItemId + 1;
            text = GetUltimateNarrative(branch, mark.ItemId);
            if (UltimateSteps[branch] == 6) {
                GameMain.history.UnlockTechUnlimited(branch == 0 ? TFE终极探索 : TFE终极仓储, true);
            }
        } else { text = AwardCache(mark); }
        Records.Add(new ExplorationRecord { Mark = CloneMark(mark), Tick = GameMain.gameTick, Text = text });
        recordsSnapshot = null;
        CivilizationModule.QueueRecoveryPopup(GetSignalName(mark), text);
        NebulaMultiplayerModAPI.BroadcastExplorationRecovery(GetSignalName(mark), text);
        MarkDirty();
        ReplenishUltimateSignals();
    }

    private static string CompleteStory(int number) {
        if (number == 1) { CivilizationRecoveryManager.CompleteFirstRelicRecovery(); }
        if (number == 4) {
            DataCenterInventory.AddExplorationReward(IFE高级交互塔, 2);
            DataCenterInventory.AddExplorationReward(IFE通用原胚, 30);
            DataCenterInventory.AddExplorationReward(IFE记忆源点, 30);
        }
        if (number is >= 7 and <= 9) { DataCenterInventory.AddExplorationReward(IFE记忆源点, 12); }
        if (number == 5) { ResearchManager.DiscoverContent($"interaction-culture:{IFE高级矿物塔}", IFE通用原胚, false); }
        if (number == 8) { ResearchManager.DiscoverContent($"interaction-culture:{IFE高级转化塔}", IFE通用原胚, false); }
        return GetStoryNarrative(number);
    }

    private static string AwardCache(ExplorationMark mark) {
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
                    return $"回收了以{LDB.items.Select(mark.ItemId)?.name}为样本的未知记录。";
                }
            }
        }
        if (mark.ItemId <= 0) {
            int[] support = [IFE通用原胚, IFE高级交互塔];
            int selected = support[NextInt(2)];
            if (HasProducedFirstMatrix() && DataCenterInventory.GetModDataItemCount(selected) < 300 && NextInt(100) < 20) {
                mark.ItemId = selected; mark.Count = selected == IFE通用原胚 ? 10 * mark.Quality : mark.Quality;
            } else {
                PlanetData planet = GameMain.galaxy.PlanetById(mark.PlanetId);
                int[] resources = planet.type == EPlanetType.Gas && planet.gasItems?.Length > 0
                    ? planet.gasItems : [I铁矿, I铜矿, I石矿, I煤矿];
                mark.ItemId = resources[NextInt(resources.Length)]; mark.Count = 50 * mark.Quality;
            }
        }
        DataCenterInventory.AddExplorationReward(mark.ItemId, mark.Count);
        return $"探索取得：{LDB.items.Select(mark.ItemId)?.name} × {mark.Count}";
    }

    private static long GetGenerationIntervalTicks() => (long)(10 * TicksPerMinute * Math.Pow(0.85, GrowthTechnologyManager.ExplorationLevel));
    private static float MarkRadius(PlanetData planet) => planet.realRadius + (planet.type == EPlanetType.Gas ? 25f : 1.5f);
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
        1 => "最初的回声", 2 => "留下的容器", 3 => "物质的另一种可能", 4 => "矩阵中的生命",
        5 => "维持", 6 => "第一座温室", 7 => "记忆的重量", 8 => "不同的形态", 9 => "尚未熄灭",
        10 => "另一颗行星", 11 => "远古航线", 12 => "生命的规模", 13 => "星际回声", 14 => "同行者",
        15 => "没有归途的记录", 16 => "星图的边缘", 17 => "漫长的守候", 18 => "留给后来者", _ => "远古记录",
    };

    /// <summary>取得主线节点对应的文明片段与可行动提示。</summary>
    /// <param name="number">主线序号。</param>
    /// <returns>文明记录。</returns>
    public static string GetStoryNarrative(int number) => number switch {
        1 => "容器的封口已经磨损，里面的装置却保存得很完整。伊卡洛斯取出了四座基础矿物塔、两座基础解析塔，以及几份标着铁、铜、石、煤样本的残缺记录。\n\n回收物资已送入刚刚接通的亚空间。按 [Shift + F] 打开面板，可以查看它们和留下的线索。",
        2 => "容器底部夹着一块刻满细线的薄片，旁边嵌着一粒普通矿石。伊卡洛斯把薄片与先前的记录放在一起，缺失的部分恰好有了轮廓。\n\n这些矿石或许就是记录的钥匙。将已有线索的样本送入基础解析塔，看看它会作出怎样的回应。",
        3 => "恢复出的第一份记录，终于让那些陌生的装置有了可以理解的用途。伊卡洛斯沿着其中的结构试了一次，塔内传来了清晰而有节奏的声响。\n\n容器里还有几页无法辨认的残片。他将它们收好，准备等新的线索出现后再来阅读。",
        4 => "矩阵的闪光透过容器外壳时，里面的装置轻轻动了一下。它们的表面带着细小裂纹，却仍在缓慢地开合，像是在等待食物。\n\n两座高级交互塔、三十个原胚和三十份源点已送入亚空间。先为这些生命塔接上一条矩阵供给，再尝试让它们工作。",
        5 => "记录者在停工清单旁重复写下了一行字：休息时也要保留供养。伊卡洛斯想起塔壳上的裂纹，再次检查了矩阵传送带。\n\n随记录回收的原胚线索已经归档。这些生命也许还保存着另一种生长的可能。",
        6 => "旧温室里只剩下排列整齐的底座。靠近中央的记录仪时，伊卡洛斯听见了与生命塔相似的脉冲，一声接着一声，仿佛这里曾经十分热闹。\n\n解析原胚留下的记录，再到高级交互塔里选择已经恢复的配方。也许这片安静还能被新的生命打破。",
        7 => "打开容器时，十二份源点沿内壁缓缓亮起。记录者把它们逐一编号，旁边写着那些曾经共同工作的生命。\n\n源点已送入亚空间。把它们交给基础解析塔，或许能继续拼合刚刚能够辨认的阶段记录。",
        8 => "这份记录画着几种形态不同的装置。边角已经破损，只有共同使用的接口仍能辨认。它们曾经各自承担什么工作，还需要样本来回答。\n\n新的原胚线索已经归档，十二份源点也已送入亚空间。",
        9 => "记录的末尾留着一段没有写完的话。伊卡洛斯无法辨认其中的名字，却看清了指向母星之外的航迹。\n\n容器里保存的十二份源点已回收。等准备好远行所需的物资，再去看看那颗星球上还留下了什么。",
        10 => "另一颗行星上的遗迹使用着相同的接口。插入记录时，装置几乎立刻作出了回应。熟悉的脉冲在陌生的天空下响起，让伊卡洛斯第一次意识到，这套工厂曾经延伸得多么远。",
        11 => "航线记录里没有货船的班次，只有一条条跨越距离的取货应答。某一端曾请求一枚零件，另一端很快就交出了它。\n\n远古接口留下的结构已经清晰。恢复它，也许就能让分散的产线重新使用同一座库房。",
        12 => "这里的容器被排成了整齐的阵列。记录者逐座清点着停止工作的塔，却始终没有把它们写进废料清单。\n\n在最后一页，他留下了一行注记：还要找个办法，把它们经历过的事保存下来。",
        13 => "曲速航迹的尽头，中继发出了熟悉的脉冲。伊卡洛斯送出试探的回应，一条中断已久的连接重新亮了起来。\n\n两颗相距遥远的星球，曾经共享同一条生产线。如今，记录终于又有人继续读下去了。",
        14 => "这份清单把每座塔都称作同行者。记录者记下它们的供养、修复和产出，也记下了某一天又有新的生命加入。\n\n伊卡洛斯把清单留在产线记录旁。那些看似相同的装置，曾经被人一座座记住。",
        15 => "最后一班运输迟迟没有回航。记录者关闭了远端接口，重新分配手边的矩阵，又将几条快要断供的传送带接了起来。\n\n此后的记录越来越短，供养清单却一直没有中断。",
        16 => "星图在边缘断开了，库房索引仍指向更远的地址。伊卡洛斯试着发出一次请求，只收到漫长的静默。\n\n那些连接还没有彻底消失。等能够读懂更深处的记录，也许就能再次找到它们。",
        17 => "中继反复发送着同一段请求。经过如此漫长的时间，它仍在等待足够的源点，等待有人把破碎的记录重新拼合。\n\n伊卡洛斯保存了这次回应。下一次抵达时，他希望能带来一些真正有用的东西。",
        18 => "记录者把最后的访问权限留在了容器中。末尾没有署名，只有一句简短的叮嘱：如果还有人来到这里，请让它们继续活着。\n\n伊卡洛斯收好记录。探索网络和亚空间库房的深处，还有一些沉默的核心等待重新接通。",
        _ => "远古记录中的微光仍在闪烁。",
    };

    private static string GetUltimateNarrative(int branch, int step) => step switch {
        0 => "常规协议已经完整，深处却传来另一段回声。三个失联节点指向同一个中心。",
        1 or 2 or 3 => $"第{step}处远古节点重新回应。记录指出了下一处连接。",
        4 => "交付的源点拼合出完整索引，核心的位置终于清晰。",
        _ => branch == 0 ? "最后一座中继苏醒了。此后每一次回收，都将带来新的回应。" : "库房的边界消失在视野之外。你接通了完整的亚空间。",
    };
}
