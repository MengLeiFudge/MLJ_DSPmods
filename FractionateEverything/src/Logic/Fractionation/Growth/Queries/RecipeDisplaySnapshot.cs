using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 配方展示页面使用的等级、解锁和倍率快照。
/// </summary>
public readonly struct RecipeDisplaySnapshot {
    /// <summary>
    /// 初始化 RecipeDisplaySnapshot 的新实例。
    /// </summary>
    public RecipeDisplaySnapshot(
        ERecipe recipeType,
        int inputId,
        RecipeFamily family,
        int level,
        int maxLevel,
        bool isUnlocked,
        bool isMaxed,
        int effectiveLegacyLevel,
        int growthExp,
        int pityProgress,
        string[] levelDescriptions,
        float remainInputRatio,
        float doubleOutputRatio,
        float destroyRatio) {
        RecipeType = recipeType;
        InputId = inputId;
        Family = family;
        Level = level;
        MaxLevel = maxLevel;
        IsUnlocked = isUnlocked;
        IsMaxed = isMaxed;
        EffectiveLegacyLevel = effectiveLegacyLevel;
        GrowthExp = growthExp;
        PityProgress = pityProgress;
        LevelDescriptions = levelDescriptions;
        RemainInputRatio = remainInputRatio;
        DoubleOutputRatio = doubleOutputRatio;
        DestroyRatio = destroyRatio;
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
    /// 获取该配方所属的成长家族。
    /// </summary>
    public RecipeFamily Family { get; }
    /// <summary>
    /// 读取或设置该分馏塔建筑的成长等级。
    /// </summary>
    public int Level { get; }
    /// <summary>
    /// 获取该规则或快照允许的最高等级。
    /// </summary>
    public int MaxLevel { get; }
    /// <summary>
    /// 判断配方是否已解锁。
    /// </summary>
    public bool IsUnlocked { get; }
    /// <summary>
    /// 判断配方是否已达到最高等级。
    /// </summary>
    public bool IsMaxed { get; }
    /// <summary>
    /// 获取兼容旧版等级显示使用的有效等级。
    /// </summary>
    public int EffectiveLegacyLevel { get; }
    /// <summary>
    /// 获取或保存配方当前成长经验。
    /// </summary>
    public int GrowthExp { get; }
    /// <summary>
    /// 获取或保存配方当前保底进度。
    /// </summary>
    public int PityProgress { get; }
    /// <summary>
    /// 获取该配方各等级的展示说明。
    /// </summary>
    public string[] LevelDescriptions { get; }
    /// <summary>
    /// 获取成长系统提供的输入保留概率。
    /// </summary>
    public float RemainInputRatio { get; }
    /// <summary>
    /// 获取成长系统提供的双倍输出概率。
    /// </summary>
    public float DoubleOutputRatio { get; }
    /// <summary>
    /// 获取该配方失败时输入物品损毁概率。
    /// </summary>
    public float DestroyRatio { get; }
}
