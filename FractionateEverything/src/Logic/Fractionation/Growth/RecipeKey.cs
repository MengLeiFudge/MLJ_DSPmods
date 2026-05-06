using FE.Logic.Fractionation.Recipes;

namespace FE.Logic.Fractionation.Growth;

public readonly struct RecipeKey {
    public RecipeKey(ERecipe recipeType, int inputId) {
        RecipeType = recipeType;
        InputId = inputId;
    }

    public ERecipe RecipeType { get; }
    public int InputId { get; }

    public static RecipeKey FromRecipe(BaseRecipe recipe) => new(recipe.RecipeType, recipe.InputID);
}
