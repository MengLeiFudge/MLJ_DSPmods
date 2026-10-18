using System.Collections.Generic;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 汇总随机研究、探索信号和确定性成长科技的生命周期。
/// </summary>
public static class CivilizationModule {
    private static readonly object NotificationSync = new();
    private static readonly Queue<string> PendingNotifications = [];
    /// <summary>已结算的回收结果，在主线程逐条显示；由 NotificationSync 保护。</summary>
    private static readonly Queue<ExplorationMark> PendingRecoveries = [];
    private static long lastProcessedGameTick = long.MinValue;
    /// <summary>上次执行按秒逻辑的游戏秒；低帧率时一帧可能推进多刻，不能用 gameTick % 60 判断整秒。</summary>
    private static long lastProcessedSecond = long.MinValue;
    /// <summary>上次执行探索认领的 10 刻（六分之一秒）序号，与按秒逻辑同理按序号变化触发。</summary>
    private static long lastProcessedClaimSlot = long.MinValue;
    private static bool civilizationSnapshotPending;

    /// <summary>
    /// 完成数据加载后的运行缓存初始化。
    /// </summary>
    public static void Initialize() {
        GrowthTechnologyManager.SyncRuntimeEffects();
    }

    /// <summary>
    /// 在 Unity 主线程推进探索、同步成长效果并发送联机状态。
    /// </summary>
    public static void Tick() {
        if (!GameMain.isRunning || GameMain.mainPlayer == null) {
            return;
        }

        AdvancedRecipeCatalog.SyncUnlocks();
        FlushRecoveryPopups();
        long gameTick = GameMain.gameTick;
        if (gameTick == lastProcessedGameTick) {
            return;
        }
        lastProcessedGameTick = gameTick;

        // Update 与逻辑帧不同步：帧率低于 60 时两次 Update 之间 gameTick 可能跳过整秒刻，按序号变化判断。
        long second = gameTick / 60L;
        bool secondBoundary = second != lastProcessedSecond;
        lastProcessedSecond = second;
        long claimSlot = gameTick / 10L;
        if (claimSlot != lastProcessedClaimSlot) {
            lastProcessedClaimSlot = claimSlot;
            ExplorationManager.GameTick(gameTick, secondBoundary);
        }
        if (secondBoundary) {
            ResearchManager.UpdateSacrificeBoosts(gameTick);
            SubspaceExpansion.GameTick(gameTick);
            MetadataExtraction.Refresh();
        }
        bool dirty = ResearchManager.FlushMainThreadNotifications();
        dirty |= ExplorationManager.TakeSnapshotDirty();
        dirty |= DataCenterInventory.TakeSnapshotDirty();
        dirty |= MetadataExtraction.TakeDirty();
        civilizationSnapshotPending |= dirty;
        FlushNotifications();

        if (secondBoundary) {
            if (!ExplorationManager.FirstRelicClaimed) {
                ExplorationManager.EnsureFirstRelicMark();
            }
            GrowthTechnologyManager.SyncRuntimeEffects();
            TutorialManager.UpdateMilestoneTips();
            NebulaMultiplayerModAPI.BroadcastAdvancedFractionatorState();
            if (civilizationSnapshotPending) {
                NebulaMultiplayerModAPI.BroadcastCivilizationState();
                civilizationSnapshotPending = false;
            }
        }
    }

    /// <summary>
    /// 将提示排入主线程队列。
    /// </summary>
    public static void QueueNotification(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }
        lock (NotificationSync) {
            PendingNotifications.Enqueue(text);
        }
    }

    /// <summary>排队显示已结算的遗迹回收结果；关键信号依次弹窗确认，普通点只浮现提示。</summary>
    /// <param name="mark">已结算的点位副本，文字由本端按当前语言生成。</param>
    public static void QueueRecoveryPopup(ExplorationMark mark) {
        if (mark == null) { return; }
        if (!ExplorationManager.IsKeySignal(mark)) {
            QueueNotification(ExplorationManager.GetRecoveryText(mark));
            return;
        }
        MessageLog.Add(ExplorationManager.GetRecoveryText(mark));
        lock (NotificationSync) { PendingRecoveries.Enqueue(mark); }
    }

    /// <summary>仅在没有其他弹窗时显示下一条回收结果，避免提示相互覆盖。</summary>
    private static void FlushRecoveryPopups() {
        if (UIRoot.instance?.uiGame?.active != true || UIMessageBox.activeCount > 0) { return; }
        ExplorationMark recovery;
        lock (NotificationSync) {
            if (PendingRecoveries.Count == 0) { return; }
            recovery = PendingRecoveries.Dequeue();
        }
        UIMessageBox.Show(ExplorationManager.GetSignalName(recovery), ExplorationManager.GetRecoveryText(recovery),
            "确定".Translate(), UIMessageBox.INFO, null);
    }

    /// <summary>
    /// 读取文明域状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Research", ResearchManager.Import),
            ("Exploration", ExplorationManager.Import),
            ("Metadata", MetadataExtraction.Import)
        );
    }

    /// <summary>
    /// 写入文明域状态。
    /// </summary>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Research", ResearchManager.Export),
            ("Exploration", ExplorationManager.Export),
            ("Metadata", MetadataExtraction.Export)
        );
    }

    /// <summary>
    /// 导入后恢复所有会影响运行的状态。
    /// </summary>
    public static void AfterImport() {
        AdvancedRecipeCatalog.SyncUnlocks(force: true);
        ResearchManager.RefreshAdvancedTowerInventoryMilestones();
        GrowthTechnologyManager.ConsolidateWhiteMatrixResearch();
        GrowthTechnologyManager.SyncResearchWorkload();
        GrowthTechnologyManager.SyncRuntimeEffects();
        if (!ExplorationManager.FirstRelicClaimed) {
            ExplorationManager.EnsureFirstRelicMark();
        }
    }

    /// <summary>
    /// 切档时清除全部文明域状态。
    /// </summary>
    public static void IntoOtherSave() {
        ResearchManager.IntoOtherSave();
        ExplorationManager.IntoOtherSave();
        CivilizationRecoveryManager.IntoOtherSave();
        MetadataExtraction.IntoOtherSave();
        MessageLog.Clear();
        lastProcessedGameTick = long.MinValue;
        lastProcessedSecond = long.MinValue;
        lastProcessedClaimSlot = long.MinValue;
        civilizationSnapshotPending = false;
        lock (NotificationSync) {
            PendingNotifications.Clear();
            PendingRecoveries.Clear();
        }
    }

    private static void FlushNotifications() {
        Queue<string> notifications = [];
        lock (NotificationSync) {
            while (PendingNotifications.Count > 0) {
                notifications.Enqueue(PendingNotifications.Dequeue());
            }
        }
        while (notifications.Count > 0) {
            UIRealtimeTip.Popup(notifications.Dequeue(), true, 2);
        }
    }
}
