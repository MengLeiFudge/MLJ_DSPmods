using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal sealed class ModSourceGitSync {
    private static readonly string[] GeneratedPathFragments = [
        "/obj/",
        "/bin/",
        "/.vs/",
        "AssemblyReference.cache",
        "Assembly-CSharp-publicized.dll",
    ];

    public SourceAuditResult Sync(ModQuickUpdateSpec spec) {
        if (!Directory.Exists(spec.SourceDir)) {
            return SourceAuditResult.ConfigError($"未找到 {spec.DisplayName} 源码目录：{spec.SourceDir}");
        }
        if (!Directory.Exists(Path.Combine(spec.SourceDir, ".git"))) {
            return SourceAuditResult.ConfigError($"{spec.DisplayName} 源码目录不是 git 仓库：{spec.SourceDir}");
        }

        CommandResult status = RunGit(spec.SourceDir, "status --short");
        if (!status.Success) {
            return SourceAuditResult.ConfigError($"读取 {spec.DisplayName} git 状态失败", status.OutputLines);
        }
        if (!string.IsNullOrWhiteSpace(status.Output)) {
            SourceAuditResult dirtyResult = InspectDirtyWorktree(spec, status.OutputLines);
            if (!dirtyResult.CanQuickUpdate) {
                return dirtyResult;
            }
            CommandResult restore = RunGit(spec.SourceDir, "restore .");
            if (!restore.Success) {
                return SourceAuditResult.ConfigError($"清理 {spec.DisplayName} 无用本地改动失败", restore.OutputLines);
            }
            Console.WriteLine($"已清理 {spec.DisplayName} 的无用本地改动。");
        }

        CommandResult fetch = RunGit(spec.SourceDir, "fetch --all --prune");
        if (!fetch.Success) {
            return SourceAuditResult.ConfigError($"同步 {spec.DisplayName} 远端引用失败", fetch.OutputLines);
        }

        CommandResult pull = RunGit(spec.SourceDir, "pull --ff-only");
        if (!pull.Success) {
            return SourceAuditResult.Uncertain($"{spec.DisplayName} 无法 fast-forward 拉取，请人工处理源码仓库", pull.OutputLines);
        }

        List<string> details = [];
        details.AddRange(fetch.OutputLines);
        details.AddRange(pull.OutputLines);
        return SourceAuditResult.Passed($"{spec.DisplayName} 源码同步完成", details);
    }

    public CommandResult RunGit(string workingDir, string arguments) {
        ProcessStartInfo startInfo = new() {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using Process process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, JoinOutput(output, error));
    }

    private SourceAuditResult InspectDirtyWorktree(ModQuickUpdateSpec spec, IReadOnlyList<string> statusLines) {
        CommandResult normalizedDiff = RunGit(spec.SourceDir, "diff --ignore-cr-at-eol --stat");
        if (!normalizedDiff.Success) {
            return SourceAuditResult.Uncertain($"无法判断 {spec.DisplayName} 本地改动类型", normalizedDiff.OutputLines);
        }

        List<string> meaningfulLines = normalizedDiff.OutputLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (meaningfulLines.Count == 0) {
            return SourceAuditResult.Passed($"{spec.DisplayName} 只有换行符改动，可自动清理");
        }

        bool onlyGeneratedFiles = statusLines
            .Select(line => line.Length > 3 ? line.Substring(3).Replace('\\', '/') : line.Replace('\\', '/'))
            .All(IsGeneratedFile);
        if (onlyGeneratedFiles) {
            return SourceAuditResult.Passed($"{spec.DisplayName} 只有生成物改动，可自动清理", statusLines);
        }

        return SourceAuditResult.Uncertain(
            $"{spec.DisplayName} 存在需要人工确认的本地改动，停止快速更新",
            statusLines);
    }

    private static bool IsGeneratedFile(string path) {
        return GeneratedPathFragments.Any(fragment =>
            path.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string JoinOutput(string output, string error) {
        if (string.IsNullOrWhiteSpace(error)) {
            return output ?? "";
        }
        if (string.IsNullOrWhiteSpace(output)) {
            return error;
        }
        return output.TrimEnd() + Environment.NewLine + error;
    }
}

internal sealed class CommandResult {
    public int ExitCode { get; }
    public string Output { get; }
    public List<string> OutputLines { get; }
    public bool Success => ExitCode == 0;

    public CommandResult(int exitCode, string output) {
        ExitCode = exitCode;
        Output = output ?? "";
        OutputLines = Output.Split([Environment.NewLine], StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}
