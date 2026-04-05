namespace FE.Logic.Manager;

public enum EDarkFogBranchStage {
    Locked = 0,
    Signal = 1,
    Material = 2,
    Singularity = 3,
}

/// <summary>
/// 黑雾旧分支管理器兼容壳。
/// 新逻辑已迁入 <see cref="DarkFogCombatManager"/>，这里只保留旧接口映射，避免零散调用点直接炸掉。
/// </summary>
public static class DarkFogBranchManager {
    public static EDarkFogBranchStage GetCurrentStage() {
        return DarkFogCombatManager.GetCurrentStage() switch {
            EDarkFogCombatStage.Dormant => EDarkFogBranchStage.Locked,
            EDarkFogCombatStage.Signal => EDarkFogBranchStage.Signal,
            EDarkFogCombatStage.GroundSuppression => EDarkFogBranchStage.Material,
            EDarkFogCombatStage.StellarHunt => EDarkFogBranchStage.Material,
            _ => EDarkFogBranchStage.Singularity,
        };
    }

    public static bool IsGrowthOfferUnlocked() {
        return DarkFogCombatManager.IsGrowthOfferUnlocked();
    }

    public static bool IsSpecialOrderUnlocked() {
        return DarkFogCombatManager.IsSpecialOrderUnlocked();
    }

    public static int GetUnlockedGrowthOfferCount() {
        return DarkFogCombatManager.GetUnlockedGrowthOfferCount();
    }

    public static int GetUnlockedSpecialOrderCount() {
        return DarkFogCombatManager.GetUnlockedSpecialOrderCount();
    }

    public static int GetNextUnlockMatrixId() {
        return DarkFogCombatManager.GetNextUnlockMatrixId();
    }
}
