using System;
using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;
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
    public const string ResourceInteractionCategoryName = "管理";
    public const string CivilizationCategoryName = "任务";
    public const string ArchiveCategoryName = "资料";
    public const string SystemSettingCategoryName = "设置";
    private static readonly string[] categoryOrder = ["任务", "配方", "管理", "资料", "设置"];

    private static readonly MainWindowPageDefinition encyclopedia = Browser("资料", "FE.Encyclopedia.Title", new UsageGuidePage());
    private static readonly MainWindowPageDefinition[] allPages = [
        Browser("任务", "文明寻踪", new TaskJournalPage(true)),
        Browser("任务", "遗迹探索", new TaskJournalPage(false)),
        Browser("配方", "基础矿物塔", new RecipeGalleryPage(ERecipe.BasicMineral)),
        Browser("配方", "高级矿物塔", new RecipeGalleryPage(ERecipe.AdvancedMineral)),
        Browser("配方", "高级交互塔", new RecipeGalleryPage(ERecipe.AdvancedInteraction)),
        Browser("配方", "高级转化塔", new RecipeGalleryPage(ERecipe.AdvancedConversion)),
        Browser("管理", "丰碑建设", new MonumentPage()),
        new("管理", "亚空间仓储", ItemInteraction.CreateUI, ItemInteraction.UpdateUI),
        encyclopedia,
        Browser("资料", "手记", new DiaryPage()),
        Browser("资料", "FE.MessageLog.Title", new MessageLogPage()),
        new("设置", "设置", Miscellaneous.CreateUI, Miscellaneous.UpdateUI),
    ];

    /// <summary>通信建立前仅创建百科，其他页面不生成控件和操作入口。</summary>
    private static readonly IReadOnlyList<MainWindowCategoryDefinition> encyclopediaCategories =
        new MainWindowCategoryDefinition[] { new("资料", new[] { encyclopedia }) };

    /// <summary>将卡片页实例接入两种外壳共用的注册表。</summary>
    /// <param name="category">分类。</param>
    /// <param name="title">页签名。</param>
    /// <param name="page">页面实例。</param>
    /// <returns>外壳所用页面定义。</returns>
    private static MainWindowPageDefinition Browser(string category, string title, CardBrowserPage page) =>
        new(category, title, page.CreateUI, page.UpdateUI);

    /// <summary>将已保存的旧目录路由映射到对应的新页面；未知入口回到文明寻踪。</summary>
    /// <param name="category">保存的分类键。</param>
    /// <param name="page">保存的子页键。</param>
    /// <returns>当前注册表中的有效路由。</returns>
    internal static (string Category, string Page) NormalizeRoute(string category, string page) {
        foreach (MainWindowPageDefinition definition in allPages) {
            if (definition.CategoryName == category && definition.SubpageName == page) { return (category, page); }
        }
        return (category, page) switch {
            ("指引", "FE.Encyclopedia.Title") => ("资料", "FE.Encyclopedia.Title"),
            ("指引", "FE.MessageLog.Title") => ("资料", "FE.MessageLog.Title"),
            ("探索", "信号追踪" or "地点档案") => ("任务", "遗迹探索"),
            ("研究", "解析样本") or ("工业", "配方图鉴") => ("配方", "基础矿物塔"),
            ("工业", "亚空间仓储") => ("管理", "亚空间仓储"),
            ("手记", _) => ("资料", "手记"),
            ("设置", _) => ("设置", "设置"),
            _ => ("任务", "文明寻踪"),
        };
    }

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
