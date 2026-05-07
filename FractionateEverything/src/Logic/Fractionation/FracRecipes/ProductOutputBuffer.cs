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
    public int InputRemoveCount;
    public int ConsumedRegisterCount;
    public int SuccessCount;
    public int DestroyedCount;
    public int PassThroughCount;

    public bool HasOutput => SuccessCount > 0;
}

/// <summary>
/// 分馏热路径复用输出缓冲，避免每次判定都分配 List 和 ProductOutputInfo。
/// </summary>
public sealed class ProductOutputBuffer {
    private ProductOutputInfo[] items = new ProductOutputInfo[4];

    public int Count { get; private set; }

    public ProductOutputInfo this[int index] => items[index];

    public void Clear() {
        Count = 0;
    }

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
