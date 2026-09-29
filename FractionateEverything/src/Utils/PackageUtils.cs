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
        Register("FE存档版本不兼容标题", "Save Incompatible", "存档版本不兼容");
        Register("FE存档版本不兼容内容",
            "This save was made with an older FE format. Previous FE-owned progression is not imported, but legacy buildings and items are migrated when they still have a current counterpart.",
            "此存档使用旧版 FE 格式。旧版自有进度不会导入；仍有当前对应物的旧建筑与物品会尽可能迁移。");
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
