using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using static AfterBuildEvent.Utils;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent;

static partial class AfterBuildEvent {
    #region 更新mod、打包、启动游戏

    /// <summary>
    /// 本机 qqbot publish-local HTTP 请求的超时时间，单位为毫秒。
    /// </summary>
    private const int QqbotArtifactUploadTimeoutMs = 60000;

    /// <summary>
    /// 自动投递压缩包时使用的 qqbot project_id。
    /// </summary>
    private const string AutoUploadProjectId = "mlj_dspmods";

    /// <summary>
    /// 接收已配置发布包的默认 QQ 群。
    /// </summary>
    private const int AutoUploadGroupId = 319567534;

    /// <summary>
    /// 仅限本机访问、接收已构建压缩包路径的 qqbot 管理端点。
    /// </summary>
    private const string QqbotArtifactUploadUrl = "http://127.0.0.1:8080/admin/api/artifacts/publish-local";

    /// <summary>
    /// 模式 1 生成的一个项目压缩包，包含完整性哈希和内容哈希。
    /// </summary>
    private sealed class GeneratedPackageInfo {
        /// <summary>
        /// 用于匹配 PublishTargets 和 CLI 筛选条件的本地项目/包 ID。
        /// </summary>
        public string ProjectName { get; set; } = "";

        /// <summary>
        /// 所生成压缩包的绝对路径。
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// 作为上传显示名称发送给 qqbot 的压缩包文件名。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 打包时压缩包的字节长度。
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// 压缩包的 UTC 最后写入时间戳，格式为 ISO-8601。
        /// </summary>
        public string LastWriteTimeUtc { get; set; } = "";

        /// <summary>
        /// 压缩包文件字节的 SHA-256；用作传输完整性校验和。
        /// </summary>
        public string Sha256 { get; set; } = "";

        /// <summary>
        /// 对排序后的压缩包条目名称、大小和字节计算的稳定哈希；由本地内容保护机制使用。
        /// </summary>
        public string ContentSha256 { get; set; } = "";
    }

    /// <summary>
    /// 声明可提供给 qqbot 的打包项目，以及接收该项目的 QQ 群。
    /// </summary>
    private sealed class PublishTarget {
        /// <summary>
        /// 必须与 GeneratedPackageInfo.ProjectName 匹配的本地项目名称。
        /// </summary>
        public string ProjectName { get; set; } = "";

        /// <summary>
        /// publish-local 文件条目 targets 数组中包含的 QQ 群 ID。
        /// </summary>
        public int[] GroupIds { get; set; } = [];
    }

    /// <summary>
    /// 当响应正文包含 uploaded/deleted/skipped 数组时解析出的 qqbot publish-local 响应计数。
    /// </summary>
    private sealed class QqbotPublishSummary {
        /// <summary>
        /// qqbot 报告为已上传的压缩包条目数。
        /// </summary>
        public int UploadedCount { get; set; }

        /// <summary>
        /// qqbot 报告为已删除的旧产物数。
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        /// qqbot 报告为因未变化而跳过的压缩包条目数。
        /// </summary>
        public int SkippedCount { get; set; }
    }

    /// <summary>
    /// 模式 1 本地打包后可提供给 qqbot 的已配置包。
    /// </summary>
    private static readonly PublishTarget[] PublishTargets = [
        new() {
            ProjectName = "BlueprintPlanet",
            GroupIds = [AutoUploadGroupId],
        },
        new() {
            ProjectName = "FractionateEverything",
            GroupIds = [AutoUploadGroupId],
        },
        new() {
            ProjectName = "SaveDataExporter",
            GroupIds = [AutoUploadGroupId],
        },
        new() {
            ProjectName = "UXAEnhance",
            GroupIds = [AutoUploadGroupId],
        },
    ];

    /// <summary>
    /// 一次模式 1 qqbot 发布尝试的结果，包括是否已发送请求。
    /// </summary>
    private sealed class QqbotPublishOutcome {
        /// <summary>
        /// 打包流程可将发布步骤视为成功或有意跳过时为 true。
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// 实际已向 qqbot 发送 HTTP publish-local 请求时为 true。
        /// </summary>
        public bool SentRequest { get; set; }
    }

