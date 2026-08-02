using System.IO;
using FE.Logic.Civilization.Analysis;
using FE.Logic.Civilization.DarkFog;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Utils;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

#region CivilizationModule

/// <summary>
/// 远古文明探索域的翻译、初始化、存档和运行投影入口。
/// </summary>
public static class CivilizationModule {
    public static void AddTranslations() {
        Register("文明解析", "Civilization Analysis");
    }

    public static void Initialize() {
        ProtocolCatalog.Initialize();
        AncientTechTreeCatalog.Initialize();
        DarkFogInvestigationService.Initialize();
        AnalysisProgressStore.IntoOtherSave();
        CivilizationRuntimeSync.Refresh();
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            // 旧 Profile 块直接忽略。
            ("Profile", _ => { }),
            ("Analysis", AnalysisProgressStore.Import),
            ("Protocols", ProtocolProgressStore.Import),
            ("Technology", AncientTechTreeState.Import),
            ("Recovery", CivilizationRecoveryManager.Import),
            ("DarkFog", DarkFogInvestigationState.Import)
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Analysis", AnalysisProgressStore.Export),
            ("Protocols", ProtocolProgressStore.Export),
            ("Technology", AncientTechTreeState.Export),
            ("Recovery", CivilizationRecoveryManager.Export),
            ("DarkFog", DarkFogInvestigationState.Export)
        );
    }

    public static void AfterImport() {
        DarkFogInvestigationService.AfterImport();
        CivilizationRuntimeSync.Refresh();
    }

    public static void IntoOtherSave() {
        AnalysisProgressStore.IntoOtherSave();
        ProtocolProgressStore.IntoOtherSave();
        AncientTechTreeState.IntoOtherSave();
        CivilizationRecoveryManager.IntoOtherSave();
        // 必须走 Service 路径：同时清 pending 清剿索引、dirty、节流 tick 与认知缓存，避免跨档泄漏。
        DarkFogInvestigationService.IntoOtherSave();
        CivilizationRuntimeSync.Refresh();
    }
}

#endregion // CivilizationModule
