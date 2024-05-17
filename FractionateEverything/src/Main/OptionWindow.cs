using FractionateEverything.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static FractionateEverything.FractionateEverything;

namespace FractionateEverything.Main {
    public static class OptionWindow {
        private static bool _initFinished;
        private static UIToggle DisableMessageBoxToggle;
        private static UIComboBox IconVersionComboBox;
        private static UIToggle EnableDestroyToggle;

        private static void Init() {
            if (_initFinished) return;

            int baseY = GenesisBook.Enable ? -340 : -220;

            CreateUIToggle("fe-dmb-setting",
                "DisableMessageBox".Translate(), "DisableMessageBoxAdditionalText".Translate(), new(30, baseY - 40 * 0),
                disableMessageBox, out DisableMessageBoxToggle);

            CreateComboBox("fe-iv-setting",
                "IconVersion".Translate(), new(30, baseY - 40 * 1),
                ["v1".Translate(), "v2".Translate(), "v3".Translate()], iconVersion - 1, out IconVersionComboBox);

            CreateUIToggle("fe-ed-setting",
                "EnableDestroy".Translate(), "EnableDestroyAdditionalText".Translate(), new(30, baseY - 40 * 2),
                enableDestroy, out EnableDestroyToggle);

            _initFinished = true;
        }

        private static void CreateUIToggle(string name, string text, string additionalTextStr,
            Vector2 position, bool defaultValue, out UIToggle toggle) {
            GameObject original = GameObject.Find(
                "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-3/list/scroll-view/viewport/content/demolish-query");
            Transform parent = GameObject
                .Find("UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5/advisor-tips")
                .transform.parent;
            GameObject settingObj = Object.Instantiate(original, parent);
            settingObj.name = name;
            Object.DestroyImmediate(settingObj.GetComponent<Localizer>());
            settingObj.GetComponent<Text>().text = text;
            var settingObjTransform = (RectTransform)settingObj.transform;
            settingObjTransform.anchoredPosition = position;

            toggle = settingObj.GetComponentInChildren<UIToggle>();
            toggle.isOn = defaultValue;
            toggle.toggle.onValueChanged.RemoveAllListeners();

            Transform additionalText = settingObj.transform.GetChild(1);
            Object.DestroyImmediate(additionalText.GetComponent<Localizer>());
            additionalText.GetComponent<Text>().text = additionalTextStr;
        }

        private static void CreateComboBox(string name, string text,
            Vector2 position, List<string> options, int defaultValue, out UIComboBox comboBox) {
            GameObject original = GameObject.Find(
                "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-1/msaa");
            Transform parent = GameObject
                .Find("UI Root/Overlay Canvas/Top Windows/Option Window/details/content-5/advisor-tips")
                .transform.parent;
            GameObject settingObj = Object.Instantiate(original, parent);
            settingObj.name = name;
            Object.DestroyImmediate(settingObj.GetComponent<Localizer>());
            settingObj.GetComponent<Text>().text = text;
            var settingObjTransform = (RectTransform)settingObj.transform;
            settingObjTransform.anchoredPosition = position;

            comboBox = settingObj.GetComponentInChildren<UIComboBox>();
            comboBox.Items = options;
            comboBox.itemIndex = defaultValue;
            comboBox.onItemIndexChange.RemoveAllListeners();
        }

        [HarmonyPatch(typeof(UIOptionWindow), "_OnOpen")]
        [HarmonyPostfix]
        public static void UIOptionWindow_OnOpen_Postfix() {
            Init();
            Reset();
        }

        [HarmonyPatch(typeof(UIOptionWindow), "OnRevertButtonClick")]
        [HarmonyPostfix]
        public static void UIOptionWindow_OnRevertButtonClick_Postfix(int idx) {
            if (idx == 4) Reset();
        }

        private static void Reset() {
            DisableMessageBoxToggle.isOn = disableMessageBox;
            IconVersionComboBox.itemIndex = iconVersion - 1;
            EnableDestroyToggle.isOn = enableDestroy;
        }

        [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnApplyClick))]
        [HarmonyPostfix]
        public static void UIOptionWindow_OnApplyClick_Postfix() =>
            SetConfig(
                DisableMessageBoxToggle.isOn,
                IconVersionComboBox.itemIndex + 1,
                EnableDestroyToggle.isOn
            );
    }
}
