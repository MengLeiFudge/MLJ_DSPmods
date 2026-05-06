using System.IO;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.Process;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Manager;
using FE.UI.MainPanel;
using static FE.Utils.Utils;

namespace FE.Persistence;

/// <summary>
/// FE 功能域存档块注册表，集中维护保存、读取和切档清理顺序。
/// </summary>
public static class FeatureSaveRegistry {
    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Recipe", RecipeManager.Import),
            ("RecipeGrowth", RecipeGrowthManager.Import),
            ("Building", BuildingManager.Import),
            ("Item", ItemManager.Import),
            ("Process", ProcessManager.Import),
            ("Gacha", GachaManager.Import),
            ("Economy", EconomyManager.Import),
            ("UI", MainWindow.Import),
            ("Station", StationManager.Import)
        );
        TechManager.RequestLoadTimeRecipeBaselineApply();
        TechManager.TryApplyLoadTimeRecipeBaselines();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Recipe", RecipeManager.Export),
            ("RecipeGrowth", RecipeGrowthManager.Export),
            ("Building", BuildingManager.Export),
            ("Item", ItemManager.Export),
            ("Process", ProcessManager.Export),
            ("Gacha", GachaManager.Export),
            ("Economy", EconomyManager.Export),
            ("UI", MainWindow.Export),
            ("Station", StationManager.Export)
        );
    }

    public static void IntoOtherSave() {
        RecipeManager.IntoOtherSave();
        RecipeGrowthManager.IntoOtherSave();
        BuildingManager.IntoOtherSave();
        ItemManager.IntoOtherSave();
        ProcessManager.IntoOtherSave();
        GachaManager.IntoOtherSave();
        EconomyManager.IntoOtherSave();
        DarkFogCombatManager.IntoOtherSave();
        MainWindow.IntoOtherSave();
        StationManager.IntoOtherSave();

        TechManager.ResetTechUnlockFlags();
    }
}
