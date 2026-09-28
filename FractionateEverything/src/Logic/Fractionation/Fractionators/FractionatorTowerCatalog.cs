using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// FE 3.0 五座活动分馏塔及其稳定物理原型映射目录。
/// </summary>
public static class FractionatorTowerCatalog {
    /// <summary>
    /// 当前版本实际注册并参与运行的两座基础塔和三座高级塔。
    /// </summary>
    public static readonly int[] ActiveFractionatorBuildingIds = [
        IFE基础矿物塔,
        IFE基础解析塔,
        IFE高级矿物塔,
        IFE高级交互塔,
        IFE高级转化塔,
    ];

    /// <summary>获取当前活动塔型数量。</summary>
    public static int ActiveFractionatorTypeCount => ActiveFractionatorBuildingIds.Length;

    /// <summary>判断物品是否为当前版本的活动分馏塔。</summary>
    public static bool IsActiveFractionator(int itemId) => GetActiveFractionatorIndex(itemId) >= 0;

    /// <summary>获取活动塔在五塔目录中的稳定索引。</summary>
    public static int GetActiveFractionatorIndex(int itemId) {
        return itemId switch {
            IFE基础矿物塔 => 0,
            IFE基础解析塔 => 1,
            IFE高级矿物塔 => 2,
            IFE高级交互塔 => 3,
            IFE高级转化塔 => 4,
            _ => -1,
        };
    }

    /// <summary>
    /// 获取活动分馏塔对应的配方分支；未知建筑返回零值。
    /// </summary>
    public static ERecipe GetRecipeType(int buildingId) {
        return buildingId switch {
            IFE基础矿物塔 => ERecipe.BasicMineral,
            IFE基础解析塔 => ERecipe.BasicAnalysis,
            IFE高级矿物塔 => ERecipe.AdvancedMineral,
            IFE高级交互塔 => ERecipe.AdvancedInteraction,
            IFE高级转化塔 => ERecipe.AdvancedConversion,
            _ => (ERecipe)0,
        };
    }

    /// <summary>判断物品是否为无需矩阵供养的基础塔。</summary>
    public static bool IsBasicTower(int itemId) => itemId is IFE基础矿物塔 or IFE基础解析塔;

    /// <summary>判断物品是否为需要矩阵供养的高级生命塔。</summary>
    public static bool IsAdvancedTower(int itemId) =>
        itemId is IFE高级矿物塔 or IFE高级交互塔 or IFE高级转化塔;

    /// <summary>判断物品是否为当前版本的三类高级塔之一。</summary>
    public static bool IsAdvancedTowerItem(int itemId) => IsAdvancedTower(itemId);
}
