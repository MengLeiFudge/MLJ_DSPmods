using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;

/// <summary>
/// 集装分拣器堆叠上限补丁。保持原版行为，只替换硬编码的 4 层假设。
/// </summary>
public static class InserterStackPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(InserterComponent), nameof(InserterComponent.InternalUpdate))]
    [HarmonyPatch(typeof(InserterComponent), nameof(InserterComponent.InternalUpdate_Bidirectional))]
    [HarmonyPatch(typeof(InserterComponent), nameof(InserterComponent.InternalUpdateNoAnim))]
    public static IEnumerable<CodeInstruction> InserterComponent_InternalUpdate_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        var result = new List<CodeInstruction>(instructions);
        int replaced = 0;
        for (int i = 0; i <= result.Count - 7; i++) {
            if (!LoadsThisField(result[i], nameof(InserterComponent.itemCount))
                || !IsLoadConstant1(result[i + 1])
                || result[i + 2].opcode != OpCodes.Sub
                || !IsLoadConstant4(result[i + 3])
                || result[i + 4].opcode != OpCodes.Div
                || !IsLoadConstant1(result[i + 5])
                || result[i + 6].opcode != OpCodes.Add) {
                continue;
            }

            result[i + 3] = new CodeInstruction(OpCodes.Ldarg_0);
            result.Insert(i + 4, new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(InserterComponent), nameof(InserterComponent.stackOutput))));
            replaced++;
            i += 4;
        }

        if (replaced != 1) {
            LogWarning("InserterStackPatch: 未找到分拣器 stackCount 固定 4 的替换点。");
        }
        return result;
    }

    private static bool LoadsThisField(CodeInstruction instruction, string fieldName) =>
        instruction.opcode == OpCodes.Ldfld
        && instruction.operand is System.Reflection.FieldInfo field
        && field.DeclaringType == typeof(InserterComponent)
        && field.Name == fieldName;

    private static bool IsLoadConstant1(CodeInstruction instruction) =>
        instruction.opcode == OpCodes.Ldc_I4_1
        || (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte value && value == 1)
        || (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int intValue && intValue == 1);

    private static bool IsLoadConstant4(CodeInstruction instruction) =>
        instruction.opcode == OpCodes.Ldc_I4_4
        || (instruction.opcode == OpCodes.Ldc_I4_S && instruction.operand is sbyte value && value == 4)
        || (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int intValue && intValue == 4);
}
