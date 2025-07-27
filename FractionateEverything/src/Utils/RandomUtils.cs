using System;

namespace FE.Utils;

public static partial class Utils {
    public static double GetRandDouble(ref uint seed) {
        seed = (uint)((seed % 2147483646U + 1U) * 48271UL % int.MaxValue) - 1U;
        return seed / 2147483646.0;
    }

    private static readonly Random rand = new Random();

    public static int GetRandInt(int min, int max) {
        return rand.Next(min, max);
    }

    public static double GetRandDouble() {
        return rand.NextDouble();
    }
}
