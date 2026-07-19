using System.IO;
using FE.Logic.Civilization.Achievements;
using FE.Logic.Civilization.Analysis;
using FE.Logic.Civilization.Configuration;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Progression;
using FE.Utils;
using static FE.Utils.Utils;
using CivilizationAchievementState = FE.Logic.Civilization.Achievements.AchievementState;

namespace FE.Logic.Civilization;

/// <summary>
/// 远古文明探索域的初始化、存档、低频更新和运行投影聚合入口。
/// </summary>
public static class CivilizationModule {
    public static void AddTranslations() {
        Register("文明解析", "Civilization Analysis");
        Register("文明阶段-电磁", "Electromagnetic Stage");
        Register("文明阶段-能量", "Energy Stage");
        Register("文明阶段-结构", "Structure Stage");
        Register("文明阶段-信息", "Information Stage");
        Register("文明阶段-引力", "Gravity Stage");
        Register("文明阶段-宇宙", "Universe Stage");
        Register("文明成就-首项协议", "First Recovered Protocol");
        Register("文明成就-完整阶段", "First Completed Stage");
        Register("文明成就-首次科技投入", "First Ancient Technology");
        Register("文明成就-千次分馏", "One Thousand Fractionations");
    }

    public static void Initialize() {
        ProgressionProfileRegistry.Initialize();
        ProtocolCatalog.Initialize();
        AncientTechTreeCatalog.Initialize();
        AchievementCatalog.Initialize();
        AnalysisProgressStore.IntoOtherSave();
        CivilizationRuntimeSync.Refresh();
    }

    public static void Tick() => AchievementService.Tick();

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Profile", ReadProfileSnapshot),
            ("Analysis", AnalysisProgressStore.Import),
            ("Protocols", ProtocolProgressStore.Import),
            ("Technology", AncientTechTreeState.Import),
            ("Achievements", CivilizationAchievementState.Import),
            ("Recovery", br => CivilizationRecoveryManager.Import(br))
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Profile", WriteProfileSnapshot),
            ("Analysis", AnalysisProgressStore.Export),
            ("Protocols", ProtocolProgressStore.Export),
            ("Technology", AncientTechTreeState.Export),
            ("Achievements", CivilizationAchievementState.Export),
            ("Recovery", bw => CivilizationRecoveryManager.Export(bw))
        );
    }

    public static void AfterImport() {
        CivilizationRuntimeSync.Refresh();
    }

    public static void IntoOtherSave() {
        AnalysisProgressStore.IntoOtherSave();
        ProtocolProgressStore.IntoOtherSave();
        AncientTechTreeState.IntoOtherSave();
        CivilizationAchievementState.IntoOtherSave();
        AchievementService.IntoOtherSave();
        CivilizationRecoveryManager.IntoOtherSave();
        CivilizationRuntimeSync.Refresh();
    }

    private static void ReadProfileSnapshot(BinaryReader r) {
        _ = r.ReadString();
        _ = r.ReadInt32();
    }

    private static void WriteProfileSnapshot(BinaryWriter w) {
        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        w.Write(profile?.ProfileId ?? string.Empty);
        w.Write(profile?.Version ?? 0);
    }
}
