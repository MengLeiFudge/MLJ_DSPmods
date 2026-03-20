using FE.Logic.Recipe;

namespace FE.Logic.Manager;

public static class GachaGalleryBonusManager {
    private static readonly float[] SuccessBonusByType = new float[6];
    private static readonly float[] DestroyReductionByType = new float[6];
    private static readonly float[] DoubleBonusByType = new float[6];
    private static long _nextRefreshTick;

    public static void Tick() {
        if (GameMain.mainPlayer == null || DSPGame.IsMenuDemo) return;
        if (GameMain.gameTick < _nextRefreshTick) return;
        Refresh();
        _nextRefreshTick = GameMain.gameTick + 3600;
    }

    public static float GetSuccessBonus(ERecipe recipeType) {
        Tick();
        return SuccessBonusByType[(int)recipeType];
    }

    public static float GetDestroyReduction(ERecipe recipeType) {
        Tick();
        return DestroyReductionByType[(int)recipeType];
    }

    public static float GetDoubleBonus(ERecipe recipeType) {
        Tick();
        return DoubleBonusByType[(int)recipeType];
    }

    public static void Refresh() {
        for (int i = 0; i < SuccessBonusByType.Length; i++) {
            SuccessBonusByType[i] = 0f;
            DestroyReductionByType[i] = 0f;
            DoubleBonusByType[i] = 0f;
        }

        foreach (ERecipe recipeType in ERecipeExtension.RecipeTypes) {
            var recipes = RecipeManager.GetRecipesByType(recipeType);
            int total = recipes.Count;
            if (total <= 0) continue;

            int unlocked = 0;
            for (int i = 0; i < recipes.Count; i++) {
                if (recipes[i].Unlocked) unlocked++;
            }

            float ratio = (float)unlocked / total;
            float success = 0f;
            if (ratio >= 0.25f) success += 0.02f;
            if (ratio >= 0.50f) success += 0.05f;
            float destroyReduction = ratio >= 0.75f ? 0.01f : 0f;
            float doubleBonus = ratio >= 1f ? 0.03f : 0f;

            SuccessBonusByType[(int)recipeType] = success;
            DestroyReductionByType[(int)recipeType] = destroyReduction;
            DoubleBonusByType[(int)recipeType] = doubleBonus;
        }
    }

    public static void IntoOtherSave() {
        _nextRefreshTick = 0;
        for (int i = 0; i < SuccessBonusByType.Length; i++) {
            SuccessBonusByType[i] = 0f;
            DestroyReductionByType[i] = 0f;
            DoubleBonusByType[i] = 0f;
        }
    }
}
