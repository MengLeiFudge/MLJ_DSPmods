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
        TabRecipeAndBuilding.LoadConfig(configFile);
        TabRaffle.LoadConfig(configFile);
        TabShop.LoadConfig(configFile);
        TabTask.LoadConfig(configFile);
        TabAchievement.LoadConfig(configFile);
        TabOtherSetting.LoadConfig(configFile);
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
        TabRecipeAndBuilding.CreateUI(wnd, trans);
        TabRaffle.CreateUI(wnd, trans);
        TabShop.CreateUI(wnd, trans);
        TabTask.CreateUI(wnd, trans);
        TabAchievement.CreateUI(wnd, trans);
        TabOtherSetting.CreateUI(wnd, trans);
    }

    private static void UpdateUI() {
        TabRecipeAndBuilding.UpdateUI();
        TabRaffle.UpdateUI();
        TabShop.UpdateUI();
        TabTask.UpdateUI();
        TabAchievement.UpdateUI();
        TabOtherSetting.UpdateUI();
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
        TabRecipeAndBuilding.Import(r);
        TabRaffle.Import(r);
        TabShop.Import(r);
        TabTask.Import(r);
        TabAchievement.Import(r);
        TabOtherSetting.Import(r);
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        TabRecipeAndBuilding.Export(w);
        TabRaffle.Export(w);
        TabShop.Export(w);
        TabTask.Export(w);
        TabAchievement.Export(w);
        TabOtherSetting.Export(w);
    }

    public static void IntoOtherSave() {
        TabRecipeAndBuilding.IntoOtherSave();
        TabRaffle.IntoOtherSave();
        TabShop.IntoOtherSave();
        TabTask.IntoOtherSave();
        TabAchievement.IntoOtherSave();
        TabOtherSetting.IntoOtherSave();
    }

    #endregion
}
