using FE.Logic.Fractionation.FracRecipes;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 按配方类型和输入物品定位成长状态的键。
/// </summary>
public readonly struct RecipeKey {
    /// <summary>
    /// 初始化 RecipeKey 的新实例。
    /// </summary>
    public RecipeKey(ERecipe recipeType, int inputId) {
        RecipeType = recipeType;
        InputId = inputId;
    }

    /// <summary>
    /// 获取该配方所属的分馏配方类型。
    /// </summary>
    public ERecipe RecipeType { get; }
    /// <summary>
    /// 获取该快照或配方键关联的输入物品 ID。
    /// </summary>
    public int InputId { get; }

    /// <summary>
    /// 执行 new 对应的分馏域操作。
    /// </summary>
    public static RecipeKey FromRecipe(BaseRecipe recipe) => new(recipe.RecipeType, recipe.InputID);
}
