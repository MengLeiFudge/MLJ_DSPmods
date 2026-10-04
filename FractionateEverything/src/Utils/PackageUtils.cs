namespace FE.Utils;

/// <summary>
/// 背包、物流背包、数据中心和增产点拆分的共享辅助方法。
/// </summary>
public static partial class Utils {
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
