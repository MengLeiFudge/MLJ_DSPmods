namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 单个分馏配方的等级、经验和解锁来源状态。
/// </summary>
public sealed class RecipeGrowthState {
    /// <summary>
    /// 读取或设置该分馏塔建筑的成长等级。
    /// </summary>
    public int Level;
    /// <summary>
    /// 获取或保存配方当前成长经验。
    /// </summary>
    public int GrowthExp;
    /// <summary>
    /// 获取或保存配方当前保底进度。
    /// </summary>
    public int PityProgress;
    /// <summary>
    /// 保存配方当前等级来源标记。
    /// </summary>
    public RecipeUnlockSourceFlags UnlockSourceFlags;
    /// <summary>
    /// 保存配方状态最后一次被修改的游戏 tick。
    /// </summary>
    public long LastTouchedTick;
}
