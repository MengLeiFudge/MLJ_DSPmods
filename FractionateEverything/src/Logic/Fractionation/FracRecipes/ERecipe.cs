using System;
using System.Linq;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 配方类型枚举
/// </summary>
public enum ERecipe {
    /// <summary>
    /// 建筑培养配方
    /// </summary>
    BuildingTrain = 1,

    /// <summary>
    /// 矿物复制配方
    /// </summary>
    MineralCopy,

    /// <summary>
    /// 转化配方
    /// </summary>
    Conversion = 4,

    /// <summary>
    /// 精馏配方
    /// </summary>
    Rectification,
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
        Register("建筑培养", "Building Train");
        Register("矿物复制", "Resource Replication", "资源复制");
        Register("转化", "Conversion");
        Register("精馏", "Civilization Analysis", "文明解析");
        Register("未知配方", "Unknown Recipe");
        Register("建筑培养配方", "Building Train Recipe");
        Register("矿物复制配方", "Resource Replication Recipe", "资源复制配方");
        Register("转化配方", "Conversion Recipe");
        Register("精馏配方", "Civilization Analysis Recipe", "文明解析配方");
    }

    /// <summary>
    /// 获取全部 FE 分馏配方类型。
    /// </summary>
    public static readonly ERecipe[] RecipeTypes = Enum.GetValues(typeof(ERecipe)).Cast<ERecipe>().ToArray();

    /// <summary>
    /// 获取全部 FE 分馏配方类型的短名称。
    /// </summary>
    public static string[] RecipeTypeShortNames => RecipeTypes.Select(t => t.GetShortName()).ToArray();

    /// <summary>
    /// 拓展方法，返回配方名称
    /// </summary>
    public static string GetShortName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => "建筑培养".Translate(),
            ERecipe.MineralCopy => "矿物复制".Translate(),
            ERecipe.Conversion => "转化".Translate(),
            ERecipe.Rectification => "精馏".Translate(),
            _ => "未知".Translate()
        };
    }

    /// <summary>
    /// 读取配方类型的完整显示名称。
    /// </summary>
    public static string GetName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => "建筑培养配方".Translate(),
            ERecipe.MineralCopy => "矿物复制配方".Translate(),
            ERecipe.Conversion => "转化配方".Translate(),
            ERecipe.Rectification => "精馏配方".Translate(),
            _ => "未知配方".Translate()
        };
    }

    /// <summary>
    /// 读取配方类型在 UI 中使用的代表物品图标 ID。
    /// </summary>
    public static int GetSpriteItemId(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => IFE交互塔,
            ERecipe.MineralCopy => IFE资源塔,
            ERecipe.Conversion => IFE转化塔,
            ERecipe.Rectification => IFE解析塔,
            _ => 0,
        };
    }
}
