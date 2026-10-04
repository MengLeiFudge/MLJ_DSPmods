using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>按星球和层级阅读已经发生的回收结果。</summary>
public sealed class LocationArchivePage : CardBrowserPage {
    protected override string Title => "地点档案".Translate();
    protected override string Summary => "FE.LocationArchivePage.Summary".Translate();
    protected override string EmptyText => GameMain.localPlanet == null
        ? "FE.LocationArchivePage.Away".Translate()
        : "FE.LocationArchivePage.Empty".Translate();
    /// <summary>只列出当前星球的普通遗迹，主线与关键信号见主线记录。</summary>
    protected override IReadOnlyList<PageCard> ReadCards() {
        int planetId = GameMain.localPlanet?.id ?? 0;
        return ExplorationManager.GetRecords()
            .Where(r => r.Mark.PlanetId == planetId && !ExplorationManager.IsKeySignal(r.Mark))
            .OrderBy(r => r.Mark.Quality).ThenByDescending(r => r.Tick)
            .Select(r => new PageCard { Key = r.Mark.Id.ToString(), Title = ExplorationManager.GetSignalName(r.Mark),
                ItemId = r.Mark.ItemId > 0 ? r.Mark.ItemId : IFE残片,
                Status = string.Format("{0}层级".Translate(), new[] { "", "普通".Translate(), "精良".Translate(), "珍贵".Translate() }[System.Math.Max(0, System.Math.Min(3, r.Mark.Quality))]),
                Detail = r.Text }).ToArray();
    }
}
