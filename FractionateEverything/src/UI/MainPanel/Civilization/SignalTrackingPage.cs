using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>列出剧情信号与永久记录的发现，未接近的普通地点不公开。</summary>
public sealed class SignalTrackingPage : CardBrowserPage {
    protected override string Title => "信号追踪".Translate();
    protected override string Summary {
        get {
            PlanetData planet = GameMain.localPlanet;
            if (planet == null) { return "FE.SignalTrackingPage.Away".Translate(); }
            ExplorationManager.GetSilentProgress(planet.id, out int found, out int total);
            int starFound = 0, starTotal = 0;
            foreach (PlanetData other in planet.star.planets) {
                ExplorationManager.GetSilentProgress(other.id, out int c, out int n);
                starFound += c; starTotal += n;
            }
            return string.Format("FE.SignalTrackingPage.CollectionSummary".Translate(), found, total, starFound, starTotal);
        }
    }
    protected override IReadOnlyList<PageCard> ReadCards() => ExplorationManager.GetActiveMarks()
        .Select(mark => new PageCard { Key = mark.Id.ToString(), Title = ExplorationManager.GetSignalName(mark), ItemId = IFE残片,
            Status = ExplorationManager.GetChallenge(mark.Id) is SignalChallengeView challenge
                ? string.Format("FE.Challenge.Status".Translate(), challenge.Progress, challenge.Required)
                : ExplorationManager.IsKeySignal(mark) ? "关键信号".Translate() : "普通广播".Translate(),
            Detail = ExplorationManager.GetSignalDescription(mark),
            ActionLabel = (ExplorationDetector.IsTracking(mark.Id) ? "取消标注" : "标注位置").Translate(),
            Action = mark.Kind == ExplorationMarkKind.FirstRelic ? null : () => ExplorationDetector.ToggleTracking(mark.Id),
            SecondaryLabel = "FE.Challenge.SwitchRoute".Translate(),
            SecondaryAction = ExplorationManager.CanSwitchChallenge(mark.Id)
                ? () => ChallengeNetwork.RequestRoute(mark.Id) : null }).ToArray();
}
