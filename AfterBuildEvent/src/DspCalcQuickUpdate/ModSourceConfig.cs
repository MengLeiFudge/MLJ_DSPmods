using System.Collections.Generic;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal sealed class ModSourceConfig {
    public string CalcName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SourceDir { get; set; } = "";
    public string VersionRemote { get; set; } = "origin";
    public string PullRemote { get; set; } = "origin";
    public string PullBranch { get; set; } = "";

    public static IReadOnlyDictionary<string, ModSourceConfig> BuildAll() {
        Dictionary<string, ModSourceConfig> result = new(System.StringComparer.OrdinalIgnoreCase);
        Add(result, new() {
            CalcName = "MoreMegaStructure",
            DisplayName = "更多巨构",
            SourceDir = MoreMegaStructureSourceDir,
            VersionRemote = "origin",
            PullRemote = "origin",
            PullBranch = "main",
        });
        Add(result, new() {
            CalcName = "TheyComeFromVoid",
            DisplayName = "深空来敌",
            SourceDir = TheyComeFromVoidSourceDir,
            VersionRemote = "origin",
            PullRemote = "origin",
            PullBranch = "master",
        });
        Add(result, new() {
            CalcName = "GenesisBook",
            DisplayName = "创世之书",
            SourceDir = GenesisBookSourceDir,
            VersionRemote = "upstream",
            PullRemote = "upstream",
            PullBranch = "main",
        });
        Add(result, new() {
            CalcName = "OrbitalRing",
            DisplayName = "星环",
            SourceDir = OrbitalRingSourceDir,
            VersionRemote = "upstream",
            PullRemote = "origin",
            PullBranch = "main",
        });
        Add(result, new() {
            CalcName = "FractionateEverything",
            DisplayName = "万物分馏",
            SourceDir = FractionateEverythingSourceDir,
            VersionRemote = "origin",
            PullRemote = "origin",
            PullBranch = "master",
        });
        return result;
    }

    private static void Add(Dictionary<string, ModSourceConfig> configs, ModSourceConfig config) {
        configs[config.CalcName] = config;
    }
}
