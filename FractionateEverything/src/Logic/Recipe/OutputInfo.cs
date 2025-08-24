using static FE.Utils.Utils;

namespace FE.Logic.Recipe;

/// <summary>
/// 分馏配方某一项产物信息。
/// 注意，只有<see cref="FE.Logic.Recipe.OutputInfo.OutputTotalCount"/>值是可变的，其余均在游戏初始化时固定。
/// </summary>
public class OutputInfo(float successRate, int outputID, float outputCount) {
    public static void AddTranslations() {
        Register("总计", "Total");
    }

    /// <summary>
    /// 输出物品的概率
    /// </summary>
    public float SuccessRate => successRate;

    /// <summary>
    /// 输出物品的ID
    /// </summary>
    public int OutputID => outputID;

    /// <summary>
    /// 输出物品的数目
    /// </summary>
    public float OutputCount => outputCount;

    /// <summary>
    /// 输出物品的总数，用于控制配方信息的隐藏显示
    /// </summary>
    public int OutputTotalCount { get; set; } = 0;

    public bool ShowSuccessRate => OutputTotalCount >= 500;
    public bool ShowOutputName => OutputTotalCount > 0;
    public bool ShowOutputCount => OutputTotalCount >= 200;

    public override string ToString() {
        ItemProto item = LDB.items.Select(OutputID);
        bool sandboxMode = GameMain.sandboxToolsEnabled;
        string s1 = ShowOutputCount || sandboxMode ? OutputCount.ToString("F3") : "???";
        string s2 = ShowOutputName || sandboxMode ? item.name : "???";
        string s3 = ShowSuccessRate || sandboxMode ? SuccessRate.ToString("P3") : "???";
        return $"{s1} {s2} ~ {s3} ({"总计".Translate()} {OutputTotalCount})";
    }
}
