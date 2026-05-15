using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal sealed class ModSourceGitSync {
    private const int NetworkGitTimeoutMs = 5000;
    private static bool skipNetworkSyncThisRun;
    private static readonly string[] GeneratedPathFragments = [
        "/obj/",
        "/bin/",
        "/.vs/",
        "AssemblyReference.cache",
        "Assembly-CSharp-publicized.dll",
    ];

    public SourceAuditResult Sync(ModQuickUpdateSpec spec) {
        string gitDir = spec.GetGitDir();
        if (!Directory.Exists(spec.SourceDir)) {
            return SourceAuditResult.ConfigError($"未找到 {spec.DisplayName} 源码目录：{spec.SourceDir}");
        }
        if (!Directory.Exists(Path.Combine(gitDir, ".git"))) {
            return SourceAuditResult.ConfigError($"{spec.DisplayName} Git 目录不是 git 仓库：{gitDir}");
        }

        CommandResult status = RunGit(gitDir, $"status --short -- {QuoteGitPath(spec.GetWorktreeScope())}");
        if (!status.Success) {
            return SourceAuditResult.ConfigError($"读取 {spec.DisplayName} git 状态失败", status.OutputLines);
        }
        if (!string.IsNullOrWhiteSpace(status.Output)) {
            SourceAuditResult dirtyResult = InspectDirtyWorktree(spec, status.OutputLines);
            if (!dirtyResult.CanQuickUpdate) {
                return dirtyResult;
            }
            CommandResult restore = RunGit(gitDir, $"restore -- {QuoteGitPath(spec.GetWorktreeScope())}");
            if (!restore.Success) {
                return SourceAuditResult.ConfigError($"清理 {spec.DisplayName} 无用本地改动失败", restore.OutputLines);
            }
            Console.WriteLine($"已清理 {spec.DisplayName} 的无用本地改动。");
        }

        if (skipNetworkSyncThisRun) {
            return SourceAuditResult.Passed($"{spec.DisplayName} 已跳过远端同步，沿用本轮本地源码检查");
        }

        CommandResult fetch = RunGit(gitDir, $"fetch {spec.PullRemote} --prune", NetworkGitTimeoutMs);
        if (!fetch.Success) {
            skipNetworkSyncThisRun = true;
            Console.WriteLine($"同步 {spec.DisplayName} 远端引用失败，改用本地源码继续检查。");
            foreach (string line in fetch.OutputLines.Take(8)) {
                Console.WriteLine($"  {line}");
            }
            return SourceAuditResult.Passed($"{spec.DisplayName} 远端同步失败，已降级为本地源码检查", fetch.OutputLines);
        }

        CommandResult pull = RunGit(gitDir, "pull --ff-only", NetworkGitTimeoutMs);
        if (!pull.Success) {
            return SourceAuditResult.Uncertain($"{spec.DisplayName} 无法 fast-forward 拉取，请人工处理源码仓库", pull.OutputLines);
        }

        List<string> details = [];
        details.AddRange(fetch.OutputLines);
        details.AddRange(pull.OutputLines);
        return SourceAuditResult.Passed($"{spec.DisplayName} 源码同步完成", details);
    }

    public CommandResult RunGit(string workingDir, string arguments) {
        return RunGit(workingDir, arguments, 0);
    }

    public CommandResult RunGit(string workingDir, string arguments, int timeoutMs) {
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
        string output = "";
        string error = "";
        if (timeoutMs > 0) {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs)) {
                KillProcessTree(process.Id);
                return new(124, $"git {arguments} 超过 {timeoutMs}ms，已停止等待");
            }

            output = outputTask.Result;
            error = errorTask.Result;
            return new(process.ExitCode, JoinOutput(output, error));
        }

        output = process.StandardOutput.ReadToEnd();
        error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, JoinOutput(output, error));
    }

    private SourceAuditResult InspectDirtyWorktree(ModQuickUpdateSpec spec, IReadOnlyList<string> statusLines) {
        CommandResult normalizedDiff = RunGit(
            spec.GetGitDir(),
            $"diff --ignore-cr-at-eol --stat -- {QuoteGitPath(spec.GetWorktreeScope())}");
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

    private static string QuoteGitPath(string path) {
        return "\"" + path.Replace("\"", "\\\"") + "\"";
    }

    private static void KillProcessTree(int processId) {
        try {
            ProcessStartInfo startInfo = new() {
                FileName = "taskkill.exe",
                Arguments = $"/PID {processId} /T /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using Process taskkill = Process.Start(startInfo);
            taskkill.WaitForExit(2000);
        }
        catch (Exception) {
            // 超时清理是快速更新的保护逻辑，清理失败时仍然降级到本地源码检查。
        }
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
        OutputLines = Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}
