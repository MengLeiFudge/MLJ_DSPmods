using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.Growth;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取单位类型。
/// </summary>
public enum GachaDrawUnitKind {
    None = 0,
    Recipe = 1,
    ResourceGroup = 2,
    ConversionChain = 3,
    TowerFamily = 4,
    RectificationFamily = 5,
}

/// <summary>
/// 抽取单位键。抽取系统以它记录回响，底层配方仍由 RecipeGrowth 保存等级和经验。
/// </summary>
public readonly struct GachaDrawUnitKey {
    public GachaDrawUnitKey(GachaDrawUnitKind kind, ERecipe recipeType, int inputId) {
        Kind = kind;
        RecipeType = recipeType;
        InputId = inputId;
    }

    public GachaDrawUnitKind Kind { get; }
    public ERecipe RecipeType { get; }
    public int InputId { get; }

    public bool IsValid => (Kind is GachaDrawUnitKind.Recipe
        or GachaDrawUnitKind.ResourceGroup
        or GachaDrawUnitKind.ConversionChain
        or GachaDrawUnitKind.TowerFamily
        or GachaDrawUnitKind.RectificationFamily) && InputId > 0;

    public static GachaDrawUnitKey FromRecipe(BaseRecipe recipe) =>
        recipe == null ? default : new(GachaDrawUnitKind.Recipe, recipe.RecipeType, recipe.InputID);
}

/// <summary>
/// 单个抽取单位的回响状态。
/// </summary>
public sealed class GachaDrawUnitState {
    public int Resonance;
}

/// <summary>
/// 抽取单位定义。一个单位可以映射单配方、资源组、转化链、塔种或精馏家族；底层等级仍由 RecipeGrowth 逐配方保存。
/// </summary>
public readonly struct GachaDrawUnit {
    public GachaDrawUnit(GachaDrawUnitKey key, int displayItemId, RecipeKey[] recipeKeys) {
        Key = key;
        DisplayItemId = displayItemId;
        RecipeKeys = recipeKeys ?? [];
    }

    public GachaDrawUnitKey Key { get; }
    public int DisplayItemId { get; }
    public RecipeKey[] RecipeKeys { get; }
    public RecipeKey RecipeKey => RecipeKeys.Length > 0 ? RecipeKeys[0] : default;

    public static GachaDrawUnit FromRecipe(BaseRecipe recipe) => new(
        GachaDrawUnitKey.FromRecipe(recipe),
        recipe?.InputID ?? 0,
        recipe == null ? [] : [RecipeKey.FromRecipe(recipe)]);
}
