namespace AfterBuildEvent.DspCalcQuickUpdate.Mods;

internal static class MoreMegaStructureQuickUpdate {
    public static ModQuickUpdateSpec Create(ModSourceConfig config) {
        return new() {
            SourceConfig = config,
            AuditPathPrefixes = [
                "MoreMegaStructure/MoreMegaStructure/MoreMegaStructure.cs",
                "MoreMegaStructure/MoreMegaStructure/StarAssembly.cs",
                "MoreMegaStructure/MoreMegaStructure/WarpArray.cs",
                "MoreMegaStructure/MoreMegaStructure/ReceiverPatchers.cs",
            ],
            ReadSourceVersion = QuickUpdateHelpers.ReadMoreMegaStructureVersionFromTags,
            AuditSource = QuickUpdateHelpers.ConservativeAudit,
        };
    }
}
