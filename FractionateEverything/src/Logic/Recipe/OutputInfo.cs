namespace FE.Logic.Recipe;

public class OutputInfo(float successRate, int outputID, int outputCount) {
    /// <summary>
    /// 输出物品的概率
    /// </summary>
    public float SuccessRate { get; set; } = successRate;

    /// <summary>
    /// 输出物品的ID
    /// </summary>
    public int OutputID { get; set; } = outputID;

    /// <summary>
    /// 输出物品的数目
    /// </summary>
    public float OutputCount { get; set; } = outputCount;

    /// <summary>
    /// 输出物品的总数，用于控制配方信息的隐藏显示
    /// </summary>
    public int OutputTotalCount { get; set; } = 0;

    bool ShowSuccessRate => OutputTotalCount >= 500;
    bool ShowOutputID => OutputTotalCount > 0;
    bool ShowOutputCount => OutputTotalCount >= 100;
}
