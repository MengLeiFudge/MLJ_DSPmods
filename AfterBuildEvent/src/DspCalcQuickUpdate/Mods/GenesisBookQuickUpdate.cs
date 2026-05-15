namespace AfterBuildEvent.DspCalcQuickUpdate.Mods;

internal static class GenesisBookQuickUpdate {
    public static ModQuickUpdateSpec Create(ModSourceConfig config) {
        return new() {
            SourceConfig = config,
            AuditPathPrefixes = [
                "data/",
                "src/ProjectGenesis.cs",
                "src/Patches/Hooks/InitialRecipePatches.cs",
                "src/Patches/Hooks/InitialTechPatches.cs",
                "src/Utils/ProtoID.cs",
            ],
            ReadSourceVersion = spec => QuickUpdateHelpers.ReadVersionConst(
                spec.RequireFile("src", "ProjectGenesis.cs")),
            AuditSource = QuickUpdateHelpers.ConservativeAudit,
        };
    }
}
