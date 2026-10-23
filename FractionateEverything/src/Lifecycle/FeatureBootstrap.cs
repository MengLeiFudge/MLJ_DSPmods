using FE.Logic.Civilization;
using FE.Logic.Civilization.Monument;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Enhancements.VanillaRecipes;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Items;
using FE.Logic.Station;

namespace FE.Lifecycle;

/// <summary>
/// FE 功能域的启动编排入口，避免插件主类直接硬编码所有 manager 生命周期。
/// </summary>
public static class FeatureBootstrap {
    private static bool finished;

    public static void PreAddData() {
        // 添加生命工业使用的核心物品与原胚
        ItemManager.AddCoreItemsAndPrototypes();
        MonumentCatalog.PreAdd();
        // 初步添加分馏塔
        FractionatorManager.AddFractionators();
        // 初步添加物流交互站
        StationManager.AddStations();
        // 添加第一页物流交互科技与第二页成长科技。
        GrowthTechnologyManager.AddTechs();
        // 添加指引手册
        TutorialManager.AddTutorials();
        RecipeManager.RegisterAdvancedRecipeProtos();
    }

    public static void PostAddData() {
        // 设置分馏塔、物流交互站颜色
        FractionatorManager.SetFractionatorMaterial();
        StationManager.SetStationMaterial();
    }

    /// <summary>
    /// 在所有内容添加完毕后，再执行的代码。
    /// </summary>
    public static void FinalAction() {
        if (finished) {
            return;
        }

        GrowthTechnologyManager.FinalizeResearchCosts();
        StackingManager.ApplyVanillaTechProtoOverrides();
        GrowthTechnologyManager.ApplyTechLayout();
        AdvancedRecipeCatalog.DetachFromItemRecipes();
        PreloadAndInitAll();
        // 获取部分数据，例如传送带最大速度等
        ProcessManager.Init();
        // 计算物品价值
        ItemManager.CalculateItemValues();
        MonumentCatalog.ApplyValues();
        MatrixNutrition.Initialize();
        // 将物品分类到各个矩阵层级中
        ItemManager.ClassifyItemsToMatrix();
        // 此时尚无玩家；必须先设置原型生命值与能耗，再由存档创建 SkillSystem 的生命值缓存。
        FractionatorManager.UpdateHpAndEnergy();
        StationManager.UpdateHpAndEnergy();
        // SetFractionatorCacheSize 用到了 Init 生成的数据
        FractionatorManager.SetFractionatorCacheSize();
        // AddFracRecipes 用到了 Init 生成的数据
        MetadataExtraction.RegisterSources();
        RecipeManager.AddFracRecipes();
        FractionationTabLayout.Apply();
        MonumentManager.InitializeCatalog();
        MonumentLoading.Initialize();
        CivilizationModule.Initialize();
        VanillaRecipeManager.AddVanillaRecipes();
        // CalculateItemModSaveCount 用到了 CalculateItemValues 生成的数据
        StationManager.CalculateItemModSaveCount();
        finished = true;
    }

    private static void PreloadAndInitAll() {
        ItemProto.InitFuelNeeds();
        ItemProto.InitTurretNeeds();
        ItemProto.InitFluids();
        ItemProto.InitTurrets();
        ItemProto.InitEnemyDropTables();
        ItemProto.InitConstructableItems();
        ItemProto.InitItemIds();
        ItemProto.InitItemIndices();
        ItemProto.InitMechaMaterials();
        ItemProto.InitFighterIndices();
        ItemProto.InitPowerFacilityIndices();
        ItemProto.InitProductionMask();
        ModelProto.InitMaxModelIndex();
        ModelProto.InitModelIndices();
        ModelProto.InitModelOrders();
        RecipeProto.InitRecipeItems();
        RecipeProto.InitFractionatorNeeds();
        SignalProtoSet.InitSignalKeyIdPairs();
        RaycastLogic.LoadStatic();
        // 重新设定堆叠大小
        StorageComponent.staticLoaded = false;
        StorageComponent.LoadStatic();
    }
}
