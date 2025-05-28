namespace FE.Logic.Recipe;

/// <summary>
/// 表示配方的输出物品（目前仅用于转化塔）
/// </summary>
public struct OutputItem {
    /// <summary>
    /// 输出物品ID
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// 输出成功率
    /// </summary>
    public float Ratio { get; set; }

    /// <summary>
    /// 输出数量
    /// </summary>
    public float Count { get; set; }

    /// <summary>
    /// 创建输出物品实例
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="ratio">成功率</param>
    /// <param name="count">数量</param>
    public OutputItem(int itemId, float ratio, float count) {
        ItemId = itemId;
        Ratio = ratio;
        Count = count;
    }

    /// <summary>
    /// 克隆一个输出物品
    /// </summary>
    /// <returns>克隆的输出物品</returns>
    public OutputItem Clone() {
        return new OutputItem(ItemId, Ratio, Count);
    }

    /// <summary>
    /// 应用增产效果
    /// </summary>
    /// <param name="ratioMultiplier">成功率乘数</param>
    /// <param name="countMultiplier">数量乘数</param>
    /// <returns>应用增产效果后的新输出物品</returns>
    public OutputItem ApplyBoost(float ratioMultiplier, float countMultiplier) {
        return new OutputItem(
            ItemId,
            Ratio * ratioMultiplier,
            Count * countMultiplier
        );
    }

    /// <summary>
    /// 转换为元组
    /// </summary>
    /// <returns>表示输出物品的元组</returns>
    public (int itemId, float ratio, float count) ToTuple() {
        return (ItemId, Ratio, Count);
    }

    /// <summary>
    /// 从元组创建输出物品
    /// </summary>
    /// <param name="tuple">表示输出物品的元组</param>
    /// <returns>创建的输出物品</returns>
    public static OutputItem FromTuple((int itemId, float ratio, float count) tuple) {
        return new OutputItem(tuple.itemId, tuple.ratio, tuple.count);
    }
}
