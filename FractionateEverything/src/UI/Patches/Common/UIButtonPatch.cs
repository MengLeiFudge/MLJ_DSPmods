using System.Collections.Generic;
using System.Reflection.Emit;
using FE.Compatibility.Mods;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.Patches.Common;

/// <summary>
/// 修正通用按钮提示弹窗层级的 UI 补丁。
/// </summary>
public static class UIButtonPatch {
    /// <summary>
    /// 将弹出提示窗口置于顶层。
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
        if (!matcher.IsValid) {
            LogWarning("UIButtonPatch: 未找到 LateUpdate 提示层级调整锚点，保留原方法。");
            return instructions;
        }
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
