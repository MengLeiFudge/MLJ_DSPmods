using System.Linq;
using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 配方类型枚举
/// </summary>
public enum ERecipe {
    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown,

    /// <summary>
    /// 建筑培养配方
    /// </summary>
    BuildingTrain,

    /// <summary>
    /// 矿物复制配方
    /// </summary>
    MineralCopy,

    /// <summary>
    /// 量子复制配方
    /// </summary>
    QuantumCopy,

    /// <summary>
    /// 点金配方
    /// </summary>
    Alchemy,

    /// <summary>
    /// 分解配方
    /// </summary>
    Deconstruction,

    /// <summary>
    /// 转化配方
    /// </summary>
    Conversion,
}

public static class ERecipeExtension {
    public static void AddTranslations() {
        Register("未知", "Unknown");
        Register("建筑培养", "Building Train");
        Register("矿物复制", "Mineral Copy");
        Register("量子复制", "Quantum Copy");
        Register("点金", "Alchemy");
        Register("分解", "Deconstruction");
        Register("转化", "Conversion");
        Register("未知配方", "Unknown Recipe");
        Register("建筑培养配方", "Building Train Recipe");
        Register("矿物复制配方", "Mineral Copy Recipe");
        Register("量子复制配方", "Quantum Copy Recipe");
        Register("点金配方", "Alchemy Recipe");
        Register("分解配方", "Deconstruction Recipe");
        Register("转化配方", "Conversion Recipe");
    }

    public static readonly ERecipe[] RecipeTypes = [
        ERecipe.BuildingTrain, ERecipe.MineralCopy, ERecipe.QuantumCopy,
        ERecipe.Alchemy, ERecipe.Deconstruction, ERecipe.Conversion
    ];

    public static string[] RecipeTypeShortNames => RecipeTypes.Select(t => t.GetShortName()).ToArray();

    /// <summary>
    /// 拓展方法，返回配方名称
    /// </summary>
    public static string GetShortName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => "建筑培养".Translate(),
            ERecipe.MineralCopy => "矿物复制".Translate(),
            ERecipe.QuantumCopy => "量子复制".Translate(),
            ERecipe.Alchemy => "点金".Translate(),
            ERecipe.Deconstruction => "分解".Translate(),
            ERecipe.Conversion => "转化".Translate(),
            _ => "未知".Translate()
        };
    }

    public static string GetName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => "建筑培养配方".Translate(),
            ERecipe.MineralCopy => "矿物复制配方".Translate(),
            ERecipe.QuantumCopy => "量子复制配方".Translate(),
            ERecipe.Alchemy => "点金配方".Translate(),
            ERecipe.Deconstruction => "分解配方".Translate(),
            ERecipe.Conversion => "转化配方".Translate(),
            _ => "未知配方".Translate()
        };
    }

    public static int GetSpriteItemId(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => IFE交互塔,
            ERecipe.MineralCopy => IFE矿物复制塔,
            ERecipe.QuantumCopy => IFE量子复制塔,
            ERecipe.Alchemy => IFE点金塔,
            ERecipe.Deconstruction => IFE分解塔,
            ERecipe.Conversion => IFE转化塔,
            _ => 0,
        };
    }
}
