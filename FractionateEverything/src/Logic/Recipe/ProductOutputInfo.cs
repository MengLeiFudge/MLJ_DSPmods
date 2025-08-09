namespace FE.Logic.Recipe;

/// <summary>
/// 分馏塔产物输出信息。
/// </summary>
/// <param name="isMainOutput">是否为主输出</param>
/// <param name="itemId">物品ID</param>
/// <param name="count">物品数目</param>
public class ProductOutputInfo(bool isMainOutput, int itemId, int count) {
    public readonly bool isMainOutput = isMainOutput;
    public readonly int itemId = itemId;
    public int count = count;
}
