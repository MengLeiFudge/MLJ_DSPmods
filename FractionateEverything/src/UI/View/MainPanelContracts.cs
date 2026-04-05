namespace FE.UI.View;

public enum FEMainPanelType {
    None = 0,
    Legacy = 1,
    Analysis = 2,
}

public interface IFEMainPanelSharedState {
    long TicketRaffleTotalDraws { get; set; }
    long TicketRaffleOpeningLineDraws { get; set; }
    int AchievementsCurrentPage { get; set; }
}

public sealed class EmptyMainPanelSharedState : IFEMainPanelSharedState {
    public static readonly EmptyMainPanelSharedState Instance = new();

    public long TicketRaffleTotalDraws { get; set; }
    public long TicketRaffleOpeningLineDraws { get; set; }
    public int AchievementsCurrentPage { get; set; }

    private EmptyMainPanelSharedState() {
    }
}
