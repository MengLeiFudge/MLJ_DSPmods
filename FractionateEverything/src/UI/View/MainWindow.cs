using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.UI.Components;
using FE.UI.View.CoreOperate;
using FE.UI.View.GetItemRecipe;
using FE.UI.View.ModPackage;
using FE.UI.View.ProgressSystem;
using FE.UI.View.RuneSystem;
using FE.UI.View.Setting;
using FE.UI.View.Statistic;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View;

public static class MainWindow {
    private const string MainPanelSelectionBlockTag = "MainPanelSelection";
    private static PressKeyBind _toggleKey;
    private static PressKeyBind _switchStyleKey;
    private static bool _legacyConfigWinInitialized;
    private static MyConfigWindow _legacyConfigWin;
    private static bool _analysisMainWindowInitialized;
    private static MyAnalysisWindow _analysisMainWindow;
    private static readonly IFEMainPanelSharedState defaultSharedPanelState = new FEMainPanelSharedState();
    private static bool sandboxMode = false;
    public static IReadOnlyList<MainWindowCategoryDefinition> AnalysisPageCategories { get; private set; } = [];

    public static FEMainPanelType SelectedMainPanelType { get; private set; } = FEMainPanelType.Legacy;
    public static FEMainPanelType OpenedMainPanelType { get; private set; } = FEMainPanelType.None;
    public static IFEMainPanelSharedState SharedPanelState { get; private set; } = defaultSharedPanelState;

    public static void AddTranslations() {
        Register("KEYOpenFracCentre", "[FE] Open Fractionation Data Centre", "[FE] 打开分馏数据中心");
        Register("KEYSwitchFracCentreStyle", "[FE] Switch Fractionation Data Centre Style",
            "[FE] 切换分馏数据中心界面风格");
        Register("分馏数据中心", "Fractionation Data Centre");
        Register("切换到分析主面板", "Switch to analysis main panel");
        Register("切换到旧版主面板", "Switch to legacy main panel");
        Register("核心操作", "Core Operation");
        FracRecipeOperate.AddTranslations();
        VanillaRecipeOperate.AddTranslations();
        BuildingOperate.AddTranslations();
        Register("物品管理", "Item Management");
        ItemInteraction.AddTranslations();
        ImportantItem.AddTranslations();
        RuneMenu.AddTranslations();
        Register("抽奖", "Raffle");
        Register("商店", "Store");
        Register("前往商店", "Go to Store");
        Register("前往抽奖", "Go to Raffle");
        GachaWindow.AddTranslations();
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
        FracRecipeOperate.LoadConfig(configFile);
        VanillaRecipeOperate.LoadConfig(configFile);
        BuildingOperate.LoadConfig(configFile);

        ItemInteraction.LoadConfig(configFile);
        ImportantItem.LoadConfig(configFile);
        RuneMenu.LoadConfig(configFile);

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

        _switchStyleKey = CustomKeyBindSystem.RegisterKeyBindWithReturn<PressKeyBind>(new() {
            key = new((int)KeyCode.F, (byte)(CombineKey.CTRL_COMB | CombineKey.SHIFT_COMB), ECombineKeyAction.OnceClick,
                false),
            conflictGroup = KeyBindConflict.MOVEMENT
                            | KeyBindConflict.FLYING
                            | KeyBindConflict.SAILING
                            | KeyBindConflict.BUILD_MODE_1
                            | KeyBindConflict.KEYBOARD_KEYBIND,
            name = "SwitchFracCentreStyle",
            canOverride = true
        });
    }

