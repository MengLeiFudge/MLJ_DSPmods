using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using UnityEngine;

namespace FE.UI.MainPanel.Civilization;

/// <summary>用原版球面线绘制已发现的固定航迹；只显示，不创建移动命令。</summary>
internal static class ExplorationRouteView {
    private static readonly List<LineGizmo> Lines = [];
    private static GalaxyData galaxy;
    private static int planetId;
    private static float nextRefresh;

    public static void Update(bool visible) {
        int current = GameMain.localPlanet?.id ?? 0;
        if (!visible || current == 0 || !ReferenceEquals(galaxy, GameMain.galaxy) || planetId != current) {
            Clear(); galaxy = GameMain.galaxy; planetId = current;
            if (!visible || current == 0) { return; }
        }
        if (Time.unscaledTime < nextRefresh) { return; }
        nextRefresh = Time.unscaledTime + 0.2f;
        var routes = ExplorationManager.GetActiveMarks().Where(mark => mark.PlanetId == current)
            .Select(mark => ExplorationManager.GetChallenge(mark.Id))
            .Where(c => c?.Kind == SignalChallengeKind.Route).ToArray();
        int needed = routes.Sum(c => c.Route.Length - 1);
        if (Lines.Any(line => !line) || Lines.Count != needed) { Clear(); }
        int index = 0;
        foreach (SignalChallengeView route in routes) {
            for (int i = 0; i + 1 < route.Route.Length; i++) {
                LineGizmo line;
                if (index == Lines.Count) {
                    line = LineGizmo.Create(0, route.Route[i], route.Route[i + 1]);
                    line.autoRefresh = true; line.spherical = true; line.width = 1.2f;
                    line.Open(); Lines.Add(line);
                } else { line = Lines[index]; }
                line.startPoint = route.Route[i]; line.endPoint = route.Route[i + 1];
                line.color = i < route.Progress ? new Color(0.25f, 0.7f, 0.4f, 0.45f)
                    : i == route.Progress ? new Color(0.4f, 1f, 1f, 0.95f) : new Color(0.5f, 0.75f, 1f, 0.5f);
                index++;
            }
        }
    }

    /// <summary>Close后对象归原版池管理，必须丢弃引用，防止后来误开被别处复用的线。</summary>
    private static void Clear() {
        foreach (LineGizmo line in Lines) { if (line) { line.Close(); } }
        Lines.Clear(); nextRefresh = 0;
    }
}
