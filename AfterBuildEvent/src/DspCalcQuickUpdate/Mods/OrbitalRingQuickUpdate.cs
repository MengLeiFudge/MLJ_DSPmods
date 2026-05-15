using System.Collections.Generic;

namespace AfterBuildEvent.DspCalcQuickUpdate.Mods;

internal static class OrbitalRingQuickUpdate {
    public static ModQuickUpdateSpec Create(ModSourceConfig config) {
        return new() {
            SourceConfig = config,
            AuditPathPrefixes = [
                "src/Protos/",
                "src/Utils/",
                "src/ProjectOrbitalRing.cs",
            ],
            ReadSourceVersion = spec => QuickUpdateHelpers.ReadVersionConst(
                spec.RequireFile("src", "ProjectOrbitalRing.cs")),
            AuditSource = QuickUpdateHelpers.ConservativeAudit,
        };
    }
}
