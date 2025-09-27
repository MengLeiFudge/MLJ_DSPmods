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
    private static PressKeyBind _toggleKey;
    private static bool _configWinInitialized;
    private static MyConfigWindow _configWin;
    private static bool sandboxMode = false;

    public static void AddTranslations() {
        Register("KEYOpenFracCentre", "[FE] Open Fractionation Data Centre", "[FE] 打开分馏数据中心");
        Register("分馏数据中心", "Fractionation Data Centre");
        Register("核心操作", "Core Operation");
        RecipeOperate.AddTranslations();
        BuildingOperate.AddTranslations();
        Register("物品管理", "Item Management");
        ItemInteraction.AddTranslations();
        ImportantItem.AddTranslations();
        Register("资源获取", "Resource Collection");
        TicketRaffle.AddTranslations();
        LimitedTimeStore.AddTranslations();
        Register("进度系统", "Progress System");
        MainTask.AddTranslations();
        RecurringTask.AddTranslations();
        Achievements.AddTranslations();
        DevelopmentDiary.AddTranslations();
        Register("统计相关", "Statistic Related");
        RecipeGallery.AddTranslations();
        FracStatistic.AddTranslations();
        Register("系统设置", "System Setting");
        VipFeatures.AddTranslations();
        Miscellaneous.AddTranslations();
        SandboxMode.AddTranslations();
    }

    public static void LoadConfig(ConfigFile configFile) {
        RecipeOperate.LoadConfig(configFile);
        BuildingOperate.LoadConfig(configFile);

        ItemInteraction.LoadConfig(configFile);
        ImportantItem.LoadConfig(configFile);

        TicketRaffle.LoadConfig(configFile);
        LimitedTimeStore.LoadConfig(configFile);

        MainTask.LoadConfig(configFile);
        RecurringTask.LoadConfig(configFile);
        Achievements.LoadConfig(configFile);
        DevelopmentDiary.LoadConfig(configFile);

        RecipeGallery.LoadConfig(configFile);
        FracStatistic.LoadConfig(configFile);

        VipFeatures.LoadConfig(configFile);
        Miscellaneous.LoadConfig(configFile);
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
            name = "OpenFracCentre",
            canOverride = true
        });
    }

    private static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        wnd.AddTabGroup(trans, "核心操作");
        RecipeOperate.CreateUI(wnd, trans);
        BuildingOperate.CreateUI(wnd, trans);
        wnd.AddTabGroup(trans, "物品管理");
        ItemInteraction.CreateUI(wnd, trans);
        ImportantItem.CreateUI(wnd, trans);
        wnd.AddTabGroup(trans, "资源获取");
        TicketRaffle.CreateUI(wnd, trans);
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
        Miscellaneous.CreateUI(wnd, trans);
        if (sandboxMode) {
            SandboxMode.CreateUI(wnd, trans);
        }
    }

    private static void UpdateUI() {
        RecipeOperate.UpdateUI();
        BuildingOperate.UpdateUI();

        ItemInteraction.UpdateUI();
        ImportantItem.UpdateUI();

        TicketRaffle.UpdateUI();
        LimitedTimeStore.UpdateUI();

        MainTask.UpdateUI();
        RecurringTask.UpdateUI();
        Achievements.UpdateUI();
        DevelopmentDiary.UpdateUI();

        RecipeGallery.UpdateUI();
        FracStatistic.UpdateUI();

        VipFeatures.UpdateUI();
        Miscellaneous.UpdateUI();
        if (sandboxMode) {
            SandboxMode.UpdateUI();
        }
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

    private static void ToggleConfigWindow() {
        if (!_configWinInitialized) {
            _configWinInitialized = true;
            sandboxMode = GameMain.sandboxToolsEnabled;
            _configWin = MyConfigWindow.CreateInstance("FEMainWindow", "分馏数据中心");
        }
        if (_configWin.active) {
            _configWin._Close();
        } else {
            if (sandboxMode != GameMain.sandboxToolsEnabled) {
                sandboxMode = GameMain.sandboxToolsEnabled;
                _configWin = MyConfigWindow.CreateInstance("FEMainWindow", "分馏数据中心");
            }
            _configWin.Open();
        }
    }

    private static void CloseConfigWindow() {
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

        RecipeOperate.Import(r);
        BuildingOperate.Import(r);

        ItemInteraction.Import(r);
        ImportantItem.Import(r);

        TicketRaffle.Import(r);
        if (version <= 1) {
            r.ReadInt32();
        }
        LimitedTimeStore.Import(r);

        MainTask.Import(r);
        RecurringTask.Import(r);
        Achievements.Import(r);
        DevelopmentDiary.Import(r);

        RecipeGallery.Import(r);
        FracStatistic.Import(r);

        VipFeatures.Import(r);
        Miscellaneous.Import(r);
        SandboxMode.Import(r);
    }

    public static void Export(BinaryWriter w) {
        w.Write(2);

        BuildingOperate.Export(w);
        RecipeOperate.Export(w);

        ItemInteraction.Export(w);
        ImportantItem.Export(w);

        TicketRaffle.Export(w);
        LimitedTimeStore.Export(w);

        MainTask.Export(w);
        RecurringTask.Export(w);
        Achievements.Export(w);
        DevelopmentDiary.Export(w);

        RecipeGallery.Export(w);
        FracStatistic.Export(w);

        VipFeatures.Export(w);
        Miscellaneous.Export(w);
        SandboxMode.Export(w);
    }

    public static void IntoOtherSave() {
        BuildingOperate.IntoOtherSave();
        RecipeOperate.IntoOtherSave();

        ItemInteraction.IntoOtherSave();
        ImportantItem.IntoOtherSave();

        TicketRaffle.IntoOtherSave();
        LimitedTimeStore.IntoOtherSave();

        MainTask.IntoOtherSave();
        RecurringTask.IntoOtherSave();
        Achievements.IntoOtherSave();
        DevelopmentDiary.IntoOtherSave();

        RecipeGallery.IntoOtherSave();
        FracStatistic.IntoOtherSave();

        VipFeatures.IntoOtherSave();
        Miscellaneous.IntoOtherSave();
        SandboxMode.IntoOtherSave();
    }

    #endregion
}
