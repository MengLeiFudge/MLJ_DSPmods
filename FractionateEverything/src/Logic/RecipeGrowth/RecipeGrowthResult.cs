namespace FE.Logic.RecipeGrowth;

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
