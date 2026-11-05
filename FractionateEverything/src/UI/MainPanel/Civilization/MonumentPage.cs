using System.Collections.Generic;
using System.Text;
using FE.Logic.Civilization;
using FE.Logic.Civilization.Monument;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>集中呈现丰碑建设、加工成长及本存档元数据来源，物品操作归亚空间。</summary>
public sealed class MonumentPage : CardBrowserPage {
    protected override string Title => "丰碑建设".Translate();
    protected override string Summary => "FE.Monument.PageSummary".Translate();

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() {
        var state = MonumentManager.Snapshot();
        var detail = new StringBuilder("FE.Monument.Guide".Translate());
        detail.AppendLine().AppendLine().AppendFormat("FE.Monument.Status".Translate(), state.Structure, state.Growth,
            state.Progress, state.Cost, state.Sets, state.Rate, MetadataExtraction.ExternalMultiplier);
        detail.AppendLine().AppendLine();
        foreach (var pair in MonumentManager.SailCounts()) {
            detail.AppendFormat("FE.Monument.MatrixCount".Translate(), LDB.items.Select(pair.Key)?.name ?? "?", pair.Value).AppendLine();
        }
        var cards = new List<PageCard> {
            new() {
                Key = "monument", Title = "FE.Monument.Title".Translate(), ItemId = IFE丰碑火箭,
                Status = string.Format("FE.Monument.Processing".Translate(), state.Growth, MonumentManager.SpeedFactor),
                Detail = detail.ToString(), DetailItems = [IFE丰碑火箭, IFE丰碑结晶],
                Progress = state.Cost > 0 ? (float)(state.Progress / state.Cost) : 0,
            },
        };
        foreach (var entry in MetadataExtraction.Snapshot()) {
            if (LDB.items.Select(entry.MatrixId) is not ItemProto matrix) { continue; }
            cards.Add(new PageCard {
                Key = $"metadata:{entry.MatrixId}", Title = matrix.name, ItemId = entry.MatrixId,
                Status = string.Format("FE.Metadata.Extracted".Translate(), entry.Extracted),
                Detail = string.Format("FE.Metadata.PeakDetail".Translate(), entry.Peak, entry.Extracted)
                    + "\n\n" + "FE.Metadata.Description".Translate(),
                DetailItems = [IFE实体元数据],
            });
        }
        return cards;
    }
}