    private static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        foreach (MainWindowCategoryDefinition category in MainWindowPageRegistry.GetCategories(
                     FEMainPanelType.Legacy, sandboxMode)) {
            wnd.AddTabGroup(trans, category.CategoryName);
            foreach (MainWindowPageDefinition page in category.Pages) {
                page.CreateUI(wnd, trans);
            }
        }
    }

    private static void UpdateUI() {
        foreach (MainWindowCategoryDefinition category in MainWindowPageRegistry.GetCategories(
                     FEMainPanelType.Legacy, sandboxMode)) {
            foreach (MainWindowPageDefinition page in category.Pages) {
                page.UpdateUI();
            }
        }
    }

    public static void OnInputUpdate() {
        RefreshOpenedMainPanelState();

        if (GameMain.isPaused
            || !GameMain.isRunning
            || GameMain.isFullscreenPaused
            || GameMain.mainPlayer == null) {
            CloseAllMainPanels();
            return;
        }
        if (VFInput.inputing) {
            return;
        }

        if (!GameMain.history.TechUnlocked(TFE分馏数据中心)) {
            return;
        }

        if (_toggleKey.keyValue) {
            if (OpenedMainPanelType == FEMainPanelType.None) {
                OpenSelectedMainPanel();
            } else {
                CloseAllMainPanels();
            }
            return;
        }

        if (_switchStyleKey.keyValue && OpenedMainPanelType != FEMainPanelType.None) {
            SwitchMainPanelFrom(OpenedMainPanelType);
        }
    }

    public static FEMainPanelType GetCurrentMainPanelType() {
        return OpenedMainPanelType != FEMainPanelType.None
            ? OpenedMainPanelType
            : NormalizeMainPanelSelection(SelectedMainPanelType);
    }

    public static void BindSharedPanelState(IFEMainPanelSharedState sharedState) {
        SharedPanelState = sharedState ?? defaultSharedPanelState;
    }

    public static void SelectMainPanel(FEMainPanelType panelType) {
        if (panelType is FEMainPanelType.Legacy or FEMainPanelType.Analysis) {
            SelectedMainPanelType = panelType;
        }
    }

    public static string GetSwitchMainPanelButtonLabel(FEMainPanelType currentPanelType) {
        return NormalizeMainPanelSelection(currentPanelType) == FEMainPanelType.Analysis
            ? "切换到旧版主面板"
            : "切换到分析主面板";
    }

    public static string GetSwitchMainPanelButtonLabel() {
        return GetSwitchMainPanelButtonLabel(GetCurrentMainPanelType());
    }

    public static void SwitchMainPanelFrom(FEMainPanelType currentPanelType) {
        SelectMainPanel(NormalizeMainPanelSelection(currentPanelType));
        SwitchSelectedMainPanelAndOpen();
    }

    public static void NavigateToPage(string categoryName, int internalTabIndex = 0) {
        if (OpenedMainPanelType == FEMainPanelType.Legacy && _legacyConfigWin != null) {
            _legacyConfigWin.JumpToGroup(categoryName, internalTabIndex);
        } else if (OpenedMainPanelType == FEMainPanelType.Analysis && _analysisMainWindow != null) {
            _analysisMainWindow.JumpToCategory(categoryName, internalTabIndex);
        }
    }

    private static FEMainPanelType NormalizeMainPanelSelection(FEMainPanelType panelType) {
        return panelType is FEMainPanelType.Legacy or FEMainPanelType.Analysis
            ? panelType
            : FEMainPanelType.Legacy;
    }

    public static void OpenSelectedMainPanel() {
        OpenMainPanel(SelectedMainPanelType);
    }

    public static void ToggleSelectedMainPanel() {
        if (OpenedMainPanelType == SelectedMainPanelType) {
            CloseAllMainPanels();
            return;
        }
        OpenSelectedMainPanel();
    }

    public static void SwitchSelectedMainPanelAndOpen() {
        SelectedMainPanelType = SelectedMainPanelType == FEMainPanelType.Legacy
            ? FEMainPanelType.Analysis
            : FEMainPanelType.Legacy;
        OpenSelectedMainPanel();
    }

    private static void OpenMainPanel(FEMainPanelType panelType) {
        if (!IsMainPanelImplemented(panelType)) {
            return;
        }

        CloseAllMainPanels();
        switch (panelType) {
            case FEMainPanelType.Legacy:
                OpenLegacyMainPanel();
                OpenedMainPanelType = FEMainPanelType.Legacy;
                break;
            case FEMainPanelType.Analysis:
                OpenAnalysisMainPanel();
                OpenedMainPanelType = FEMainPanelType.Analysis;
                break;
        }
    }

    private static bool IsMainPanelImplemented(FEMainPanelType panelType) {
        return panelType is FEMainPanelType.Legacy or FEMainPanelType.Analysis;
    }

    private static void OpenLegacyMainPanel() {
        bool sandboxModeChanged = sandboxMode != GameMain.sandboxToolsEnabled;
        sandboxMode = GameMain.sandboxToolsEnabled;
        if (!_legacyConfigWinInitialized) {
            _legacyConfigWinInitialized = true;
            _legacyConfigWin = MyConfigWindow.CreateInstance("FEMainWindow", "分馏数据中心");
        } else if (sandboxModeChanged) {
            _legacyConfigWin = MyConfigWindow.CreateInstance("FEMainWindow", "分馏数据中心");
        }

        _legacyConfigWin?.Open();
    }

    private static void OpenAnalysisMainPanel() {
        sandboxMode = GameMain.sandboxToolsEnabled;
        RefreshAnalysisPageCategories();
        if (!_analysisMainWindowInitialized) {
            _analysisMainWindowInitialized = true;
            _analysisMainWindow = MyAnalysisWindow.CreateInstance("FEAnalysisMainWindow", "分馏数据中心");
        }

        _analysisMainWindow?.OpenWindow();
    }

    private static void CloseAllMainPanels() {
        CloseLegacyMainPanel();
        CloseAnalysisMainPanel();
        OpenedMainPanelType = FEMainPanelType.None;
    }

    private static void CloseLegacyMainPanel() {
        if (!_legacyConfigWinInitialized) {
            return;
        }

        if (_legacyConfigWin != null && _legacyConfigWin.active) {
            _legacyConfigWin._Close();
        }
    }

    private static void CloseAnalysisMainPanel() {
        if (!_analysisMainWindowInitialized) {
            return;
        }

        if (_analysisMainWindow != null && _analysisMainWindow.active) {
            _analysisMainWindow.CloseWindow();
        }
    }

    private static void RefreshOpenedMainPanelState() {
        if (_legacyConfigWinInitialized && _legacyConfigWin != null && _legacyConfigWin.active) {
            OpenedMainPanelType = FEMainPanelType.Legacy;
            return;
        }

        if (_analysisMainWindowInitialized && _analysisMainWindow != null && _analysisMainWindow.active) {
            OpenedMainPanelType = FEMainPanelType.Analysis;
            return;
        }

        OpenedMainPanelType = FEMainPanelType.None;
    }

    private static void ImportMainPanelSelection(BinaryReader r) {
        SelectedMainPanelType = NormalizeMainPanelSelection((FEMainPanelType)r.ReadInt32());
    }

    private static void ExportMainPanelSelection(BinaryWriter w) {
        w.Write((int)NormalizeMainPanelSelection(SelectedMainPanelType));
    }

    private static void IntoOtherSaveMainPanelSelection() {
        SelectedMainPanelType = FEMainPanelType.Legacy;
        OpenedMainPanelType = FEMainPanelType.None;
    }

    private static void RefreshAnalysisPageCategories() {
        AnalysisPageCategories = MainWindowPageRegistry.GetCategories(FEMainPanelType.Analysis, sandboxMode, true);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            (MainPanelSelectionBlockTag, ImportMainPanelSelection),
            ("FracRecipeOperate", FracRecipeOperate.Import),
            ("VanillaRecipeOperate", VanillaRecipeOperate.Import),
            ("BuildingOperate", BuildingOperate.Import),
            ("ItemInteraction", ItemInteraction.Import),
            ("ImportantItem", ImportantItem.Import),
            ("RuneMenu", RuneMenu.Import),
            ("TicketRaffle", TicketRaffle.Import),
            ("LimitedTimeStore", LimitedTimeStore.Import),
            ("MainTask", MainTask.Import),
            ("RecurringTask", RecurringTask.Import),
            ("Achievements", Achievements.Import),
            ("DevelopmentDiary", DevelopmentDiary.Import),
            ("RecipeGallery", RecipeGallery.Import),
            ("FracStatistic", FracStatistic.Import),
            ("VipFeatures", VipFeatures.Import),
            ("Miscellaneous", Miscellaneous.Import),
            ("SandboxMode", SandboxMode.Import)
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            (MainPanelSelectionBlockTag, ExportMainPanelSelection),
            ("FracRecipeOperate", FracRecipeOperate.Export),
            ("VanillaRecipeOperate", VanillaRecipeOperate.Export),
            ("BuildingOperate", BuildingOperate.Export),
            ("ItemInteraction", ItemInteraction.Export),
            ("ImportantItem", ImportantItem.Export),
            ("RuneMenu", RuneMenu.Export),
            ("TicketRaffle", TicketRaffle.Export),
            ("LimitedTimeStore", LimitedTimeStore.Export),
            ("MainTask", MainTask.Export),
            ("RecurringTask", RecurringTask.Export),
            ("Achievements", Achievements.Export),
            ("DevelopmentDiary", DevelopmentDiary.Export),
            ("RecipeGallery", RecipeGallery.Export),
            ("FracStatistic", FracStatistic.Export),
            ("VipFeatures", VipFeatures.Export),
            ("Miscellaneous", Miscellaneous.Export),
            ("SandboxMode", SandboxMode.Export)
        );
    }

    public static void IntoOtherSave() {
        IntoOtherSaveMainPanelSelection();
        FracRecipeOperate.IntoOtherSave();
        VanillaRecipeOperate.IntoOtherSave();
        BuildingOperate.IntoOtherSave();

        ItemInteraction.IntoOtherSave();
        ImportantItem.IntoOtherSave();
        RuneMenu.IntoOtherSave();

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
