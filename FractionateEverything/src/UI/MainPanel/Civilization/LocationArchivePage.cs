using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>按星球和层级阅读已经发生的回收结果。</summary>
public sealed class LocationArchivePage : CardBrowserPage {
    protected override string Title => "地点档案";
    protected override string Summary => "每一次停留，都在荒寂的星图上留下了痕迹。";
    protected override IReadOnlyList<PageCard> ReadCards() => ExplorationManager.GetRecords()
        .OrderBy(r => r.Mark.PlanetId).ThenBy(r => r.Mark.Quality).ThenByDescending(r => r.Tick)
        .Select(r => new PageCard { Key = r.Mark.Id.ToString(), Title = ExplorationManager.GetSignalName(r.Mark), ItemId = IFE残片,
            Status = $"{GameMain.galaxy?.PlanetById(r.Mark.PlanetId)?.displayName} · {r.Mark.Quality}层",
            Detail = r.Text }).ToArray();
}
