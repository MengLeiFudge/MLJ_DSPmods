using System.Collections.Generic;
using System.Linq;
using FE.Logic.Civilization;
using FE.UI.MainPanel.Theme;

namespace FE.UI.MainPanel.Civilization;

/// <summary>显示本次游戏最近50条提示，页面不拥有日志状态。</summary>
public sealed class MessageLogPage : CardBrowserPage {
    /// <inheritdoc />
    protected override string Title => "FE.MessageLog.Title".Translate();
    /// <inheritdoc />
    protected override string Summary => "FE.MessageLog.Summary".Translate();

    /// <inheritdoc />
    protected override IReadOnlyList<PageCard> ReadCards() => MessageLog.GetEntries()
        .Select(entry => new PageCard {
            Key = entry.Id.ToString(),
            Title = entry.Text.Split('\n')[0],
            Detail = entry.Text,
        }).ToArray();
}
