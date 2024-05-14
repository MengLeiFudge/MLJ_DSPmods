using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AfterBuildEvent {
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
            process.OutputDataReceived += (s, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (s, e) => Console.Error.WriteLine(e.Data);
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

        public void Dispose() {
            input.WriteLine("exit");
            process.WaitForExit();
            process.Dispose();
        }
    }
}
