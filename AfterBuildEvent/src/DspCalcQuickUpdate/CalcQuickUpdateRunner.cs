using System;
using System.Collections.Generic;
using System.Linq;
using AfterBuildEvent.DspCalcQuickUpdate.Mods;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal static class CalcQuickUpdateRunner {
    public static void Run(string[] args) {
        try {
            Dictionary<string, ModQuickUpdateSpec> specs = BuildSpecs();
            IReadOnlyList<ModQuickUpdateSpec> selectedSpecs = SelectSpecs(specs, args);
            if (selectedSpecs.Count == 0) {
                Console.WriteLine("没有可快速更新的模组。");
                return;
            }

            List<string> updatedMods = [];
            List<string> skippedMods = [];
            List<string> failedMods = [];
            foreach (ModQuickUpdateSpec spec in selectedSpecs) {
                QuickUpdateOutcome outcome = RunOne(spec);
                switch (outcome.Status) {
                    case QuickUpdateOutcomeStatus.Updated:
                        updatedMods.Add(outcome.Message);
                        break;
                    case QuickUpdateOutcomeStatus.Skipped:
                        skippedMods.Add(outcome.Message);
                        break;
                    case QuickUpdateOutcomeStatus.Failed:
                        failedMods.Add(outcome.Message);
                        break;
                }
            }

            PrintSummary(updatedMods, skippedMods, failedMods);
            Console.WriteLine("快速更新检查完成；未执行 git commit。");
        }
        finally {
            Console.WriteLine("按回车键结束模式 5。");
            Console.ReadLine();
        }
    }

    private static QuickUpdateOutcome RunOne(ModQuickUpdateSpec spec) {
        try {
            Console.WriteLine();
            Console.WriteLine($"开始快速更新计算器数据：{spec.DisplayName} / {spec.CalcName}");
            ModSourceGitSync gitSync = new();
            SourceAuditResult syncResult = gitSync.Sync(spec);
            PrintAuditResult(syncResult);
            if (!syncResult.CanQuickUpdate) {
                string message = $"{spec.CalcName}: 源码同步未通过";
                Console.WriteLine($"{message}。请人工处理后重试，或执行模式 3 完整导出。");
                return QuickUpdateOutcome.Failed(message);
            }

            string sourceVersion = spec.ReadSourceVersion(spec);
            DspCalcVersionUpdater versionUpdater = new();
            string currentVersion = versionUpdater.ReadCurrentVersion(spec.CalcName);
            spec.CurrentCalcVersion = currentVersion;
            spec.SourceVersion = sourceVersion;
            Console.WriteLine($"计算器当前版本：{currentVersion}");
            Console.WriteLine($"源码读取版本：{sourceVersion}");
            if (string.Equals(currentVersion, sourceVersion, StringComparison.OrdinalIgnoreCase)) {
                string message = $"{spec.CalcName}: 已是 {currentVersion}";
                Console.WriteLine("版本已一致，无需快速更新。");
                return QuickUpdateOutcome.Skipped(message);
            }

            SourceAuditResult auditResult = spec.RunAudit();
            PrintAuditResult(auditResult);
            if (!auditResult.CanQuickUpdate) {
                string message = $"{spec.CalcName}: {currentVersion} -> {sourceVersion} 审计未通过";
                Console.WriteLine($"{message}。请执行模式 3 完整导出。");
                return QuickUpdateOutcome.Failed(message);
            }

            RawJsonCopyResult copyResult = new RawJsonVersionCopier()
                .CopyVersionFiles(spec.CalcName, currentVersion, sourceVersion);
            bool gameDataChanged = versionUpdater.UpdateVersion(spec.CalcName, sourceVersion);
            Console.WriteLine(gameDataChanged
                ? $"已更新计算器 gameData.ts：{spec.CalcName} {currentVersion} -> {sourceVersion}"
                : "计算器 gameData.ts 版本无需修改。");
            PrintCopyResult(copyResult);
            return QuickUpdateOutcome.Updated($"{spec.CalcName}: {currentVersion} -> {sourceVersion}");
        }
        catch (Exception ex) {
            string message = $"{spec.CalcName}: {ex.Message}";
            Console.WriteLine($"快速更新失败：{message}");
            Console.WriteLine("请修复上述问题后重试，或执行模式 3 完整导出。");
            return QuickUpdateOutcome.Failed(message);
        }
    }

    private static Dictionary<string, ModQuickUpdateSpec> BuildSpecs() {
        IReadOnlyDictionary<string, ModSourceConfig> configs = ModSourceConfig.BuildAll();
        Dictionary<string, ModQuickUpdateSpec> specs = new(StringComparer.OrdinalIgnoreCase) {
            ["MoreMegaStructure"] = MoreMegaStructureQuickUpdate.Create(configs["MoreMegaStructure"]),
            ["TheyComeFromVoid"] = TheyComeFromVoidQuickUpdate.Create(configs["TheyComeFromVoid"]),
            ["GenesisBook"] = GenesisBookQuickUpdate.Create(configs["GenesisBook"]),
            ["OrbitalRing"] = OrbitalRingQuickUpdate.Create(configs["OrbitalRing"]),
            ["FractionateEverything"] = FractionateEverythingQuickUpdate.Create(configs["FractionateEverything"]),
        };
        return specs;
    }

    private static IReadOnlyList<ModQuickUpdateSpec> SelectSpecs(
        Dictionary<string, ModQuickUpdateSpec> specs,
        string[] args) {
        string input = args.Length >= 2 ? args[1].Trim() : "";
        if (string.IsNullOrWhiteSpace(input)) {
            return specs.Values.ToList();
        }
        if (specs.TryGetValue(input, out ModQuickUpdateSpec result)) {
            return [result];
        }

        ModQuickUpdateSpec displayMatch = specs.Values.FirstOrDefault(spec =>
            string.Equals(spec.DisplayName, input, StringComparison.OrdinalIgnoreCase));
        if (displayMatch != null) {
            return [displayMatch];
        }

        Console.WriteLine($"未知模组：{input}");
        return [];
    }

    private static void PrintAuditResult(SourceAuditResult result) {
        Console.WriteLine($"{result.Status}: {result.Message}");
        foreach (string detail in result.Details.Take(20)) {
            Console.WriteLine($"  {detail}");
        }
        if (result.Details.Count > 20) {
            Console.WriteLine($"  ... 还有 {result.Details.Count - 20} 行");
        }
    }

    private static void PrintCopyResult(RawJsonCopyResult result) {
        Console.WriteLine($"新增 raw JSON：{result.CreatedFiles.Count}");
        foreach (string file in result.CreatedFiles) {
            Console.WriteLine($"  + {file}");
        }
        Console.WriteLine($"已存在且相同 raw JSON：{result.SkippedSameFiles.Count}");
        foreach (string file in result.SkippedSameFiles) {
            Console.WriteLine($"  = {file}");
        }
        Console.WriteLine($"保留旧 raw JSON：{result.KeptOldFiles.Count}");
        foreach (string file in result.KeptOldFiles) {
            Console.WriteLine($"  old {file}");
        }
    }

    private static void PrintSummary(
        List<string> updatedMods,
        List<string> skippedMods,
        List<string> failedMods) {
        Console.WriteLine();
        Console.WriteLine("模式 5 汇总：");
        Console.WriteLine($"已更新：{updatedMods.Count}");
        foreach (string item in updatedMods) {
            Console.WriteLine($"  + {item}");
        }
        Console.WriteLine($"无需更新：{skippedMods.Count}");
        foreach (string item in skippedMods) {
            Console.WriteLine($"  = {item}");
        }
        Console.WriteLine($"失败/需兜底：{failedMods.Count}");
        foreach (string item in failedMods) {
            Console.WriteLine($"  ! {item}");
        }
    }
}

internal enum QuickUpdateOutcomeStatus {
    Updated,
    Skipped,
    Failed,
}

internal sealed class QuickUpdateOutcome {
    public QuickUpdateOutcomeStatus Status { get; }
    public string Message { get; }

    private QuickUpdateOutcome(QuickUpdateOutcomeStatus status, string message) {
        Status = status;
        Message = message;
    }

    public static QuickUpdateOutcome Updated(string message) {
        return new(QuickUpdateOutcomeStatus.Updated, message);
    }

    public static QuickUpdateOutcome Skipped(string message) {
        return new(QuickUpdateOutcomeStatus.Skipped, message);
    }

    public static QuickUpdateOutcome Failed(string message) {
        return new(QuickUpdateOutcomeStatus.Failed, message);
    }
}
