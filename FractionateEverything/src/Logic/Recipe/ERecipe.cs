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

public static class EnumExtensions {
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
        return recipe.GetShortName() + "配方".Translate();
    }
}
