namespace FE.Logic.Fractionation.Growth;
/// <summary>
/// 配方成长经验来源模式。
/// </summary>
public enum RecipeGrowthMode {
    None = 0,
    DrawDuplicate = 1,
    ProcessExp = 2,
    Hybrid = 3,
    ProcessExpWithPity = 4,
    FixedMax = 5,
}
