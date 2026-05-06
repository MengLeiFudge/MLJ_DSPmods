using FE.Compatibility;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 建筑师模式，所有建筑数目显示为999且建造时不消耗
    /// </summary>
    private static bool ArchitectMode => Multfunction_mod.ArchitectMode
                                         || CheatEnabler.ArchitectMode
                                         || DeliverySlotsTweaks.ArchitectMode;

    /// <summary>
    /// 指示物品交互科技是否已解锁
    /// </summary>
    public static bool TechItemInteractionUnlocked => GameMain.history.TechUnlocked(TFE物品交互);
    private const int MinTurretAmmoNeedCount = 6;
    public static void AddTranslations() {
        Register("提示", "Tip");
        Register("确定", "Confirm");
        Register("取消", "Cancel");
        Register("要花费", "Would you like to spend");
        Register("来兑换", "to exchange");
        Register("吗？", "?");
        Register("兑换", "Exchange");
        Register("已兑换", "Exchanged");
        Register("无法兑换", "Can not exchange");
        Register("配方经验", "recipe experience");
        Register("FE存档版本不兼容标题", "Save Incompatible", "存档版本不兼容");
        Register("FE存档版本不兼容内容",
            "Due to a major mod update, this save is incompatible with the previous version. Some data has been cleared. As compensation, 5000 Fragments have been added to your data centre.",
            "由于模组大版本更新，本次更新不兼容以前的存档，部分数据已被清除。作为补偿，5000残片已添加至数据中心。");
    }
}
