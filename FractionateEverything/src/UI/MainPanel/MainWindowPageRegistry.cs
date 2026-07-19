using System;
using System.Collections.Generic;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Archive;
using FE.UI.MainPanel.Civilization;
using FE.UI.MainPanel.CoreOperate;
using FE.UI.MainPanel.ProgressTask;
using FE.UI.MainPanel.ResourceInteraction;
using FE.UI.MainPanel.Setting;
using UnityEngine;

namespace FE.UI.MainPanel;

/// <summary>
/// FE 主面板页面注册、分类顺序和可见性过滤中心。
/// </summary>
public static class MainWindowPageRegistry {
    public const string CoreOperateCategoryName = "生产管理";
    public const string ResourceInteractionCategoryName = "资源管理";
    public const string CivilizationCategoryName = "文明解析";
    public const string ProgressTaskCategoryName = "恢复指引";
    public const string ArchiveCategoryName = "图鉴档案";
    public const string SystemSettingCategoryName = "系统设置";
    private static readonly string[] categoryOrder = [
        CoreOperateCategoryName,
        CivilizationCategoryName,
        ResourceInteractionCategoryName,
        ProgressTaskCategoryName,
        ArchiveCategoryName,
        SystemSettingCategoryName,
    ];

    private static readonly MainWindowPageDefinition[] allPages = [
        new(CoreOperateCategoryName, "分馏配方", FracRecipeOperate.CreateUI, FracRecipeOperate.UpdateUI),
        new(CoreOperateCategoryName, "原版配方", VanillaRecipeOperate.CreateUI, VanillaRecipeOperate.UpdateUI),

        new(CivilizationCategoryName, "文明总览", CivilizationOverviewPage.CreateUI,
            CivilizationOverviewPage.UpdateUI),
        new(CivilizationCategoryName, "文明协议恢复", ProtocolRecoveryPage.CreateUI, ProtocolRecoveryPage.UpdateUI),
        new(CivilizationCategoryName, "远古科技树", AncientTechTreePage.CreateUI, AncientTechTreePage.UpdateUI),
        new(CivilizationCategoryName, "文明成就", CivilizationAchievementPage.CreateUI,
            CivilizationAchievementPage.UpdateUI),

        new(ResourceInteractionCategoryName, "物品交互", ItemInteraction.CreateUI, ItemInteraction.UpdateUI),
        new(ProgressTaskCategoryName, "恢复手册", RecoveryGuide.CreateUI, RecoveryGuide.UpdateUI),

        new(ArchiveCategoryName, "配方图鉴", RecipeGallery.CreateUI, RecipeGallery.UpdateUI),
        new(ArchiveCategoryName, "开发日记", DevelopmentDiary.CreateUI, DevelopmentDiary.UpdateUI),

        new(SystemSettingCategoryName, "杂项设置", Miscellaneous.CreateUI, Miscellaneous.UpdateUI),
    ];

    private static readonly IReadOnlyList<MainWindowCategoryDefinition>[,,] categoryCache = BuildCategoryCache();

    public static IReadOnlyList<string> CategoryOrder => categoryOrder;
    public static IReadOnlyList<MainWindowPageDefinition> AllPages => allPages;

    public static IReadOnlyList<MainWindowCategoryDefinition> GetCategories(FEMainPanelType panelType, bool sandboxMode,
        bool includeAllPages = false) {
        int panelIndex = panelType switch {
            FEMainPanelType.Legacy => 0,
            FEMainPanelType.Analysis => 1,
            _ => -1,
        };
        if (panelIndex < 0) {
            return BuildCategories(panelType, sandboxMode, includeAllPages);
        }

        return categoryCache[panelIndex, sandboxMode ? 1 : 0, includeAllPages ? 1 : 0];
    }

    private static IReadOnlyList<MainWindowCategoryDefinition>[,,] BuildCategoryCache() {
        var cache = new IReadOnlyList<MainWindowCategoryDefinition>[2, 2, 2];
        cache[0, 0, 0] = BuildCategories(FEMainPanelType.Legacy, false, false);
        cache[0, 1, 0] = BuildCategories(FEMainPanelType.Legacy, true, false);
        cache[0, 0, 1] = BuildCategories(FEMainPanelType.Legacy, false, true);
        cache[0, 1, 1] = BuildCategories(FEMainPanelType.Legacy, true, true);
        cache[1, 0, 0] = BuildCategories(FEMainPanelType.Analysis, false, false);
        cache[1, 1, 0] = BuildCategories(FEMainPanelType.Analysis, true, false);
        cache[1, 0, 1] = BuildCategories(FEMainPanelType.Analysis, false, true);
        cache[1, 1, 1] = BuildCategories(FEMainPanelType.Analysis, true, true);
        return cache;
    }

    private static IReadOnlyList<MainWindowCategoryDefinition> BuildCategories(FEMainPanelType panelType,
        bool sandboxMode,
        bool includeAllPages) {
        var categories = new List<MainWindowCategoryDefinition>(categoryOrder.Length);
        foreach (string categoryName in categoryOrder) {
            List<MainWindowPageDefinition> pages = [];
            foreach (MainWindowPageDefinition page in allPages) {
                if (page.CategoryName != categoryName) {
                    continue;
                }

                if (!includeAllPages && !page.IsEnabledFor(panelType, sandboxMode)) {
                    continue;
                }

                pages.Add(page);
            }

            if (pages.Count > 0 || includeAllPages) {
                categories.Add(new(categoryName, pages));
            }
        }

        return categories;
    }
}

/// <summary>
/// 分馏主面板页面分类定义。
/// </summary>
public sealed class MainWindowCategoryDefinition(string categoryName, IReadOnlyList<MainWindowPageDefinition> pages) {
    public string CategoryName { get; } = categoryName;
    public IReadOnlyList<MainWindowPageDefinition> Pages { get; } = pages;
}

/// <summary>
/// 分馏主面板页面定义。
/// </summary>
public sealed class MainWindowPageDefinition(
    string categoryName,
    string subpageName,
    Action<MyWindow, RectTransform> createUI,
    Action updateUI,
    bool sandboxOnly = false) {
    public string CategoryName { get; } = categoryName;
    public string SubpageName { get; } = subpageName;
    public Action<MyWindow, RectTransform> CreateUI { get; } = createUI;
    public Action UpdateUI { get; } = updateUI;
    public bool SandboxOnly { get; } = sandboxOnly;

    public bool IsEnabledFor(FEMainPanelType panelType, bool sandboxMode) {
        if (SandboxOnly && !sandboxMode) {
            return false;
        }

        return panelType switch {
            FEMainPanelType.Legacy or FEMainPanelType.Analysis => true,
            _ => false,
        };
    }
}
