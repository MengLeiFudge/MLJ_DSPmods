using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using static AfterBuildEvent.Utils;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent;

static partial class AfterBuildEvent {
    #region 更新mod、打包、启动游戏

    private sealed class QqbotPublishOutcome {
        public bool Succeeded { get; set; }
        public bool SentRequest { get; set; }
    }

    private sealed class LocalPublishState {
        public string Name { get; set; } = "";
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
                //fe
                string originFEAssets = @"D:\project\unity\DSP_FEAssets\AssetBundles\StandaloneWindows64\fe";
                string projectFEAssets = $@"{SolutionDir}\FractionateEverything\Assets\fe";
                if (File.Exists(originFEAssets)) {
                    File.Copy(originFEAssets, projectFEAssets, true);
                }
                fileList.Add(projectFEAssets);
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

        List<GeneratedPackageInfo> publishablePackages = GetLocallyChangedPublishPackages(configuredPackages);
        JArray files = BuildQqbotPublishFiles(publishablePackages);
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
                QqbotPublishSummary summary = ParseQqbotPublishSummary(responseBody);
                if (summary.HasCounts) {
                    Console.WriteLine($"自动上传完成：上传 {summary.UploadedCount} 个 zip，跳过 {summary.SkippedCount} 个未变化 zip，删除旧文件 {summary.DeletedCount} 个");
                } else {
                    Console.WriteLine($"自动上传完成：qqbot 已接受 {files.Count} 个 zip 的发布请求");
                }
                SaveLocalPublishStates(publishablePackages);
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
        IReadOnlyList<GeneratedPackageInfo> generatedPackages) {
        List<GeneratedPackageInfo> publishablePackages = [];
        foreach (GeneratedPackageInfo package in generatedPackages) {
            LocalPublishState state = LoadLocalPublishState(package.Name);
            if (string.Equals(package.Name, state.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(package.ContentSha256, state.ContentSha256, StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine($"本地上传跳过：{package.Name} 内容未变化 ({package.ContentSha256})");
                continue;
            }

            publishablePackages.Add(package);
        }
        return publishablePackages;
    }

    private static void SaveLocalPublishStates(IReadOnlyList<GeneratedPackageInfo> packages) {
        foreach (GeneratedPackageInfo package in packages) {
            SaveLocalPublishState(package);
        }
    }

    private static LocalPublishState LoadLocalPublishState(string packageName) {
        string path = GetLocalPublishStatePath(packageName);
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

    private static void SaveLocalPublishState(GeneratedPackageInfo package) {
        string path = GetLocalPublishStatePath(package.Name);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? @".\ModZips");
        JObject payload = new() {
            ["name"] = package.Name,
            ["content_sha256"] = package.ContentSha256,
            ["sha256"] = package.Sha256,
            ["updated_at_utc"] = DateTime.UtcNow.ToString("O"),
        };
        File.WriteAllText(path, payload.ToString(), Utf8NoBom);
    }

    private static string GetLocalPublishStatePath(string packageName) {
        string safeName = string.Join("_", packageName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(@".\ModZips", "publish-content-sha256", $"{safeName}.json");
    }

    private static string ReadResponseBody(HttpWebResponse response) {
        using Stream responseStream = response.GetResponseStream();
        if (responseStream == null) {
            return "";
        }
        using StreamReader reader = new(responseStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static QqbotPublishSummary ParseQqbotPublishSummary(string responseBody) {
        if (string.IsNullOrWhiteSpace(responseBody)) {
            return new();
        }

        try {
            JObject response = JObject.Parse(responseBody);
            if (response["uploaded"] is not JArray uploaded ||
                response["deleted"] is not JArray deleted ||
                response["skipped"] is not JArray skipped) {
                return new();
            }

            return new() {
                HasCounts = true,
                UploadedCount = uploaded.Count,
                DeletedCount = deleted.Count,
                SkippedCount = skipped.Count,
            };
        }
        catch {
            return new();
        }
    }

    private static JArray BuildQqbotPublishFiles(IReadOnlyList<GeneratedPackageInfo> generatedPackages) {
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

    private static readonly DateTime StableZipEntryTime = new(2000, 1, 1, 0, 0, 0);

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
                DateTime = StableZipEntryTime,
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
