namespace FE.Utils;

/// <summary>
/// 背包、物流背包、数据中心和增产点拆分的共享辅助方法。
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

    /// <summary>
    /// 原版分割增产点数的方法。
    /// 使用前需注意物品总数 n 不能为 0。
    /// </summary>
    public static int split_inc(ref int n, ref int m, int p) {
        int num1 = m / n;
        int num2 = m - num1 * n;
        n -= p;
        int num3 = num2 - n;
        int num4 = num3 > 0 ? num1 * p + num3 : num1 * p;
        m -= num4;
        return num4;
    }

    /// <summary>
    /// 原版分割增产点数的方法。
    /// 使用前需注意物品总数 n 不能为 0。
    /// </summary>
    public static long split_inc(ref long n, ref long m, long p) {
        long num1 = m / n;
        long num2 = m - num1 * n;
        n -= p;
        long num3 = num2 - n;
        long num4 = num3 > 0 ? num1 * p + num3 : num1 * p;
        m -= num4;
        return num4;
    }
}