    /// <summary>
    /// 用于跳过未变化包上传的本地 publish-content-sha256 缓存条目。
    /// </summary>
    private sealed class LocalPublishState {
        /// <summary>
        /// 缓存的压缩包文件名。
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 上次成功请求 qqbot 时缓存的包内容哈希。
        /// </summary>
        public string ContentSha256 { get; set; } = "";
    }

    private static void UpdateModsThenStart(bool automationMode = false, string[] args = null) {
        using CmdProcess cmd = new();
        List<GeneratedPackageInfo> generatedPackages = [];
        HashSet<string> selectedProjects = ParseSelectedPublishProjects(args);
        HashSet<string> qqPublishProjectFilter = ResolveQqPublishProjectFilter(automationMode, selectedProjects);
        //强制终止游戏进程
        Console.WriteLine("终止游戏进程...");
        cmd.Exec(KillDSP);
        //遍历所有csproj，拷贝dll（本程序Debug则仅拷贝所有debug的dll，Release则仅拷贝release的dll）
        foreach (var dirInfo in new DirectoryInfo(SolutionDir).GetDirectories()) {
            string csproj = $@"{dirInfo.FullName}\{dirInfo.Name}.csproj";
            if (!File.Exists(csproj)) {
                continue;
            }
            XmlDocument xmlDocument = new();
            xmlDocument.Load(csproj);
            if (xmlDocument.SelectSingleNode("/Project/PropertyGroup/BepInExPluginGuid") == null) {
                continue;
            }
            string projectName = xmlDocument.SelectSingleNode("/Project/PropertyGroup/PackageId")?.InnerText;
            if (projectName == null) {
                continue;
            }
            if (selectedProjects.Count > 0 && !selectedProjects.Contains(projectName)) {
                continue;
            }
            //要打包的所有文件，也是要复制到R2_BepInEx的文件
            List<string> fileList = [];
            string r2ModDir = $@"{R2ProfileDir}\BepInEx\plugins\MengLei-{projectName}";
            string projectDir = dirInfo.FullName;
            //mod.dll
            string projectModFile = GetProjectOutputPath(projectName, BuildConfiguration, $"{projectName}.dll");
            string projectModPdbFile = GetProjectOutputPath(projectName, BuildConfiguration, $"{projectName}.pdb");
            string projectModMdbFile = GetProjectOutputPath(projectName, BuildConfiguration, $"{projectName}.dll.mdb");
            if (!File.Exists(projectModFile)) {
                continue;
            }
            fileList.Add(projectModFile);
            //mod.dll.mdb，供Attach to Unity Editor调试使用
            //注：dll和pdb在同一目录下，才能生成mdb文件；但是参数只需要传dll路径
            if (!File.Exists(projectModPdbFile)) {
                Console.WriteLine($"未找到{projectName}的pdb文件！");
            } else {
                Console.WriteLine($"开始尝试生成{projectName}的mdb文件");
                if (File.Exists(projectModMdbFile)) {
                    File.Delete(projectModMdbFile);
                }
                cmd.Run(Pdb2mdbExe.FullName, $"\"{new FileInfo(projectModFile).FullName}\"", Pdb2mdbExe.DirectoryName);
                if (!File.Exists(projectModMdbFile)) {
                    Console.Error.WriteLine($"生成mdb失败，说明需要调整项目设置，勾选debug symbols并且修改debug type为full");
                } else {
                    Console.WriteLine($"已生成{projectName}的mdb文件");
                }
                //注：mdb文件不加到fileList里面，因为它不需要打包。最后会单独处理它。
            }
            //README.md
            string projectReadme = $@"{projectDir}\README.md";
            if (File.Exists(projectReadme)) {
                fileList.Add(projectReadme);
            }
            //CHANGELOG.md
            string projectChangeLog = $@"{projectDir}\CHANGELOG.md";
            if (File.Exists(projectChangeLog)) {
                fileList.Add(projectChangeLog);
            }
            //manifest.json、version
            string projectManifest = $@"{projectDir}\Assets\manifest.json";
            string version = "";
            string manifestVersion = "";
            string thunderstoreModName = $@"MengLei-{projectName}";
            if (File.Exists(projectManifest)) {
                fileList.Add(projectManifest);
                var obj = JObject.Parse(File.ReadAllText(projectManifest));
                if (obj.TryGetValue("version_number", out JToken value)) {
                    manifestVersion = value.ToString();
                    version = "_" + manifestVersion;
                }
                string author = obj.TryGetValue("author", out JToken authorValue)
                    ? authorValue.ToString()
                    : "MengLei";
                string modName = obj.TryGetValue("name", out JToken nameValue)
                    ? nameValue.ToString()
                    : projectName;
                if (!string.IsNullOrWhiteSpace(author) && !string.IsNullOrWhiteSpace(modName)) {
                    thunderstoreModName = $"{author}-{modName}";
                }
            }
            //icon.png
            string projectIcon = $@"{projectDir}\Assets\icon.png";
            if (File.Exists(projectIcon)) {
                fileList.Add(projectIcon);
            }
            //额外文件
            if (projectName == "GetDspData") {
                //Newtonsoft.Json.dll
                string jsonDll = $@"{SolutionDir}\lib\Newtonsoft.Json.dll";
                fileList.Add(jsonDll);
            } else if (projectName == "FractionateEverything") {
                fileList.Add(GetProjectOutputPath(projectName, BuildConfiguration, "FractionateEverything.Resources.dll"));
            }
            //打包
            if (!Directory.Exists(r2ModDir)) {
                Directory.CreateDirectory(r2ModDir);
            }
            if (!Directory.Exists(@".\ModZips")) {
                Directory.CreateDirectory(@".\ModZips");
            }
            string zipFile = $@".\ModZips\{projectName}{version}.zip";
            DeleteExistingVersionModZip(zipFile);
            string contentSha256 = CalculatePackageContentSha256(fileList);
            ZipMod(fileList, zipFile);
            Console.WriteLine($"创建 {zipFile}");
            generatedPackages.Add(BuildGeneratedPackageInfo(projectName, zipFile, contentSha256));
            //所有文件复制到R2，注意R2是否禁用了mod
            //mdb也要复制到R2（pdb不需要）
            fileList.Add(projectModMdbFile);
            foreach (var file in fileList) {
                string relativePath = Path.GetFileName(file);
                string r2FilePath = $@"{R2ProfileDir}\BepInEx\plugins\MengLei-{projectName}\{relativePath}";
                string r2OldFilePath = $"{r2FilePath}.old";
                string targetPath = !File.Exists(r2OldFilePath) ? r2FilePath : r2OldFilePath;
                FileInfo fileInfo = new FileInfo(targetPath);
                if (!fileInfo.Directory.Exists) {
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                }
                CopyFileWithRetry(file, targetPath);
            }
            if (projectName == "FractionateEverything") { RetireLegacyFeAssetBundle(); }
            if (!string.IsNullOrWhiteSpace(manifestVersion)) {
                if (UpdateModVersionInConfig(thunderstoreModName, manifestVersion)) {
                    Console.WriteLine($"已同步 mods.yml 版本：{thunderstoreModName} -> {manifestVersion}");
                } else {
                    Console.WriteLine($"未在 mods.yml 中更新版本：{thunderstoreModName}");
                }
            }
            //复制导入教学视频
            if (projectName == "FractionateEverything") {
                string file = $@"{projectDir}\Assets\[看我看我！]如何导入测试版万物分馏.mp4";
                string targetPath = $@"{projectDir}\Assets\[看我看我！]如何导入测试版万物分馏.mp4";
                File.Copy(file, @".\ModZips\[看我看我！]如何导入测试版万物分馏.mp4", true);
                Console.WriteLine($"复制 {file} -> {targetPath}");
            }
        }

        ReportMissingSelectedProjects(selectedProjects, generatedPackages);

        //将R2的winhttp.dll、doorstop_config.ini复制到游戏目录
        PrepareR2Doorstop();
        QqbotPublishOutcome publishOutcome = TryPublishGeneratedPackagesToQqbot(
            generatedPackages,
            qqPublishProjectFilter);
        if (automationMode) {
            if (publishOutcome.Succeeded) {
                string finishMessage = publishOutcome.SentRequest
                    ? "自动模式完成：已处理生成的 zip 发布请求，不打开 ModZips 文件夹，不启动游戏"
                    : "自动模式完成：zip 内容未变化，已跳过 QQ 群上传，不打开 ModZips 文件夹，不启动游戏";
                Console.WriteLine(finishMessage);
                return;
            }
            Process.Start("explorer", @".\ModZips");
            Console.WriteLine("自动模式完成：自动上传失败，已打开 ModZips 文件夹，不启动游戏");
            return;
        }

        if (publishOutcome.Succeeded) {
            Console.WriteLine(publishOutcome.SentRequest
                ? "手动模式：已处理生成的 zip 发布请求"
                : "手动模式：zip 内容未变化，已跳过 QQ 群上传");
        } else {
            Process.Start("explorer", @".\ModZips");
            Console.WriteLine("手动模式：自动上传失败，已打开 ModZips 文件夹；继续保留是否启动游戏的手动选择");
        }

        //启动使用R2MOD的游戏
        Console.WriteLine("是否启动游戏？1或回车表示启动，其他表示结束程序");
        string str = Console.ReadLine();
        if (str == "" || str == "1") {
            cmd.Exec(RunDSP);
        }
    }

