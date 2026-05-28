namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 一次配方成长操作前后的等级和解锁变化。
/// </summary>
public readonly struct RecipeGrowthResult {
    /// <summary>
    /// 初始化 RecipeGrowthResult 的新实例。
    /// </summary>
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

    /// <summary>
    /// 获取成长操作执行前的配方等级。
    /// </summary>
    public int PreviousLevel { get; }
    /// <summary>
    /// 获取成长操作执行后的配方等级。
    /// </summary>
    public int CurrentLevel { get; }
    /// <summary>
    /// 判断成长操作前配方是否已解锁。
    /// </summary>
    public bool WasUnlocked { get; }
    /// <summary>
    /// 判断配方是否已解锁。
    /// </summary>
    public bool IsUnlocked { get; }
    /// <summary>
    /// 判断配方是否已达到最高等级。
    /// </summary>
    public bool IsMaxed { get; }
    /// <summary>
    /// 判断成长操作是否改变了配方状态。
    /// </summary>
    public bool StateChanged { get; }
    /// <summary>
    /// 获取成长操作产生的残片奖励数量。
    /// </summary>
    public int FragmentReward { get; }
}
