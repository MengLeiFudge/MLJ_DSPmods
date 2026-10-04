using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>仅列出已经收到的广播，静默遗迹只显示本地收集统计。</summary>
public sealed class SignalTrackingPage : CardBrowserPage {
    private static readonly MethodInfo TrackMethod = AccessTools.Method(typeof(UIGlobemap), "SetLocationTracking",
        [typeof(int), typeof(Vector3), typeof(int)]);
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
            return string.Format("FE.SignalTrackingPage.Summary".Translate(), found, total, starFound, starTotal, ExplorationManager.GetHighestStrength());
        }
    }
    protected override IReadOnlyList<PageCard> ReadCards() => ExplorationManager.GetActiveMarks()
        .Select(mark => new PageCard { Key = mark.Id.ToString(), Title = ExplorationManager.GetSignalName(mark), ItemId = IFE残片,
            Status = ExplorationManager.IsKeySignal(mark) ? "关键信号".Translate() : "普通广播".Translate(),
            Detail = ExplorationManager.GetSignalDescription(mark),
            ActionLabel = "标注位置".Translate(), Action = GrowthTechnologyManager.ExplorationLocationTrackingUnlocked
                ? () => TrackMethod?.Invoke(UIRoot.instance?.uiGame?.globemap, [mark.PlanetId, mark.LocalPosition, 519]) : null }).ToArray();
}
