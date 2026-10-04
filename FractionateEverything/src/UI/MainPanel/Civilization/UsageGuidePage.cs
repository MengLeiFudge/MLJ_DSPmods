using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>随实际发现开放的操作笔记，具体步骤仅涉及玩家已经接触的设备。</summary>
public sealed class UsageGuidePage : CardBrowserPage {
    protected override string Title => "使用指南".Translate();
    protected override string Summary => "FE.UsageGuidePage.Summary".Translate();

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() {
        List<PageCard> cards = [
            new() { Key = "analysis", Title = "从样本开始".Translate(), ItemId = IFE基础解析塔, Status = "基础解析塔".Translate(),
                Detail = "FE.UsageGuidePage.Analysis".Translate() },
            new() { Key = "explore", Title = "循着回声寻找".Translate(), ItemId = IFE残片, Status = "遗迹探索".Translate(),
                Detail = "FE.UsageGuidePage.Explore".Translate() },
            new() { Key = "storage", Title = "随身的库房".Translate(), ItemId = IFE基础矿物塔, Status = "亚空间".Translate(),
                Detail = "FE.UsageGuidePage.Storage".Translate() },
        ];
        if (ResearchManager.IsContentUnlocked($"mineral-basic:{I铁矿}")
            || ResearchManager.IsContentUnlocked($"mineral-basic:{I铜矿}")
            || ResearchManager.IsContentUnlocked($"mineral-basic:{I石矿}")
            || ResearchManager.IsContentUnlocked($"mineral-basic:{I煤矿}")) {
            cards.Add(new PageCard { Key = "mineral", Title = "第一条复制产线".Translate(), ItemId = IFE基础矿物塔, Status = "基础矿物塔".Translate(),
                Detail = "FE.UsageGuidePage.Mineral".Translate() });
        }
        if (ExplorationManager.StoryCompleted >= 4) {
            cards.Add(new PageCard { Key = "living", Title = "维持生命".Translate(), ItemId = IFE高级交互塔, Status = "生命塔".Translate(),
                Detail = "FE.UsageGuidePage.Living".Translate() });
        }
        if (ResearchManager.IsContentUnlocked(GrowthTechnologyManager.PlanetaryInteractionTechnologyContentId)
            || ResearchManager.IsContentUnlocked(GrowthTechnologyManager.InterstellarInteractionTechnologyContentId)) {
            cards.Add(new PageCard { Key = "logistics", Title = "接通亚空间".Translate(), ItemId = IFE行星内物流交互站, Status = "物流交互站".Translate(),
                Detail = "FE.UsageGuidePage.Logistics".Translate() });
        }
        if (ResearchManager.SacrificeUnlocked) {
            cards.Add(new PageCard { Key = "memory", Title = "留下来的光".Translate(), ItemId = IFE记忆源点, Status = "源点".Translate(),
                Detail = "FE.UsageGuidePage.Memory".Translate() });
        }
        return cards;
    }
}
