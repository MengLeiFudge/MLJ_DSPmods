using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace FE.UI.Patches;

public static class UIComboBoxPatch {
    /// <summary>
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
        // 在 m_DropDownList.gameObject.SetActive(this.isDroppedDown) 之后插入代码来确保下拉框在最上层
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
            // 调用辅助方法来设置下拉框的渲染层级
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UIComboBox), "m_DropDownList")),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call,
                AccessTools.PropertyGetter(typeof(UIComboBox), nameof(UIComboBox.isDroppedDown))),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(UIComboBoxPatch), nameof(EnsureDropdownOnTop)))
        );
        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 确保下拉框显示在最上层
    /// </summary>
    private static void EnsureDropdownOnTop(Transform dropdownTransform, bool isDroppedDown) {
        if (dropdownTransform == null) {
            return;
        }
        if (isDroppedDown) {
            // 在SetState中也调用SetAsLastSibling，确保每次Update都保持在最上层
            // 因为其他UI元素可能在Update中改变层级顺序
            if (dropdownTransform.parent != null) {
                dropdownTransform.parent.SetAsLastSibling();
            }
            dropdownTransform.SetAsLastSibling();
        }
    }
}
