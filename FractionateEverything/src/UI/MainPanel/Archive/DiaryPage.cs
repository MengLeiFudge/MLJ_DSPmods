using System.Collections.Generic;
using FE.UI.MainPanel.Theme;

namespace FE.UI.MainPanel.Archive;

/// <summary>在同一页面内切换三类手记，保持历史记录与当前教程分离。</summary>
public sealed class DiaryPage : CardBrowserPage {
    protected override string Title => "手记".Translate();
    protected override IReadOnlyList<string> Filters => ["1.x手记", "2.x手记", "伊卡洛斯手记"];
    protected override string Summary => (SelectedFilter == 2 ? "FE.DiaryPage.IcarusSummary" : "FE.DiaryPage.HistorySummary").Translate();
    protected override IReadOnlyList<PageCard> ReadCards() => DevelopmentDiary.GetCards(SelectedFilter);
}
