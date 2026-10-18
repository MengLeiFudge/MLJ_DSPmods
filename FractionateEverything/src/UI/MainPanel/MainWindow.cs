using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.UI.MainPanel.Civilization;
using FE.UI.MainPanel.ResourceInteraction;
using FE.UI.MainPanel.Setting;
using FE.UI.MainPanel.Shell.Analysis;
using FE.UI.MainPanel.Shell.MessageBox;
using FE.UI.MainPanel.Theme;
using FE.Logic.Civilization;
using UnityEngine;
using static FE.Utils.Utils;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel;

/// <summary>
/// FE 双主面板的打开、切换、导航和状态持久化入口。
/// </summary>
public static class MainWindow {
    private const string MainPanelSelectionBlockTag = "MainPanelSelection";
    private const float LegacyContentGap = 10f;
    private static PressKeyBind _toggleKey;
    private static PressKeyBind _switchStyleKey;
    private static bool _legacyConfigWinInitialized;
    private static MessageBoxMainPanelWindow _legacyConfigWin;
    private static bool _analysisMainWindowInitialized;
    private static AnalysisMainPanelWindow _analysisMainWindow;
    private static bool sandboxMode = false;
    /// <summary>当前窗口创建时的通信资格；变化后必须重建目录与页面。</summary>
    private static bool openedWithCommunication;
    private static bool legacyPageCategoriesInitialized;
    private static bool legacyPageCategoriesSandboxMode;
    private static IReadOnlyList<MainWindowCategoryDefinition> legacyPageCategories = [];
    public static IReadOnlyList<MainWindowCategoryDefinition> AnalysisPageCategories { get; private set; } = [];

    public static FEMainPanelType SelectedMainPanelType { get; private set; } = FEMainPanelType.Analysis;
    public static FEMainPanelType OpenedMainPanelType { get; private set; } = FEMainPanelType.None;
    private static string currentPageCategoryName;
    private static string currentPageSubpageName;

    public static void LoadConfig(ConfigFile configFile) {

        ItemInteraction.LoadConfig(configFile);

        Miscellaneous.LoadConfig(configFile);
    }

