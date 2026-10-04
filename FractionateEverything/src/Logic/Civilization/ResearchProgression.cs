using System;
using System.IO;
using System.Linq;
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

    /// <summary>返回阶段恢复的期望 Memory 预算。</summary>
    /// <param name="stage">矩阵阶段，1～6。</param>
    /// <returns>长期期望投入量。</returns>
    public static int GetStageMemoryBudget(int stage) => StageMemoryBudgets[Math.Max(0, Math.Min(5, stage - 1))];

    /// <summary>获取已实际投入某阶段的 Memory 数量，区别于期望成本。</summary>
    /// <param name="stage">矩阵阶段，1～6。</param>
    /// <returns>累计实际投入量。</returns>
    public static long GetStageMemoryInputs(int stage) {
        lock (Sync) { return StageMemoryInputs[Math.Max(0, Math.Min(5, stage - 1))]; }
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

    /// <summary>按内容自身的阶段取得普通样本预算，特质和塔体使用独立预算。</summary>
    /// <param name="contentId">配方或特质身份。</param>
    /// <returns>完成一份内容所需的期望成功消耗数；失败回流不计入预算。</returns>
    public static int GetExpectedSampleCount(string contentId) {
        if (IsTraitContent(contentId)) { return 20000; }
        WorldviewRecipe recipe = RecipeManager.GetRecipe(contentId);
        if (recipe?.Kind == WorldviewRecipeKind.EmbryoBreeding) { return 200; }
        if (recipe?.Kind == WorldviewRecipeKind.TowerCultivation) { return 50; }
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

    /// <summary>将阶段内容注册到目录，物流方向由任务直接恢复。</summary>
    private static void AddStageContentDefinitions() {
        AddTechnologyContentDefinitions();
        for (int stage = 1; stage <= 6; stage++) {
            AddTechnologyContent(StageContentId(stage),
                string.Format("{0}文明记录".Translate(), LDB.items.Select(GetStageMatrix(stage))?.name), stage);
        }
    }

    /// <summary>处理 Memory：阶段按累计进度恢复，全部恢复后提供下一分钟增益。</summary>
    /// <param name="count">本批实际输入数量。</param>
    /// <returns>是否消耗输入。</returns>
    private static bool ProcessMemory(int count) {
        if (NebulaMultiplayerModAPI.IsClient) { return false; }
        lock (Sync) {
            for (int stage = 1; stage <= CurrentMatrixStage; stage++) {
                string id = StageContentId(stage);
                if (UnlockedContents.Contains(id)) { continue; }
                StageMemoryInputs[stage - 1] = Math.Min(long.MaxValue - count, StageMemoryInputs[stage - 1]) + count;
                // 与样本解析一致使用确定进度，避免几何分布让少数玩家投入数倍预算。
                if (StageMemoryInputs[stage - 1] >= GetStageMemoryBudget(stage)) {
                    UnlockContent(id);
                    QueueNotification("FE.ResearchProgression.StageRestored".Translate());
                }
                snapshotDirty = true;
                return true;
            }
            if (!SacrificeUnlocked) { return false; }
            AdvanceMemoryMinute(GameMain.gameTick / 3600L);
            minuteMemoryCount = Math.Min(long.MaxValue - count, minuteMemoryCount) + count;
            snapshotDirty = true;
            return true;
        }
    }

    /// <summary>完整分钟切换时发布上一个窗口的增益；跨过空窗口时增益为零。</summary>
    /// <param name="minute">当前游戏分钟。</param>
    private static void AdvanceMemoryMinute(long minute) {
        if (memoryMinute == minute) { return; }
        memoryBoost = memoryMinute == minute - 1
            ? (float)Math.Min(0.75d, 0.05d * Math.Floor(Math.Sqrt(minuteMemoryCount))) : 0f;
        minuteMemoryCount = 0;
        memoryMinute = minute;
        snapshotDirty = true;
    }

    /// <summary>写入任务资格、阶段累计投入和当前分钟窗口。</summary>
    /// <param name="writer">存档或主机快照输出。</param>
    private static void WriteProgression(BinaryWriter writer) {
        writer.Write(sacrificeUnlocked);
        writer.Write(memoryMinute);
        writer.Write(minuteMemoryCount);
        writer.Write(memoryBoost);
        foreach (long count in StageMemoryInputs) { writer.Write(count); }
    }

    /// <summary>恢复当前格式的成长状态，限制非有限和负数输入。</summary>
    /// <param name="reader">存档或主机快照输入。</param>
    private static void ReadProgression(BinaryReader reader) {
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
