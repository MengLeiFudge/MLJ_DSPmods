using System.Collections.Generic;
using CommonAPI.Systems;
using FE.Compatibility;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.Patches;

/// <summary>
/// 为标签图标选择弹窗补充 CommonAPI 自定义页面入口。
/// 创世已接管 UISignalTagPicker，避免与其重复注入。
/// </summary>
public static class UISignalTagPickerPatch {
    private static readonly List<UIButton> customTabButtons = [];

    [HarmonyPrepare]
    private static bool Prepare() {
        return !GenesisBook.Enable;
    }

    [HarmonyPatch(typeof(UISignalTagPicker), nameof(UISignalTagPicker._OnCreate))]
    [HarmonyPostfix]
    private static void UISignalTagPicker_OnCreate_Postfix(UISignalTagPicker __instance) {
        customTabButtons.Clear();
        Vector2 anchor = ((RectTransform)__instance.upgradeTab2Btn.transform).anchoredPosition;
        foreach (TabData tabData in TabSystem.GetAllTabs()) {
            if (tabData == null) {
                continue;
            }

            GameObject tabObject =
                UnityEngine.Object.Instantiate(__instance.upgradeTab2Btn.gameObject, __instance.pickerTrans, false);
            ((RectTransform)tabObject.transform).anchoredPosition =
                new(anchor.x + (tabData.tabIndex - 5) * 48, anchor.y);
            UIButton button = tabObject.GetComponent<UIButton>();
            button.data = tabData.tabIndex + 6;
            tabObject.transform.Find("icon").GetComponent<Image>().sprite = Resources.Load<Sprite>(tabData.tabIconPath);
            customTabButtons.Add(button);
        }

        // 复用原版科技/升级按钮区域承载自定义页签。
        __instance.upgradeTab2Btn.gameObject.SetActive(false);
        __instance.upgradeTab1Btn.gameObject.SetActive(false);
        __instance.techTabBtn.gameObject.SetActive(false);
    }

    [HarmonyPatch(typeof(UISignalTagPicker), nameof(UISignalTagPicker._OnRegEvent))]
    [HarmonyPostfix]
    private static void UISignalTagPicker_OnRegEvent_Postfix(UISignalTagPicker __instance) {
        foreach (UIButton button in customTabButtons) {
            button.onClick += __instance.OnTypeButtonClick;
        }
    }

    [HarmonyPatch(typeof(UISignalTagPicker), nameof(UISignalTagPicker._OnUnregEvent))]
    [HarmonyPostfix]
    private static void UISignalTagPicker_OnUnregEvent_Postfix(UISignalTagPicker __instance) {
        foreach (UIButton button in customTabButtons) {
            button.onClick -= __instance.OnTypeButtonClick;
        }
    }

    [HarmonyPatch(typeof(UISignalTagPicker), nameof(UISignalTagPicker.OnTypeButtonClick))]
    [HarmonyPostfix]
    private static void UISignalTagPicker_OnTypeButtonClick_Postfix(UISignalTagPicker __instance, int type) {
        foreach (UIButton button in customTabButtons) {
            bool selected = button.data == type;
            button.highlighted = selected;
            button.button.interactable = !selected;
        }
    }

    [HarmonyPatch(typeof(UISignalTagPicker), nameof(UISignalTagPicker.RefreshIcons))]
    [HarmonyPostfix]
    private static void UISignalTagPicker_RefreshIcons_Postfix(UISignalTagPicker __instance) {
        if ((int)__instance.currentType <= 8) {
            return;
        }

        GameHistoryData history = GameMain.history;
        IconSet iconSet = GameMain.iconSet;
        foreach (ItemProto itemProto in LDB.items.dataArray) {
            if (itemProto == null || itemProto.GridIndex < 1101) {
                continue;
            }

            int page = itemProto.GridIndex / 1000;
            if (page != (int)__instance.currentType - 6) {
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

            if (!UISignalTagPicker.showUnlock && !history.ItemUnlocked(itemProto.ID)) {
                continue;
            }

            int signalId = SignalProtoSet.SignalId(ESignalType.Item, itemProto.ID);
            __instance.indexArray[index] = iconSet.signalIconIndex[signalId];
            __instance.signalArray[index] = signalId;
        }
    }
}
