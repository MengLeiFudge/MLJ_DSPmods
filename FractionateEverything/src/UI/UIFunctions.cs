using CommonAPI.Systems;
using FE.UI.Components;
using FE.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI;

public static class UIFunctions {
    private static bool _initialized;
    private static PressKeyBind _toggleKey;
    private static bool _configWinInitialized;
    private static MyConfigWindow _configWin;
    private static GameObject _buttonOnPlanetGlobe;

    public static void Init() {
        _toggleKey = KeyBindings.RegisterKeyBinding(new BuiltinKey {
            key = new CombineKey((int)KeyCode.F, CombineKey.SHIFT_COMB, ECombineKeyAction.OnceClick, false),
            conflictGroup = KeyBindConflict.MOVEMENT
                            | KeyBindConflict.FLYING
                            | KeyBindConflict.SAILING
                            | KeyBindConflict.BUILD_MODE_1
                            | KeyBindConflict.KEYBOARD_KEYBIND,
            name = "OpenFEMainWindow",
            canOverride = true
        });
        TranslationUtils.Register("KEYOpenFEMainWindow", "[FE] Open main window", "[FE] 打开主页");
        // I18N.OnInitialized += RecreateConfigWindow;
    }

    public static void OnInputUpdate() {
        if (_toggleKey.keyValue) {
            ToggleConfigWindow();
        }
    }

    #region ConfigWindow

    public static void ToggleConfigWindow() {
        if (!_configWinInitialized) {
            // if (!I18N.Initialized()) return;
            _configWinInitialized = true;
            _configWin = MyConfigWindow.CreateInstance();
        }

        if (_configWin.active) {
            _configWin._Close();
        } else {
            _configWin.Open();
        }
    }

    #endregion
}
