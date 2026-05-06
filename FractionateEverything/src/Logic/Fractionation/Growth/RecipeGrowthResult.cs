namespace FE.Logic.Fractionation.Growth;
/// <summary>
/// 一次配方成长操作前后的等级和解锁变化。
/// </summary>
public readonly struct RecipeGrowthResult {
    public RecipeGrowthResult(
        int previousLevel,
        int currentLevel,
        bool wasUnlocked,
        bool isUnlocked,
        bool isMaxed,
        bool stateChanged,
        int fragmentReward) {
        PreviousLevel = previousLevel;
        CurrentLevel = currentLevel;
        WasUnlocked = wasUnlocked;
        IsUnlocked = isUnlocked;
        IsMaxed = isMaxed;
        StateChanged = stateChanged;
        FragmentReward = fragmentReward;
    }

    public int PreviousLevel { get; }
    public int CurrentLevel { get; }
    public bool WasUnlocked { get; }
    public bool IsUnlocked { get; }
    public bool IsMaxed { get; }
    public bool StateChanged { get; }
    public int FragmentReward { get; }
}
