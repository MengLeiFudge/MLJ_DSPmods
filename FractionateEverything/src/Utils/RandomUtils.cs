using System;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 戴森球的随机数发生器。
    /// </summary>
    /// <param name="seed">输入输出随机种子（会在方法内推进）</param>
    /// <returns>[0,1) 区间随机数</returns>
    public static double GetRandDouble(ref uint seed) {
        seed = (uint)((seed % 2147483646U + 1U) * 48271UL % int.MaxValue) - 1U;
        return seed / 2147483646.0;
    }

    public static uint randSeed = (uint)new Random().Next(1, 2147483646);

    /// <summary>
    /// 获取 [min, max) 区间的随机整数。
    /// </summary>
    /// <param name="min">下界（包含）</param>
    /// <param name="max">上界（不包含）</param>
    public static int GetRandInt(int min, int max) {
        // if (min >= max) return min;
        randSeed = (uint)((randSeed % 2147483646U + 1U) * 48271UL % int.MaxValue) - 1U;
        double randomValue = randSeed / 2147483646.0;
        return (int)(randomValue * (max - min)) + min;
    }

    /// <summary>
    /// 使用全局随机种子获取 [0,1) 区间随机数。
    /// </summary>
    public static double GetRandDouble() {
        randSeed = (uint)((randSeed % 2147483646U + 1U) * 48271UL % int.MaxValue) - 1U;
        return randSeed / 2147483646.0;
    }
}
