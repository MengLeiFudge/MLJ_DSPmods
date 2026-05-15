using System;
using System.Collections.Generic;
using System.Linq;
using AfterBuildEvent.DspCalcQuickUpdate.Mods;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal static class CalcQuickUpdateRunner {
    public static void Run(string[] args) {
        try {
            Dictionary<string, ModQuickUpdateSpec> specs = BuildSpecs();
            ModQuickUpdateSpec spec = SelectSpec(specs, args);
            if (spec == null) {
                return;
            }

            Console.WriteLine($"开始快速更新计算器数据：{spec.DisplayName} / {spec.CalcName}");
            ModSourceGitSync gitSync = new();
            SourceAuditResult syncResult = gitSync.Sync(spec);
            PrintAuditResult(syncResult);
            if (!syncResult.CanQuickUpdate) {
                Console.WriteLine("源码同步未通过，停止快速更新。请人工处理后重试，或执行模式 3 完整导出。");
                return;
            }

            string sourceVersion = spec.ReadSourceVersion(spec);
            DspCalcVersionUpdater versionUpdater = new();
            string currentVersion = versionUpdater.ReadCurrentVersion(spec.CalcName);
            spec.CurrentCalcVersion = currentVersion;
            spec.SourceVersion = sourceVersion;
            Console.WriteLine($"计算器当前版本：{currentVersion}");
            Console.WriteLine($"源码读取版本：{sourceVersion}");
            if (string.Equals(currentVersion, sourceVersion, StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine("版本已一致，无需快速更新。");
                return;
            }

            SourceAuditResult auditResult = spec.RunAudit();
            PrintAuditResult(auditResult);
            if (!auditResult.CanQuickUpdate) {
                Console.WriteLine("源码审计未通过，停止快速更新。请执行模式 3 完整导出。");
                return;
            }

            RawJsonCopyResult copyResult = new RawJsonVersionCopier()
                .CopyVersionFiles(spec.CalcName, currentVersion, sourceVersion);
            bool gameDataChanged = versionUpdater.UpdateVersion(spec.CalcName, sourceVersion);
            Console.WriteLine(gameDataChanged
                ? $"已更新计算器 gameData.ts：{spec.CalcName} {currentVersion} -> {sourceVersion}"
                : "计算器 gameData.ts 版本无需修改。");
            PrintCopyResult(copyResult);
            Console.WriteLine("快速更新完成；未执行 git commit。");
        }
        catch (Exception ex) {
            Console.WriteLine($"快速更新失败：{ex.Message}");
            Console.WriteLine("请修复上述问题后重试，或执行模式 3 完整导出。");
        }
        finally {
            Console.WriteLine("按回车键结束模式 5。");
            Console.ReadLine();
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

    private static ModQuickUpdateSpec SelectSpec(Dictionary<string, ModQuickUpdateSpec> specs, string[] args) {
        string input = args.Length >= 2 ? args[1].Trim() : "";
        if (string.IsNullOrWhiteSpace(input)) {
            Console.WriteLine("可快速更新的模组：");
            foreach (ModQuickUpdateSpec spec in specs.Values) {
                Console.WriteLine($"- {spec.CalcName}（{spec.DisplayName}）");
            }
            Console.WriteLine("请输入模组名：");
            input = Console.ReadLine()?.Trim() ?? "";
        }
        if (string.IsNullOrWhiteSpace(input)) {
            Console.WriteLine("未输入模组名，取消快速更新。");
            return null;
        }
        if (specs.TryGetValue(input, out ModQuickUpdateSpec result)) {
            return result;
        }

        ModQuickUpdateSpec displayMatch = specs.Values.FirstOrDefault(spec =>
            string.Equals(spec.DisplayName, input, StringComparison.OrdinalIgnoreCase));
        if (displayMatch != null) {
            return displayMatch;
        }

        Console.WriteLine($"未知模组：{input}");
        return null;
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
}
