namespace FE.UI.View;

public sealed class FEMainPanelSharedState : IFEMainPanelSharedState {
    public long TicketRaffleTotalDraws { get; set; }
    public long TicketRaffleOpeningLineDraws { get; set; }
    public int AchievementsCurrentPage { get; set; }
}
