namespace FE.Logic.Fractionation.Recipes;

/// <summary>
/// 分馏塔产物输出信息。
/// </summary>
/// <param name="isMainOutput">是否为主输出</param>
/// <param name="itemId">物品ID</param>
/// <param name="count">物品数目</param>
public class ProductOutputInfo(bool isMainOutput, int itemId, int count) {
    public bool isMainOutput = isMainOutput;
    public int itemId = itemId;
    public int count = count;

    public void Set(bool newIsMainOutput, int newItemId, int newCount) {
        isMainOutput = newIsMainOutput;
        itemId = newItemId;
        count = newCount;
    }
}
