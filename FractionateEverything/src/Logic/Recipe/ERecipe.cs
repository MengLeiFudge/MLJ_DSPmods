using System;
using System.Linq;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

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
    Conversion,

    /// <summary>
    /// 回收配方
    /// </summary>
    Recycle,
}

public static class ERecipeExtension {
    public static void AddTranslations() {
        Register("未知", "Unknown");
        Register("建筑培养", "Building Train");
        Register("矿物复制", "Mineral Replication");
        Register("转化", "Conversion");
        Register("回收", "Recycle");
        Register("未知配方", "Unknown Recipe");
        Register("建筑培养配方", "Building Train Recipe");
        Register("矿物复制配方", "Mineral Replication Recipe");
        Register("转化配方", "Conversion Recipe");
        Register("回收配方", "Recycle Recipe");
    }

    public static readonly ERecipe[] RecipeTypes = (ERecipe[])Enum.GetValues(typeof(ERecipe));

    public static string[] RecipeTypeShortNames => RecipeTypes.Select(t => t.GetShortName()).ToArray();

    /// <summary>
    /// 拓展方法，返回配方名称
    /// </summary>
    public static string GetShortName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => "建筑培养".Translate(),
            ERecipe.MineralCopy => "矿物复制".Translate(),
            ERecipe.Conversion => "转化".Translate(),
            ERecipe.Recycle => "回收".Translate(),
            _ => "未知".Translate()
        };
    }

    public static string GetName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => "建筑培养配方".Translate(),
            ERecipe.MineralCopy => "矿物复制配方".Translate(),
            ERecipe.Conversion => "转化配方".Translate(),
            ERecipe.Recycle => "回收配方".Translate(),
            _ => "未知配方".Translate()
        };
    }

    public static int GetSpriteItemId(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => IFE交互塔,
            ERecipe.MineralCopy => IFE矿物复制塔,
            ERecipe.Conversion => IFE转化塔,
            ERecipe.Recycle => IFE回收塔,
            _ => 0,
        };
    }
}
