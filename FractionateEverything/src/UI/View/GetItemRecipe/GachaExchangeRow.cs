namespace FE.UI.View.GetItemRecipe;

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
