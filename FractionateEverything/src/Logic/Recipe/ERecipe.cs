namespace FE.Logic.Recipe;

/// <summary>
/// 配方类型枚举
/// </summary>
public enum ERecipe {
    Origin = -1,//仅用于原版分馏塔
    /// <summary>
    /// 未知类型
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 转化配方
    /// </summary>
    Conversion = 1,

    /// <summary>
    /// 矿物复制配方
    /// </summary>
    MineralCopy = 2,

    /// <summary>
    /// 点数聚集配方
    /// </summary>
    PointAggregator = 3,

    /// <summary>
    /// 量子复制配方
    /// </summary>
    QuantumDuplicate = 4,

    /// <summary>
    /// 合成配方
    /// </summary>
    Synthesis = 5,

    /// <summary>
    /// 提取配方
    /// </summary>
    Extraction = 6,

    /// <summary>
    /// 研究配方
    /// </summary>
    Research = 7,

    /// <summary>
    /// 点金配方
    /// </summary>
    Alchemy = 8,

    /// <summary>
    /// 分解配方
    /// </summary>
    Deconstruction = 9
}
