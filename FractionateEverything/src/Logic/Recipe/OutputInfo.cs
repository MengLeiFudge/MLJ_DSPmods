using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

public class OutputInfo(float successRate, int outputID, int outputCount) {
    public static void AddTranslations() {
        Register("总计 ", "Total ");
    }

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

    public bool ShowSuccessRate => OutputTotalCount >= 1000;
    public bool ShowOutputName => OutputTotalCount > 0;
    public bool ShowOutputCount => OutputTotalCount >= 200;

    public override string ToString() {
        ItemProto item = LDB.items.Select(OutputID);
        string s1 = ShowOutputCount ? OutputCount.ToString("F2") : "???";
        string s2 = ShowOutputName ? item.name : "???";
        string s3 = ShowSuccessRate ? SuccessRate.ToString("P3") : "???";
        return $"{s1} {s2} ~ {s3} ({"总计 ".Translate()}{OutputTotalCount})";
    }
}
