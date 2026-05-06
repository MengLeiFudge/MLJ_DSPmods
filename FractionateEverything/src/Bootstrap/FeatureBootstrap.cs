using CommonAPI.Systems.ModLocalization;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.Process;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.UI.MainPanel;
using FeUtils = FE.Utils.Utils;
using static FE.Utils.Utils;

namespace FE.Bootstrap;

/// <summary>
/// FE 功能域的启动编排入口，避免插件主类直接硬编码所有 manager 生命周期。
/// </summary>
public static class FeatureBootstrap {
    private static bool finished;

    public static void AddTranslations() {
        Register("分馏页面", "Fractionate", "分馏");
        Register("分馏与插件页面", "Frac&Beacon", "分馏&插件");
        ERecipeExtension.AddTranslations();
        OutputInfo.AddTranslations();
        FeUtils.AddTranslations();
        BuildingManager.AddTranslations();
        ItemManager.AddTranslations();
        ProcessManager.AddTranslations();
        StationManager.AddTranslations();
        TechManager.AddTranslations();
        TutorialManager.AddTranslations();
        MainWindow.AddTranslations();
    }

    public static void PreAddData() {
        // 添加 2.3 主路径使用的核心物品与原胚
        ItemManager.AddCoreItemsAndPrototypes();
        // 初步添加分馏塔
        BuildingManager.AddFractionators();
        // 添加科技
        TechManager.AddTechs();
        // 添加指引手册
        TutorialManager.AddTutorials();
    }

    public static void PostAddData() {
        // 设置分馏塔、物流交互站颜色
        BuildingManager.SetFractionatorMaterial();
    }

    /// <summary>
    /// 在所有内容添加完毕后，再执行的代码。
    /// </summary>
    public static void FinalAction() {
        if (finished) {
            return;
        }

        PreloadAndInitAll();
        // 获取部分数据，例如传送带最大速度等
        ProcessManager.Init();
        // 计算物品价值
        ItemManager.CalculateItemValues();
        // 将物品分类到各个矩阵层级中
        ItemManager.ClassifyItemsToMatrix();
        // 动态经济系统依赖基础价值与矩阵阶段映射
        EconomyManager.Init();
        // UpdateHpAndEnergy 用到了 Init 生成的数据
        BuildingManager.UpdateHpAndEnergy();
        // SetFractionatorCacheSize 用到了 Init 生成的数据
        BuildingManager.SetFractionatorCacheSize();
        // AddFracRecipes 用到了 Init 生成的数据
        RecipeManager.AddFracRecipes();
        RecipeGrowthManager.InitializeFromRecipes();
        RecipeManager.AddVanillaRecipes();
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
