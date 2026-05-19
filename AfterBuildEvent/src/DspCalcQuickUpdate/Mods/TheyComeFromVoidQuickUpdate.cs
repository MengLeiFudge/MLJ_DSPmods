namespace AfterBuildEvent.DspCalcQuickUpdate.Mods;

internal static class TheyComeFromVoidQuickUpdate {
    public static ModQuickUpdateSpec Create(ModSourceConfig config) {
        return new() {
            SourceConfig = config,
            AuditPathPrefixes = [
                "src/Configs.cs",
                "src/BattleProtos.cs",
                "src/EventProto.cs",
                "src/Relic.cs",
                "src/StarFortress.cs",
            ],
            ReadSourceVersion = spec => QuickUpdateHelpers.ReadTheyComeFromVoidVersion(
                spec.RequireFile("src", "Configs.cs")),
            AuditSource = QuickUpdateHelpers.ConservativeAudit,
        };
    }
}
