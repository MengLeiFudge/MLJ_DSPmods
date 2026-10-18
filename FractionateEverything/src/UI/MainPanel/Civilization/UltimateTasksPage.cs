using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>展示已经收到线索的终极任务，提供原版导航，交付由到地背包结算负责。</summary>
public sealed class UltimateTasksPage : CardBrowserPage {
    protected override string Title => "终极任务".Translate();
    protected override string Summary => "FE.UltimateTasksPage.Summary".Translate();
    protected override string EmptyText => "FE.UltimateTasksPage.Empty".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() {
        var result = new List<PageCard>();
        for (int branch = 0; branch < 2; branch++) {
            int step = ExplorationManager.GetUltimateStep(branch);
            bool ready = branch == 0 ? GrowthTechnologyManager.ExplorationLevel >= 10 : SubspaceExpansion.UltimateQuestReady;
            if (!ready && step == 0) { continue; }
            string content = branch == 0 ? "exploration" : "capacity";
            ExplorationMark mark = ExplorationManager.GetActiveMarks().FirstOrDefault(m => m.Kind == ExplorationMarkKind.Ultimate && m.ContentId == content);
            result.Add(new PageCard { Key = content, ItemId = IFE记忆源点,
                Title = branch == 0 ? "长夜中的回声".Translate() : "没有尽头的库房".Translate(),
                Status = step >= 6 ? "已经完成".Translate() : string.Format("进程 {0}/6".Translate(), step + 1),
                Progress = step / 6f,
                Detail = step >= 6
                    ? branch == 0 ? "FE.UltimateTasksPage.ExplorationComplete".Translate()
                        : "FE.UltimateTasksPage.CapacityComplete".Translate()
                    : (mark == null ? "FE.UltimateTasksPage.Waiting".Translate() : ExplorationManager.GetSignalDescription(mark))
                        + (step == 4 ? string.Format("FE.UltimateTasksPage.Delivery".Translate(), ExplorationManager.GetUltimateInput(branch)) : ""),
                ActionLabel = "查看信号".Translate(), Action = mark == null ? null : () => ExplorationDetector.Track(mark) });
        }
        return result;
    }
}
