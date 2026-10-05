using System.Threading;
using System;
using FE.UI.MainPanel.Setting;
using static FE.Logic.Items.ItemManager;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 分馏配方某一项产物信息。
/// 注意，只有<see cref="FE.Logic.Fractionation.Recipes.OutputInfo.OutputTotalCount"/>值是可变的，其余均在游戏初始化时固定。
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

/// <summary>
/// 分馏塔产物输出信息。
/// </summary>
public class ProductOutputInfo(bool isMainOutput, int itemId, int count) {
    /// <summary>
    /// 记录产物条目是否属于主产物输出。
    /// </summary>
    public bool isMainOutput = isMainOutput;
    /// <summary>
    /// 记录产物条目的物品 ID。
    /// </summary>
    public int itemId = itemId;
    /// <summary>
    /// 记录产物条目的数量。
    /// </summary>
    public int count = count;

    /// <summary>
    /// 更新复用产物条目的主副产物标记、物品和数量。
    /// </summary>
    public void Set(bool newIsMainOutput, int newItemId, int newCount) {
        isMainOutput = newIsMainOutput;
        itemId = newItemId;
        count = newCount;
    }
}

/// <summary>
/// 一批分馏处理的消耗、产出和增产点统计。
/// </summary>
public struct FractionationBatchResult {
    /// <summary>
    /// 记录本批次从输入缓存移除的物品数量。
    /// </summary>
    public int InputRemoveCount;
    /// <summary>
    /// 记录本批次写入消耗统计的物品数量。
    /// </summary>
    public int ConsumedRegisterCount;
    /// <summary>
    /// 记录本批次成功分馏的次数。
    /// </summary>
    public int SuccessCount;
    /// <summary>
    /// 记录本批次因损毁消耗的输入数量。
    /// </summary>
    public int DestroyedCount;
    /// <summary>
    /// 记录本批次失败直通的输入数量。
    /// </summary>
    public int PassThroughCount;
    /// <summary>
    /// 保存批量结算中直通输入保留的增产点数。
    /// </summary>
    public int PassThroughInc;

    /// <summary>
    /// 判断本次批量结算是否产生成功输出。
    /// </summary>
    public bool HasOutput => SuccessCount > 0;
}

/// <summary>
/// 分馏热路径复用输出缓冲，避免每次判定都分配 List 和 ProductOutputInfo。
/// </summary>
public sealed class ProductOutputBuffer {
    private ProductOutputInfo[] items = new ProductOutputInfo[4];

    /// <summary>
    /// 获取缓存中的产物条目数量。
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// 按索引读取缓存中的产物条目。
    /// </summary>
    public ProductOutputInfo this[int index] => items[index];

    /// <summary>
    /// 清空缓存中的全部产物条目。
    /// </summary>
    public void Clear() {
        Count = 0;
    }

    /// <summary>
    /// 向缓存追加一条产物条目。
    /// </summary>
    public void Add(bool isMainOutput, int itemId, int count) {
        if (count <= 0) {
            return;
        }
        if (Count >= items.Length) {
            ProductOutputInfo[] newItems = new ProductOutputInfo[items.Length * 2];
            items.CopyTo(newItems, 0);
            items = newItems;
        }

        ProductOutputInfo item = items[Count];
        if (item == null) {
            items[Count] = new ProductOutputInfo(isMainOutput, itemId, count);
        } else {
            item.Set(isMainOutput, itemId, count);
        }
        Count++;
    }
}
