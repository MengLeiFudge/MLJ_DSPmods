using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 研究站堆叠适配。
/// </summary>
public static class LabManager {
    private const int VanillaLabMaxStack = 4;
    private const int VanillaLabTransferLimit = 5;
    private const int LongRecipeTimeSpendThreshold = 5400000;

    public static int GetLabStackTransferLimit() {
        int maxStack = MonitorManager.GetMonitorMaxStack();
        return Math.Max(VanillaLabTransferLimit, (VanillaLabTransferLimit * maxStack + VanillaLabMaxStack - 1) / VanillaLabMaxStack);
    }

    public static int GetAdjustedAssembleNeedThreshold(ref LabComponent lab, int vanillaThreshold) {
        if (lab.recipeExecuteData.timeSpend > LongRecipeTimeSpendThreshold) {
            return vanillaThreshold;
        }

        // 保留原版的安全余量，再按动态传料上限抬高输入缓存目标。
        int keepCount = 1 + lab.speedOverride / 20000;
        return Math.Max(vanillaThreshold, keepCount + GetLabStackTransferLimit() + 1);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.UpdateNeedsAssemble))]
    public static IEnumerable<CodeInstruction> LabComponent_UpdateNeedsAssemble_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var result = new List<CodeInstruction>(instructions);
        int inserted = 0;
        var helper = AccessTools.Method(typeof(LabManager), nameof(GetAdjustedAssembleNeedThreshold));

        for (int i = 0; i < result.Count; i++) {
            if (!IsStoreLocal(result[i], 1)) {
                continue;
            }

            result.InsertRange(i + 1, [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, helper),
                new CodeInstruction(OpCodes.Stloc_1)
            ]);
            inserted++;
            i += 4;
        }

        if (inserted != 1) {
            LogWarning($"LabComponent.UpdateNeedsAssemble: expected 1 threshold adjustment, actual {inserted}.");
        }
        return result;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.UpdateOutputToNext))]
    public static IEnumerable<CodeInstruction> LabComponent_UpdateOutputToNext_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var result = new List<CodeInstruction>(instructions);
        int replaced = 0;
        var helper = AccessTools.Method(typeof(LabManager), nameof(GetLabStackTransferLimit));

        for (int i = 0; i < result.Count - 4; i++) {
            if (!IsLoadLocal(result[i])
                || !IsIntConstant(result[i + 1], VanillaLabTransferLimit)
                || !IsBranchLessOrEqual(result[i + 2])
                || !IsIntConstant(result[i + 3], VanillaLabTransferLimit)
                || !IsStoreLocal(result[i + 4])) {
                continue;
            }

            result[i + 1].opcode = OpCodes.Call;
            result[i + 1].operand = helper;
            result[i + 3].opcode = OpCodes.Call;
            result[i + 3].operand = helper;
            replaced++;
        }

        if (replaced != 1) {
            LogWarning($"LabComponent.UpdateOutputToNext: expected 1 transfer limit replacement, actual {replaced}.");
        }
        return result;
    }

    private static bool IsLoadLocal(CodeInstruction instruction) =>
        instruction.opcode == OpCodes.Ldloc
        || instruction.opcode == OpCodes.Ldloc_S
        || instruction.opcode == OpCodes.Ldloc_0
        || instruction.opcode == OpCodes.Ldloc_1
        || instruction.opcode == OpCodes.Ldloc_2
        || instruction.opcode == OpCodes.Ldloc_3;

    private static bool IsStoreLocal(CodeInstruction instruction, int? index = null) {
        if (index is not { } localIndex) {
            return instruction.opcode == OpCodes.Stloc
                   || instruction.opcode == OpCodes.Stloc_S
                   || instruction.opcode == OpCodes.Stloc_0
                   || instruction.opcode == OpCodes.Stloc_1
                   || instruction.opcode == OpCodes.Stloc_2
                   || instruction.opcode == OpCodes.Stloc_3;
        }

        return localIndex switch {
            0 => instruction.opcode == OpCodes.Stloc_0,
            1 => instruction.opcode == OpCodes.Stloc_1,
            2 => instruction.opcode == OpCodes.Stloc_2,
            3 => instruction.opcode == OpCodes.Stloc_3,
            _ => false
        };
    }

    private static bool IsBranchLessOrEqual(CodeInstruction instruction) =>
        instruction.opcode == OpCodes.Ble
        || instruction.opcode == OpCodes.Ble_S;

    private static bool IsIntConstant(CodeInstruction instruction, int value) {
        if (value == 5 && instruction.opcode == OpCodes.Ldc_I4_5) return true;
        if (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte sbyteValue) return sbyteValue == value;
        if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int intValue) return intValue == value;
        return false;
    }
}
