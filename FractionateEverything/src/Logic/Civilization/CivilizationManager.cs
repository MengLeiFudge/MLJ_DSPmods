using System.Collections.Generic;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// FE 3.0 文明域聚合入口：随机研究、探索信号和确定性成长科技。
/// </summary>
public static class CivilizationModule {
    private static readonly object NotificationSync = new();
    private static readonly Queue<string> PendingNotifications = [];
    private static long lastProcessedGameTick = long.MinValue;
    private static bool civilizationSnapshotPending;

    /// <summary>
    /// 注册文明域本地化文本。
    /// </summary>
    public static void AddTranslations() {
        GrowthTechnologyManager.AddTranslations();
        Register("已取得解析资格：{0}", "Research eligibility acquired: {0}");
        Register("解析发现：{0}", "Analysis discovered: {0}");
        Register("已恢复一座原型：{0}", "Prototype recovered: {0}");
        Register("已完成 FE 3.0 生命工业成长线",
            "FE 3.0 living-industry progression completed.");
        Register("检测到母星上的古文明遗物信号",
            "An old-civilization relic signal was detected on the birth planet.");
        Register("{0}发现 {1} 个探索信号", "{0} revealed {1} exploration signal(s).");
        Register("Frag 解析", "Frag analysis");
        Register("古文明网络发现了新的探索信号",
            "The old-civilization network revealed a new exploration signal.");
        Register("已接入古文明亚空间网络",
            "Connected to the old-civilization subspace network.");
        Register("探索取得：{0} × {1}", "Exploration recovered: {0} x {1}");
        Register("首个遗物已恢复：基础塔与亚空间访问已就绪",
            "First relic recovered: basic towers and subspace access are ready.");
    }

    /// <summary>
    /// 完成数据加载后的运行缓存初始化。
    /// </summary>
    public static void Initialize() {
        GrowthTechnologyManager.SyncRuntimeEffects();
    }

    /// <summary>
    /// 在 Unity 主线程推进探索、成长投影和联机广播。
    /// </summary>
    public static void Tick() {
        if (!GameMain.isRunning || GameMain.mainPlayer == null) {
            return;
        }

        long gameTick = GameMain.gameTick;
        if (gameTick == lastProcessedGameTick) {
            return;
        }
        lastProcessedGameTick = gameTick;

        ExplorationManager.GameTick(gameTick);
        bool dirty = ResearchManager.FlushMainThreadNotifications();
        dirty |= ExplorationManager.TakeSnapshotDirty();
        dirty |= DataCenterInventory.TakeSnapshotDirty();
        civilizationSnapshotPending |= dirty;
        FlushNotifications();

        if (gameTick % 60L == 0L) {
            if (!ExplorationManager.FirstRelicClaimed) {
                ExplorationManager.EnsureFirstRelicMark();
                ExplorationManager.EnsureFirstRelicGuidance();
            }
            GrowthTechnologyManager.SyncRuntimeEffects();
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
    /// 导入后重建运行投影。
    /// </summary>
    public static void AfterImport() {
        GrowthTechnologyManager.SyncRuntimeEffects();
        if (!ExplorationManager.FirstRelicClaimed) {
            ExplorationManager.EnsureFirstRelicMark();
            ExplorationManager.EnsureFirstRelicGuidance();
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
