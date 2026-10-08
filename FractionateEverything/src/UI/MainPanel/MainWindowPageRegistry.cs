using System;
using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Archive;
using FE.UI.MainPanel.Civilization;
using FE.UI.MainPanel.CoreOperate;
using FE.UI.MainPanel.ResourceInteraction;
using FE.UI.MainPanel.Setting;
using FE.UI.MainPanel.Theme;
using UnityEngine;

namespace FE.UI.MainPanel;

/// <summary>
/// FE 主面板页面注册、分类顺序和可见性过滤中心。
/// </summary>
public static class MainWindowPageRegistry {
    public const string ResourceInteractionCategoryName = "工业";
    public const string CivilizationCategoryName = "研究";
    public const string ArchiveCategoryName = "手记";
    public const string SystemSettingCategoryName = "设置";
    private static readonly string[] categoryOrder = ["指引", "研究", "探索", "工业", "手记", "设置"];

    private static readonly MainWindowPageDefinition[] allPages = [
        Browser("指引", "当前目标", new CurrentGoalPage()),
        Browser("指引", "主线记录", new StoryRecordsPage()),
        Browser("指引", "FE.Encyclopedia.Title", new UsageGuidePage()),
        Browser("指引", "FE.MessageLog.Title", new MessageLogPage()),
        Browser("研究", "解析样本", new AnalysisSamplesPage()),
        Browser("研究", "阶段恢复", new StageRecoveryPage()),
        Browser("研究", "塔体特质", new TowerTraitsPage()),
        Browser("探索", "信号追踪", new SignalTrackingPage()),
        Browser("探索", "地点档案", new LocationArchivePage()),
        Browser("探索", "终极任务", new UltimateTasksPage()),
        Browser("工业", "配方图鉴", new RecipeGalleryPage()),
        Browser("工业", "生命塔", new LivingTowersPage()),
        new("工业", "亚空间仓储", ItemInteraction.CreateUI, ItemInteraction.UpdateUI),
        Browser("手记", "1.x手记", new DiaryPage(0)),
        Browser("手记", "2.x手记", new DiaryPage(1)),
        Browser("手记", "伊卡洛斯手记", new DiaryPage(2)),
        new("设置", "亚空间存取", Miscellaneous.CreateStorageUI, Miscellaneous.UpdateUI),
        new("设置", "界面与兼容", Miscellaneous.CreateInterfaceUI, Miscellaneous.UpdateUI),
    ];

    /// <summary>通信建立前仅创建百科，其他页面不生成控件和操作入口。</summary>
    private static readonly IReadOnlyList<MainWindowCategoryDefinition> encyclopediaCategories =
        new MainWindowCategoryDefinition[] { new("指引", new[] { allPages[2] }) };

    /// <summary>将卡片页实例接入两种外壳共用的注册表。</summary>
    /// <param name="category">分类。</param>
    /// <param name="title">页签名。</param>
    /// <param name="page">页面实例。</param>
    /// <returns>外壳所用页面定义。</returns>
    private static MainWindowPageDefinition Browser(string category, string title, CardBrowserPage page) =>
        new(category, title, page.CreateUI, page.UpdateUI);

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

        if (!includeAllPages && !CivilizationRecoveryManager.HasDataCenterCommunication) {
            return encyclopediaCategories;
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
