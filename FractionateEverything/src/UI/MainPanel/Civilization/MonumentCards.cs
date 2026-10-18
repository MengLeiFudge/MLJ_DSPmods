using System.Collections.Generic;
using System.Text;
using FE.Logic.Civilization;
using FE.Logic.Civilization.Monument;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>阶段恢复页内的丰碑记录，展示永久建设、当前均衡以及所有合法矩阵的零供给。</summary>
internal static class MonumentCards {
    internal static IEnumerable<PageCard> Read() {
        if (ResearchManager.CurrentMatrixStage < 6) { yield break; }
        var state = MonumentManager.Snapshot();
        var detail = new StringBuilder("FE.Monument.Guide".Translate());
        detail.AppendLine().AppendLine().AppendFormat("FE.Monument.Status".Translate(), state.Level, state.Growth,
            state.Progress, state.Balance, state.Supply, state.Rate, MetadataExtraction.ExternalMultiplier);
        detail.AppendLine().AppendLine();
        foreach (var pair in MonumentManager.SailCounts()) {
            detail.AppendFormat("FE.Monument.MatrixCount".Translate(), LDB.items.Select(pair.Key)?.name ?? "?", pair.Value).AppendLine();
        }
        yield return new PageCard {
            Key = "monument", Title = "FE.Monument.Title".Translate(), ItemId = IFE丰碑火箭,
            Status = string.Format("FE.Monument.Balance".Translate(), state.Balance, state.Supply),
            Detail = detail.ToString(), DetailItems = [IFE丰碑火箭, IFE丰碑结晶],
            Progress = (float)state.Balance,
        };
    }
}
