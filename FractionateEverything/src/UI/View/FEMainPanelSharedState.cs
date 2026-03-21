namespace FE.UI.View;

public sealed class FEMainPanelSharedState : IFEMainPanelSharedState {
    public long TicketRaffleTotalDraws { get; set; }
    public int AchievementsCurrentPage { get; set; }
    public int RuneMenuFilterMode { get; set; }
    public int RuneMenuSortMode { get; set; }
}
