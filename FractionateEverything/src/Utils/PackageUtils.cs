namespace FE.Utils;

/// <summary>
/// 背包、物流背包和数据中心物品存取辅助方法。
/// </summary>
public static partial class Utils {
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
