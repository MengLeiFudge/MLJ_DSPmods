using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using static AfterBuildEvent.PathConfig;
using static AfterBuildEvent.Utils;

namespace AfterBuildEvent;

static partial class AfterBuildEvent {
    private sealed class ExternalModReferenceTarget {
        public string PropertyName { get; set; } = "";
        public string ModName { get; set; } = "";
        public string[] RelativeDllPaths { get; set; } = [];
        public string[] Keywords { get; set; } = [];
    }

    private sealed class ExternalModReferenceResult {
        public string PropertyName { get; set; } = "";
        public string ModName { get; set; } = "";
        public string PackageName { get; set; } = "";
        public string Version { get; set; } = "";
        public string HintPath { get; set; } = "";
    }

    private static readonly ExternalModReferenceTarget[] ExternalModReferenceTargets = [
        new() {
            PropertyName = "BuildBarToolHintPath",
            ModName = "BuildBarTool",
            RelativeDllPaths = ["BuildBarTool.dll", @"plugins\BuildBarTool.dll"],
            Keywords = ["BuildBarTool"],
        },
        new() {
            PropertyName = "TheyComeFromVoidHintPath",
            ModName = "TheyComeFromVoid",
            RelativeDllPaths = ["DSP_Battle.dll", @"plugins\DSP_Battle.dll"],
            Keywords = ["TheyComeFromVoid", "DSP_Battle"],
        },
        new() {
            PropertyName = "GenesisBookHintPath",
            ModName = "GenesisBook",
            RelativeDllPaths = [@"plugins\ProjectGenesis.dll", "ProjectGenesis.dll"],
            Keywords = ["GenesisBook", "ProjectGenesis"],
        },
    ];

    private static void GenerateExternalModReferencesProps() {
        if (!TryGenerateExternalModReferencesProps(out List<ExternalModReferenceResult> results)) {
            return;
        }

        Console.WriteLine($"已生成外部模组引用配置：{ExternalModReferencesGeneratedPropsPath}");
        foreach (ExternalModReferenceResult result in results) {
            Console.WriteLine($"{result.ModName} {result.Version}: {result.HintPath}");
        }
    }

    private static bool TryGenerateExternalModReferencesProps(out List<ExternalModReferenceResult> results) {
        results = [];
        LoadModInfos();
        IReadOnlyList<ModInfo> installedMods = GetAllModInfos();
        if (installedMods.Count == 0) {
            Console.WriteLine("mods.yml 中没有可用模组信息，无法生成外部模组引用配置。");
            return false;
        }

        List<string> errors = [];
        foreach (ExternalModReferenceTarget target in ExternalModReferenceTargets) {
            ModInfo modInfo = FindBestMatchingMod(installedMods, target.Keywords);
            if (modInfo == null) {
                errors.Add($"mods.yml 中未找到 {target.ModName}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(modInfo.version)) {
                errors.Add($"mods.yml 中 {modInfo.name} 缺少 versionNumber");
                continue;
            }

            string packageDir = Path.Combine(R2CacheDir, modInfo.name, modInfo.version);
            if (!Directory.Exists(packageDir)) {
                errors.Add($"R2 cache 中未找到 {modInfo.name} {modInfo.version}：{packageDir}");
                continue;
            }

            string hintPath = ResolveCacheDllPath(packageDir, target, modInfo);
            if (string.IsNullOrWhiteSpace(hintPath)) {
                errors.Add($"R2 cache 中未找到 {modInfo.name} {modInfo.version} 的主 DLL：{packageDir}");
                continue;
            }

            results.Add(new() {
                PropertyName = target.PropertyName,
                ModName = target.ModName,
                PackageName = modInfo.name,
                Version = modInfo.version,
                HintPath = hintPath,
            });
        }

        if (errors.Count > 0) {
            foreach (string error in errors) {
                Console.WriteLine(error);
            }
            return false;
        }

        WriteExternalModReferencesProps(results);
        return true;
    }

    private static string ResolveCacheDllPath(
        string packageDir,
        ExternalModReferenceTarget target,
        ModInfo modInfo) {
        foreach (string relativePath in target.RelativeDllPaths) {
            string candidate = Path.Combine(packageDir, relativePath);
            if (File.Exists(candidate)) {
                return candidate;
            }
        }

        string[] dllCandidates = Directory.GetFiles(packageDir, "*.dll", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredCompanionDll(Path.GetFileName(path)))
            .ToArray();
        if (dllCandidates.Length == 0) {
            return "";
        }
        if (dllCandidates.Length == 1) {
            return dllCandidates[0];
        }

        return dllCandidates
            .Select(path => new {
                Path = path,
                Score = ScoreAssemblyCandidate(path, modInfo, target.Keywords),
            })
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault(candidate => candidate.Score > 0)
            ?.Path ?? "";
    }

    private static void WriteExternalModReferencesProps(IReadOnlyList<ExternalModReferenceResult> results) {
        StringBuilder builder = new();
        builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        builder.AppendLine("<!-- Auto-generated by AfterBuildEvent.exe 6. Do not commit this file. -->");
        builder.AppendLine("<Project>");
        builder.AppendLine(@"    <PropertyGroup Label=""ExternalModReferences"">");
        foreach (ExternalModReferenceResult result in results) {
            builder.AppendLine(
                $"        <{result.PropertyName}>{EscapeXml(result.HintPath)}</{result.PropertyName}>");
            builder.AppendLine(
                $"        <{result.PropertyName}Package>{EscapeXml(result.PackageName)}</{result.PropertyName}Package>");
            builder.AppendLine(
                $"        <{result.PropertyName}Version>{EscapeXml(result.Version)}</{result.PropertyName}Version>");
        }
        builder.AppendLine("    </PropertyGroup>");
        builder.AppendLine("</Project>");

        File.WriteAllText(ExternalModReferencesGeneratedPropsPath, builder.ToString(), new UTF8Encoding(false));
    }

    private static string EscapeXml(string value) {
        return SecurityElement.Escape(value) ?? "";
    }
}
