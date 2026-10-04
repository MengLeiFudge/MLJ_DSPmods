using System.Collections.Generic;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 汇总随机研究、探索信号和确定性成长科技的生命周期。
/// </summary>
public static class CivilizationModule {
    private static readonly object NotificationSync = new();
    private static readonly Queue<string> PendingNotifications = [];
    /// <summary>已结算的回收结果，在主线程逐条显示；由 NotificationSync 保护。</summary>
    private static readonly Queue<(string Title, string Text)> PendingRecoveries = [];
    private static long lastProcessedGameTick = long.MinValue;
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

        FlushRecoveryPopups();
        long gameTick = GameMain.gameTick;
        if (gameTick == lastProcessedGameTick) {
            return;
        }
        lastProcessedGameTick = gameTick;

        ExplorationManager.GameTick(gameTick);
        bool secondBoundary = gameTick % 60L == 0L;
        if (secondBoundary) {
            ResearchManager.UpdateSacrificeBoosts(gameTick);
        }
        bool dirty = ResearchManager.FlushMainThreadNotifications();
        dirty |= ExplorationManager.TakeSnapshotDirty();
        dirty |= DataCenterInventory.TakeSnapshotDirty();
        civilizationSnapshotPending |= dirty;
        FlushNotifications();

        if (secondBoundary) {
            if (!ExplorationManager.FirstRelicClaimed) {
                ExplorationManager.EnsureFirstRelicMark();
            }
            GrowthTechnologyManager.SyncRuntimeEffects();
            TutorialManager.UpdateMilestoneTips();
            PermanentProfileStore.TryRecordCompletion();
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
    /// <param name="title">遗迹名称。</param>
    /// <param name="text">实际获得的记录或物资。</param>
    /// <param name="modal">是否以需要确认的弹窗显示。</param>
    public static void QueueRecoveryPopup(string title, string text, bool modal) {
        if (string.IsNullOrWhiteSpace(text)) { return; }
        if (!modal) {
            QueueNotification(text);
            return;
        }
        lock (NotificationSync) { PendingRecoveries.Enqueue((title, text)); }
    }

    /// <summary>仅在没有其他弹窗时显示下一条回收结果，避免提示相互覆盖。</summary>
    private static void FlushRecoveryPopups() {
        if (UIRoot.instance?.uiGame?.active != true || UIMessageBox.activeCount > 0) { return; }
        (string Title, string Text) recovery;
        lock (NotificationSync) {
            if (PendingRecoveries.Count == 0) { return; }
            recovery = PendingRecoveries.Dequeue();
        }
        UIMessageBox.Show(recovery.Title, recovery.Text, "确定".Translate(), UIMessageBox.INFO, null);
    }

    /// <summary>
    /// 读取文明域状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Research", ResearchManager.Import),
            ("Exploration", ExplorationManager.Import)
        );
    }

    /// <summary>
    /// 写入文明域状态。
    /// </summary>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Research", ResearchManager.Export),
            ("Exploration", ExplorationManager.Export)
        );
    }

    /// <summary>
    /// 导入后恢复所有会影响运行的状态。
    /// </summary>
    public static void AfterImport() {
        ResearchManager.RefreshAdvancedTowerInventoryMilestones();
        GrowthTechnologyManager.ConsolidateWhiteMatrixResearch();
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
        lastProcessedGameTick = long.MinValue;
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
