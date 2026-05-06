using System.IO;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using FE.Logic.DarkFog;
using FE.Logic.Gacha;

namespace FE.Logic.Fractionation.Growth;
/// <summary>
/// RecipeGrowthManager 类型。
/// </summary>
public static class RecipeGrowthManager {
    public static readonly RecipeGrowthStateStore Store = new();
    private static long lastRuntimeSyncTick = long.MinValue;

    public static void InitializeFromRecipes() {
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            Store.GetOrCreate(recipe);
        }
        RecipeGrowthQueries.ClearProcessingCache();
    }

    public static void Import(BinaryReader r) {
        Store.Import(r);
        RecipeGrowthQueries.ClearProcessingCache();
    }

    public static void Export(BinaryWriter w) {
        Store.Export(w);
    }

    public static void IntoOtherSave() {
        Store.IntoOtherSave();
        RecipeGrowthQueries.ClearProcessingCache();
    }

    public static void ImportLegacyState(BaseRecipe recipe, int legacyLevel) {
        RecipeGrowthState state = Store.GetOrCreate(recipe);
        state.Level = RecipeGrowthRules.ConvertLegacyLevelToStored(recipe, legacyLevel);
        state.UnlockSourceFlags |= RecipeUnlockSourceFlags.LegacyImport;
        state.LastTouchedTick = GameMain.gameTick;
        RecipeGrowthQueries.InvalidateProcessingCache(recipe);
    }

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
