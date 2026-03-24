using System;
using System.Collections.Generic;
using FE.UI.Components;
using FE.UI.View.CoreOperate;
using FE.UI.View.GetItemRecipe;
using FE.UI.View.ModPackage;
using FE.UI.View.ProgressSystem;
using FE.UI.View.RuneSystem;
using FE.UI.View.Setting;
using FE.UI.View.Statistic;
using UnityEngine;

namespace FE.UI.View;

public static class MainWindowPageRegistry {
    public const string CoreOperateCategoryName = "核心操作";
    public const string ItemManageCategoryName = "物品管理";
    public const string GachaCategoryName = "抽奖";
    public const string StoreCategoryName = "商店";
    public const string ProgressSystemCategoryName = "进度系统";
    public const string StatisticCategoryName = "统计相关";
    public const string SystemSettingCategoryName = "系统设置";

    private static readonly string[] categoryOrder = [
        CoreOperateCategoryName,
        ItemManageCategoryName,
        GachaCategoryName,
        StoreCategoryName,
        ProgressSystemCategoryName,
        StatisticCategoryName,
        SystemSettingCategoryName,
    ];

    private static readonly MainWindowPageDefinition[] allPages = [
        new(CoreOperateCategoryName, "分馏配方", FracRecipeOperate.CreateUI, FracRecipeOperate.UpdateUI),
        new(CoreOperateCategoryName, "原版配方", VanillaRecipeOperate.CreateUI, VanillaRecipeOperate.UpdateUI),
        new(CoreOperateCategoryName, "建筑操作", BuildingOperate.CreateUI, BuildingOperate.UpdateUI),

        new(ItemManageCategoryName, "物品交互", ItemInteraction.CreateUI, ItemInteraction.UpdateUI),
        new(ItemManageCategoryName, "重要物品", ImportantItem.CreateUI, ImportantItem.UpdateUI, enabledInAnalysis: true, createUIInAnalysis: ImportantItem.CreateUIInAnalysis),
        new(ItemManageCategoryName, "符文系统", RuneMenu.CreateUI, RuneMenu.UpdateUI),

        new(GachaCategoryName, "配方抽奖/原胚抽奖/UP抽奖/限定抽奖", TicketRaffle.CreateUI, TicketRaffle.UpdateUI),
        new(StoreCategoryName, "普通奖券兑换/精选奖券兑换", LimitedTimeStore.CreateUI, LimitedTimeStore.UpdateUI),

        new(ProgressSystemCategoryName, "主线任务", MainTask.CreateUI, MainTask.UpdateUI),
        new(ProgressSystemCategoryName, "循环任务", RecurringTask.CreateUI, RecurringTask.UpdateUI),
        new(ProgressSystemCategoryName, "成就系统", Achievements.CreateUI, Achievements.UpdateUI),
        new(ProgressSystemCategoryName, "开发日记", DevelopmentDiary.CreateUI, DevelopmentDiary.UpdateUI),

        new(StatisticCategoryName, "配方图鉴", RecipeGallery.CreateUI, RecipeGallery.UpdateUI),
        new(StatisticCategoryName, "分馏统计", FracStatistic.CreateUI, FracStatistic.UpdateUI),

        new(SystemSettingCategoryName, "VIP特权", VipFeatures.CreateUI, VipFeatures.UpdateUI, enabledInAnalysis: true, createUIInAnalysis: VipFeatures.CreateUIInAnalysis),
        new(SystemSettingCategoryName, "杂项设置", Miscellaneous.CreateUI, Miscellaneous.UpdateUI, enabledInAnalysis: true, createUIInAnalysis: Miscellaneous.CreateUIInAnalysis),
        new(SystemSettingCategoryName, "沙盒模式", SandboxMode.CreateUI, SandboxMode.UpdateUI, sandboxOnly: true, enabledInAnalysis: true, createUIInAnalysis: SandboxMode.CreateUIInAnalysis),
    ];

    public static IReadOnlyList<string> CategoryOrder => categoryOrder;
    public static IReadOnlyList<MainWindowPageDefinition> AllPages => allPages;

    public static IReadOnlyList<MainWindowCategoryDefinition> GetCategories(FEMainPanelType panelType, bool sandboxMode,
        bool includeAllPages = false) {
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
    Action<MyConfigWindow, RectTransform> createUI, Action updateUI, bool enabledInAnalysis = false,
    bool sandboxOnly = false, Action<MyAnalysisWindow, RectTransform> createUIInAnalysis = null) {
    public string CategoryName { get; } = categoryName;
    public string SubpageName { get; } = subpageName;
    public Action<MyConfigWindow, RectTransform> CreateUI { get; } = createUI;
    public Action UpdateUI { get; } = updateUI;
    public Action<MyAnalysisWindow, RectTransform> CreateUIInAnalysis { get; } = createUIInAnalysis;
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
