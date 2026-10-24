using System;
using System.IO;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>研究域的矩阵阶段恢复与分钟献祭状态。</summary>
public static partial class ResearchManager {
    private static readonly int[] StageMatrices = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵];
    private static readonly int[] StageMemoryBudgets = [10, 40, 120, 300, 600, 1200];
    private static readonly int[] SampleBudgets = [100, 200, 300, 1000, 3000, 8000, 20000];
    /// <summary>源点阶段恢复的逐次成功概率；源点不损毁，失败继续流回。</summary>
    private const float MemoryAnalysisSuccessRatio = 0.02f;
    private static readonly long[] StageMemoryInputs = new long[6];
    private static bool sacrificeUnlocked;
    private static long memoryMinute = -1;
    private static long minuteMemoryCount;
    private static float memoryBoost;

    /// <summary>当前已取得研究资格的矩阵阶段，0 表示尚未解锁电磁矩阵。</summary>
    public static int CurrentMatrixStage {
        get {
            for (int i = StageMatrices.Length - 1; i >= 0; i--) {
                if (GameMain.history?.ItemUnlocked(StageMatrices[i]) == true) {
                    return i + 1;
                }
            }
            return 0;
        }
    }

    /// <summary>上一完整分钟的Memory投入为三类高级塔提供的统一相对增益。</summary>
    public static float MemorySuccessBoost => Volatile.Read(ref memoryBoost);

    /// <summary>当前完整分钟窗口内已实际投入的献祭Memory。</summary>
    public static long CurrentMemoryOffering { get { lock (Sync) { return minuteMemoryCount; } } }

    /// <summary>千塔主线是否已开放塔体献祭。</summary>
    public static bool SacrificeUnlocked => Volatile.Read(ref sacrificeUnlocked);

    /// <summary>是否有任意高级塔达到过千塔库存门槛。</summary>
    public static bool HasTowerInventoryMilestone {
        get { lock (Sync) { return AdvancedTowerInventoryMilestones.Count > 0; } }
    }

    /// <summary>返回某矩阵阶段的稳定恢复内容身份。</summary>
    /// <param name="stage">矩阵阶段，1～6。</param>
    /// <returns>阶段恢复的稳定身份。</returns>
    public static string StageContentId(int stage) => $"stage:{stage}";

    /// <summary>返回阶段恢复所用矩阵的物品身份。</summary>
    /// <param name="stage">矩阵阶段，1～6。</param>
    /// <returns>矩阵物品身份。</returns>
    public static int GetStageMatrix(int stage) => StageMatrices[Math.Max(0, Math.Min(5, stage - 1))];

    /// <summary>返回阶段恢复的内部成功投入需求，不向玩家公开数量。</summary>
    /// <param name="stage">矩阵阶段，1～6。</param>
    /// <returns>完成恢复所需的成功源点数量。</returns>
    private static int GetStageMemoryBudget(int stage) => StageMemoryBudgets[Math.Max(0, Math.Min(5, stage - 1))];

    /// <summary>挑战奖励为生成阶段预算的十分之一且至少三件；珍贵挑战翻倍，不按解析概率折价。</summary>
    internal static int GetChallengeMemoryReward(int stage, int quality) => Math.Max(3, GetStageMemoryBudget(stage) / 10) * (quality >= 3 ? 2 : 1);

    /// <summary>返回当前阶段的恢复比例，界面只公开进度，不公开内部投入需求。</summary>
    /// <param name="stage">矩阵阶段，1～6。</param>
    /// <returns>恢复比例，范围0～1。</returns>
    public static float GetStageRecoveryProgress(int stage) {
        lock (Sync) {
            int index = Math.Max(0, Math.Min(5, stage - 1));
            return UnlockedContents.Contains(StageContentId(stage)) ? 1f
                : Math.Min(1f, StageMemoryInputs[index] / (float)StageMemoryBudgets[index]);
        }
    }

    /// <summary>由千塔剧情节点开放献祭，库存里程碑保留各类独立资格。</summary>
    public static void UnlockSacrifice() {
        lock (Sync) {
            sacrificeUnlocked = true;
            snapshotDirty = true;
        }
    }

    /// <summary>由物流地点恢复直接授予科技方向，不收取 Memory。</summary>
    /// <param name="contentId">物流科技的稳定内容身份。</param>
    public static void RecoverInfrastructure(string contentId) {
        lock (Sync) {
            if (contentId is GrowthTechnologyManager.PlanetaryInteractionTechnologyContentId
                or GrowthTechnologyManager.InterstellarInteractionTechnologyContentId) {
                UnlockWithoutReward(contentId);
                snapshotDirty = true;
            }
        }
    }

    /// <summary>按稀有内容自身的阶段取得样本预算，特质使用独立预算。</summary>
    /// <param name="contentId">配方或特质身份。</param>
    /// <returns>完成一份内容所需的期望成功投入数；流回和损毁不计入进度。</returns>
    public static int GetExpectedSampleCount(string contentId) {
        if (IsTraitContent(contentId)) { return 20000; }
        int stage = ContentDefinitions.TryGetValue(contentId, out ResearchContentDefinition definition)
            ? definition.Tier : 0;
        return SampleBudgets[Math.Max(0, Math.Min(6, stage))];
    }

    /// <summary>每秒在主线程开放当前阶段的 Memory 解析资格。</summary>
    private static void RefreshStageResearch() {
        if (!ExplorationManager.FirstRelicClaimed) { return; }
        for (int stage = 1; stage <= CurrentMatrixStage; stage++) {
            DiscoverContent(StageContentId(stage), IFE记忆源点, false);
        }
    }

    /// <summary>取得最低未恢复且已开放的源点阶段，与解析窗口共用资格判断。</summary>
    /// <returns>矩阵阶段1～6；没有待恢复阶段时为0。</returns>
    private static int GetPendingMemoryStage() {
        for (int stage = 1; stage <= CurrentMatrixStage; stage++) {
            if (!IsContentUnlocked(StageContentId(stage))) { return stage; }
        }
        return 0;
    }

    /// <summary>按概率恢复阶段资料，阶段完成后的多余成功源点回流。</summary>
    /// <param name="count">本批通过数量。</param>
    /// <param name="seed">分馏随机种子。</param>
    /// <param name="analysisSuccessCount">实际计入阶段进度或分钟献祭的源点数。</param>
    /// <returns>实际消耗的源点数；献祭只登记被处理的件数。</returns>
    private static int ProcessMemory(int count, ref uint seed, out int analysisSuccessCount) {
        analysisSuccessCount = 0;
        if (NebulaMultiplayerModAPI.IsClient) { return 0; }
        lock (Sync) {
            int stage = GetPendingMemoryStage();
            if (stage > 0) {
                long remaining = Math.Max(0L, GetStageMemoryBudget(stage) - StageMemoryInputs[stage - 1]);
                int successes = WorldviewRecipe.RollExpected(ref seed, count * (double)MemoryAnalysisSuccessRatio);
                analysisSuccessCount = (int)Math.Min(remaining, successes);
                StageMemoryInputs[stage - 1] += analysisSuccessCount;
                if (StageMemoryInputs[stage - 1] >= GetStageMemoryBudget(stage)) {
                    // 阶段内容没有实体原型奖励，完成后只开放本阶段科技资格。
                    _ = UnlockContent(StageContentId(stage));
                    QueueNotification("FE.ResearchProgression.StageRestored".Translate());
                }
                snapshotDirty |= analysisSuccessCount > 0;
                return analysisSuccessCount;
            }
            if (!SacrificeUnlocked) { return 0; }
            AdvanceMemoryMinute(GameMain.gameTick / 3600L);
            analysisSuccessCount = WorldviewRecipe.RollExpected(ref seed, count * AnalysisProcessingRatio);
            minuteMemoryCount = Math.Min(long.MaxValue - analysisSuccessCount, minuteMemoryCount) + analysisSuccessCount;
            snapshotDirty |= analysisSuccessCount > 0;
            return analysisSuccessCount;
        }
    }

    /// <summary>完整分钟切换时发布上一个窗口的增益；跨过空窗口时增益为零。</summary>
    /// <param name="minute">当前游戏分钟。</param>
    private static void AdvanceMemoryMinute(long minute) {
        if (memoryMinute == minute) { return; }
        if (memoryMinute == minute - 1 && minuteMemoryCount >= 50 && SacrificeUnlocked) {
            RecoverOfferingLegendaryUnsafe();
        }
        memoryBoost = memoryMinute == minute - 1
            ? (float)Math.Min(0.75d, 0.05d * Math.Floor(Math.Sqrt(minuteMemoryCount))) : 0f;
        minuteMemoryCount = 0;
        memoryMinute = minute;
        snapshotDirty = true;
    }

    /// <summary>写入任务资格、阶段累计投入和当前分钟窗口。</summary>
    /// <param name="writer">存档或主机快照输出。</param>
    private static void WriteProgression(BinaryWriter writer) {
        writer.WriteBlocks(
            ("SacrificeUnlocked", w => w.Write(sacrificeUnlocked)),
            ("MemoryMinute", w => w.Write(memoryMinute)),
            ("MinuteMemoryCount", w => w.Write(minuteMemoryCount)),
            ("MemoryBoost", w => w.Write(memoryBoost)),
            ("StageMemoryInputs", w => w.WriteList(StageMatrices, (item, id) => {
                item.Write(id);
                item.Write(StageMemoryInputs[Array.IndexOf(StageMatrices, id)]);
            }))
        );
    }

    /// <summary>按独立字段和矩阵身份恢复成长状态，缺项保持默认值。</summary>
    private static void ReadProgression(BinaryReader reader) {
        ResetProgression();
        reader.ReadBlocks(
            ("SacrificeUnlocked", r => sacrificeUnlocked = r.ReadBoolean()),
            ("MemoryMinute", r => memoryMinute = r.ReadInt64()),
            ("MinuteMemoryCount", r => minuteMemoryCount = Math.Max(0L, r.ReadInt64())),
            ("MemoryBoost", r => {
                float boost = r.ReadSingle();
                memoryBoost = float.IsNaN(boost) ? 0f : Math.Max(0f, Math.Min(0.75f, boost));
            }),
            ("StageMemoryInputs", r => r.ReadList(item => {
                int index = Array.IndexOf(StageMatrices, item.ReadInt32());
                long count = Math.Max(0L, item.ReadInt64());
                if (index >= 0) { StageMemoryInputs[index] = count; }
            }, 12000))
        );
    }

    /// <summary>读取已有的顺序布局成长块。</summary>
    private static void ReadLegacyProgression(BinaryReader reader) {
        sacrificeUnlocked = reader.ReadBoolean();
        memoryMinute = reader.ReadInt64();
        minuteMemoryCount = Math.Max(0L, reader.ReadInt64());
        float boost = reader.ReadSingle();
        memoryBoost = float.IsNaN(boost) ? 0f : Math.Max(0f, Math.Min(0.75f, boost));
        for (int i = 0; i < StageMemoryInputs.Length; i++) {
            StageMemoryInputs[i] = Math.Max(0L, reader.ReadInt64());
        }
    }

    /// <summary>切档或导入前清空当前存档的成长状态。</summary>
    private static void ResetProgression() {
        sacrificeUnlocked = false;
        memoryMinute = -1;
        minuteMemoryCount = 0;
        memoryBoost = 0;
        Array.Clear(StageMemoryInputs, 0, StageMemoryInputs.Length);
    }
}
