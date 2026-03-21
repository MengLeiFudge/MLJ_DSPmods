namespace FE.UI.View;

public enum FEMainPanelType {
    None = 0,
    Legacy = 1,
    Analysis = 2,
}

public interface IFEMainPanelSharedState {
}

public sealed class EmptyMainPanelSharedState : IFEMainPanelSharedState {
    public static readonly EmptyMainPanelSharedState Instance = new();

    private EmptyMainPanelSharedState() {
    }
}
