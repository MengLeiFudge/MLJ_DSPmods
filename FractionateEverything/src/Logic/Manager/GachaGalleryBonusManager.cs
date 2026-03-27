using FE.Logic.Recipe;
using FE.UI.View.ProgressTask;

namespace FE.Logic.Manager;

public static class GachaGalleryBonusManager {
    public static void Tick() { }

    public static float GetSuccessBonus(ERecipe recipeType) {
        return Achievements.GetSuccessRateBonus();
    }

    public static float GetDestroyReduction(ERecipe recipeType) {
        return Achievements.GetDestroyReductionBonus();
    }

    public static float GetDoubleBonus(ERecipe recipeType) {
        return Achievements.GetDoubleOutputBonus();
    }

    public static float GetEnergyReductionBonus() {
        return Achievements.GetEnergyReductionBonus();
    }

    public static float GetLogisticsBonus() {
        return Achievements.GetLogisticsBonus();
    }

    public static float GetPowerStageBonus() {
        return Achievements.GetPowerStageBonus();
    }

    public static void Refresh() { }

    public static void IntoOtherSave() { }
}
