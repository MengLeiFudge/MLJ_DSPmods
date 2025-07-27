using System.Collections.Generic;
using System.Reflection.Emit;
using FE.Compatibility;
using HarmonyLib;
using UnityEngine;

namespace FE.UI.Patches;

public static class UIButtonPatch {
    /// <summary>
    /// Bring popup tip window to top layer.
    /// 使通过AddTipsButton、将通过AddTipsButton2方法增加的弹窗在最上层显示。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIButton), nameof(UIButton.LateUpdate))]
    private static IEnumerable<CodeInstruction> UIButton_LateUpdate_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        if (UxAssist.Enable) {
            return instructions;
        }
        var matcher = new CodeMatcher(instructions, generator);
        matcher.MatchForward(false,
            new CodeMatch(ci => ci.opcode == OpCodes.Ldloc || ci.opcode == OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))),
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(GameObject), nameof(GameObject.activeSelf)))
        );
        var ldLocOpr = matcher.Operand;
        var labels = matcher.Labels;
        matcher.Labels = null;
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldloc_S, ldLocOpr).WithLabels(labels),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Component), nameof(Component.transform))),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.parent))),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.parent))),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.Method(typeof(Transform), nameof(Transform.SetAsLastSibling)))
        );
        return matcher.InstructionEnumeration();
    }
}
