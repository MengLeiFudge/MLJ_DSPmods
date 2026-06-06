using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.Growth;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取单位类型。
/// </summary>
public enum GachaDrawUnitKind {
    None = 0,
    Recipe = 1,
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

    public bool IsValid => Kind == GachaDrawUnitKind.Recipe && InputId > 0;

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
/// 抽取单位定义。当前先让开线池一条生产配方对应一个抽取单位，后续可扩为配方组或家族。
/// </summary>
public readonly struct GachaDrawUnit {
    public GachaDrawUnit(GachaDrawUnitKey key, int displayItemId, RecipeKey recipeKey) {
        Key = key;
        DisplayItemId = displayItemId;
        RecipeKey = recipeKey;
    }

    public GachaDrawUnitKey Key { get; }
    public int DisplayItemId { get; }
    public RecipeKey RecipeKey { get; }

    public static GachaDrawUnit FromRecipe(BaseRecipe recipe) => new(
        GachaDrawUnitKey.FromRecipe(recipe),
        recipe?.InputID ?? 0,
        recipe == null ? default : RecipeKey.FromRecipe(recipe));
}
