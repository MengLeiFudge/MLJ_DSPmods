using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 黑雾配方追赶进度的展示快照。
/// </summary>
public readonly struct DarkFogRecipeProgressSnapshot {
    /// <summary>
    /// 初始化 DarkFogRecipeProgressSnapshot 的新实例。
    /// </summary>
    public DarkFogRecipeProgressSnapshot(
        ERecipe recipeType,
        int inputId,
        int level,
        int maxLevel,
        int growthExp,
        int pityProgress,
        int tier,
        float processMultiplier,
        float catchupMultiplier,
        bool isUnlocked,
        bool isMaxed) {
        RecipeType = recipeType;
        InputId = inputId;
        Level = level;
        MaxLevel = maxLevel;
        GrowthExp = growthExp;
        PityProgress = pityProgress;
        Tier = tier;
        ProcessMultiplier = processMultiplier;
        CatchupMultiplier = catchupMultiplier;
        IsUnlocked = isUnlocked;
        IsMaxed = isMaxed;
    }

    /// <summary>
    /// 获取该配方所属的分馏配方类型。
    /// </summary>
    public ERecipe RecipeType { get; }
    /// <summary>
    /// 获取该快照或配方键关联的输入物品 ID。
    /// </summary>
    public int InputId { get; }
    /// <summary>
    /// 读取或设置该分馏塔建筑的成长等级。
    /// </summary>
    public int Level { get; }
    /// <summary>
    /// 获取该规则或快照允许的最高等级。
    /// </summary>
    public int MaxLevel { get; }
    /// <summary>
    /// 获取或保存配方当前成长经验。
    /// </summary>
    public int GrowthExp { get; }
    /// <summary>
    /// 获取或保存配方当前保底进度。
    /// </summary>
    public int PityProgress { get; }
    /// <summary>
    /// 获取黑雾配方进度所属的阶层。
    /// </summary>
    public int Tier { get; }
    /// <summary>
    /// 获取黑雾加工成长倍率。
    /// </summary>
    public float ProcessMultiplier { get; }
    /// <summary>
    /// 获取黑雾追赶成长倍率。
    /// </summary>
    public float CatchupMultiplier { get; }
    /// <summary>
    /// 判断配方是否已解锁。
    /// </summary>
    public bool IsUnlocked { get; }
    /// <summary>
    /// 判断配方是否已达到最高等级。
    /// </summary>
    public bool IsMaxed { get; }
}
