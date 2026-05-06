using System.Collections.Generic;
using FE.UI.Controls;
using FE.Utils;
using HarmonyLib;

namespace FE.UI.Foundation.Window;

/// <summary>
/// FE 自定义窗口的初始化与启停管理基类。
/// </summary>
public abstract class MyWindowManager {
    private static readonly List<ManualBehaviour> Windows = new(4);

    public static bool Initialized { get; private set; }

    public static void Enable(bool on) {
        Patch.Enable(on);
    }

    public static void InitBaseObjects() {
        MyWindow.InitBaseObject();
        MyCheckButton.InitBaseObject();
        MyCheckBox.InitBaseObject();
        MyComboBox.InitBaseObject();
        MyCornerComboBox.InitBaseObject();
        MyFlatButton.InitBaseObject();
        MyImageButton.InitBaseObject();
    }

    public static T CreateWindow<T>(string name, string title = "") where T : MyWindow {
        var win = MyWindow.Create<T>(name, title);
        if (win) Windows.Add(win);
        return win;
    }

    public static void DestroyWindow(ManualBehaviour win) {
        if (win == null) return;
        Windows.Remove(win);
        win._Free();
        win._Destroy();
    }

    /*
    public static void SetRect(ManualBehaviour win, RectTransform rect)
    {
        var rectTransform = win.GetComponent<RectTransform>();
        //rectTransform.position =
        //rectTransform.sizeDelta = rect;
    }
    */
    /// <summary>
    /// 在 UI 根节点创建后初始化自定义窗口。
    /// </summary>
    public class Patch : PatchImpl<Patch> {
        protected override void OnEnable() {
            InitAllWindows();
        }

        private static void InitAllWindows() {
            if (Initialized) return;
            if (!UIRoot.instance) return;
            foreach (var win in Windows) {
                win._Init(win.data);
            }
            Initialized = true;
        }

        /*
        //_Create -> _Init
        [HarmonyPostfix, HarmonyPatch(typeof(UIGame), "_OnCreate")]
        public static void UIGame__OnCreate_Postfix()
        {
        }
        */

        [HarmonyPostfix, HarmonyPatch(typeof(UIRoot), nameof(UIRoot._OnDestroy))]
        public static void UIRoot__OnDestroy_Postfix() {
            foreach (var win in Windows) {
                win._Free();
                win._Destroy();
            }

            Windows.Clear();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIRoot), nameof(UIRoot._OnOpen))]
        public static void UIRoot__OnOpen_Postfix() {
            InitAllWindows();
        }

        /*
        [HarmonyPostfix, HarmonyPatch(typeof(UIGame), "_OnFree")]
        public static void UIGame__OnFree_Postfix()
        {
            foreach (var win in Windows)
            {
                win._Free();
            }
        }
        */

        [HarmonyPostfix, HarmonyPatch(typeof(UIRoot), nameof(UIRoot._OnUpdate))]
        public static void UIRoot__OnUpdate_Postfix() {
            if (GameMain.isPaused || !GameMain.isRunning) {
                return;
            }

            foreach (var win in Windows) {
                win._Update();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIGame), nameof(UIGame.ShutAllFunctionWindow))]
        public static void UIGame_ShutAllFunctionWindow_Postfix() {
            foreach (var win in Windows) {
                if (win is MyWindow theWin && theWin.IsWindowFunctional()) {
                    theWin.TryClose();
                }
            }
        }
    }
}
