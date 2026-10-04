using System.Collections.Generic;
using FE.UI.MainPanel.Theme;

namespace FE.UI.MainPanel.Archive;

/// <summary>使用同一阅读布局分别承载三类独立手记。</summary>
/// <param name="category">0为1.x，1为2.x，2为伊卡洛斯。</param>
public sealed class DiaryPage(int category) : CardBrowserPage {
    protected override string Title => category switch { 0 => "1.x手记".Translate(), 1 => "2.x手记".Translate(), _ => "伊卡洛斯手记".Translate() };
    protected override string Summary => category == 2 ? "FE.DiaryPage.IcarusSummary".Translate() : "FE.DiaryPage.HistorySummary".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() => DevelopmentDiary.GetCards(category);
}
