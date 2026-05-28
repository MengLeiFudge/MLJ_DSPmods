using System.IO;
using FE.Logic.DarkFog;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Gacha;
using FE.Logic.Items;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 配方成长状态初始化、同步和存档聚合入口。
/// </summary>
public static class RecipeGrowthManager {
    /// <summary>
    /// 保存全局配方成长状态存储。
    /// </summary>
    public static readonly RecipeGrowthStateStore Store = new();
    private static long lastRuntimeSyncTick = long.MinValue;

    /// <summary>
    /// 根据已注册配方初始化成长状态存储。
    /// </summary>
    public static void InitializeFromRecipes() {
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            Store.GetOrCreate(recipe);
        }
        RecipeGrowthQueries.ClearProcessingCache();
    }

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        Store.Import(r);
        RecipeGrowthQueries.ClearProcessingCache();
    }

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public static void Export(BinaryWriter w) {
        Store.Export(w);
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public static void IntoOtherSave() {
        Store.IntoOtherSave();
        RecipeGrowthQueries.ClearProcessingCache();
    }

    /// <summary>
    /// 导入旧版配方等级并转换为当前成长状态。
    /// </summary>
    public static void ImportLegacyState(BaseRecipe recipe, int legacyLevel) {
        RecipeGrowthState state = Store.GetOrCreate(recipe);
        state.Level = RecipeGrowthRules.ConvertLegacyLevelToStored(recipe, legacyLevel);
        state.UnlockSourceFlags |= RecipeUnlockSourceFlags.LegacyImport;
        state.LastTouchedTick = GameMain.gameTick;
        RecipeGrowthQueries.InvalidateProcessingCache(recipe);
    }

    /// <summary>
    /// 构建一次配方成长结算使用的上下文。
    /// </summary>
    public static RecipeGrowthContext BuildContext(bool manual = false) {
        return new RecipeGrowthContext(
            GachaManager.IsSpeedrunMode,
            ItemManager.GetCurrentProgressStageIndex(),
            DarkFogCombatManager.GetCurrentStage(),
            GachaManager.CurrentFocus,
            manual,
            GameMain.gameTick
        );
    }

    /// <summary>
    /// 同步运行时由科技和默认规则决定的配方解锁状态。
    /// </summary>
    public static void SyncRuntimeUnlocks() {
        long currentTick = GameMain.gameTick;
        if (currentTick >= 0 && lastRuntimeSyncTick >= 0 && currentTick - lastRuntimeSyncTick < 60) {
            return;
        }

        RecipeGrowthContext context = BuildContext();
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
            if (family is RecipeFamily.MineralCopyDarkFog or RecipeFamily.ConversionMaterialDarkFog) {
                RecipeGrowthExecutor.EnsureUnlockedByDarkFogDrop(recipe, context);
            }
        }

        lastRuntimeSyncTick = currentTick;
    }
}
