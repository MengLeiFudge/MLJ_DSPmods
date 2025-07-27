using CommonAPI.Systems;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI;

/// <summary>
/// 使用快捷键打开窗口。默认快捷键为Shift+F。
/// </summary>
public static class UIFunctions {
    private static bool _initialized;
    private static PressKeyBind _toggleKey;
    private static bool _configWinInitialized;
    private static MyConfigWindow _configWin;
    private static GameObject _buttonOnPlanetGlobe;

    public static void Init() {
        _toggleKey = CustomKeyBindSystem.RegisterKeyBindWithReturn<PressKeyBind>(new() {
            key = new((int)KeyCode.F, CombineKey.SHIFT_COMB, ECombineKeyAction.OnceClick, false),
            conflictGroup = KeyBindConflict.MOVEMENT
                            | KeyBindConflict.FLYING
                            | KeyBindConflict.SAILING
                            | KeyBindConflict.BUILD_MODE_1
                            | KeyBindConflict.KEYBOARD_KEYBIND,
            name = "OpenFracCenter",
            canOverride = true
        });
        Utils.Utils.Register("KEYOpenFracCenter", "[FE] Open Fractionate Center", "[FE] 打开分馏数据中心");
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
