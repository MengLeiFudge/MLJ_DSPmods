namespace FE.Logic.Fractionation.Growth;
/// <summary>
/// 单个分馏配方的等级、经验和解锁来源状态。
/// </summary>
public sealed class RecipeGrowthState {
    public int Level;
    public int GrowthExp;
    public int PityProgress;
    public RecipeUnlockSourceFlags UnlockSourceFlags;
    public long LastTouchedTick;
}
