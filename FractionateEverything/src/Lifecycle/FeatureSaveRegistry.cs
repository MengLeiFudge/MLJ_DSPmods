using System.IO;
using FE.Logic.Buildings;
using FE.Logic.Civilization;
using FE.Logic.DarkFog;
using FE.Logic.Fractionation.Process;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.DataCenter;
using FE.Logic.Progression;
using FE.Logic.Station;
using FE.Logic.VanillaRecipes;
using FE.UI.MainPanel;
using static FE.Utils.Utils;

namespace FE.Lifecycle;

/// <summary>
/// FE 功能域存档块注册表，集中维护保存、读取和切档清理顺序。
/// </summary>
public static class FeatureSaveRegistry {
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Recipe", RecipeManager.Import),
            ("VanillaRecipes", VanillaRecipeManager.Import),
            ("Stacking", StackingManager.Import),
            ("Building", BuildingManager.Import),
            ("Item", DataCenterInventory.Import),
            ("Process", ProcessManager.Import),
            ("AncientCivilization", CivilizationModule.Import),
            ("UI", MainWindow.Import),
            ("Station", StationManager.Import)
        );
        VanillaRecipeManager.SyncRuntimeStateAfterImport();
        CivilizationModule.AfterImport();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Recipe", RecipeManager.Export),
            ("VanillaRecipes", VanillaRecipeManager.Export),
            ("Stacking", StackingManager.Export),
            ("Building", BuildingManager.Export),
            ("Item", DataCenterInventory.Export),
            ("Process", ProcessManager.Export),
            ("AncientCivilization", CivilizationModule.Export),
            ("UI", MainWindow.Export),
            ("Station", StationManager.Export)
        );
    }

    public static void IntoOtherSave() {
        RecipeManager.IntoOtherSave();
        VanillaRecipeManager.IntoOtherSave();
        StackingManager.IntoOtherSave();
        BuildingManager.IntoOtherSave();
        DataCenterInventory.IntoOtherSave();
        ProcessManager.IntoOtherSave();
        DarkFogCombatManager.IntoOtherSave();
        CivilizationModule.IntoOtherSave();
        MainWindow.IntoOtherSave();
        StationManager.IntoOtherSave();

        TechManager.ResetTechUnlockFlags();
    }
}
