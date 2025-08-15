using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.UI.Components;
using FE.UI.View.CoreOperate;
using FE.UI.View.GetItemRecipe;
using FE.UI.View.ModPackage;
using FE.UI.View.ProgressSystem;
using FE.UI.View.Setting;
using FE.UI.View.Statistic;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View;

public static class MainWindow {
    private static RectTransform _windowTrans;
    private static PressKeyBind _toggleKey;
    private static bool _configWinInitialized;
    private static MyConfigWindow _configWin;

    public static void AddTranslations() {
        Register("KEYOpenFracCenter", "[FE] Open Fractionation Data Center", "[FE] 打开分馏数据中心");
        Register("分馏数据中心", "Fractionation Data Center");
        Register("核心操作", "Core Operation");
        Register("物品管理", "Item Management");
        Register("资源获取", "Resource Collection");
        Register("进度系统", "Progress System");
        Register("统计相关", "Statistic Related");
        Register("系统设置", "System Setting");
    }

    public static void LoadConfig(ConfigFile configFile) {
        BuildingOperate.LoadConfig(configFile);
        RecipeOperate.LoadConfig(configFile);

        ItemInteraction.LoadConfig(configFile);
        ImportantItem.LoadConfig(configFile);

        TicketRaffle.LoadConfig(configFile);
        SelectedRaffle.LoadConfig(configFile);
        LimitedTimeStore.LoadConfig(configFile);

        MainTask.LoadConfig(configFile);
        RecurringTask.LoadConfig(configFile);
        Achievements.LoadConfig(configFile);
        DevelopmentDiary.LoadConfig(configFile);

        RecipeGallery.LoadConfig(configFile);
        FracStatistic.LoadConfig(configFile);

        VipFeatures.LoadConfig(configFile);
        PopupDisplay.LoadConfig(configFile);
        SandboxMode.LoadConfig(configFile);
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
    }

    private static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        wnd.AddTabGroup(trans, "核心操作");
        BuildingOperate.CreateUI(wnd, trans);
        RecipeOperate.CreateUI(wnd, trans);
        wnd.AddTabGroup(trans, "物品管理");
        ItemInteraction.CreateUI(wnd, trans);
        ImportantItem.CreateUI(wnd, trans);
        wnd.AddTabGroup(trans, "资源获取");
        TicketRaffle.CreateUI(wnd, trans);
        SelectedRaffle.CreateUI(wnd, trans);
        LimitedTimeStore.CreateUI(wnd, trans);
        wnd.AddTabGroup(trans, "进度系统");
        MainTask.CreateUI(wnd, trans);
        RecurringTask.CreateUI(wnd, trans);
        Achievements.CreateUI(wnd, trans);
        DevelopmentDiary.CreateUI(wnd, trans);
        wnd.AddTabGroup(trans, "统计相关");
        RecipeGallery.CreateUI(wnd, trans);
        FracStatistic.CreateUI(wnd, trans);
        wnd.AddTabGroup(trans, "系统设置");
        VipFeatures.CreateUI(wnd, trans);
        PopupDisplay.CreateUI(wnd, trans);
        SandboxMode.CreateUI(wnd, trans);
    }

    private static void UpdateUI() {
        BuildingOperate.UpdateUI();
        RecipeOperate.UpdateUI();

        ItemInteraction.UpdateUI();
        ImportantItem.UpdateUI();

        TicketRaffle.UpdateUI();
        SelectedRaffle.UpdateUI();
        LimitedTimeStore.UpdateUI();

        MainTask.UpdateUI();
        RecurringTask.UpdateUI();
        Achievements.UpdateUI();
        DevelopmentDiary.UpdateUI();

        RecipeGallery.UpdateUI();
        FracStatistic.UpdateUI();

        VipFeatures.UpdateUI();
        PopupDisplay.UpdateUI();
        SandboxMode.UpdateUI();
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

        BuildingOperate.Import(r);
        RecipeOperate.Import(r);

        ItemInteraction.Import(r);
        ImportantItem.Import(r);

        TicketRaffle.Import(r);
        SelectedRaffle.Import(r);
        LimitedTimeStore.Import(r);

        MainTask.Import(r);
        RecurringTask.Import(r);
        Achievements.Import(r);
        DevelopmentDiary.Import(r);

        RecipeGallery.Import(r);
        FracStatistic.Import(r);

        VipFeatures.Import(r);
        PopupDisplay.Import(r);
        SandboxMode.Import(r);
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);

        BuildingOperate.Export(w);
        RecipeOperate.Export(w);

        ItemInteraction.Export(w);
        ImportantItem.Export(w);

        TicketRaffle.Export(w);
        SelectedRaffle.Export(w);
        LimitedTimeStore.Export(w);

        MainTask.Export(w);
        RecurringTask.Export(w);
        Achievements.Export(w);
        DevelopmentDiary.Export(w);

        RecipeGallery.Export(w);
        FracStatistic.Export(w);

        VipFeatures.Export(w);
        PopupDisplay.Export(w);
        SandboxMode.Export(w);
    }

    public static void IntoOtherSave() {
        BuildingOperate.IntoOtherSave();
        RecipeOperate.IntoOtherSave();

        ItemInteraction.IntoOtherSave();
        ImportantItem.IntoOtherSave();

        TicketRaffle.IntoOtherSave();
        SelectedRaffle.IntoOtherSave();
        LimitedTimeStore.IntoOtherSave();

        MainTask.IntoOtherSave();
        RecurringTask.IntoOtherSave();
        Achievements.IntoOtherSave();
        DevelopmentDiary.IntoOtherSave();

        RecipeGallery.IntoOtherSave();
        FracStatistic.IntoOtherSave();

        VipFeatures.IntoOtherSave();
        PopupDisplay.IntoOtherSave();
        SandboxMode.IntoOtherSave();
    }

    #endregion
}
