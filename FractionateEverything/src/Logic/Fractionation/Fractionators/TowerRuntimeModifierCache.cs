using System.Collections.Generic;
using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 保存文明科技树投影到各塔型的运行能力，不持有科技树业务状态。
/// </summary>
public static class TowerRuntimeModifierCache {
    private static readonly HashSet<ERecipe> fluidOutputStackingTypes = [];
    private static readonly HashSet<ERecipe> productOutputStackingTypes = [];
    private static readonly HashSet<ERecipe> fractionationForeverTypes = [];

    public static void Reset() {
        fluidOutputStackingTypes.Clear();
        productOutputStackingTypes.Clear();
        fractionationForeverTypes.Clear();
    }

    public static void EnableFluidOutputStacking(ERecipe recipeType) => fluidOutputStackingTypes.Add(recipeType);

    public static bool IsFluidOutputStackingEnabled(ERecipe recipeType) =>
        fluidOutputStackingTypes.Contains(recipeType);

    public static void EnableProductOutputStacking(ERecipe recipeType) => productOutputStackingTypes.Add(recipeType);

    public static bool IsProductOutputStackingEnabled(ERecipe recipeType) =>
        productOutputStackingTypes.Contains(recipeType);

    public static void EnableFractionationForever(ERecipe recipeType) => fractionationForeverTypes.Add(recipeType);

    public static bool IsFractionationForeverEnabled(ERecipe recipeType) =>
        fractionationForeverTypes.Contains(recipeType);
}
