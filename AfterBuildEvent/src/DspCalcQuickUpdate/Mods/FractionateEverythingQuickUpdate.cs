using System;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent.DspCalcQuickUpdate.Mods;

internal static class FractionateEverythingQuickUpdate {
    public static ModQuickUpdateSpec Create(ModSourceConfig config) {
        return new() {
            SourceConfig = config,
            GitDir = SolutionFullDir,
            WorktreeScope = "FractionateEverything",
            AuditPathPrefixes = [
                "FractionateEverything/FractionateEverything.csproj",
                "FractionateEverything/Assets/manifest.json",
                "FractionateEverything/src/Logic/",
                "FractionateEverything/src/Utils/ProtoID.cs",
            ],
            ReadSourceVersion = ReadVersion,
            AuditSource = QuickUpdateHelpers.ConservativeAudit,
        };
    }

    private static string ReadVersion(ModQuickUpdateSpec spec) {
        string projectVersion = QuickUpdateHelpers.ReadProjectVersion(
            spec.RequireFile("FractionateEverything.csproj"));
        string manifestVersion = QuickUpdateHelpers.ReadManifestVersion(
            spec.RequireFile("Assets", "manifest.json"));
        if (!string.Equals(projectVersion, manifestVersion, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException(
                $"万物分馏 csproj 版本 {projectVersion} 与 manifest 版本 {manifestVersion} 不一致");
        }
        return projectVersion;
    }
}
