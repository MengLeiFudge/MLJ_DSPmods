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
            "仅由当前格式存档在取得全部三类高级塔并达到 20 堆叠后写入；旧版存档不会触发。");
    }

    /// <summary>
    /// 在严格完成条件满足时写入跨存档标记。
    /// </summary>
    public static void TryRecordCompletion() {
        if (hasPlayedFractionation == null || hasPlayedFractionation.Value
            || !ResearchManager.BelongsToGeneration3
            || !ResearchManager.HasAcquiredAllAdvancedTowers
            || StackingManager.CurrentMaxStack < 20) {
            return;
        }

        hasPlayedFractionation.Value = true;
        configFile?.Save();
        CivilizationModule.QueueNotification("生命工业传承已记录".Translate());
    }
}
