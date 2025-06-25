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
    QuantumDuplicate,

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
    public static string GetName(this ERecipe recipe) {
        return recipe switch {
            ERecipe.BuildingTrain => "建筑培养",
            ERecipe.MineralCopy => "矿物复制",
            ERecipe.QuantumDuplicate => "量子复制",
            ERecipe.Alchemy => "点金",
            ERecipe.Deconstruction => "分解",
            ERecipe.Conversion => "转化",
            _ => "未知"
        };
    }
}
