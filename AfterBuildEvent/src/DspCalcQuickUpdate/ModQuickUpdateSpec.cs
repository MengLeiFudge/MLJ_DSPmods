using System;
using System.Collections.Generic;
using System.IO;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal sealed class ModQuickUpdateSpec {
    public ModSourceConfig SourceConfig { get; set; }
    public IReadOnlyList<string> AuditPathPrefixes { get; set; } = [];
    public Func<ModQuickUpdateSpec, string> ReadSourceVersion { get; set; }
    public Func<ModQuickUpdateSpec, SourceAuditResult> AuditSource { get; set; }
    public string CalcName => SourceConfig.CalcName;
    public string DisplayName => SourceConfig.DisplayName;
    public string SourceDir => SourceConfig.SourceDir;
    public string GitDir { get; set; } = "";
    public string WorktreeScope { get; set; } = "";
    public string VersionRemote => SourceConfig.VersionRemote;
    public string PullRemote => SourceConfig.PullRemote;
    public string PullBranch => SourceConfig.PullBranch;
    public string CurrentCalcVersion { get; set; } = "";
    public string SourceVersion { get; set; } = "";

    public string RequireFile(params string[] relativeParts) {
        string path = Path.Combine(SourceDir, Path.Combine(relativeParts));
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"未找到 {DisplayName} 源码文件：{path}", path);
        }
        return path;
    }

    public string GetGitDir() {
        return string.IsNullOrWhiteSpace(GitDir) ? SourceDir : GitDir;
    }

    public string GetWorktreeScope() {
        return string.IsNullOrWhiteSpace(WorktreeScope) ? "." : WorktreeScope;
    }

    public SourceAuditResult RunAudit() {
        return AuditSource?.Invoke(this) ?? SourceAuditResult.Uncertain($"{DisplayName} 未配置源码审计规则");
    }
}
