namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 分馏配方某一项产物信息。
/// 各项参数在游戏初始化时固定；可见性由研究发现资格决定。
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
    /// 生成该输出信息面向 UI 的本地化描述文本。
    /// </summary>
    public override string ToString() {
        ItemProto item = LDB.items.Select(OutputID);
        return $"{OutputCount:F3} × {item.name}  {SuccessRatio:P3}";
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
    /// <summary>正面产物中返还原输入的件数，不应计入新制造统计。</summary>
    public int ReturnedProductCount;
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
