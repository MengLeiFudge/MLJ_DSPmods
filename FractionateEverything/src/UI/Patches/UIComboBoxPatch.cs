using System.Collections.Generic;
using System.Reflection.Emit;
using FE.Compatibility;
using HarmonyLib;
using UnityEngine;

namespace FE.UI.Patches;

public static class UIComboBoxPatch {
    /// <summary>
    /// Bring ComboBox dropdown to top layer.
    /// 使ComboBox的下拉列表在最上层显示，不被其他UI元素覆盖。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(UIComboBox), nameof(UIComboBox.SetState))]
    private static IEnumerable<CodeInstruction> UIComboBox_SetState_Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        // if (UxAssist.Enable) {
        //     return instructions;
        // }
        var matcher = new CodeMatcher(instructions, generator);
        // 在 m_DropDownList.gameObject.SetActive(this.isDroppedDown) 之后插入 SetAsLastSibling
        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(UIComboBox), "m_DropDownList")),
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Call,
                AccessTools.PropertyGetter(typeof(UIComboBox), nameof(UIComboBox.isDroppedDown))),
            new CodeMatch(OpCodes.Callvirt,
                AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive)))
        );
        matcher.Advance(1);
        matcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UIComboBox), "m_DropDownList")),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Transform), nameof(Transform.parent))),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.Method(typeof(Transform), nameof(Transform.SetAsLastSibling))),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UIComboBox), "m_DropDownList")),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.Method(typeof(Transform), nameof(Transform.SetAsLastSibling)))
        );
        return matcher.InstructionEnumeration();
    }
}
