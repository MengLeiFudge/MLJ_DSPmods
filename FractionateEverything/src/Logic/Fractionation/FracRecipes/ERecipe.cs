using System.Linq;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 配方类型枚举
/// </summary>
public enum ERecipe {
    /// <summary>
    /// 培养配方。
    /// </summary>
    Cultivation = 1,

    /// <summary>
    /// 解析配方。
    /// </summary>
    Analysis = 2,

    /// <summary>
    /// 复制配方。
    /// </summary>
    Replication = 3,

    /// <summary>
    /// 转化配方。
    /// </summary>
    Conversion = 4,
}

/// <summary>
/// 配方类型扩展方法。
/// </summary>
public static class ERecipeExtension {
    /// <summary>
    /// 注册该分馏域对象需要的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("未知", "Unknown");
        Register("培养", "Cultivation");
        Register("解析", "Analysis");
        Register("复制", "Replication");
        Register("转化", "Conversion");
        Register("未知配方", "Unknown Recipe");
        Register("培养配方", "Cultivation Recipe");
        Register("解析配方", "Analysis Recipe");
        Register("复制配方", "Replication Recipe");
        Register("转化配方", "Conversion Recipe");
    }

    /// <summary>
    /// 获取全部 FE 分馏配方类型。
    /// </summary>
    public static readonly ERecipe[] RecipeTypes = [
        ERecipe.Cultivation,
        ERecipe.Analysis,
        ERecipe.Replication,
        ERecipe.Conversion,
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
            ERecipe.Cultivation => "培养".Translate(),
            ERecipe.Analysis => "解析".Translate(),
            ERecipe.Replication => "复制".Translate(),
            ERecipe.Conversion => "转化".Translate(),
            _ => "未知".Translate()
        };
    }

    /// <summary>
    /// 读取配方类型的完整显示名称。
    /// </summary>
    public static string GetName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.Cultivation => "培养配方".Translate(),
            ERecipe.Analysis => "解析配方".Translate(),
            ERecipe.Replication => "复制配方".Translate(),
            ERecipe.Conversion => "转化配方".Translate(),
            _ => "未知配方".Translate()
        };
    }

    /// <summary>
    /// 读取配方类型在 UI 中使用的代表物品图标 ID。
    /// </summary>
    public static int GetSpriteItemId(this ERecipe recipe) {
        return recipe switch {
            ERecipe.Cultivation => IFE培养塔,
            ERecipe.Analysis => IFE解析塔,
            ERecipe.Replication => IFE复制塔,
            ERecipe.Conversion => IFE转化塔,
            _ => 0,
        };
    }
}
