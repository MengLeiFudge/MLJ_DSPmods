using System;
using System.Collections.Generic;
using System.Linq;

namespace VanillaCurveSim;

/// <summary>在供料和排出充足的边界内按60tick结算单塔，分别记录真实消费、失败回流及产物。</summary>
internal static class FeProductionSimulator {
    public static FeProductionResult Run(FeRecipe recipe, FeScenarioConfig scenario, FeDataSet data,
        int seconds, IReadOnlyList<FeOfferingMinute> offerings, uint seed) {
        var (energy, speed) = FeRules.Food(data, scenario);
        if (!recipe.RequiresFood) { speed = 1; }
        float[] probabilities = FeRules.InputProbabilities(recipe, scenario);
        float[] outputs = FeRules.OutputCounts(recipe, scenario);
        int maxMainCount = recipe.InputCounts.Where((_, i) => recipe.ConsumeProbabilities[i] >= 0.9999f).Max();
        float rate = (float)(30d * scenario.Stack / maxMainCount * speed);
        int availableRounds = (recipe.RequiresFood ? 40 : 20) * scenario.Stack / maxMainCount;
        int[] stocks = recipe.InputCounts.Select((count, i) => recipe.ConsumeProbabilities[i] < 0.1f
            ? Math.Max(5, count) : (recipe.RequiresFood ? 40 : 20) * scenario.Stack).ToArray();
        var result = new FeProductionResult {
            Scenario = scenario.Name, RecipeId = recipe.Id, RecipeName = recipe.Name, TowerId = recipe.TowerId,
            RoundsPerSecond = rate, FoodIgnited = recipe.RequiresFood ? 1 : 0,
            FoodEnergyRemaining = recipe.RequiresFood ? energy : 0,
            IdleFoodPerMinute = recipe.RequiresFood ? 90 / energy : 0,
        };
        foreach (int id in recipe.Inputs) { result.Consumed.Add(id, 0); result.Returned.Add(id, 0); }
        foreach (int id in recipe.Outputs) { result.Produced.Add(id, 0); }
        for (int i = 0; i < recipe.Inputs.Length; i++) {
            if (recipe.ConsumeProbabilities[i] < 0.9999f) { result.MinimumHeld.Add(recipe.Inputs[i], recipe.InputCounts[i]); }
        }
        int progress = 0;
        double remainder = 0;
        for (int tick = 0; tick < seconds * 60; tick++) {
            double next = remainder + (500d / 3d) * rate;
            double whole = Math.Floor(next);
            remainder = next - whole;
            progress = (int)Math.Min(progress + whole, 10000d * Math.Max(10, scenario.Stack * 2));
            int rounds = Math.Min(progress / 10000, availableRounds);
            if (rounds == 0) { continue; }
            progress -= rounds * 10000;
            float boost = recipe.RequiresFood ? offerings[tick / 3600].AppliedBoost : 0;
            float successProbability = Math.Min(1, recipe.SuccessProbability * (1 + boost));
            int successes = Math.Min(rounds, FeRules.Roll(ref seed, rounds * successProbability));
            for (int i = 0; i < recipe.Inputs.Length; i++) {
                if (recipe.ConsumeProbabilities[i] >= 0.9999f || probabilities[i] <= 0) { continue; }
                successes = (int)Math.Min(successes, Math.Floor(stocks[i] / (recipe.InputCounts[i] * (double)probabilities[i])));
            }
            result.Attempts += rounds;
            result.ExpectedSuccesses += rounds * (double)successProbability;
            result.Successes += successes;
            int consumedMain = 0;
            for (int i = 0; i < recipe.Inputs.Length; i++) {
                int count;
                if (recipe.ConsumeProbabilities[i] >= 0.9999f) {
                    count = successes * recipe.InputCounts[i];
                    consumedMain += count;
                    result.Returned[recipe.Inputs[i]] += (rounds - successes) * recipe.InputCounts[i];
                } else {
                    count = Math.Min(stocks[i], FeRules.Roll(ref seed,
                        successes * (double)recipe.InputCounts[i] * probabilities[i]));
                }
                result.Consumed[recipe.Inputs[i]] += count;
            }
            if (recipe.RequiresFood) {
                result.WorkNutrition += consumedMain;
                double needed = consumedMain;
                while (needed > 0) {
                    double taken = Math.Min(needed, result.FoodEnergyRemaining);
                    needed -= taken;
                    result.FoodEnergyRemaining -= taken;
                    if (result.FoodEnergyRemaining <= 0) {
                        result.FoodEnergyRemaining = energy;
                        result.FoodIgnited++;
                    }
                }
            }
            if (successes == 0) { continue; }
            for (int i = 0; i < recipe.Outputs.Length; i++) {
                result.Produced[recipe.Outputs[i]] += recipe.RequiresFood
                    ? FeRules.Roll(ref seed, successes * outputs[i])
                    : successes * (int)outputs[i];
            }
        }
        result.MaterialNetValue = result.Produced.Sum(pair => pair.Value * data.Values[pair.Key])
            - result.Consumed.Sum(pair => pair.Value * data.Values[pair.Key]);
        return result;
    }
}
