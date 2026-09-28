using System.Collections.Generic;
using System.Threading;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 将研究域的稳定内容解锁状态投影为基础分馏热路径可查询的配方可用性。
/// </summary>
public static class RecipeAvailabilityStore {
    private sealed class AvailabilitySnapshot(
        HashSet<RecipeKey> managedRecipes,
        HashSet<RecipeKey> availableRecipes) {
        public readonly HashSet<RecipeKey> ManagedRecipes = managedRecipes;
        public readonly HashSet<RecipeKey> AvailableRecipes = availableRecipes;
    }

    private static AvailabilitySnapshot snapshot = new([], []);

    /// <summary>
    /// 清空当前存档的研究投影。
    /// </summary>
    public static void Reset() => Volatile.Write(ref snapshot, new([], []));

    /// <summary>
    /// 原子替换由研究域管理的完整配方可用性投影。
    /// </summary>
    public static void ReplaceManaged(IEnumerable<KeyValuePair<RecipeKey, bool>> recipes) {
        HashSet<RecipeKey> managedRecipes = [];
        HashSet<RecipeKey> availableRecipes = [];
        foreach (KeyValuePair<RecipeKey, bool> recipe in recipes) {
            managedRecipes.Add(recipe.Key);
            if (recipe.Value) {
                availableRecipes.Add(recipe.Key);
            }
        }
        Volatile.Write(ref snapshot, new AvailabilitySnapshot(managedRecipes, availableRecipes));
    }

    /// <summary>
    /// 判断配方是否由研究域管理。
    /// </summary>
    public static bool IsManaged(BaseRecipe recipe) => recipe != null && IsManaged(RecipeKey.FromRecipe(recipe));

    /// <summary>
    /// 判断稳定键是否由研究域管理。
    /// </summary>
    public static bool IsManaged(RecipeKey recipeKey) =>
        Volatile.Read(ref snapshot).ManagedRecipes.Contains(recipeKey);

    /// <summary>
    /// 判断配方是否可以运行；不受研究管理的基础设施配方默认可用。
    /// </summary>
    public static bool IsAvailable(BaseRecipe recipe) => recipe != null && IsAvailable(RecipeKey.FromRecipe(recipe));

    /// <summary>
    /// 判断稳定键是否可以运行。
    /// </summary>
    public static bool IsAvailable(RecipeKey recipeKey) {
        AvailabilitySnapshot current = Volatile.Read(ref snapshot);
        return !current.ManagedRecipes.Contains(recipeKey) || current.AvailableRecipes.Contains(recipeKey);
    }
}
