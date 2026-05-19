using System.Collections.Generic;

namespace AfterBuildEvent.DspCalcQuickUpdate.Mods;

internal static class OrbitalRingQuickUpdate {
    public static ModQuickUpdateSpec Create(ModSourceConfig config) {
        return new() {
            SourceConfig = config,
            AuditPathPrefixes = [
                "data/items_mod.json",
                "data/items_vanilla.json",
                "data/prefabDescs.json",
                "data/recipes.json",
                "data/techs.json",
                "src/Utils/JsonDataUtils.cs",
                "src/Utils/JsonHelper.cs",
                "src/Utils/ProtoID.cs",
            ],
            ReadSourceVersion = spec => QuickUpdateHelpers.ReadVersionConst(
                spec.RequireFile("src", "ProjectOrbitalRing.cs")),
            AuditSource = QuickUpdateHelpers.ConservativeAudit,
        };
    }
}
