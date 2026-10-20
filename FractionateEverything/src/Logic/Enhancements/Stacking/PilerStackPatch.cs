using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Enhancements.Stacking;

/// <summary>
/// 集装机沿用原版取货、供电和拆堆流程，仅在六层研究后提高合并上限。
/// </summary>
public static class PilerStackPatch {
    /// <summary>同时替换容量比较、货物输出、剩余数量和增产点分配中的四层常量。</summary>
    /// <param name="instructions">原版集装机更新指令。</param>
    /// <returns>完整匹配时返回动态上限指令，否则保留输入并记录错误。</returns>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(PilerComponent), nameof(PilerComponent.InternalUpdate))]
    private static IEnumerable<CodeInstruction> PilerComponent_InternalUpdate_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var original = new List<CodeInstruction>(instructions);
        var result = new List<CodeInstruction>();
        int integerCount = 0;
        int floatCount = 0;
        foreach (CodeInstruction instruction in original) {
            var code = new CodeInstruction(instruction);
            bool integer = code.LoadsConstant(4L);
            bool floating = code.opcode == OpCodes.Ldc_R4 && code.operand is float value && value == 4f;
            if (!integer && !floating) {
                result.Add(code);
                continue;
            }
            // 标签仍位于替换入口，实例引用供四处计算读取相同的缓存边界。
            code.opcode = OpCodes.Ldarg_0;
            code.operand = null;
            result.Add(code);
            result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PilerStackPatch),
                integer ? nameof(GetStackLimit) : nameof(GetStackLimitFloat))));
            if (integer) { integerCount++; } else { floatCount++; }
        }
        if (integerCount != 3 || floatCount != 1) {
            LogError($"集装机堆叠补丁未应用：整数匹配{integerCount}/3，浮点匹配{floatCount}/1，请检查游戏版本及其他集装机补丁。");
            return original;
        }
        return result;
    }

    /// <summary>六层后提升合并能力；旧档较大货物只保留原有层数，避免剩余缓存的byte增产点溢出。</summary>
    /// <param name="piler">当前集装机，缓存尚未扣除本次输出。</param>
    /// <returns>研究上限与已有两堆大小的最大值，不把旧货物继续叠高。</returns>
    private static int GetStackLimit(ref PilerComponent piler) => Math.Max(StackingManager.GetCompatibleMaxStack(4),
        Math.Max(piler.cacheCargoStack1, piler.cacheCargoStack2));

    /// <summary>增产点按与物品相同的合并上限分配。</summary>
    /// <param name="piler">当前集装机。</param>
    /// <returns>浮点指令使用的合并件数。</returns>
    private static float GetStackLimitFloat(ref PilerComponent piler) => GetStackLimit(ref piler);
}
