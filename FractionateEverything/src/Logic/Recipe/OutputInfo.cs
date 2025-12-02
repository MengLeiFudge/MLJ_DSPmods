using System;
using FE.UI.View.Setting;
using static FE.Logic.Manager.ItemManager;
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

    public bool ShowSuccessRate => OutputTotalCount >= Math.Sqrt(500000 / itemValue[OutputID]);
    public bool ShowOutputName => OutputTotalCount > 0;
    public bool ShowOutputCount => OutputTotalCount >= Math.Sqrt(80000 / itemValue[OutputID]);

    public override string ToString() {
        ItemProto item = LDB.items.Select(OutputID);
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        string s1 = forceShow || ShowOutputCount ? OutputCount.ToString("F3") : "???";
        string s2 = forceShow || ShowOutputName ? item.name : "???";
        string s3 = forceShow || ShowSuccessRate ? SuccessRate.ToString("P3") : "???";
        return $"{s1} {s2} ~ {s3} ({"总计".Translate()} {OutputTotalCount})";
    }
}
