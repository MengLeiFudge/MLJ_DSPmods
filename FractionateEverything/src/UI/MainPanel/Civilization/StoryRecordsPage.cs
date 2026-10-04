using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>按完成顺序阅读主线与重要遗迹的文明记录。</summary>
public sealed class StoryRecordsPage : CardBrowserPage {
    protected override string Title => "主线记录".Translate();
    protected override string Summary => "FE.StoryRecordsPage.Summary".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() => ExplorationManager.GetRecords()
        .Where(r => ExplorationManager.IsKeySignal(r.Mark))
        .Select(r => new PageCard { Key = r.Mark.Id.ToString(), Title = ExplorationManager.GetSignalName(r.Mark),
            ItemId = IFE记忆源点, Status = "已回收".Translate(), Detail = r.Text }).ToArray();
}
