namespace FE.UI.MainPanel.DrawGrowth;
/// <summary>
/// 奖券兑换表中的输入输出物品数量。
/// </summary>
public readonly struct GachaExchangeRow(
    int inputItemId,
    int inputCount,
    int outputItemId,
    int outputCount,
    bool isShardRow) {
    public int InputItemId => inputItemId;
    public int InputCount => inputCount;
    public int OutputItemId => outputItemId;
    public int OutputCount => outputCount;
    public bool IsShardRow => isShardRow;
}
