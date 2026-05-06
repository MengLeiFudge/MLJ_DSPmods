namespace FE.Logic.Fractionation.Growth;
/// <summary>
/// RecipeGrowthState 类型。
/// </summary>
public sealed class RecipeGrowthState {
    public int Level;
    public int GrowthExp;
    public int PityProgress;
    public RecipeUnlockSourceFlags UnlockSourceFlags;
    public long LastTouchedTick;
}
