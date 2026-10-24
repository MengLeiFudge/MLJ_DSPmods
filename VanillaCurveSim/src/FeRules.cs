using System;
using System.Linq;
using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace VanillaCurveSim;

/// <summary>现行FE离线规则。概率和预算对应ResearchProgression/ResearchUnlocks；供养对应MatrixNutrition。</summary>
internal static class FeRules {
    internal static readonly int[] SampleBudgets = [100, 200, 300, 1000, 3000, 8000, 20000];
    internal static readonly int[] StageMemoryBudgets = [10, 40, 120, 300, 600, 1200];
    internal static readonly int[] MatrixMemoryBudgets = [100, 60, 40, 25, 15, 10];
    private static readonly double[] IncBonus = [0, 0.125, 0.2, 0.225, 0.25, 0.275, 0.3, 0.325, 0.35, 0.375, 0.4];

    internal static int Roll(ref uint seed, double expected) => expected <= 0 ? 0 : (int)(expected + GetRandDouble(ref seed));
    internal static float MemoryBoost(long memory) => (float)Math.Min(0.75, 0.05 * Math.Floor(Math.Sqrt(memory)));

    internal static (double energy, float speed) Food(FeDataSet data, FeScenarioConfig scenario) {
        double ratio = data.Values[scenario.FoodItemId] / data.Values[6001];
        return (5.25 * Math.Sqrt(ratio) * (1 + IncBonus[scenario.FoodInc]), (float)Math.Pow(ratio, 0.15));
    }

    internal static (float success, float destroy) SampleProbabilities(double value) {
        double q = 1 + Math.Log10(Math.Max(1, value));
        return ((float)(0.03 / q), (float)Math.Min(0.15, 0.04 + 0.02 * q));
    }

    internal static float[] InputProbabilities(FeRecipe recipe, FeScenarioConfig scenario) => recipe.ConsumeProbabilities
        .Select(p => recipe.TowerId == 8125 && p < 0.9999f ? p * (1 - scenario.TraitLevel * 0.1f) : p).ToArray();

    internal static float[] OutputCounts(FeRecipe recipe, FeScenarioConfig scenario) {
        if (recipe.Id.StartsWith("interaction-breed:", StringComparison.Ordinal)) {
            return [1.5f + 0.25f * scenario.TraitLevel];
        }
        ConversionSpec spec = ConversionSpec.CreateAll().FirstOrDefault(s => s.Key == recipe.Id);
        return spec == null ? recipe.OutputCounts : spec.Outputs
            .Select(o => o.Count * (o.Recovery ? 0.5f + 0.05f * scenario.TraitLevel : 1f)).ToArray();
    }
}
