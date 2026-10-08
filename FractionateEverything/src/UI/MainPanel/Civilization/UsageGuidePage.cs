using System.Collections.Generic;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>始终可翻阅的百科目录，条目随阶段或高级残页恢复。</summary>
public sealed class UsageGuidePage : CardBrowserPage {
    /// <inheritdoc />
    protected override string Title => "FE.Encyclopedia.Title".Translate();
    /// <inheritdoc />
    protected override string Summary => "FE.Encyclopedia.Summary".Translate();

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() {
        bool first = ExplorationManager.FirstRelicClaimed;
        int stage = ResearchManager.CurrentMatrixStage;
        bool page1 = ResearchManager.IsContentUnlocked("encyclopedia:1");
        bool page2 = ResearchManager.IsContentUnlocked("encyclopedia:2");
        bool page3 = ResearchManager.IsContentUnlocked("encyclopedia:3");
        return [
            Card("analysis", "从样本开始", IFE基础解析塔, first || page1, "FE.Encyclopedia.Analysis"),
            Card("explore", "循着回声寻找", IFE残片, first || page1, "FE.Encyclopedia.Explore"),
            Card("storage", "随身的库房", IFE基础矿物塔, first || page1, "FE.Encyclopedia.Storage"),
            Card("mineral", "第一条复制产线", IFE基础矿物塔, stage >= 1 || page1, "FE.Encyclopedia.Mineral"),
            Card("living", "维持生命", IFE高级交互塔, stage >= 1 || page1, "FE.UsageGuidePage.Living"),
            Card("memory", "留下来的光", IFE记忆源点, stage >= 2 || page2, "FE.Encyclopedia.Memory"),
            Card("logistics", "接通亚空间", IFE行星内物流交互站, stage >= 3 || page2, "FE.UsageGuidePage.Logistics"),
            Card("legendary", "FE.Encyclopedia.LegendaryTitle", IFE残片, stage >= 4 || page3, "FE.Encyclopedia.Legendary"),
        ];
    }

    /// <summary>保留稳定目录位置，未恢复条目不显示真实标题、图标或操作内容。</summary>
    /// <param name="key">条目选择身份。</param>
    /// <param name="title">标题翻译键。</param>
    /// <param name="itemId">已恢复条目的图标。</param>
    /// <param name="readable">当前能否阅读。</param>
    /// <param name="detail">正文翻译键。</param>
    /// <returns>当前知识状态对应的卡片。</returns>
    private static PageCard Card(string key, string title, int itemId, bool readable, string detail) => new() {
        Key = key,
        Title = readable ? title.Translate() : "#?% / ???",
        ItemId = readable ? itemId : 0,
        Status = readable ? "FE.Encyclopedia.Readable".Translate() : "FE.Encyclopedia.Damaged".Translate(),
        Detail = readable ? detail.Translate() : "FE.Encyclopedia.Locked".Translate(),
    };
}
