namespace FE.UI.MainPanel;
/// <summary>
/// FEMainPanelSharedState 类型。
/// </summary>
public sealed class FEMainPanelSharedState : IFEMainPanelSharedState {
    public long TicketRaffleTotalDraws { get; set; }
    public long TicketRaffleOpeningLineDraws { get; set; }
    public int AchievementsCurrentPage { get; set; }
}
