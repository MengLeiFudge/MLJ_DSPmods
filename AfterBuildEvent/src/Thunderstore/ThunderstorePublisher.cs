using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AfterBuildEvent.Thunderstore;

/// <summary>独立商店发布入口；准备阶段不读取密钥，上传必须指定包、版本和已批准 ZIP 的哈希。</summary>
internal static class ThunderstorePublisher {
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private const string Usage = "用法：7 prepare <项目>；用户明确批准后：7 publish <项目> <版本> --approve <ZIP SHA256>";

    /// <summary>模式 7 不调用模式 1，也不关闭游戏、替换本地模组或通知 QQ。</summary>
    public static void Run(string[] args, string configuration) {
        try {
            bool prepare = args.Length == 3 && args[1] == "prepare";
            bool publish = args.Length == 6 && args[1] == "publish" && args[4] == "--approve"
                && Regex.IsMatch(args[5], "^[a-fA-F0-9]{64}$");
            if (!prepare && !publish) {
                throw new ArgumentException(Usage);
            }
            using ThunderstorePackage package = ThunderstorePackage.Open(args[2], configuration);
            if (publish && (args[3] != package.Version
                || !args[5].Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))) {
                throw new InvalidOperationException("批准的版本或 SHA256 与当前 ZIP 不一致，请重新准备并取得批准。");
            }
            string directory = Path.Combine(PathConfig.SolutionFullDir, ".codex", "publish", "thunderstore",
                package.Name, package.Version, package.Sha256);
            string planPath = Path.Combine(directory, "plan.json");
            string configPath = Path.Combine(directory, "thunderstore.toml");
            if (publish && !File.Exists(planPath)) {
                throw new InvalidOperationException("请先使用 prepare 生成发布清单，再取得用户明确批准。");
            }
            JObject remote = ThunderstoreApi.GetPackage(package.Name);
            var catalog = ThunderstoreApi.GetCategoryCatalog();
            string[] categories = ThunderstoreApi.ResolveCategories(package.Name, remote, catalog);
            JObject existingVersion = ThunderstoreApi.GetVersion(package.Name, package.Version);
            if (existingVersion != null) {
                throw new InvalidOperationException($"MengLei-{package.Name}-{package.Version} 已存在，禁止重复上传。");
            }
            if (prepare) {
                Directory.CreateDirectory(directory);
                WriteConfig(configPath, package, categories);
                JObject plan = new() {
                    ["prepared_at_utc"] = DateTime.UtcNow.ToString("O"),
                    ["namespace"] = "MengLei", ["name"] = package.Name, ["version"] = package.Version,
                    ["configuration"] = configuration, ["zip"] = package.ZipPath, ["sha256"] = package.Sha256,
                    ["community"] = ThunderstoreApi.Community, ["categories"] = new JArray(categories),
                    ["latest_remote_version"] = remote?["latest"]?["version_number"],
                    ["requires_explicit_user_approval"] = true,
                };
                File.WriteAllText(planPath, plan.ToString(Formatting.Indented), Utf8);
                Console.WriteLine($"发布准备完成：MengLei-{package.Name}-{package.Version}");
                Console.WriteLine($"分类：{string.Join(", ", categories)}");
                Console.WriteLine($"ZIP：{package.ZipPath}\nSHA256：{package.Sha256}\n清单：{planPath}");
                Console.WriteLine("尚未上传；仅在用户明确批准上述包、版本和文件后执行：");
                Console.WriteLine($"AfterBuildEvent.exe 7 publish {package.Name} {package.Version} --approve {package.Sha256}");
                return;
            }
            JObject approvedPlan = JObject.Parse(File.ReadAllText(planPath));
            if ((string)approvedPlan["name"] != package.Name || (string)approvedPlan["version"] != package.Version
                || (string)approvedPlan["sha256"] != package.Sha256
                || (approvedPlan["categories"] as JArray)?.Values<string>().SequenceEqual(categories) != true) {
                throw new InvalidOperationException("发布清单或远端分类已变化，请重新准备并取得批准。");
            }
            // 不信任可被手工编辑的 TOML；在已核对清单和哈希后重新生成固定目标的配置。
            WriteConfig(configPath, package, categories);
            string cliPath = Path.Combine(PathConfig.SolutionFullDir, ".codex", "tools", "tcli", "tcli.exe");
            if (!File.Exists(cliPath)) {
                throw new FileNotFoundException("缺少 tcli 0.2.4；在仓库根目录执行："
                    + "dotnet tool install tcli --version 0.2.4 --tool-path .codex/tools/tcli", cliPath);
            }
            string token = Environment.GetEnvironmentVariable("THUNDERSTORE_API_TOKEN", EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(token)) {
                token = Environment.GetEnvironmentVariable("THUNDERSTORE_API_TOKEN", EnvironmentVariableTarget.Machine);
            }
            if (string.IsNullOrWhiteSpace(token)) {
                throw new InvalidOperationException("Windows User/Machine 环境变量缺少 THUNDERSTORE_API_TOKEN。");
            }
            JObject receipt = new() {
                ["started_at_utc"] = DateTime.UtcNow.ToString("O"),
                ["name"] = package.Name, ["version"] = package.Version, ["sha256"] = package.Sha256,
                ["categories"] = new JArray(categories), ["cli"] = RunCli(cliPath, configPath, package.ZipPath, token),
            };
            bool confirmed = false;
            try {
                JObject version = ThunderstoreApi.GetVersion(package.Name, package.Version);
                JObject current = ThunderstoreApi.GetPackage(package.Name);
                receipt["remote_version"] = version;
                receipt["remote_package"] = current;
                confirmed = ThunderstoreApi.IsPublished(package, version, current, categories, catalog);
            } catch (Exception ex) {
                receipt["remote_query_error"] = ex.Message;
            }
            receipt["remote_confirmed"] = confirmed;
            string receiptPath = Path.Combine(directory, $"receipt-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.WriteAllText(receiptPath, receipt.ToString(Formatting.Indented), Utf8);
            Console.WriteLine($"CLI 退出码：{receipt["cli"]?["exit_code"]}；上传回执：{receiptPath}");
            if (!confirmed) {
                throw new InvalidOperationException("发布未确认完成。已查询远端并保留 CLI 结果；"
                    + "不要直接重传或递增版本，请先核对回执及远端状态。");
            }
            Console.WriteLine($"Thunderstore 已确认 {package.Name} {package.Version} 激活且社区、分类一致。");
            Console.WriteLine($"社区审核状态：{ThunderstoreApi.GetListing((JObject)receipt["remote_package"])?["review_status"]}");
        } catch (Exception ex) {
            Console.Error.WriteLine("Thunderstore 流程失败：" + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>生成 --file 所需的 TOML；不包含密钥，也不混用 --package-name。</summary>
    private static void WriteConfig(string path, ThunderstorePackage package, string[] categories) {
        string categoryList = string.Join(", ", categories.Select(category => JsonConvert.SerializeObject(category)));
        string config = $"[config]\nschemaVersion = \"0.0.1\"\n"
            + $"[package]\nnamespace = \"MengLei\"\nname = \"{package.Name}\"\nversionNumber = \"{package.Version}\"\n"
            + "[build]\noutdir = \".\"\n[publish]\nrepository = \"https://thunderstore.io\"\n"
            + $"communities = [\"{ThunderstoreApi.Community}\"]\n[publish.categories]\n"
            + $"{ThunderstoreApi.Community} = [{categoryList}]\n";
        File.WriteAllText(path, config, Utf8);
    }

    /// <summary>认证只注入子进程；五分钟超时结束进程树，输出脱敏后进入回执，不自动重试。</summary>
    private static JObject RunCli(string cliPath, string configPath, string zipPath, string token) {
        JObject result = new();
        try {
            ProcessStartInfo start = new(cliPath,
                $"publish --config-path \"{configPath}\" --file \"{zipPath}\"") {
                WorkingDirectory = PathConfig.SolutionFullDir,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Utf8, StandardErrorEncoding = Utf8,
            };
            start.EnvironmentVariables["TCLI_AUTH_TOKEN"] = token;
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException("无法启动 tcli。");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(300000);
            result["timed_out"] = !exited;
            if (!exited) {
                using Process kill = Process.Start(new ProcessStartInfo("taskkill.exe", $"/PID {process.Id} /T /F") {
                    UseShellExecute = false, CreateNoWindow = true,
                });
                if (kill == null || !kill.WaitForExit(10000) || !process.WaitForExit(5000)) {
                    throw new InvalidOperationException("tcli 已超时，无法确认上传进程树已退出；请先检查进程与远端。");
                }
            }
            result["exit_code"] = process.ExitCode;
            if (Task.WaitAll([output, error], 5000)) {
                result["stdout"] = output.Result.Replace(token, "[REDACTED]");
                result["stderr"] = error.Result.Replace(token, "[REDACTED]");
            } else {
                result["output_error"] = "读取 CLI 输出超时。";
            }
        } catch (Exception ex) {
            result["error"] = ex.Message.Replace(token, "[REDACTED]");
        }
        return result;
    }
}
