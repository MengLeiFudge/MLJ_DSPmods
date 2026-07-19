using System.Collections.Generic;
using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Fractionation.FracRecipes.Runtime;

/// <summary>
/// 保存文明系统投影到分馏域的协议管理范围和当前可用配方。
/// </summary>
public static class RecipeAvailabilityStore {
    private static readonly HashSet<RecipeKey> managedRecipes = [];
    private static readonly HashSet<RecipeKey> availableRecipes = [];

    public static void Reset() {
        managedRecipes.Clear();
        availableRecipes.Clear();
    }

    public static void RegisterManaged(RecipeKey recipeKey, bool available) {
        managedRecipes.Add(recipeKey);
        if (available) {
            availableRecipes.Add(recipeKey);
        }
    }

    public static bool IsManaged(BaseRecipe recipe) => recipe != null && IsManaged(RecipeKey.FromRecipe(recipe));

    public static bool IsManaged(RecipeKey recipeKey) => managedRecipes.Contains(recipeKey);

    public static bool IsAvailable(BaseRecipe recipe) => recipe != null && IsAvailable(RecipeKey.FromRecipe(recipe));

    public static bool IsAvailable(RecipeKey recipeKey) =>
        !managedRecipes.Contains(recipeKey) || availableRecipes.Contains(recipeKey);
}