    private static HashSet<string> ParseSelectedPublishProjects(string[] args) {
        HashSet<string> selectedProjects = new(StringComparer.OrdinalIgnoreCase);
        if (args == null || args.Length <= 1) {
            return selectedProjects;
        }

        foreach (string arg in args.Skip(1)) {
            foreach (string projectName in arg.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)) {
                string trimmed = projectName.Trim();
                if (trimmed.Length > 0) {
                    selectedProjects.Add(trimmed);
                }
            }
        }

        if (selectedProjects.Count > 0) {
            Console.WriteLine($"仅处理指定项目：{string.Join(", ", selectedProjects.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}");
        }

        return selectedProjects;
    }

    private static HashSet<string> ResolveQqPublishProjectFilter(
        bool automationMode,
        HashSet<string> selectedProjects) {
        if (selectedProjects.Count > 0) {
            return new(selectedProjects, StringComparer.OrdinalIgnoreCase);
        }
        if (!automationMode) {
            return null;
        }

        HashSet<string> changedProjects = GetLatestCommitChangedPublishProjects();
        if (changedProjects.Count > 0) {
            Console.WriteLine($"自动发布目标：{string.Join(", ", changedProjects.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}");
        } else {
            Console.WriteLine("自动发布目标：最新提交未修改配置发布项目，QQ 上传将跳过");
        }
        return changedProjects;
    }

    private static HashSet<string> GetLatestCommitChangedPublishProjects() {
        HashSet<string> projects = new(StringComparer.OrdinalIgnoreCase);
        string output = TryGetGitOutput("diff-tree --no-commit-id --name-only -r HEAD");
        foreach (string rawPath in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)) {
            string path = rawPath.Trim().Replace('\\', '/');
            if (path.Length == 0) {
                continue;
            }
            if (IsSharedPackageAffectingPath(path)) {
                foreach (PublishTarget target in PublishTargets) {
                    projects.Add(target.ProjectName);
                }
                continue;
            }
            string projectName = GetProjectNameFromChangedPath(path);
            if (!string.IsNullOrWhiteSpace(projectName)) {
                projects.Add(projectName);
            }
        }
        return projects;
    }

    private static bool IsSharedPackageAffectingPath(string path) {
        return string.Equals(path, "Directory.Build.props", StringComparison.OrdinalIgnoreCase)
               || string.Equals(path, "MLJ_DSPmods.sln", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("lib/", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetProjectNameFromChangedPath(string path) {
        if (path.StartsWith("FractionateEverything.Resources/", StringComparison.OrdinalIgnoreCase)) {
            return "FractionateEverything";
        }
        foreach (PublishTarget target in PublishTargets) {
            string prefix = $"{target.ProjectName}/";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                return target.ProjectName;
            }
        }
        return "";
    }

    private static void ReportMissingSelectedProjects(
        HashSet<string> selectedProjects,
        IReadOnlyList<GeneratedPackageInfo> generatedPackages) {
        if (selectedProjects.Count == 0) {
            return;
        }

        HashSet<string> generatedProjectNames = generatedPackages
            .Select(package => package.ProjectName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string projectName in selectedProjects.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)) {
            if (!generatedProjectNames.Contains(projectName)) {
                Console.WriteLine($"指定项目未生成 zip，已跳过：{projectName}");
            }
        }
    }

    private static void CopyFileWithRetry(string source, string targetPath) {
        for (int attempt = 1; attempt <= R2CopyRetryCount; attempt++) {
            try {
                File.Copy(source, targetPath, true);
                Console.WriteLine($"复制 {source} -> {targetPath}");
                return;
            }
            catch (Exception ex) when (attempt < R2CopyRetryCount) {
                Console.WriteLine($"复制失败，稍后重试 {attempt}/{R2CopyRetryCount}：{source} -> {targetPath}，{ex.Message}");
                Thread.Sleep(R2CopyRetryDelayMs);
            }
        }
        File.Copy(source, targetPath, true);
        Console.WriteLine($"复制 {source} -> {targetPath}");
    }

    private static GeneratedPackageInfo BuildGeneratedPackageInfo(string projectName, string zipFile, string contentSha256) {
        string fullPath = Path.GetFullPath(zipFile);
        FileInfo fileInfo = new(fullPath);
        return new() {
            ProjectName = projectName,
            Path = fullPath,
            Name = fileInfo.Name,
            SizeBytes = fileInfo.Length,
            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc.ToString("O"),
            Sha256 = CalculateSha256(fullPath),
            ContentSha256 = contentSha256,
        };
    }

    private static string CalculateSha256(string path) {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        byte[] hash = sha256.ComputeHash(stream);
        return string.Concat(hash.Select(value => value.ToString("x2")));
    }

    private static string CalculatePackageContentSha256(IReadOnlyList<string> fileList) {
        using SHA256 sha256 = SHA256.Create();
        foreach (string file in fileList.OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)) {
            FileInfo fileInfo = new(file);
            WriteHashText(sha256, Path.GetFileName(file));
            WriteHashText(sha256, fileInfo.Length.ToString());
            using FileStream stream = File.OpenRead(file);
            byte[] buffer = new byte[4096];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) {
                sha256.TransformBlock(buffer, 0, read, null, 0);
            }
            sha256.TransformBlock([0], 0, 1, null, 0);
        }
        sha256.TransformFinalBlock([], 0, 0);
        return string.Concat(sha256.Hash.Select(value => value.ToString("x2")));
    }

    private static void WriteHashText(HashAlgorithm hashAlgorithm, string text) {
        byte[] bytes = Utf8NoBom.GetBytes(text ?? "");
        hashAlgorithm.TransformBlock(bytes, 0, bytes.Length, null, 0);
        hashAlgorithm.TransformBlock([0], 0, 1, null, 0);
    }

    private static string TryGetGitOutput(string arguments) {
        try {
            ProcessStartInfo startInfo = new() {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = SolutionDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Utf8NoBom,
                StandardErrorEncoding = Utf8NoBom,
            };
            using Process process = Process.Start(startInfo);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output.Trim() : "";
        }
        catch {
            return "";
        }
    }

    private static QqbotPublishOutcome TryPublishGeneratedPackagesToQqbot(
        IReadOnlyList<GeneratedPackageInfo> generatedPackages,
        HashSet<string> qqPublishProjectFilter) {
        List<GeneratedPackageInfo> configuredPackages = GetConfiguredPublishPackages(
            generatedPackages,
            qqPublishProjectFilter);
        if (configuredPackages.Count == 0) {
            Console.WriteLine("自动上传跳过：本次没有配置需要推送到 QQ 群的 zip");
            return new() {
                Succeeded = true,
                SentRequest = false,
            };
        }

        string folderId = QqFolderId;
        List<GeneratedPackageInfo> publishablePackages = GetLocallyChangedPublishPackages(configuredPackages, folderId);
        JArray files = BuildQqbotPublishFiles(publishablePackages, folderId);
        if (files.Count == 0) {
            Console.WriteLine("自动上传跳过：本次没有内容变化的配置 zip，无需通知 qqbot");
            return new() {
                Succeeded = true,
                SentRequest = false,
            };
        }

        try {
            JObject payload = new() {
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["project_id"] = AutoUploadProjectId,
                ["branch"] = TryGetGitOutput("branch --show-current"),
                ["commit_hash"] = TryGetGitOutput("rev-parse HEAD"),
                ["commit_subject"] = TryGetGitOutput("log -1 --pretty=%s"),
                ["commit_detail"] = TryGetGitOutput("log -1 --pretty=%b"),
                ["files"] = files,
            };
            byte[] body = Utf8NoBom.GetBytes(payload.ToString());
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(QqbotArtifactUploadUrl);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = QqbotArtifactUploadTimeoutMs;
            request.ContentLength = body.Length;
            using (Stream requestStream = request.GetRequestStream()) {
                requestStream.Write(body, 0, body.Length);
            }

            using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string responseBody = ReadResponseBody(response);
            bool ok = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300;
            if (ok) {
                QqbotPublishSummary summary = ParseQqbotPublishSummary(responseBody, files);
                Console.WriteLine($"自动上传完成：上传 {summary.UploadedCount} 个 zip，跳过 {summary.SkippedCount} 个未变化 zip，删除旧文件 {summary.DeletedCount} 个");
                SaveLocalPublishStates(publishablePackages, folderId);
            } else {
                Console.WriteLine($"自动上传失败：qqbot 返回 HTTP {(int)response.StatusCode}");
            }
            return new() {
                Succeeded = ok,
                SentRequest = true,
            };
        }
        catch (WebException ex) {
            string detail = ex.Message;
            if (ex.Response is HttpWebResponse response) {
                using StreamReader reader = new(response.GetResponseStream(), Encoding.UTF8);
                detail = $"HTTP {(int)response.StatusCode} {response.StatusCode}：{reader.ReadToEnd()}";
            }
            Console.WriteLine($"自动上传失败：{detail}");
            return new() {
                Succeeded = false,
                SentRequest = true,
            };
        }
        catch (Exception ex) {
            Console.WriteLine($"自动上传失败：{ex.Message}");
            return new() {
                Succeeded = false,
                SentRequest = true,
            };
        }
    }

    private static List<GeneratedPackageInfo> GetConfiguredPublishPackages(
        IReadOnlyList<GeneratedPackageInfo> generatedPackages,
        HashSet<string> qqPublishProjectFilter) {
        List<GeneratedPackageInfo> configuredPackages = [];
        foreach (GeneratedPackageInfo package in generatedPackages) {
            if (qqPublishProjectFilter != null && !qqPublishProjectFilter.Contains(package.ProjectName)) {
                continue;
            }
            PublishTarget target = PublishTargets.FirstOrDefault(item =>
                string.Equals(item.ProjectName, package.ProjectName, StringComparison.OrdinalIgnoreCase));
            if (target == null || target.GroupIds.Length == 0) {
                continue;
            }

            configuredPackages.Add(package);
        }

        return configuredPackages;
    }

    private static List<GeneratedPackageInfo> GetLocallyChangedPublishPackages(
        IReadOnlyList<GeneratedPackageInfo> generatedPackages, string folderId) {
        List<GeneratedPackageInfo> publishablePackages = [];
        foreach (GeneratedPackageInfo package in generatedPackages) {
            LocalPublishState state = LoadLocalPublishState(package, folderId);
            if (string.Equals(package.Name, state.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(package.ContentSha256, state.ContentSha256, StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine($"本地上传跳过：{package.Name} 内容未变化 ({package.ContentSha256})");
                continue;
            }

            publishablePackages.Add(package);
        }
        return publishablePackages;
    }

    private static void SaveLocalPublishStates(IReadOnlyList<GeneratedPackageInfo> packages, string folderId) {
        foreach (GeneratedPackageInfo package in packages) {
            SaveLocalPublishState(package, folderId);
        }
    }

    private static LocalPublishState LoadLocalPublishState(GeneratedPackageInfo package, string folderId) {
        string path = GetLocalPublishStatePath(package, folderId);
        if (!File.Exists(path)) {
            return new();
        }

        try {
            JObject payload = JObject.Parse(File.ReadAllText(path));
            return new() {
                Name = payload.Value<string>("name") ?? "",
                ContentSha256 = payload.Value<string>("content_sha256")?.Trim().ToLowerInvariant() ?? "",
            };
        }
        catch {
            return new();
        }
    }

    private static void SaveLocalPublishState(GeneratedPackageInfo package, string folderId) {
        string path = GetLocalPublishStatePath(package, folderId);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? @".\ModZips");
        JObject payload = new() {
            ["name"] = package.Name,
            ["folder_id"] = folderId,
            ["content_sha256"] = package.ContentSha256,
            ["sha256"] = package.Sha256,
            ["updated_at_utc"] = DateTime.UtcNow.ToString("O"),
        };
        File.WriteAllText(path, payload.ToString(), Utf8NoBom);
    }

    /// <summary>按接收群集合、目录和包隔离成功缓存；原固定群的根目录沿用旧路径。</summary>
    private static string GetLocalPublishStatePath(GeneratedPackageInfo package, string folderId) {
        string safeName = string.Join("_", package.Name.Split(Path.GetInvalidFileNameChars()));
        string directory = Path.Combine(@".\ModZips", "publish-content-sha256");
        int[] groups = PublishTargets.Single(target =>
                string.Equals(target.ProjectName, package.ProjectName, StringComparison.OrdinalIgnoreCase)).GroupIds
            .Distinct().OrderBy(group => group).ToArray();
        if (folderId.Length > 0 || groups.Length != 1 || groups[0] != AutoUploadGroupId) {
            using SHA256 hash = SHA256.Create();
            string identity = string.Join(",", groups) + "\0" + folderId;
            string targetKey = BitConverter.ToString(hash.ComputeHash(Utf8NoBom.GetBytes(identity)))
                .Replace("-", "").ToLowerInvariant();
            directory = Path.Combine(directory, "targets", targetKey);
        }
        return Path.Combine(directory, $"{safeName}.json");
    }

    private static string ReadResponseBody(HttpWebResponse response) {
        using Stream responseStream = response.GetResponseStream();
        if (responseStream == null) {
            return "";
        }
        using StreamReader reader = new(responseStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>核对每个文件的群、目录和内容回执；未确认目标时不能写入成功缓存。</summary>
    private static QqbotPublishSummary ParseQqbotPublishSummary(string responseBody, JArray files) {
        JObject response = JObject.Parse(responseBody);
        if (response["uploaded"] is not JArray uploaded ||
            response["deleted"] is not JArray deleted ||
            response["skipped"] is not JArray skipped) {
            throw new InvalidDataException("qqbot 回执缺少上传、删除或跳过数组。");
        }
        JToken[] confirmations = uploaded.Concat(skipped).ToArray();
        foreach (JToken file in files) {
            foreach (int group in file["targets"].Values<int>()) {
                if (!confirmations.Any(entry => (int?)entry["group_id"] == group
                    && ((string)entry["folder_id"] ?? "") == (string)file["folder_id"]
                    && (string)entry["name"] == (string)file["name"]
                    && (string)entry["content_sha256"] == (string)file["content_sha256"])) {
                    throw new InvalidDataException($"qqbot 未确认群 {group} 目标目录中的 {file["name"]}，未写成功缓存。");
                }
            }
        }
        return new() {
            UploadedCount = uploaded.Count,
            DeletedCount = deleted.Count,
            SkippedCount = skipped.Count,
        };
    }

    private static JArray BuildQqbotPublishFiles(IReadOnlyList<GeneratedPackageInfo> generatedPackages, string folderId) {
        JArray files = [];
        foreach (GeneratedPackageInfo package in generatedPackages) {
            PublishTarget target = PublishTargets.FirstOrDefault(item =>
                string.Equals(item.ProjectName, package.ProjectName, StringComparison.OrdinalIgnoreCase));
            if (target == null || target.GroupIds.Length == 0) {
                continue;
            }
            files.Add(new JObject {
                ["path"] = package.Path,
                ["name"] = package.Name,
                ["sha256"] = package.Sha256,
                ["content_sha256"] = package.ContentSha256,
                ["targets"] = new JArray(target.GroupIds),
                ["folder_id"] = folderId,
            });
        }
        return files;
    }

    private static void PrepareR2Doorstop() {
        File.Copy($@"{R2ProfileDir}\winhttp.dll", $@"{DSPGameDir}\winhttp.dll", true);
        string doorstop_config = $@"{DSPGameDir}\doorstop_config.ini";
        File.Copy($@"{R2ProfileDir}\doorstop_config.ini", doorstop_config, true);
        string[] lines = File.ReadAllLines(doorstop_config);
        for (int i = 0; i < lines.Length; i++) {
            if (lines[i].StartsWith("enabled=")) {
                lines[i] = "enabled=true";
            } else if (lines[i].StartsWith("targetAssembly=")) {
                lines[i] = $@"targetAssembly={R2ProfileDir}\BepInEx\core\BepInEx.Preloader.dll";
            } else if (lines[i].StartsWith("ignoreDisableSwitch=")) {
                lines[i] = "ignoreDisableSwitch=false";
            }
        }
        File.WriteAllLines(doorstop_config, lines);
    }

    static void ZipMod(List<string> fileList, string zipPath) {
        string zipParentDir = new FileInfo(zipPath).DirectoryName;
        if (zipParentDir == null) {
            throw new("路径异常！");
        }
        if (!Directory.Exists(zipParentDir)) {
            Directory.CreateDirectory(zipParentDir);
        }
        if (File.Exists(zipPath)) {
            File.Delete(zipPath);
        }
        using var zipStream = new ZipOutputStream(File.Create(zipPath));
        foreach (var file in fileList.OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)) {
            //MOD上传至R2的时候，文件要直接打包在里面，不能嵌套文件夹，所以相对路径直接使用文件名
            //但是也有一些特殊情况需要文件夹
            FileInfo fileInfo = new(file);
            var entry = new ZipEntry(Path.GetFileName(file)) {
                // BepInEx 按 DLL 修改时间判断是否重读插件及依赖声明。
                DateTime = fileInfo.LastWriteTime,
                Size = fileInfo.Length,
            };
            zipStream.PutNextEntry(entry);
            using FileStream fs = File.OpenRead(file);
            byte[] buffer = new byte[4096];
            int sourceBytes;
            do {
                sourceBytes = fs.Read(buffer, 0, buffer.Length);
                zipStream.Write(buffer, 0, sourceBytes);
            } while (sourceBytes > 0);
        }
        zipStream.Finish();
        zipStream.Close();
    }

    private static void DeleteExistingVersionModZip(string zipFile) {
        if (!File.Exists(zipFile)) {
            return;
        }

        File.Delete(zipFile);
        Console.WriteLine($"删除 {zipFile}");
    }

    #endregion
}
