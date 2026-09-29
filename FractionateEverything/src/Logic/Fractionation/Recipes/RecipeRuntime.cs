using System.Collections.Generic;
using System.Threading;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 把研究中已解锁的内容整理成配方可用表，供基础塔逐帧快速查询。
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
    /// 切换存档时清空配方可用表。
    /// </summary>
    public static void Reset() => Volatile.Write(ref snapshot, new([], []));

    /// <summary>
    /// 用研究系统给出的最新结果整体替换配方可用表；替换是原子的，逐帧查询不会读到一半的数据。
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
