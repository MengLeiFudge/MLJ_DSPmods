namespace FE.UI.MainPanel;

/// <summary>
/// Analysis 与 MessageBox 主面板共用的页面状态。
/// </summary>
public sealed class FEMainPanelSharedState : IFEMainPanelSharedState {
    public long TicketRaffleTotalDraws { get; set; }
    public long TicketRaffleOpeningLineDraws { get; set; }
    public int AchievementsCurrentPage { get; set; }
}
