namespace FE.Logic.Fractionation.FracRecipes;

/// <summary>
/// 单次分馏输入的销毁、通过或产出结果。
/// </summary>
public enum FractionationOutcome {
    Destroyed,
    PassThrough,
    Produced,
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
