using System;

namespace AfterBuildEvent.DspCalcQuickUpdate.Mods;

internal static class FractionateEverythingQuickUpdate {
    public static ModQuickUpdateSpec Create(ModSourceConfig config) {
        return new() {
            SourceConfig = config,
            AuditPathPrefixes = [
                "FractionateEverything.csproj",
                "Assets/manifest.json",
                "src/Logic/",
                "src/Utils/ProtoID.cs",
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
