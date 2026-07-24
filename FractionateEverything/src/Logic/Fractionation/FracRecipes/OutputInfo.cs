using System;
using System.Threading;
using FE.UI.MainPanel.Setting;
using static FE.Logic.Items.ItemManager;

namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 分馏配方某一项产物信息。
/// 注意，只有<see cref="FE.Logic.Fractionation.FracRecipes.OutputInfo.OutputTotalCount"/>值是可变的，其余均在游戏初始化时固定。
/// </summary>
public class OutputInfo(float successRatio, int outputID, float outputCount) {
    /// <summary>
    /// 输出物品的概率
    /// </summary>
    public float SuccessRatio => successRatio;

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
    public int OutputTotalCount {
        get => Volatile.Read(ref outputTotalCount);
        set => Volatile.Write(ref outputTotalCount, Math.Max(0, value));
    }

    private int outputTotalCount;

    /// <summary>
    /// 以线程安全方式累计产物数量，并在达到 int 上限后保持饱和。
    /// </summary>
    public void AddOutputCount(int count) {
        if (count <= 0) {
            return;
        }

        int current;
        int updated;
        do {
            current = Volatile.Read(ref outputTotalCount);
            updated = current > int.MaxValue - count ? int.MaxValue : current + count;
        } while (Interlocked.CompareExchange(ref outputTotalCount, updated, current) != current);
    }

    /// <summary>
    /// 判断 UI 是否展示该输出的概率。
    /// </summary>
    public bool ShowSuccessRatio => OutputTotalCount >= Math.Sqrt(500000 / itemValue[OutputID]);
    /// <summary>
    /// 判断 UI 是否展示该输出名称。
    /// </summary>
    public bool ShowOutputName => OutputTotalCount > 0;
    /// <summary>
    /// 判断 UI 是否展示该输出数量。
    /// </summary>
    public bool ShowOutputCount => OutputTotalCount >= Math.Sqrt(80000 / itemValue[OutputID]);

    /// <summary>
    /// 生成该输出信息面向 UI 的本地化描述文本。
    /// </summary>
    public override string ToString() {
        ItemProto item = LDB.items.Select(OutputID);
        bool forceShow = GameMain.sandboxToolsEnabled || Miscellaneous.ShowFractionateRecipeDetails;
        string s1 = forceShow || ShowOutputCount ? OutputCount.ToString("F3") : "???";
        string s2 = forceShow || ShowOutputName ? item.name : "???";
        string s3 = forceShow || ShowSuccessRatio ? SuccessRatio.ToString("P3") : "???";
        return $"{s1} × {s2}  {s3}";
    }
}
