using FE.Logic.DarkFog;
using FE.Logic.Gacha;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 配方成长规则评估所需的当前进度上下文。
/// </summary>
public readonly struct RecipeGrowthContext {
    /// <summary>
    /// 初始化 RecipeGrowthContext 的新实例。
    /// </summary>
    public RecipeGrowthContext(
        bool isSpeedrunMode,
        int currentStageIndex,
        EDarkFogCombatStage darkFogStage,
        GachaFocusType currentFocus,
        bool manual,
        long gameTick) {
        IsSpeedrunMode = isSpeedrunMode;
        CurrentStageIndex = currentStageIndex;
        DarkFogStage = darkFogStage;
        CurrentFocus = currentFocus;
        Manual = manual;
        GameTick = gameTick;
    }

    /// <summary>
    /// 获取或保存 IsSpeedrunMode 对应的分馏域状态值。
    /// </summary>
    public bool IsSpeedrunMode { get; }
    /// <summary>
    /// 获取当前矩阵阶段序号。
    /// </summary>
    public int CurrentStageIndex { get; }
    /// <summary>
    /// 获取当前黑雾战斗阶段。
    /// </summary>
    public EDarkFogCombatStage DarkFogStage { get; }
    /// <summary>
    /// 获取当前抽取焦点类型。
    /// </summary>
    public GachaFocusType CurrentFocus { get; }
    /// <summary>
    /// 判断本次成长上下文是否来自手动操作。
    /// </summary>
    public bool Manual { get; }
    /// <summary>
    /// 获取构建成长上下文时的游戏 tick。
    /// </summary>
    public long GameTick { get; }
}
