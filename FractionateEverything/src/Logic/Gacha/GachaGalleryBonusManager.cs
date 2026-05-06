using FE.Logic.Fractionation.Recipes;
using FE.UI.MainPanel.ProgressTask;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取图鉴完成度带来的成功率加成入口。
/// </summary>
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
