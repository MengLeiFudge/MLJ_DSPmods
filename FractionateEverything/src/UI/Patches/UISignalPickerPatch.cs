using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CommonAPI.Systems;
using CommonAPI.Systems.UI;
using FE.Compatibility;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.Patches;

/// <summary>
/// 为原版图标选择弹窗补充 CommonAPI 自定义页面入口。
/// 创世和星环已各自接管 UISignalPicker，避免重复注入。
/// </summary>
public static class UISignalPickerPatch {
    private static readonly FieldInfo currentTypeField = AccessTools.Field(typeof(UISignalPicker), nameof(UISignalPicker.currentType));
    private static readonly List<UITabButton> customTabs = [];

    [HarmonyPrepare]
    private static bool Prepare() {
        return !GenesisBook.Enable && !OrbitalRing.Enable;
    }

    [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker._OnCreate))]
    [HarmonyPostfix]
    private static void UISignalPicker_OnCreate_Postfix(UISignalPicker __instance) {
        customTabs.Clear();
        foreach (TabData tabData in TabSystem.GetAllTabs()) {
            if (tabData == null) {
                continue;
            }
            GameObject tabObject = UnityEngine.Object.Instantiate(TabSystem.GetTabPrefab(), __instance.pickerTrans, false);
            ((RectTransform)tabObject.transform).anchoredPosition = new((tabData.tabIndex + 3) * 70 - 54, -75f);
            UITabButton tabButton = tabObject.GetComponent<UITabButton>();
            Sprite tabIcon = Resources.Load<Sprite>(tabData.tabIconPath);
            tabButton.Init(tabIcon, tabData.tabName, tabData.tabIndex + 6, __instance.OnTypeButtonClick);
            customTabs.Add(tabButton);
        }

        // 复用原版科技/升级按钮位置给自定义页签腾空间。
        __instance.typeButton5.gameObject.SetActive(false);
        __instance.typeButton6.gameObject.SetActive(false);
        __instance.typeButton7.gameObject.SetActive(false);
    }

    [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.OnTypeButtonClick))]
    [HarmonyPostfix]
    private static void UISignalPicker_OnTypeButtonClick_Postfix(int type) {
        foreach (UITabButton tabButton in customTabs) {
            tabButton.TabSelected(type);
        }
    }

    [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker._OnUpdate))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UISignalPicker_OnUpdate_Transpiler(IEnumerable<CodeInstruction> instructions) {
        CodeMatcher matcher = new(instructions);
        matcher.MatchForward(true,
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch(OpCodes.Ldfld, currentTypeField),
            new CodeMatch(OpCodes.Ldc_I4_2));
        if (matcher.IsInvalid) {
            CheckPlugins.LogWarning("UISignalPickerPatch: 未找到 _OnUpdate 里的 currentType == 2 判断，跳过 tooltip 扩展。");
            return instructions;
        }

        object skipLabel = matcher.Advance(1).Operand;
        matcher.Advance(1).InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, currentTypeField),
            new CodeInstruction(OpCodes.Ldc_I4_8),
            new CodeInstruction(OpCodes.Bge, skipLabel)
        );
        return matcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(UISignalPicker), nameof(UISignalPicker.RefreshIcons))]
    [HarmonyPostfix]
    private static void UISignalPicker_RefreshIcons_Postfix(UISignalPicker __instance) {
        if (__instance.currentType <= 8) {
            return;
        }

        IconSet iconSet = GameMain.iconSet;
        foreach (ItemProto itemProto in LDB.items.dataArray) {
            if (itemProto == null || itemProto.GridIndex < 1101) {
                continue;
            }

            int page = itemProto.GridIndex / 1000;
            if (page != __instance.currentType - 6) {
                continue;
            }

            int row = (itemProto.GridIndex - page * 1000) / 100 - 1;
            int col = itemProto.GridIndex % 100 - 1;
            if (row < 0 || col < 0 || row >= 10 || col >= 14) {
                continue;
            }

            int index = row * 14 + col;
            if (index < 0 || index >= __instance.indexArray.Length) {
                continue;
            }

            int signalId = SignalProtoSet.SignalId(ESignalType.Item, itemProto.ID);
            __instance.indexArray[index] = iconSet.signalIconIndex[signalId];
            __instance.signalArray[index] = signalId;
        }
    }
}
