﻿using FractionateEverything.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static FractionateEverything.FractionateEverything;

namespace FractionateEverything.Main {
    /// <summary>
    /// 游戏加载完成后，弹窗提示某些信息。
    /// </summary>
    public static class UIOptionWindowPatch {
        private const string details = "UI Root/Overlay Canvas/Top Windows/Option Window/details";
        private static bool _initFinished;
        private static UIToggle DisableMessageBoxToggle;
        private static UIComboBox IconVersionComboBox;
        private static UIToggle EnableDestroyToggle;
        private static UIToggle EnableFuelRodFracToggle;
        private static UIToggle EnableMatrixFracToggle;
        private static UIToggle EnableBuildingAsTrashToggle;

        private static void Init() {
            if (_initFinished) return;

            int baseY = GenesisBook.Enable ? -380 : -220;

            CreateUIToggle("fe-dmb-setting",
                "DisableMessageBox".Translate(), "DisableMessageBoxAdditionalText".Translate(), new(30, baseY - 40 * 0),
                disableMessageBox, out DisableMessageBoxToggle);

            CreateComboBox("fe-iv-setting",
                "IconVersion".Translate(), "IconVersionAdditionalText".Translate(), new(30, baseY - 40 * 1),
                ["v1".Translate(), "v2".Translate(), "v3".Translate()], iconVersion - 1, out IconVersionComboBox);

            CreateUIToggle("fe-ed-setting",
                "EnableDestroy".Translate(), "EnableDestroyAdditionalText".Translate(), new(30, baseY - 40 * 2),
                enableDestroy, out EnableDestroyToggle);

            CreateUIToggle("fe-efrf-setting",
                "EnableFuelRodFrac".Translate(), "EnableFuelRodFracAdditionalText".Translate(), new(30, baseY - 40 * 3),
                enableFuelRodFrac, out EnableFuelRodFracToggle);

            CreateUIToggle("fe-emf-setting",
                "EnableMatrixFrac".Translate(), "EnableMatrixFracAdditionalText".Translate(), new(30, baseY - 40 * 4),
                enableMatrixFrac, out EnableMatrixFracToggle);

            CreateUIToggle("fe-ebat-setting",
                "EnableBuildingAsTrash".Translate(), "EnableBuildingAsTrashAdditionalText".Translate(),
                new(30, baseY - 40 * 5),
                enableBuildingAsTrash, out EnableBuildingAsTrashToggle);

            _initFinished = true;
        }

        private static void CreateUIToggle(string name, string text, string additionalTextStr,
            Vector2 position, bool defaultValue, out UIToggle toggle) {
            GameObject obj = Object.Instantiate(
                GameObject.Find($"{details}/content-3/list/scroll-view/viewport/content/demolish-query"),
                GameObject.Find($"{details}/content-5/advisor-tips").transform.parent
            );
            Object.DestroyImmediate(obj.GetComponent<Localizer>());
            obj.name = name;
            obj.GetComponent<Text>().text = text;
            ((RectTransform)obj.transform).anchoredPosition = position;
            toggle = obj.GetComponentInChildren<UIToggle>();
            toggle.isOn = defaultValue;
            toggle.toggle.onValueChanged.RemoveAllListeners();
            Transform additionalTextTransform = obj.transform.GetChild(1);
            Object.DestroyImmediate(additionalTextTransform.GetComponent<Localizer>());
            additionalTextTransform.GetComponent<Text>().text = additionalTextStr;
        }

        private static void CreateComboBox(string name, string text, string additionalTextStr,
            Vector2 position, List<string> options, int defaultValue, out UIComboBox comboBox) {
            GameObject obj = Object.Instantiate(
                GameObject.Find($"{details}/content-1/msaa"),
                GameObject.Find($"{details}/content-5/advisor-tips").transform.parent
            );
            Object.DestroyImmediate(obj.GetComponent<Localizer>());
            obj.name = name;
            obj.GetComponent<Text>().text = text;
            ((RectTransform)obj.transform).anchoredPosition = position;
            comboBox = obj.GetComponentInChildren<UIComboBox>();
            comboBox.Items = options;
            comboBox.itemIndex = defaultValue;
            comboBox.onItemIndexChange.RemoveAllListeners();
            //在最右边加一行提示文本
            GameObject obj2 = Object.Instantiate(
                GameObject.Find($"{details}/content-3/list/scroll-view/viewport/content/demolish-query/Text"),
                GameObject.Find($"{details}/content-5/advisor-tips").transform.parent
            );
            Object.DestroyImmediate(obj2.GetComponent<Localizer>());
            Transform additionalTextTransform = obj2.transform;
            additionalTextTransform.GetComponent<Text>().text = additionalTextStr;
            additionalTextTransform.SetParent(obj.transform);
            ((RectTransform)additionalTextTransform).anchoredPosition = new(480, 0);
        }

        [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow._OnOpen))]
        [HarmonyPostfix]
        public static void UIOptionWindow_OnOpen_Postfix() {
            Init();
        }

        [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnRevertButtonClick))]
        [HarmonyPostfix]
        public static void UIOptionWindow_OnRevertButtonClick_Postfix(int idx) {
            if (idx == 4) Reset();
        }

        private static void Reset() {
            DisableMessageBoxToggle.isOn = (bool)DisableMessageBoxEntry.DefaultValue;
            IconVersionComboBox.itemIndex = (int)IconVersionEntry.DefaultValue - 1;
            EnableDestroyToggle.isOn = (bool)EnableDestroyEntry.DefaultValue;
            EnableFuelRodFracToggle.isOn = (bool)EnableFuelRodFracEntry.DefaultValue;
            EnableMatrixFracToggle.isOn = (bool)EnableMatrixFracEntry.DefaultValue;
            EnableBuildingAsTrashToggle.isOn = (bool)EnableBuildingAsTrashEntry.DefaultValue;
        }

        [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnApplyClick))]
        [HarmonyPostfix]
        public static void UIOptionWindow_OnApplyClick_Postfix() =>
            SetConfig(
                DisableMessageBoxToggle.isOn,
                IconVersionComboBox.itemIndex + 1,
                EnableDestroyToggle.isOn,
                EnableFuelRodFracToggle.isOn,
                EnableMatrixFracToggle.isOn,
                EnableBuildingAsTrashToggle.isOn
            );
    }
}
