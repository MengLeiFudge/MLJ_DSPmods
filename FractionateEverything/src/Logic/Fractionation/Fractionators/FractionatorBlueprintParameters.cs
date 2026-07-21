using System;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 在同一蓝图参数数组中共存读写 FE 分馏塔的多个实例状态块。
/// </summary>
internal static class FractionatorBlueprintParameters {
    private const int BlockLength = 3;

    /// <summary>
    /// 更新已有状态块；不存在时在参数尾部追加新的三整数状态块。
    /// </summary>
    public static int[] Upsert(int[] parameters, int magic, int version, int value) {
        int[] result = parameters ?? [];
        int index = Find(result, magic, version);
        if (index >= 0) {
            result = (int[])result.Clone();
            result[index + 2] = value;
            return result;
        }

        int originalLength = result.Length;
        Array.Resize(ref result, originalLength + BlockLength);
        result[originalLength] = magic;
        result[originalLength + 1] = version;
        result[originalLength + 2] = value;
        return result;
    }

    /// <summary>
    /// 从任意位置读取指定状态块，允许锁定、谱系和弃置状态同时存在。
    /// </summary>
    public static bool TryRead(int[] parameters, int magic, int version, out int value) {
        int index = Find(parameters, magic, version);
        value = index >= 0 ? parameters[index + 2] : 0;
        return index >= 0;
    }

    private static int Find(int[] parameters, int magic, int version) {
        if (parameters == null || parameters.Length < BlockLength) {
            return -1;
        }

        for (int i = parameters.Length - BlockLength; i >= 0; i--) {
            if (parameters[i] == magic && parameters[i + 1] == version) {
                return i;
            }
        }
        return -1;
    }
}
