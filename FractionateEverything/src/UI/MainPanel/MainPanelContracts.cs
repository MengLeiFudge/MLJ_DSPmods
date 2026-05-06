namespace FE.UI.MainPanel;

/// <summary>
/// FE 主面板窗口风格和打开态标识。
/// </summary>
public enum FEMainPanelType {
    None = 0,
    Legacy = 1,
    Analysis = 2,
}

/// <summary>
/// 跨主面板共享页面状态的最小接口。
/// </summary>
public interface IFEMainPanelSharedState {
    long TicketRaffleTotalDraws { get; set; }
    long TicketRaffleOpeningLineDraws { get; set; }
    int AchievementsCurrentPage { get; set; }
}

/// <summary>
/// 未绑定主面板时使用的空共享状态对象。
/// </summary>
public sealed class EmptyMainPanelSharedState : IFEMainPanelSharedState {
    public static readonly EmptyMainPanelSharedState Instance = new();

    public long TicketRaffleTotalDraws { get; set; }
    public long TicketRaffleOpeningLineDraws { get; set; }
    public int AchievementsCurrentPage { get; set; }

    private EmptyMainPanelSharedState() { }
}
