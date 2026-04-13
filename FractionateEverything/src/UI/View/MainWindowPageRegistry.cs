using System;
using System.Collections.Generic;
using FE.UI.Components;
using FE.UI.View.CoreOperate;
using FE.UI.View.DrawGrowth;
using FE.UI.View.ResourceInteraction;
using FE.UI.View.ProgressTask;
using FE.UI.View.Setting;
using FE.UI.View.Archive;
using UnityEngine;

namespace FE.UI.View;

public static class MainWindowPageRegistry {
    public const string CoreOperateCategoryName = "生产管理";
    public const string ResourceInteractionCategoryName = "资源管理";
    public const string DrawGrowthCategoryName = "抽取成长";
    public const string ProgressTaskCategoryName = "任务成就";
    public const string ArchiveCategoryName = "图鉴档案";
    public const string SystemSettingCategoryName = "系统设置";
    public const string ItemManageCategoryName = ResourceInteractionCategoryName;
    public const string GachaCategoryName = DrawGrowthCategoryName;
    public const string StoreCategoryName = DrawGrowthCategoryName;
    public const string ProgressSystemCategoryName = ProgressTaskCategoryName;
    public const string StatisticCategoryName = ArchiveCategoryName;

    private static readonly string[] categoryOrder = [
        CoreOperateCategoryName,
        DrawGrowthCategoryName,
        ResourceInteractionCategoryName,
        ProgressTaskCategoryName,
        ArchiveCategoryName,
        SystemSettingCategoryName,
    ];

    private static readonly MainWindowPageDefinition[] allPages = [
        new(CoreOperateCategoryName, "分馏配方", FracRecipeOperate.CreateUI, FracRecipeOperate.UpdateUI, enabledInAnalysis: true),
        new(CoreOperateCategoryName, "原版配方", VanillaRecipeOperate.CreateUI, VanillaRecipeOperate.UpdateUI, enabledInAnalysis: true),
        new(CoreOperateCategoryName, "建筑操作", BuildingOperate.CreateUI, BuildingOperate.UpdateUI, enabledInAnalysis: true),

        new(ResourceInteractionCategoryName, "物品交互", ItemInteraction.CreateUI, ItemInteraction.UpdateUI, enabledInAnalysis: true),
        new(ResourceInteractionCategoryName, "市场总览", ResourceOverview.CreateUI, ResourceOverview.UpdateUI, enabledInAnalysis: true),
        new(ResourceInteractionCategoryName, "市场板", MarketBoard.CreateUI, MarketBoard.UpdateUI, enabledInAnalysis: true),
        new(ResourceInteractionCategoryName, "交易所", Exchange.CreateUI, Exchange.UpdateUI, enabledInAnalysis: true),
        new(ResourceInteractionCategoryName, "残片兑换", FragmentExchange.CreateUI, FragmentExchange.UpdateUI, enabledInAnalysis: true),

        new(DrawGrowthCategoryName, "开线抽取", TicketRaffle.CreateRecipeUI, TicketRaffle.UpdateUI, enabledInAnalysis: true),
        new(DrawGrowthCategoryName, "原胚抽取", TicketRaffle.CreateProtoUI, TicketRaffle.UpdateUI, enabledInAnalysis: true),
        new(DrawGrowthCategoryName, "成长规划", LimitedTimeStore.CreateRecipeUI, LimitedTimeStore.UpdateUI, enabledInAnalysis: true),
        new(DrawGrowthCategoryName, "流派聚焦", LimitedTimeStore.CreateProtoUI, LimitedTimeStore.UpdateUI, enabledInAnalysis: true),
        new(DrawGrowthCategoryName, "抽取总览", TicketExchange.CreateUI, TicketExchange.UpdateUI, enabledInAnalysis: true),

        new(ProgressTaskCategoryName, "主线任务", MainTask.CreateUI, MainTask.UpdateUI, enabledInAnalysis: true),
        new(ProgressTaskCategoryName, "循环任务", RecurringTask.CreateUI, RecurringTask.UpdateUI, enabledInAnalysis: true),
        new(ProgressTaskCategoryName, "成就系统", Achievements.CreateUI, Achievements.UpdateUI, enabledInAnalysis: true),

        new(ArchiveCategoryName, "配方图鉴", RecipeGallery.CreateUI, RecipeGallery.UpdateUI, enabledInAnalysis: true),
        new(ArchiveCategoryName, "分馏统计", FracStatistic.CreateUI, FracStatistic.UpdateUI, enabledInAnalysis: true),
        new(ArchiveCategoryName, "开发日记", DevelopmentDiary.CreateUI, DevelopmentDiary.UpdateUI, enabledInAnalysis: true),

        new(SystemSettingCategoryName, "杂项设置", Miscellaneous.CreateUI, Miscellaneous.UpdateUI, enabledInAnalysis: true),
        new(SystemSettingCategoryName, "沙盒模式", SandboxMode.CreateUI, SandboxMode.UpdateUI, sandboxOnly: true, enabledInAnalysis: true),
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

    private static IReadOnlyList<MainWindowCategoryDefinition> BuildCategories(FEMainPanelType panelType, bool sandboxMode,
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

public sealed class MainWindowCategoryDefinition(string categoryName, IReadOnlyList<MainWindowPageDefinition> pages) {
    public string CategoryName { get; } = categoryName;
    public IReadOnlyList<MainWindowPageDefinition> Pages { get; } = pages;
}

public sealed class MainWindowPageDefinition(string categoryName, string subpageName,
    Action<MyWindow, RectTransform> createUI, Action updateUI, bool enabledInAnalysis = false,
    bool sandboxOnly = false) {
    public string CategoryName { get; } = categoryName;
    public string SubpageName { get; } = subpageName;
    public Action<MyWindow, RectTransform> CreateUI { get; } = createUI;
    public Action UpdateUI { get; } = updateUI;
    public bool EnabledInAnalysis { get; } = enabledInAnalysis;
    public bool SandboxOnly { get; } = sandboxOnly;

    public bool IsEnabledFor(FEMainPanelType panelType, bool sandboxMode) {
        if (SandboxOnly && !sandboxMode) {
            return false;
        }

        return panelType switch {
            FEMainPanelType.Legacy => true,
            FEMainPanelType.Analysis => EnabledInAnalysis,
            _ => false,
        };
    }
}
