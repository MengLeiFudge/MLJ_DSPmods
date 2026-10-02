using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>以样本图标与共享进度展示解析，不提前披露结果。</summary>
public sealed class AnalysisSamplesPage : CardBrowserPage {
    protected override string Title => "解析样本";
    protected override string Summary => "记录只留下了样本，答案仍藏在物质之中。";
    protected override IReadOnlyList<PageCard> ReadCards() => ResearchManager.GetDirectionSnapshots()
        .Where(s => s.ItemId != IFE记忆源点 && s.HasPendingContent)
        .Select(s => new PageCard { Key = s.ItemId.ToString(), ItemId = s.ItemId,
            Title = LDB.items.Select(s.ItemId)?.name ?? "未知样本",
            Status = $"待解析 {s.PendingContentCount} 份",
            Progress = s.Progress / 10000f,
            Detail = $"记录中反复提到了{LDB.items.Select(s.ItemId)?.name}。把它送进基础解析塔，也许能让这些模糊的纹路变得清晰。\n\n解析进度：{s.Progress / 100f:0.##}%\n尚未读懂的记录：{s.PendingContentCount} 份\n\n将另一侧流出的样本接回入口，可以继续循环解析。",
            ActionLabel = "查看已恢复配方", Action = () => MainWindow.NavigateToPage("工业", 0) }).ToArray();
}
