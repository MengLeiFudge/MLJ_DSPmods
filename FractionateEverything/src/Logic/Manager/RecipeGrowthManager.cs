using System.IO;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;

namespace FE.Logic.Manager;

public static class RecipeGrowthManager {
    public static readonly RecipeGrowthStateStore Store = new();

    public static void InitializeFromRecipes() {
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            Store.GetOrCreate(recipe);
        }
    }

    public static void Import(BinaryReader r) {
        Store.Import(r);
    }

    public static void Export(BinaryWriter w) {
        Store.Export(w);
    }

    public static void IntoOtherSave() {
        Store.IntoOtherSave();
    }

    public static void ImportLegacyState(BaseRecipe recipe, int legacyLevel) {
        RecipeGrowthState state = Store.GetOrCreate(recipe);
        state.Level = RecipeGrowthRules.ConvertLegacyLevelToStored(recipe, legacyLevel);
        state.UnlockSourceFlags |= RecipeUnlockSourceFlags.LegacyImport;
        state.LastTouchedTick = GameMain.gameTick;
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
}
