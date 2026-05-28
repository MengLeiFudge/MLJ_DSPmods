namespace FE.Logic.Fractionation.FracRecipes;

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
