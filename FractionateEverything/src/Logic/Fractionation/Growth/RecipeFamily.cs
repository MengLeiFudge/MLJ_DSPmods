namespace FE.Logic.Fractionation.Growth;
/// <summary>
/// 配方成长规则使用的配方家族分类。
/// </summary>
public enum RecipeFamily {
    Unknown = 0,
    BuildingTrainForward = 1,
    BuildingTrainReverse = 2,
    MineralCopyNormal = 3,
    MineralCopyDarkFog = 4,
    ConversionMaterialNormal = 5,
    ConversionMaterialDarkFog = 6,
    ConversionBuilding = 7,
    PointAggregate = 8,
    Rectification = 9,
}
