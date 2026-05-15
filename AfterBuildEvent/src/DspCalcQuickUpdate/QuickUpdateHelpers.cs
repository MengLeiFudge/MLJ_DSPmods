using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json.Linq;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal static class QuickUpdateHelpers {
    private static readonly Regex VersionConstRegex =
        new(@"public\s+const\s+string\s+VERSION\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex VersionStringRegex =
        new(@"versionString\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex MegaStructureTagRegex =
        new(@"^MMSv(?<version>\d+\.\d+\.\d+)[A-Za-z]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ReadVersionConst(string sourceFile) {
        string content = File.ReadAllText(sourceFile);
        Match match = VersionConstRegex.Match(content);
        if (!match.Success) {
            throw new InvalidOperationException($"未找到 VERSION 常量：{sourceFile}");
        }
        return match.Groups[1].Value;
    }

    public static string ReadTheyComeFromVoidVersion(string sourceFile) {
        string content = File.ReadAllText(sourceFile);
        Match match = VersionStringRegex.Match(content);
        if (!match.Success) {
            throw new InvalidOperationException($"未找到 versionString：{sourceFile}");
        }
        return match.Groups[1].Value;
    }

    public static string ReadProjectVersion(string csprojPath) {
        XmlDocument document = new();
        document.Load(csprojPath);
        string value = document.SelectSingleNode("/Project/PropertyGroup/Version")?.InnerText;
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException($"未找到项目 Version：{csprojPath}");
        }
        return value.Trim();
    }

    public static string ReadManifestVersion(string manifestPath) {
        JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
        string value = manifest.Value<string>("version_number");
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException($"未找到 manifest version_number：{manifestPath}");
        }
        return value.Trim();
    }

    public static string ReadMoreMegaStructureVersionFromTags(ModQuickUpdateSpec spec) {
        ModSourceGitSync git = new();
        CommandResult result = git.RunGit(spec.SourceDir, "tag --sort=-creatordate");
        if (!result.Success) {
            throw new InvalidOperationException($"读取巨构 tag 失败：{result.Output}");
        }
        foreach (string tag in result.OutputLines) {
            Match match = MegaStructureTagRegex.Match(tag.Trim());
            if (match.Success) {
                return match.Groups["version"].Value;
            }
        }
        throw new InvalidOperationException("未找到可解析的巨构版本 tag，例如 MMSv1.9.0a");
    }

    public static SourceAuditResult ConservativeAudit(ModQuickUpdateSpec spec) {
        ModSourceGitSync git = new();
        if (string.IsNullOrWhiteSpace(spec.CurrentCalcVersion) || string.IsNullOrWhiteSpace(spec.SourceVersion)) {
            return SourceAuditResult.Uncertain($"{spec.DisplayName} 缺少版本上下文，无法审计源码变化");
        }

        if (spec.AuditPathPrefixes.Count == 0) {
            return SourceAuditResult.Uncertain($"{spec.DisplayName} 未配置审计关注路径");
        }

        string oldRevision = FindRevisionForVersion(spec, spec.CurrentCalcVersion);
        string newRevision = FindRevisionForVersion(spec, spec.SourceVersion);
        if (string.IsNullOrWhiteSpace(oldRevision)) {
            return SourceAuditResult.Uncertain(
                $"{spec.DisplayName} 无法定位当前计算器版本 {spec.CurrentCalcVersion} 对应的源码基线");
        }
        if (string.IsNullOrWhiteSpace(newRevision)) {
            return SourceAuditResult.Uncertain(
                $"{spec.DisplayName} 无法定位源码版本 {spec.SourceVersion} 对应的源码基线");
        }

        if (oldRevision == newRevision) {
            return SourceAuditResult.Passed($"{spec.DisplayName} 新旧版本指向同一源码基线：{oldRevision}");
        }

        string pathspec = string.Join(" ", spec.AuditPathPrefixes.Select(QuoteGitPath));
        CommandResult diffResult = git.RunGit(spec.SourceDir, $"diff --name-only {oldRevision}..{newRevision} -- {pathspec}");
        if (!diffResult.Success) {
            return SourceAuditResult.Uncertain($"{spec.DisplayName} 无法比较版本源码差异", diffResult.OutputLines);
        }
        if (diffResult.OutputLines.Count > 0) {
            return SourceAuditResult.Changed(
                $"{spec.DisplayName} 从 {spec.CurrentCalcVersion} 到 {spec.SourceVersion} 修改了计算器关注路径",
                diffResult.OutputLines);
        }

        return SourceAuditResult.Passed(
            $"{spec.DisplayName} 从 {spec.CurrentCalcVersion} 到 {spec.SourceVersion} 未修改计算器关注路径",
            [$"{oldRevision}..{newRevision}"]);
    }

    private static string FindRevisionForVersion(ModQuickUpdateSpec spec, string version) {
        ModSourceGitSync git = new();
        foreach (string candidate in BuildVersionRevisionCandidates(spec, version)) {
            CommandResult result = git.RunGit(spec.SourceDir, $"rev-parse --verify {candidate}^{{commit}}");
            if (result.Success && result.OutputLines.Count > 0) {
                return result.OutputLines[0].Trim();
            }
        }
        CommandResult grep = git.RunGit(spec.SourceDir, $"log --all --grep={QuoteGitArgument("v" + version)} --format=%H -1");
        if (grep.Success && grep.OutputLines.Count > 0) {
            return grep.OutputLines[0].Trim();
        }
        return "";
    }

    private static string[] BuildVersionRevisionCandidates(ModQuickUpdateSpec spec, string version) {
        if (spec.CalcName == "MoreMegaStructure") {
            return [
                $"MMSv{version}",
                $"MMSv{version}a",
                $"refs/tags/MMSv{version}",
                $"refs/tags/MMSv{version}a",
            ];
        }
        return [
            $"v{version}",
            $"refs/tags/v{version}",
        ];
    }

    private static string QuoteGitPath(string path) {
        return "\"" + path.Replace("\"", "\\\"") + "\"";
    }

    private static string QuoteGitArgument(string value) {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
