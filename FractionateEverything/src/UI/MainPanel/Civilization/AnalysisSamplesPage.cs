using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>以样本图标与共享进度展示解析，不提前披露结果。</summary>
public sealed class AnalysisSamplesPage : CardBrowserPage {
    protected override string Title => "解析样本".Translate();
    protected override string Summary => "FE.AnalysisSamplesPage.Summary".Translate();
    protected override string EmptyText => "FE.AnalysisSamplesPage.Empty".Translate();
    protected override IReadOnlyList<PageCard> ReadCards() => ResearchManager.GetDirectionSnapshots()
        .Where(s => s.ItemId != IFE记忆源点 && s.HasPendingContent)
        .Select(s => {
            string name = LDB.items.Select(s.ItemId)?.name ?? "未知样本".Translate();
            long stock = DataCenterInventory.GetModDataItemCount(s.ItemId)
                         + (GameMain.mainPlayer?.package?.GetItemCount(s.ItemId) ?? 0);
            string stockText = stock > 0
                ? string.Format("手头库存：{0}（亚空间与背包）".Translate(), stock)
                : "FE.AnalysisSamplesPage.NoStock".Translate();
            return new PageCard { Key = s.ItemId.ToString(), ItemId = s.ItemId, Title = name,
                Status = stock > 0 ? string.Format("待解析 {0} 份".Translate(), s.PendingContentCount) : "尚未取得样本".Translate(),
                Progress = s.Progress / 10000f,
                Detail = string.Format("FE.AnalysisSamplesPage.Detail".Translate(), name, stockText, s.Progress / 100f, s.PendingContentCount) };
        }).ToArray();
}
