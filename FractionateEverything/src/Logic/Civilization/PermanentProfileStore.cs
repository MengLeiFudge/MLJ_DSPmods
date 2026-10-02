using BepInEx.Configuration;
using FE.Logic.Enhancements.Stacking;

namespace FE.Logic.Civilization;

/// <summary>
/// 保存跨存档的生命工业成长线完成标记。
/// </summary>
public static class PermanentProfileStore {
    private static ConfigFile configFile;
    private static ConfigEntry<bool> hasPlayedFractionation;

    /// <summary>
    /// 获取该本地配置是否已经完整走过生命工业成长线。
    /// </summary>
    public static bool HasPlayedFractionation => hasPlayedFractionation?.Value == true;

    /// <summary>
    /// 绑定 BepInEx 配置项。
    /// </summary>
    public static void Bind(ConfigFile config) {
        configFile = config;
        hasPlayedFractionation = config.Bind("Progression", "HasPlayedFractionation3", false,
            "在正常模式下取得全部三类高级塔并达到20堆叠后记录。沙盒、使用过一键解锁及旧格式存档不能写入此标记。");
    }

    /// <summary>
    /// 在严格完成条件满足时写入跨存档标记。
    /// </summary>
    public static void TryRecordCompletion() {
        if (GameMain.sandboxToolsEnabled || GameMain.data?.gameDesc?.isSandboxMode == true
            || GameMain.history?.createWithSandboxMode == true) {
            ResearchManager.DisqualifyCompletion();
        }
        if (hasPlayedFractionation == null || hasPlayedFractionation.Value
            || !ResearchManager.CanRecordCompletion
            || !ResearchManager.HasAcquiredAllAdvancedTowers
            || StackingManager.CurrentMaxStack < 20) {
            return;
        }

        hasPlayedFractionation.Value = true;
        configFile?.Save();
        CivilizationModule.QueueNotification("生命工业传承已记录".Translate());
    }
}
