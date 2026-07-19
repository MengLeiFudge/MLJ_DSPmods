using System;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 以塔型配方类别和输入物品唯一定位一张 FE 分馏配方。
/// </summary>
public readonly struct RecipeKey(ERecipe recipeType, int inputId) : IEquatable<RecipeKey> {
    public ERecipe RecipeType { get; } = recipeType;
    public int InputId { get; } = inputId;

    public static RecipeKey FromRecipe(BaseRecipe recipe) => new(recipe.RecipeType, recipe.InputID);

    public bool Equals(RecipeKey other) => RecipeType == other.RecipeType && InputId == other.InputId;

    public override bool Equals(object obj) => obj is RecipeKey other && Equals(other);

    public override int GetHashCode() => ((int)RecipeType * 397) ^ InputId;
}