    public static void Init() {
        MessageBoxMainPanelWindow.OnUICreated += CreateUI;
        MessageBoxMainPanelWindow.OnUpdateUI += UpdateUI;
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

    private static void CreateUI(MessageBoxMainPanelWindow wnd, RectTransform trans) {
        foreach (MainWindowCategoryDefinition category in GetLegacyPageCategories()) {
            wnd.AddTabGroup(trans, category.CategoryName);
            foreach (MainWindowPageDefinition page in category.Pages) {
                RectTransform pageRoot = wnd.AddTab(trans, page.SubpageName);
                RectTransform designRoot = CreateLegacyDesignRoot(pageRoot);
                page.CreateUI(wnd, designRoot);
            }
        }
    }

    private static RectTransform CreateLegacyDesignRoot(RectTransform pageRoot) {
        var obj = new GameObject("legacy-design-root", typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, LegacyContentGap, LegacyContentGap, pageRoot);
        rect.sizeDelta = new(PageLayout.DesignWidth, PageLayout.DesignHeight);
        return rect;
    }

    private static void UpdateUI() {
        foreach (MainWindowCategoryDefinition category in GetLegacyPageCategories()) {
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
            ExplorationSignalHud.Update();
            CloseAllMainPanels();
            return;
        }
        ExplorationSignalHud.Update();
        if (OpenedMainPanelType != FEMainPanelType.None
            && openedWithCommunication != CivilizationRecoveryManager.HasDataCenterCommunication) {
            OpenMainPanel(OpenedMainPanelType);
        }
        if (VFInput.inputing) {
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
        if (!CivilizationRecoveryManager.HasDataCenterCommunication) {
            categoryName = "指引";
            internalTabIndex = 0;
        }
        if (OpenedMainPanelType == FEMainPanelType.Legacy && _legacyConfigWin != null) {
            _legacyConfigWin.JumpToGroup(categoryName, internalTabIndex);
            RememberCurrentPageRouteFromLegacy();
        } else if (OpenedMainPanelType == FEMainPanelType.Analysis && _analysisMainWindow != null) {
            _analysisMainWindow.JumpToCategory(categoryName, internalTabIndex);
            RememberCurrentPageRouteFromAnalysis();
        }
    }

    private static FEMainPanelType NormalizeMainPanelSelection(FEMainPanelType panelType) {
        return panelType is FEMainPanelType.Legacy or FEMainPanelType.Analysis
            ? panelType
            : FEMainPanelType.Analysis;
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
        openedWithCommunication = CivilizationRecoveryManager.HasDataCenterCommunication;
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

        if (openedWithCommunication) {
            ApplyCurrentPageRouteToOpenedPanel();
        } else {
            NavigateToPage("指引");
        }

    }

    private static bool IsMainPanelImplemented(FEMainPanelType panelType) {
        return panelType is FEMainPanelType.Legacy or FEMainPanelType.Analysis;
    }

    private static void OpenLegacyMainPanel() {
        sandboxMode = GameMain.sandboxToolsEnabled;
        RefreshLegacyPageCategories();
        RecreateLegacyMainPanelWindow();
        _legacyConfigWin?.Open();
    }

    private static void OpenAnalysisMainPanel() {
        sandboxMode = GameMain.sandboxToolsEnabled;
        RefreshAnalysisPageCategories();
        RecreateAnalysisMainPanelWindow();
        _analysisMainWindow?.OpenWindow();
    }

    /// <summary>
    /// 页面层仍保留静态控件引用，切换风格后必须重建旧版窗口，避免继续指向另一种风格的页面实例。
    /// </summary>
    private static void RecreateLegacyMainPanelWindow() {
        if (_legacyConfigWin != null) {
            MessageBoxMainPanelWindow.DestroyInstance(_legacyConfigWin);
            _legacyConfigWin = null;
        }

        _legacyConfigWinInitialized = true;
        _legacyConfigWin = MessageBoxMainPanelWindow.CreateInstance("FEMainWindow", "分馏数据中心");
        if (_legacyConfigWin != null) {
            _legacyConfigWin.OnFree += () => OnMainPanelWindowFreed(FEMainPanelType.Legacy);
        }
    }

    /// <summary>
    /// 分析面板缓存页面内容，但页面内部仍是静态 UI 引用；重新打开时重建整窗可确保当前风格重新绑定正确控件。
    /// </summary>
    private static void RecreateAnalysisMainPanelWindow() {
        if (_analysisMainWindow != null) {
            AnalysisMainPanelWindow.DestroyInstance(_analysisMainWindow);
            _analysisMainWindow = null;
        }

        _analysisMainWindowInitialized = true;
        _analysisMainWindow = AnalysisMainPanelWindow.CreateInstance("FEAnalysisMainWindow", "分馏数据中心");
        if (_analysisMainWindow != null) {
            _analysisMainWindow.OnFree += () => OnMainPanelWindowFreed(FEMainPanelType.Analysis);
        }
    }

    private static void CloseAllMainPanels() {
        CaptureCurrentPageRouteFromOpenedPanel();
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

    private static void OnMainPanelWindowFreed(FEMainPanelType panelType) {
        if (OpenedMainPanelType != panelType) {
            return;
        }

        // 右上角 X、Esc、游戏统一关窗都可能绕过 CloseAllMainPanels，
        // 这里统一补上关闭前的页面路由保存。
        CaptureCurrentPageRouteFromOpenedPanel();
        OpenedMainPanelType = FEMainPanelType.None;
    }

    private static void ImportMainPanelSelection(BinaryReader r) {
        SelectedMainPanelType = NormalizeMainPanelSelection((FEMainPanelType)r.ReadInt32());
    }

    private static void ExportMainPanelSelection(BinaryWriter w) {
        w.Write((int)NormalizeMainPanelSelection(SelectedMainPanelType));
    }

    private static void IntoOtherSaveMainPanelSelection() {
        SelectedMainPanelType = FEMainPanelType.Analysis;
        OpenedMainPanelType = FEMainPanelType.None;
        currentPageCategoryName = null;
        currentPageSubpageName = null;
    }

    private static void RefreshAnalysisPageCategories() {
        AnalysisPageCategories = MainWindowPageRegistry.GetCategories(FEMainPanelType.Analysis, sandboxMode);
    }

    private static IReadOnlyList<MainWindowCategoryDefinition> GetLegacyPageCategories() {
        if (!legacyPageCategoriesInitialized || legacyPageCategoriesSandboxMode != sandboxMode) {
            RefreshLegacyPageCategories();
        }

        return legacyPageCategories;
    }

    private static void RefreshLegacyPageCategories() {
        legacyPageCategories = MainWindowPageRegistry.GetCategories(FEMainPanelType.Legacy, sandboxMode);
        legacyPageCategoriesSandboxMode = sandboxMode;
        legacyPageCategoriesInitialized = true;
    }

    private static void CaptureCurrentPageRouteFromOpenedPanel() {
        switch (OpenedMainPanelType) {
            case FEMainPanelType.Legacy:
                RememberCurrentPageRouteFromLegacy();
                break;
            case FEMainPanelType.Analysis:
                RememberCurrentPageRouteFromAnalysis();
                break;
        }
    }

    private static void RememberCurrentPageRouteFromLegacy() {
        if (_legacyConfigWin != null
            && _legacyConfigWin.TryGetCurrentTabRoute(out string categoryName, out string subpageName)) {
            RememberCurrentPageRoute(categoryName, subpageName);
        }
    }

    private static void RememberCurrentPageRouteFromAnalysis() {
        if (_analysisMainWindow != null
            && _analysisMainWindow.TryGetCurrentPageRoute(out string categoryName, out string subpageName)) {
            RememberCurrentPageRoute(categoryName, subpageName);
        }
    }

    private static void RememberCurrentPageRoute(string categoryName, string subpageName) {
        if (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(subpageName)) {
            return;
        }

        currentPageCategoryName = categoryName;
        currentPageSubpageName = subpageName;
    }

    private static void ApplyCurrentPageRouteToOpenedPanel() {
        if (string.IsNullOrEmpty(currentPageCategoryName) || string.IsNullOrEmpty(currentPageSubpageName)) {
            return;
        }

        switch (OpenedMainPanelType) {
            case FEMainPanelType.Legacy:
                if (_legacyConfigWin == null) {
                    return;
                }

                if (!_legacyConfigWin.JumpToPage(currentPageCategoryName, currentPageSubpageName)) {
                    _legacyConfigWin.JumpToGroup(currentPageCategoryName);
                    RememberCurrentPageRouteFromLegacy();
                }
                break;
            case FEMainPanelType.Analysis:
                if (_analysisMainWindow == null) {
                    return;
                }

                if (!_analysisMainWindow.JumpToPage(currentPageCategoryName, currentPageSubpageName)) {
                    _analysisMainWindow.JumpToCategory(currentPageCategoryName);
                    RememberCurrentPageRouteFromAnalysis();
                }
                break;
        }
    }

    #region 模组存档接口实现

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            (MainPanelSelectionBlockTag, ImportMainPanelSelection),
            ("ItemInteraction", ItemInteraction.Import),
            ("Miscellaneous", Miscellaneous.Import)
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            (MainPanelSelectionBlockTag, ExportMainPanelSelection),
            ("ItemInteraction", ItemInteraction.Export),
            ("Miscellaneous", Miscellaneous.Export)
        );
    }

    public static void IntoOtherSave() {
        ExplorationDetector.Reset();
        IntoOtherSaveMainPanelSelection();

        ItemInteraction.IntoOtherSave();

        Miscellaneous.IntoOtherSave();
    }

    #endregion
}
