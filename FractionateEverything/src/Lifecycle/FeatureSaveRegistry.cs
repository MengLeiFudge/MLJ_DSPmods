using System.IO;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Enhancements.VanillaRecipes;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Station;
using FE.UI.MainPanel;
using static FE.Utils.Utils;

namespace FE.Lifecycle;

/// <summary>
/// FE 功能域存档块注册表，集中维护保存、读取和切档清理顺序。
/// </summary>
public static class FeatureSaveRegistry {
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            // 旧配方与过程统计块由ReadBlock按长度隔离，识别标签后整块跳过。
            ("Recipe", _ => { }),
            ("VanillaRecipes", VanillaRecipeManager.Import),
            ("Stacking", StackingManager.Import),
            ("Building", FractionatorManager.Import),
            ("Item", DataCenterInventory.Import),
            ("Process", _ => { }),
            ("Civilization3", CivilizationModule.Import),
            ("UI", MainWindow.Import),
            ("Station", StationManager.Import)
        );
        VanillaRecipeManager.SyncRuntimeStateAfterImport();
        CivilizationModule.AfterImport();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("VanillaRecipes", VanillaRecipeManager.Export),
            ("Stacking", StackingManager.Export),
            ("Building", FractionatorManager.Export),
            ("Item", DataCenterInventory.Export),
            ("Civilization3", CivilizationModule.Export),
            ("UI", MainWindow.Export),
            ("Station", StationManager.Export)
        );
    }

    public static void IntoOtherSave() {
        RecipeManager.IntoOtherSave();
        VanillaRecipeManager.IntoOtherSave();
        StackingManager.IntoOtherSave();
        FractionatorManager.IntoOtherSave();
        DataCenterInventory.IntoOtherSave();
        CivilizationModule.IntoOtherSave();
        MainWindow.IntoOtherSave();
        StationManager.IntoOtherSave();
    }
}
