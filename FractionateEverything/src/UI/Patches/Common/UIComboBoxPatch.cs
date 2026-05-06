using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FE.UI.Patches.Common;
/// <summary>
/// 修正通用下拉框弹层层级的 UI 补丁。
/// </summary>
public static class UIComboBoxPatch {
    private const string DropdownLayerName = "fe-dropdown-layer";
    private static readonly Dictionary<UIComboBox, DropdownPortalState> DropdownStates = [];

    /// <summary>
    /// 使ComboBox的下拉列表在最上层显示，不被其他UI元素覆盖。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIComboBox), nameof(UIComboBox.SetState))]
    private static void UIComboBox_SetState_Postfix(UIComboBox __instance) {
        if (__instance == null) {
            return;
        }

        EnsureDropdownOnTop(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIComboBox), nameof(UIComboBox.OnDisable))]
    private static void UIComboBox_OnDisable_Postfix(UIComboBox __instance) {
        if (__instance == null || __instance.m_DropDownList == null) {
            return;
        }

        RestoreDropdownParent(__instance, __instance.m_DropDownList);
    }

    /// <summary>
    /// 确保下拉框显示在最上层
    /// </summary>
    private static void EnsureDropdownOnTop(UIComboBox comboBox) {
        RectTransform dropdownTransform = comboBox.m_DropDownList;
        if (dropdownTransform == null) {
            return;
        }

        if (comboBox.isDroppedDown) {
            MoveComboBoxToTopLayer(comboBox, dropdownTransform);
        } else {
            RestoreDropdownParent(comboBox, dropdownTransform);
        }
    }

    private static void MoveComboBoxToTopLayer(UIComboBox comboBox, RectTransform dropdownTransform) {
        RectTransform comboTransform = comboBox.transform as RectTransform;
        if (comboTransform == null) {
            dropdownTransform.SetAsLastSibling();
            return;
        }

        RectTransform pageRoot = FindPageRoot(comboTransform);
        if (pageRoot == null) {
            dropdownTransform.SetAsLastSibling();
            return;
        }

        RectTransform layer = GetOrCreateDropdownLayer(pageRoot);
        if (!DropdownStates.ContainsKey(comboBox)) {
            DropdownStates[comboBox] = DropdownPortalState.Capture(comboTransform);
            comboTransform.SetParent(layer, true);
        }

        layer.SetAsLastSibling();
        comboTransform.SetAsLastSibling();
        dropdownTransform.SetAsLastSibling();
    }

    private static void RestoreDropdownParent(UIComboBox comboBox, RectTransform dropdownTransform) {
        if (!DropdownStates.TryGetValue(comboBox, out DropdownPortalState state)) {
            return;
        }

        DropdownStates.Remove(comboBox);
        RectTransform comboTransform = comboBox.transform as RectTransform;
        if (state.Parent == null || comboTransform == null) {
            return;
        }

        comboTransform.SetParent(state.Parent, false);
        comboTransform.SetSiblingIndex(Mathf.Min(state.SiblingIndex, state.Parent.childCount - 1));
        comboTransform.anchorMin = state.AnchorMin;
        comboTransform.anchorMax = state.AnchorMax;
        comboTransform.pivot = state.Pivot;
        comboTransform.anchoredPosition = state.AnchoredPosition;
        comboTransform.sizeDelta = state.SizeDelta;
        comboTransform.offsetMin = state.OffsetMin;
        comboTransform.offsetMax = state.OffsetMax;
        comboTransform.localScale = state.LocalScale;
    }

    private static RectTransform FindPageRoot(Transform source) {
        Transform cursor = source;
        while (cursor != null) {
            string name = cursor.name;
            if (name.StartsWith("tab-")
                || name.StartsWith("analysis-content-") && name != "analysis-content-root") {
                return cursor as RectTransform;
            }

            cursor = cursor.parent;
        }

        return null;
    }

    private static RectTransform GetOrCreateDropdownLayer(RectTransform pageRoot) {
        Transform existing = pageRoot.Find(DropdownLayerName);
        if (existing is RectTransform existingRect) {
            return existingRect;
        }

        var obj = new GameObject(DropdownLayerName, typeof(RectTransform));
        RectTransform layer = obj.GetComponent<RectTransform>();
        layer.SetParent(pageRoot, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.localScale = Vector3.one;

        return layer;
    }
    /// <summary>
    /// 下拉框层级迁移状态。
    /// </summary>
    private sealed class DropdownPortalState {
        public RectTransform Parent { get; private set; }
        public int SiblingIndex { get; private set; }
        public Vector2 AnchorMin { get; private set; }
        public Vector2 AnchorMax { get; private set; }
        public Vector2 Pivot { get; private set; }
        public Vector2 AnchoredPosition { get; private set; }
        public Vector2 SizeDelta { get; private set; }
        public Vector2 OffsetMin { get; private set; }
        public Vector2 OffsetMax { get; private set; }
        public Vector3 LocalScale { get; private set; }

        public static DropdownPortalState Capture(RectTransform dropdown) {
            return new() {
                Parent = dropdown.parent as RectTransform,
                SiblingIndex = dropdown.GetSiblingIndex(),
                AnchorMin = dropdown.anchorMin,
                AnchorMax = dropdown.anchorMax,
                Pivot = dropdown.pivot,
                AnchoredPosition = dropdown.anchoredPosition,
                SizeDelta = dropdown.sizeDelta,
                OffsetMin = dropdown.offsetMin,
                OffsetMax = dropdown.offsetMax,
                LocalScale = dropdown.localScale,
            };
        }
    }
}
