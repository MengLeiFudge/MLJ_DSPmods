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
