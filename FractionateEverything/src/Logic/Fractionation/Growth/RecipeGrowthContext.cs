using FE.Logic.Manager;
using FE.Logic.DarkFog;
using FE.Logic.Gacha;

namespace FE.Logic.Fractionation.Growth;
/// <summary>
/// 配方成长规则评估所需的当前进度上下文。
/// </summary>
public readonly struct RecipeGrowthContext {
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

    public bool IsSpeedrunMode { get; }
    public int CurrentStageIndex { get; }
    public EDarkFogCombatStage DarkFogStage { get; }
    public GachaFocusType CurrentFocus { get; }
    public bool Manual { get; }
    public long GameTick { get; }
}
