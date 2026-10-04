using System.Linq;
using System;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 配方类型枚举
/// </summary>
public enum ERecipe {
    /// <summary>
    /// 高级交互配方。
    /// </summary>
    AdvancedInteraction = 1,

    /// <summary>
    /// 基础解析配方。
    /// </summary>
    BasicAnalysis = 2,

    /// <summary>
    /// 基础矿物配方。
    /// </summary>
    BasicMineral = 3,

    /// <summary>
    /// 高级转化配方。
    /// </summary>
    AdvancedConversion = 4,

    /// <summary>
    /// 高级矿物配方。
    /// </summary>
    AdvancedMineral = 5,
}

/// <summary>
/// 配方类型扩展方法。
/// </summary>
public static class ERecipeExtension {
    /// <summary>
    /// 获取全部 FE 分馏配方类型。
    /// </summary>
    public static readonly ERecipe[] RecipeTypes = [
        ERecipe.BasicMineral,
        ERecipe.BasicAnalysis,
        ERecipe.AdvancedMineral,
        ERecipe.AdvancedInteraction,
        ERecipe.AdvancedConversion,
    ];

    /// <summary>
    /// 获取全部 FE 分馏配方类型的短名称。
    /// </summary>
    public static string[] RecipeTypeShortNames => RecipeTypes.Select(t => t.GetShortName()).ToArray();

    /// <summary>
    /// 拓展方法，返回配方名称
    /// </summary>
    public static string GetShortName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BasicMineral => "基础矿物".Translate(),
            ERecipe.BasicAnalysis => "基础解析".Translate(),
            ERecipe.AdvancedMineral => "高级矿物".Translate(),
            ERecipe.AdvancedInteraction => "高级交互".Translate(),
            ERecipe.AdvancedConversion => "高级转化".Translate(),
            _ => "未知".Translate()
        };
    }

    /// <summary>
    /// 读取配方类型的完整显示名称。
    /// </summary>
    public static string GetName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BasicMineral => "基础矿物配方".Translate(),
            ERecipe.BasicAnalysis => "基础解析配方".Translate(),
            ERecipe.AdvancedMineral => "高级矿物配方".Translate(),
            ERecipe.AdvancedInteraction => "高级交互配方".Translate(),
            ERecipe.AdvancedConversion => "高级转化配方".Translate(),
            _ => "未知配方".Translate()
        };
    }

    /// <summary>
    /// 读取配方类型在 UI 中使用的代表物品图标 ID。
    /// </summary>
    public static int GetSpriteItemId(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BasicMineral => IFE基础矿物塔,
            ERecipe.BasicAnalysis => IFE基础解析塔,
            ERecipe.AdvancedMineral => IFE高级矿物塔,
            ERecipe.AdvancedInteraction => IFE高级交互塔,
            ERecipe.AdvancedConversion => IFE高级转化塔,
            _ => 0,
        };
    }
}

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
