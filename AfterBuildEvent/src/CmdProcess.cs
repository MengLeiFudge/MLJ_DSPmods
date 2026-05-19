using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AfterBuildEvent;

public class CmdProcess : IDisposable {
    private readonly Process process;
    private readonly StreamWriter input;

    public CmdProcess() {
        process = new();
        process.StartInfo = new() {
            FileName = "cmd.exe",
            //关闭Shell的使用，这样才能重定向输出
            UseShellExecute = false,
            //重新定向标准输入，标准输出，错误输出
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            //设置cmd窗口不显示
            CreateNoWindow = true,
        };
        process.OutputDataReceived += (_, e) => {
            if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"[cmd]{e.Data}");
        };
        process.ErrorDataReceived += (_, e) => {
            if (!string.IsNullOrEmpty(e.Data)) Console.Error.WriteLine($"[err]{e.Data}");
        };
        process.Start();
        input = process.StandardInput;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Thread.Sleep(0);
    }

    public void Exec(string command) {
        input.WriteLine(command);
        Thread.Sleep(0);
    }

    /// <summary>
    /// 同步执行命令并等待结束
    /// </summary>
    public int Run(string fileName, string arguments, string workingDir = "") {
        ProcessStartInfo startInfo = new() {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using Process p = new() { StartInfo = startInfo };
        p.OutputDataReceived += (_, e) => {
            if (e.Data != null) Console.WriteLine($"[cmd] {e.Data}");
        };
        p.ErrorDataReceived += (_, e) => {
            if (e.Data != null) Console.Error.WriteLine($"[err] {e.Data}");
        };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }

    public void Dispose() {
        input.WriteLine("exit");
        process.WaitForExit();
        process.Dispose();
    }
}
