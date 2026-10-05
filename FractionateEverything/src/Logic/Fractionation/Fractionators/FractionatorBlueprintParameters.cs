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
    /// <param name="parameters">蓝图参数数组，可以为空。</param>
    /// <param name="magic">状态块的身份标记。</param>
    /// <param name="version">状态块版本。</param>
    /// <param name="value">本状态块的单个整数值。</param>
    /// <returns>保留其他状态块的参数副本。</returns>
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
    /// 从任意位置读取指定状态块，允许不同 FE 实例状态共用参数数组。
    /// </summary>
    /// <param name="parameters">蓝图参数数组，可以为空。</param>
    /// <param name="magic">目标状态块的身份标记。</param>
    /// <param name="version">可读取的状态块版本。</param>
    /// <param name="value">找到的状态值，未找到时为零。</param>
    /// <returns>是否找到身份和版本均匹配的完整状态块。</returns>
    public static bool TryRead(int[] parameters, int magic, int version, out int value) {
        int index = Find(parameters, magic, version);
        value = index >= 0 ? parameters[index + 2] : 0;
        return index >= 0;
    }

    /// <summary>从尾部查找最后一个完整且身份匹配的状态块。</summary>
    /// <param name="parameters">包含多个状态块的参数数组。</param>
    /// <param name="magic">目标状态块的身份标记。</param>
    /// <param name="version">要求的版本。</param>
    /// <returns>状态块起始索引；未找到时为 -1。</returns>
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
