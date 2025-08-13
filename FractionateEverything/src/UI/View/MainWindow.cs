using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View;

public static class MainWindow {
    private static RectTransform _windowTrans;
    private static PressKeyBind _toggleKey;
    private static bool _configWinInitialized;
    private static MyConfigWindow _configWin;

    public static void LoadConfig(ConfigFile configFile) {
        TabCoreOperate.LoadConfig(configFile);
        TabPackage.LoadConfig(configFile);
        TabGetItemRecipe.LoadConfig(configFile);
        TabProgress.LoadConfig(configFile);
        TabStatistic.LoadConfig(configFile);
        TabSetting.LoadConfig(configFile);
    }

    public static void Init() {
        MyConfigWindow.OnUICreated += CreateUI;
        MyConfigWindow.OnUpdateUI += UpdateUI;
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
        Register("KEYOpenFracCenter", "[FE] Open Fractionation Data Center", "[FE] 打开分馏数据中心");
        Register("分馏数据中心", "Fractionation Data Center");
    }

    private static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        TabCoreOperate.CreateUI(wnd, trans);
        TabPackage.CreateUI(wnd, trans);
        TabGetItemRecipe.CreateUI(wnd, trans);
        TabProgress.CreateUI(wnd, trans);
        TabStatistic.CreateUI(wnd, trans);
        TabSetting.CreateUI(wnd, trans);
    }

    private static void UpdateUI() {
        TabCoreOperate.UpdateUI();
        TabPackage.UpdateUI();
        TabGetItemRecipe.UpdateUI();
        TabProgress.UpdateUI();
        TabStatistic.UpdateUI();
        TabSetting.UpdateUI();
    }

    public static void OnInputUpdate() {
        if (GameMain.isPaused
            || !GameMain.isRunning
            || GameMain.isFullscreenPaused
            || GameMain.mainPlayer == null) {
            CloseConfigWindow();
            return;
        }
        if (VFInput.inputing) {
            return;
        }
        if (_toggleKey.keyValue && GameMain.history.TechUnlocked(TFE分馏数据中心)) {
            ToggleConfigWindow();
        }
    }

    public static void ToggleConfigWindow() {
        if (!_configWinInitialized) {
            _configWinInitialized = true;
            _configWin = MyConfigWindow.CreateInstance("FEMainWindow", "分馏数据中心");
        }
        if (_configWin.active) {
            _configWin._Close();
        } else {
            _configWin.Open();
        }
    }

    public static void CloseConfigWindow() {
        if (!_configWinInitialized) {
            return;
        }
        if (_configWin.active) {
            _configWin._Close();
        }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        TabCoreOperate.Import(r);
        TabPackage.Import(r);
        TabGetItemRecipe.Import(r);
        TabProgress.Import(r);
        TabStatistic.Import(r);
        TabSetting.Import(r);
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        TabCoreOperate.Export(w);
        TabPackage.Export(w);
        TabGetItemRecipe.Export(w);
        TabProgress.Export(w);
        TabStatistic.Export(w);
        TabSetting.Export(w);
    }

    public static void IntoOtherSave() {
        TabCoreOperate.IntoOtherSave();
        TabPackage.IntoOtherSave();
        TabGetItemRecipe.IntoOtherSave();
        TabProgress.IntoOtherSave();
        TabStatistic.IntoOtherSave();
        TabSetting.IntoOtherSave();
    }

    #endregion
}
