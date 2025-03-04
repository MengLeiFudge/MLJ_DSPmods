using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace FractionateEverything.UI {
    public static class UIHotkeySettingPatcher {
        /*public static void On_Shift_F_Switch()
        {
            if (this.dysonEditor.active || this.gameData.mainPlayer.fastTravelling || !this.gameData.mainPlayer.isAlive)
                return;
            if (this.replicator.active)
            {
                this.ShutAllFunctionWindow();
                this.CloseEnemyBriefInfo();
                this.ShutPlayerInventory();
            }
            else
            {
                this.ShutAllFunctionWindow();
                this.CloseEnemyBriefInfo();
                this.OpenReplicatorWindow();
                this.OpenPlayerInventory();
            }
        }








        //KeyCode都是大于等于8的，所以modifier shift=1 ctrl=2 alt=4
        public static Text title1;
        public static Text keyText1;
        public static UIButton uibtn1;
        public static GameObject waitingTextObj1;
        public static InputField inputField1;
        public static bool isWaiting1;
        public static KeyCode temp1;
        public static int tempModifier1;

        public static void Init() {
            GameObject oriSettingObj =
                GameObject.Find(
                    "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-4/list/scroll-view/viewport/content/key-entry");
            GameObject oriParent =
                GameObject.Find(
                    "UI Root/Overlay Canvas/Top Windows/Option Window/details/content-4/list/scroll-view/viewport/content");

            float oriWidth = oriParent.GetComponent<RectTransform>().sizeDelta.x;
            float oriHeight = oriParent.GetComponent<RectTransform>().sizeDelta.y;
            oriParent.GetComponent<RectTransform>().sizeDelta = new Vector2(oriWidth, oriHeight + 84);

            GameObject openWindowHKSettingObj = Object.Instantiate(oriSettingObj, oriParent.transform);
            openWindowHKSettingObj.SetActive(true);
            Object.DestroyImmediate(openWindowHKSettingObj.GetComponent<UIKeyEntry>());
            openWindowHKSettingObj.transform.Find("clear-key-btn").gameObject.SetActive(false);

            openWindowHKSettingObj.GetComponent<RectTransform>().anchoredPosition3D =
                new Vector3(30, -(oriHeight + 42) + 40);
            title1 = openWindowHKSettingObj.GetComponent<Text>();
            title1.text = "打开分馏界面".Translate();
            keyText1 = openWindowHKSettingObj.transform.Find("key").GetComponent<Text>();
            uibtn1 = openWindowHKSettingObj.transform.Find("input/InputField").GetComponent<UIButton>();
            inputField1 = openWindowHKSettingObj.transform.Find("input/InputField").GetComponent<InputField>();
            waitingTextObj1 = openWindowHKSettingObj.transform.Find("input/waiting-text").gameObject;
            uibtn1.onClick += _ => { OnSetKey1ButtonClick(); };

            openWindowHKSettingObj.transform.Find("set-default-btn").GetComponent<Button>().onClick
                .RemoveAllListeners();
            openWindowHKSettingObj.transform.Find("set-default-btn").GetComponent<Button>().onClick
                .AddListener(OnSetDefault1ButtonClick);

            RefreshAll();
        }

        public static void OnUpdate() {
            bool ShiftDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool CtrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool AltDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (isWaiting1) {
                //KeyCode key = KeyCode.LeftShift;
                KeyCode key = KeyCode.F;
                bool got = false;
                for (int i = 0; i < 26; i++) {
                    if (Input.GetKeyDown((KeyCode)(97 + i))) {
                        key = (KeyCode.A + i);
                        got = true;
                        break;
                    }
                }
                if (!got) {
                    for (int i = 0; i < 12; i++) {
                        if (Input.GetKeyDown((KeyCode)(KeyCode.F1 + i))) {
                            key = (KeyCode.F1 + i);
                            got = true;
                            break;
                        }
                    }
                }
                if (!got) {
                    for (int i = 0; i < 10; i++) {
                        if (Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + i))) {
                            key = (KeyCode.Keypad0 + i);
                            got = true;
                            break;
                        }
                    }
                }
                if (got && key >= KeyCode.A && key <= KeyCode.Z
                    || key >= KeyCode.F1 && key <= KeyCode.F12
                    || key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) {
                    if (isWaiting1)
                        SetOpenWindowHotKey(key, ShiftDown, CtrlDown, AltDown);

                    isWaiting1 = false;

                    RefreshAll();
                }

            }
        }

        public static void RefreshAll() {
            if (isWaiting1) {
                uibtn1.highlighted = true;
                waitingTextObj1.SetActive(true);
            } else {
                uibtn1.highlighted = false;
                waitingTextObj1.SetActive(false);
            }

            int modifier1 = tempModifier1;
            string txt1 = "";
            if ((modifier1 & 1) > 0) {
                txt1 += "Shift";
            }
            if ((modifier1 & 2) > 0) {
                if (txt1.Length > 0)
                    txt1 += " + ";
                txt1 += "Ctrl";
            }
            if ((modifier1 & 4) > 0) {
                if (txt1.Length > 0)
                    txt1 += " + ";
                txt1 += "Alt";
            }
            if (txt1.Length > 0)
                txt1 += " + ";
            txt1 += temp1.ToString();
            keyText1.text = txt1;
        }

        public static void OnSetKey1ButtonClick() {
            isWaiting1 = !isWaiting1;
            RefreshAll();
        }

        public static void OnSetDefault1ButtonClick() {
            isWaiting1 = false;
            SetOpenWindowHotKey(KeyCode.Q, false, false, false);
            RefreshAll();
        }

        public static void SetOpenWindowHotKey(KeyCode key, bool shift, bool ctrl, bool alt) {
            temp1 = key;
            int modifier = 0;
            if (shift) modifier += 1;
            if (ctrl) modifier += 2;
            if (alt) modifier += 4;
            tempModifier1 = modifier;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnApplyClick))]
        public static void Confirm() {
            // DSPCalculatorPlugin.OpenWindowHotKey.Value = temp1;
            // DSPCalculatorPlugin.OpenWindowModifier.Value = tempModifier1;
            // DSPCalculatorPlugin.OpenWindowHotKey.ConfigFile.Save();
            // RefreshAll();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow.OnCancelClick))]
        public static void Cancel() {
            // temp1 = DSPCalculatorPlugin.OpenWindowHotKey.Value;
            // tempModifier1 = DSPCalculatorPlugin.OpenWindowModifier.Value;
            // RefreshAll();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIOptionWindow), nameof(UIOptionWindow._OnOpen))]
        public static void OnOpen() {
            // temp1 = DSPCalculatorPlugin.OpenWindowHotKey.Value;
            // tempModifier1 = DSPCalculatorPlugin.OpenWindowModifier.Value;
            // RefreshAll();
        }

        // public static string GetFoldHotkeyString() {
        //     string result = "";
        //     int modifier = DSPCalculatorPlugin.SwitchWindowModifier.Value;
        //     if ((modifier & 1) > 0)
        //         result += "Shift";
        //     if ((modifier & 2) > 0) {
        //         if (result.Length > 0)
        //             result += " + ";
        //         result += "Ctrl";
        //     }
        //     if ((modifier & 4) > 0) {
        //         if (result.Length > 0)
        //             result += " + ";
        //         result += "Alt";
        //     }
        //     if (result.Length > 0)
        //         result += " + ";
        //     result += DSPCalculatorPlugin.SwitchWindowSizeHotKey.Value;
        //     return " (" + result + ")";
        // }*/
    }
}
