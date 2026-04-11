using FE.Logic.Recipe;

namespace FE.Logic.RecipeGrowth;

public readonly struct RecipeKey {
    public RecipeKey(ERecipe recipeType, int inputId) {
        RecipeType = recipeType;
        InputId = inputId;
    }

    public ERecipe RecipeType { get; }
    public int InputId { get; }

    public static RecipeKey FromRecipe(BaseRecipe recipe) => new(recipe.RecipeType, recipe.InputID);
}
