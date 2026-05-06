using FE.Logic.Fractionation.Recipes;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 按配方类型和输入物品定位成长状态的键。
/// </summary>
public readonly struct RecipeKey {
    public RecipeKey(ERecipe recipeType, int inputId) {
        RecipeType = recipeType;
        InputId = inputId;
    }

    public ERecipe RecipeType { get; }
    public int InputId { get; }

    public static RecipeKey FromRecipe(BaseRecipe recipe) => new(recipe.RecipeType, recipe.InputID);
}
