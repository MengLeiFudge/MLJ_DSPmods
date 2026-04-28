using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using static AfterBuildEvent.Utils;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent;

static class AfterBuildEvent {
    private static readonly Regex SoftDependencyRegex =
        new(@"\[BepInDependency\(([^,\)]+),\s*BepInDependency\.DependencyFlags\.SoftDependency\)\]",
            RegexOptions.Compiled);
    private static readonly Regex GuidLiteralRegex =
        new(@"public\s+const\s+string\s+GUID\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly string[] IgnoredModDllPrefixes =
        ["System.", "Newtonsoft.Json", "0Harmony", "BepInEx.", "Mono.", "MonoMod.", "K4os.", "Unity."];
    private static readonly string[] IgnoredModDllNames =
        ["websocket-sharp", "discord_game_sdk", "discord_game_sdk_dotnet", "Open.Nat", "HarmonyXInterop"];
    private static readonly string[] CalcJsonLocalProjectNames = ["FractionateEverything", "GetDspData"];
    private const int CalcJsonCacheVersion = 2;

    private sealed class ModDecompileTarget {
        public string DependencyExpression { get; set; } = "";
        public string SourceName { get; set; } = "";
        public List<string> Keywords { get; set; } = [];
    }

    private sealed class CalcIconExportTarget {
        public string TargetMod { get; set; } = "";
        public string SourceDirName { get; set; } = "";
        public string SourcePrefix { get; set; } = "";
        public string AssetStudioPackageName { get; set; } = "";
        public List<string> AssetStudioRelativePaths { get; set; } = [];
        public List<string> AssetStudioGameDataRelativePaths { get; set; } = [];
        public string EmbeddedDllPackageName { get; set; } = "";
        public string EmbeddedDllRelativePath { get; set; } = "";
        public List<string> EnabledMods { get; set; } = [];
        public List<string> LowerPriorityMods { get; set; } = [];
    }

    private sealed class MissingCalcIcon {
        public string IconName { get; set; } = "";
        public HashSet<string> CandidateTargets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DataFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Examples { get; set; } = [];
    }

    public static void Main(string[] args) {
        Console.WriteLine("本项目需要依赖于其他所有项目，且其他项目输出类型需要设定为类库");
        Console.WriteLine("输入要执行的命令（直接回车表示1）：");
        Console.WriteLine("1表示更新所有mod到R2，打包mod，然后启动游戏");
        Console.WriteLine("2表示更新部分需要的dll类库");
        Console.WriteLine("3表示生成计算器 JSON + 图标 + 同步所需图标");
        Console.WriteLine("4表示仅重建计算器所需图标资源（排障用，游戏内提取）");
        string str = Console.ReadLine();
        if (str == "1" || str == "") {
            UpdateModsThenStart();
        } else if (str == "2") {
            UpdateLibDll();
        } else if (str == "3") {
            GetAllCalcJson();
        } else if (str == "4") {
            ExportCalcIcons();
        } else {
            Console.WriteLine("输入有误！");
        }
    }

    #region 更新mod、打包、启动游戏

    private static void UpdateModsThenStart() {
        using CmdProcess cmd = new();
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
            //要打包的所有文件，也是要复制到R2_BepInEx的文件
            List<string> fileList = [];
            string r2ModDir = $@"{R2ProfileDir}\BepInEx\plugins\MengLei-{projectName}";
            string projectDir = dirInfo.FullName;
            //mod.dll
#if DEBUG
            string projectModFile = $@"{projectDir}\bin\win\debug\{projectName}.dll";
            string projectModPdbFile = $@"{projectDir}\bin\win\debug\{projectName}.pdb";
            string projectModMdbFile = $@"{projectDir}\bin\win\debug\{projectName}.dll.mdb";
#else
            string projectModFile = $@"{projectDir}\bin\win\release\{projectName}.dll";
            string projectModPdbFile = $@"{projectDir}\bin\win\release\{projectName}.pdb";
            string projectModMdbFile = $@"{projectDir}\bin\win\release\{projectName}.dll.mdb";
#endif
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
            foreach (var file in Directory.GetFiles(@".\ModZips")) {
                if (file.StartsWith($@".\ModZips\{projectName}") && file.EndsWith(".zip")) {
                    File.Delete(file);
                    Console.WriteLine($"删除 {file}");
                }
            }
            string zipFile = $@".\ModZips\{projectName}{version}.zip";
            ZipMod(fileList, zipFile);
            Console.WriteLine($"创建 {zipFile}");
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
                while (true) {
                    try {
                        File.Copy(file, targetPath, true);
                        Console.WriteLine($"复制 {file} -> {targetPath}");
                        break;
                    }
                    catch { }
                }
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

        //打开所有压缩包的文件夹
        Process.Start("explorer", @".\ModZips");

        //将R2的winhttp.dll、doorstop_config.ini复制到游戏目录
        File.Copy($@"{R2ProfileDir}\winhttp.dll", $@"{DSPGameDir}\winhttp.dll", true);
        string doorstop_config = $@"{DSPGameDir}\doorstop_config.ini";
        File.Copy($@"{R2ProfileDir}\doorstop_config.ini", doorstop_config, true);
        //修改doorstop_config.ini，使其目标指向R2的preloader.dll
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

        //启动使用R2MOD的游戏
        Console.WriteLine("是否启动游戏？1或回车表示启动，其他表示结束程序");
        string str = Console.ReadLine();
        if (str == "" || str == "1") {
            cmd.Exec(RunDSP);
        }
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
        foreach (var file in fileList) {
            //MOD上传至R2的时候，文件要直接打包在里面，不能嵌套文件夹，所以相对路径直接使用文件名
            //但是也有一些特殊情况需要文件夹
            var entry = new ZipEntry(Path.GetFileName(file));
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

    #endregion

    #region 更新类库需要的部分DLL

    private static void UpdateLibDll() {
        using CmdProcess cmd = new();
        if (NugetGameLibNet45Dir != null) {
            PublizeDll(cmd, DSPACDll, $@"{NugetGameLibNet45Dir}\Assembly-CSharp.dll");
            DecompileDll(cmd, "Assembly-CSharp.dll");
            PublizeDll(cmd, DSPUIDll, $@"{NugetGameLibNet45Dir}\UnityEngine.UI.dll");
            DecompileDll(cmd, "UnityEngine.UI.dll");
        } else {
            Console.WriteLine("NugetGameLibNet45Dir为空，跳过Publize游戏dll");
        }
        DecompileModsFromR2(cmd);
    }

    /// <summary>
    /// 以 CheckPlugins 的软依赖为准，再通过 mods.yml 和插件目录确认是否真的需要反编译。
    /// </summary>
    private static void DecompileModsFromR2(CmdProcess cmd) {
        LoadModInfos();
        IReadOnlyList<ModInfo> installedMods = GetAllModInfos();
        if (installedMods.Count == 0) {
            Console.WriteLine("mods.yml 中没有可用模组信息，跳过 mod 反编译。");
            return;
        }

        List<ModDecompileTarget> targets = GetModDecompileTargets();
        if (targets.Count == 0) {
            Console.WriteLine("CheckPlugins 中没有解析到软依赖，跳过 mod 反编译。");
            return;
        }

        HashSet<string> handledDlls = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModDecompileTarget target in targets) {
            ModInfo modInfo = FindBestMatchingMod(installedMods, target.Keywords);
            if (modInfo == null) {
                Console.WriteLine(
                    $"mods.yml 中未找到 {target.SourceName}（表达式：{target.DependencyExpression}），跳过。");
                continue;
            }

            string pluginDir = Path.Combine(R2PluginsDir, modInfo.name);
            if (!Directory.Exists(pluginDir)) {
                Console.WriteLine($"已在 mods.yml 找到 {modInfo.name}，但插件目录不存在：{pluginDir}");
                continue;
            }

            string dllPath = TrySelectPrimaryModDll(pluginDir, modInfo, target);
            if (string.IsNullOrWhiteSpace(dllPath)) {
                Console.WriteLine($"插件目录 {pluginDir} 中未找到可反编译的主 DLL，跳过。");
                continue;
            }

            string fullDllPath = Path.GetFullPath(dllPath);
            if (!handledDlls.Add(fullDllPath)) {
                Console.WriteLine($"DLL 已处理过，跳过重复目标：{fullDllPath}");
                continue;
            }

            Console.WriteLine($"开始处理模组 {modInfo.name} -> {dllPath}");
            DecompileModDll(cmd, dllPath);
        }
    }

    private static List<ModDecompileTarget> GetModDecompileTargets() {
        List<ModDecompileTarget> targets = [];
        if (!File.Exists(CheckPluginsSourcePath)) {
            Console.WriteLine($"未找到 CheckPlugins 源文件：{CheckPluginsSourcePath}");
            return targets;
        }

        string source = File.ReadAllText(CheckPluginsSourcePath);
        HashSet<string> seenExpressions = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SoftDependencyRegex.Matches(source)) {
            string dependencyExpression = match.Groups[1].Value.Trim();
            if (!seenExpressions.Add(dependencyExpression)) {
                continue;
            }

            string sourceName = dependencyExpression.Split('.')[0].Trim();
            targets.Add(new() {
                DependencyExpression = dependencyExpression,
                SourceName = sourceName,
                Keywords = BuildTargetKeywords(sourceName),
            });
        }
        return targets;
    }

    /// <summary>
    /// 关键字优先取兼容类名；如果本地兼容文件里有 GUID 字面量，再把 GUID 末段加入候选，兼容命名差异。
    /// </summary>
    private static List<string> BuildTargetKeywords(string sourceName) {
        HashSet<string> keywords = new(StringComparer.OrdinalIgnoreCase) { sourceName };
        if (sourceName.EndsWith("Plugin", StringComparison.OrdinalIgnoreCase)) {
            keywords.Add(sourceName.Substring(0, sourceName.Length - "Plugin".Length));
        }

        string compatibilitySourcePath = Path.Combine(CompatibilityDir, $"{sourceName}.cs");
        if (File.Exists(compatibilitySourcePath)) {
            Match match = GuidLiteralRegex.Match(File.ReadAllText(compatibilitySourcePath));
            if (match.Success) {
                string guid = match.Groups[1].Value.Trim();
                string guidTail = guid.Split('.').LastOrDefault();
                if (!string.IsNullOrWhiteSpace(guidTail)) {
                    keywords.Add(guidTail);
                }
            }
        }

        return keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).ToList();
    }

    private static ModInfo FindBestMatchingMod(IReadOnlyList<ModInfo> installedMods, IEnumerable<string> keywords) {
        ModInfo bestMod = null;
        int bestScore = 0;
        foreach (ModInfo modInfo in installedMods) {
            int score = ScoreInstalledMod(modInfo, keywords);
            if (score > bestScore) {
                bestScore = score;
                bestMod = modInfo;
            }
        }
        return bestScore > 0 ? bestMod : null;
    }

    private static int ScoreInstalledMod(ModInfo modInfo, IEnumerable<string> keywords) {
        string displayName = NormalizeForMatch(modInfo.displayName);
        string fullName = NormalizeForMatch(modInfo.name);
        string packageSuffix = NormalizeForMatch(GetPackageSuffix(modInfo.name));
        string authorName = NormalizeForMatch(modInfo.authorName);
        int bestScore = 0;
        foreach (string keyword in keywords) {
            string normalizedKeyword = NormalizeForMatch(keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword)) {
                continue;
            }
            if (displayName == normalizedKeyword) {
                bestScore = Math.Max(bestScore, 100);
            }
            if (packageSuffix == normalizedKeyword) {
                bestScore = Math.Max(bestScore, 95);
            }
            if (fullName == normalizedKeyword) {
                bestScore = Math.Max(bestScore, 90);
            }
            if (!string.IsNullOrWhiteSpace(displayName)
                && (displayName.Contains(normalizedKeyword) || normalizedKeyword.Contains(displayName))) {
                bestScore = Math.Max(bestScore, 80);
            }
            if (!string.IsNullOrWhiteSpace(packageSuffix)
                && (packageSuffix.Contains(normalizedKeyword) || normalizedKeyword.Contains(packageSuffix))) {
                bestScore = Math.Max(bestScore, 75);
            }
            if (!string.IsNullOrWhiteSpace(fullName)
                && (fullName.Contains(normalizedKeyword) || normalizedKeyword.Contains(fullName))) {
                bestScore = Math.Max(bestScore, 70);
            }
            if (authorName == normalizedKeyword) {
                bestScore = Math.Max(bestScore, 40);
            }
        }
        return bestScore;
    }

    private static string TrySelectPrimaryModDll(string pluginDir, ModInfo modInfo, ModDecompileTarget target) {
        List<string> dllCandidates = Directory.GetFiles(pluginDir)
            .Where(IsDllOrOld)
            .Where(path => !IsIgnoredCompanionDll(Path.GetFileName(path)))
            .ToList();
        if (dllCandidates.Count == 0) {
            return null;
        }
        if (dllCandidates.Count == 1) {
            return dllCandidates[0];
        }

        string bestPath = null;
        int bestScore = 0;
        foreach (string path in dllCandidates) {
            int score = ScoreAssemblyCandidate(path, modInfo, target.Keywords);
            if (score > bestScore) {
                bestScore = score;
                bestPath = path;
            }
        }
        return bestScore > 0 ? bestPath : null;
    }

    private static int ScoreAssemblyCandidate(string assemblyPath, ModInfo modInfo, IEnumerable<string> keywords) {
        string assemblyName = NormalizeForMatch(GetAssemblyBaseName(Path.GetFileName(assemblyPath)));
        string displayName = NormalizeForMatch(modInfo.displayName);
        string packageSuffix = NormalizeForMatch(GetPackageSuffix(modInfo.name));
        int bestScore = 0;
        if (assemblyName == displayName) {
            bestScore = Math.Max(bestScore, 100);
        }
        if (assemblyName == packageSuffix) {
            bestScore = Math.Max(bestScore, 95);
        }
        foreach (string keyword in keywords) {
            string normalizedKeyword = NormalizeForMatch(keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword)) {
                continue;
            }
            if (assemblyName == normalizedKeyword) {
                bestScore = Math.Max(bestScore, 90);
            }
            if (!string.IsNullOrWhiteSpace(assemblyName)
                && (assemblyName.Contains(normalizedKeyword) || normalizedKeyword.Contains(assemblyName))) {
                bestScore = Math.Max(bestScore, 75);
            }
        }
        return bestScore;
    }

    private static bool IsDllOrOld(string path) {
        return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".dll.old", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIgnoredCompanionDll(string fileName) {
        string assemblyBaseName = GetAssemblyBaseName(fileName);
        if (IgnoredModDllPrefixes.Any(prefix =>
                assemblyBaseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) {
            return true;
        }
        return IgnoredModDllNames.Any(name => string.Equals(assemblyBaseName, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void DecompileModDll(CmdProcess cmd, string dllPath) {
        string tempDllPath = null;
        try {
            string actualDllPath = EnsureDllPathForDecompile(dllPath, out tempDllPath);
            DecompileDll(cmd, Path.GetFileName(actualDllPath), Path.GetDirectoryName(actualDllPath));
        }
        finally {
            if (!string.IsNullOrWhiteSpace(tempDllPath) && File.Exists(tempDllPath)) {
                File.Delete(tempDllPath);
            }
        }
    }

    private static string EnsureDllPathForDecompile(string dllPath, out string tempDllPath) {
        tempDllPath = null;
        if (dllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            return dllPath;
        }

        string tempFileName = Path.GetFileNameWithoutExtension(dllPath);
        if (!tempFileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            tempFileName += ".dll";
        }
        tempDllPath = Path.Combine(Path.GetDirectoryName(dllPath) ?? "", tempFileName);
        File.Copy(dllPath, tempDllPath, true);
        return tempDllPath;
    }

    private static string GetPackageSuffix(string packageName) {
        int splitIndex = packageName.IndexOf('-');
        return splitIndex >= 0 ? packageName.Substring(splitIndex + 1) : packageName;
    }

    private static string GetAssemblyBaseName(string fileName) {
        return fileName.EndsWith(".dll.old", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName.Substring(0, fileName.Length - ".old".Length))
            : Path.GetFileNameWithoutExtension(fileName);
    }

    private static string NormalizeForMatch(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return "";
        }

        StringBuilder builder = new(value.Length);
        foreach (char ch in value) {
            if (char.IsLetterOrDigit(ch)) {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }
        return builder.ToString();
    }

    private static void DecompileDll(CmdProcess cmd, string dllName, string sourceDir = null) {
        string dllPath = sourceDir != null
            ? $@"{sourceDir}\{dllName}"
            : $@"{NugetGameLibNet45Dir}\{dllName}";
        string dllNameNoExt = Path.GetFileNameWithoutExtension(dllName).Replace("-publicized", "");
        string dllBaseName = Path.GetFileNameWithoutExtension(dllName);
        string outputDir = Path.GetFullPath($@"{SolutionDir}\gamedata\DecompiledSource\{dllNameNoExt}");
        string csprojPath = Path.Combine(outputDir, $"{dllNameNoExt}.csproj");
        string publicizedCsprojPath = Path.Combine(outputDir, $"{dllBaseName}.csproj");
        if (!File.Exists(dllPath)) {
            Console.WriteLine($"未找到{dllPath}，跳过反编译");
            return;
        }
        if (Directory.Exists(outputDir)) {
            try {
                Directory.Delete(outputDir, true);
            }
            catch (Exception ex) {
                Console.WriteLine($"无法删除旧目录: {ex.Message}");
            }
        }
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"开始反编译 {dllName} -> {outputDir}");
        Console.WriteLine("注意：此过程可能耗时数分钟，请耐心等待...");

        try {
            int exitCode = cmd.Run("ilspycmd", $"-p --nested-directories -o \"{outputDir}\" \"{dllPath}\"");
            if (exitCode != 0) {
                Console.Error.WriteLine($"ilspycmd 退出，错误码: {exitCode}");
            }
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"执行 ilspycmd 失败: {ex.Message}");
            Console.WriteLine("请确保已安装 ilspycmd: dotnet tool install -g ilspycmd");
        }

        if (!File.Exists(csprojPath) && File.Exists(publicizedCsprojPath)) {
            try {
                File.Move(publicizedCsprojPath, csprojPath);
                Console.WriteLine($"已将 {Path.GetFileName(publicizedCsprojPath)} 重命名为 {Path.GetFileName(csprojPath)}");
            }
            catch (Exception ex) {
                Console.WriteLine($"重命名 csproj 失败: {ex.Message}");
            }
        }

        if (File.Exists(csprojPath)) {
            Console.WriteLine($"反编译完成：{outputDir}");
        } else {
            Console.Error.WriteLine($"反编译失败，未生成 {csprojPath}");
        }
    }

    private static void PublizeDll(CmdProcess cmd, string dllPath, string targetPath) {
        string actualSourcePath = dllPath;
        if (!File.Exists(actualSourcePath)) {
            if (File.Exists(dllPath + ".old")) {
                actualSourcePath = dllPath + ".old";
            } else {
                Console.WriteLine($"未找到 {dllPath} (且无 .old 备份)！");
                return;
            }
        }

        // 为了 publicize 能够产出预期的 -publicized.dll 名字，如果输入文件后缀不是 .dll，我们临时创建一个
        string workingDllPath = actualSourcePath;
        bool isTemporary = false;
        if (!actualSourcePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            workingDllPath = Path.Combine(Path.GetDirectoryName(actualSourcePath),
                Path.GetFileNameWithoutExtension(actualSourcePath));
            if (!workingDllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                workingDllPath += ".dll";
            }

            try {
                File.Copy(actualSourcePath, workingDllPath, true);
                isTemporary = true;
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"无法创建临时 DLL 文件: {ex.Message}");
                return;
            }
        }

        Console.WriteLine($"开始publicize {workingDllPath}");
        cmd.Run(PublicizerExe.FullName, $"\"{workingDllPath}\"", PublicizerExe.DirectoryName);
        string publicizedPath = workingDllPath.Replace(".dll", "-publicized.dll");
        if (!File.Exists(publicizedPath)) {
            Console.Error.WriteLine($"publicize 失败，未找到：{publicizedPath}");
            if (isTemporary) File.Delete(workingDllPath);
            return;
        }

        while (true) {
            try {
                File.Copy(publicizedPath, targetPath, true);
                Console.WriteLine($"复制 {publicizedPath} -> {targetPath}");
                break;
            }
            catch {
                Thread.Sleep(100);
            }
        }

        try {
            File.Delete(publicizedPath);
            if (isTemporary) File.Delete(workingDllPath);
        }
        catch { }
    }

    #endregion

    #region 提取计算器图标资源

    private static void ExportCalcIcons() {
        string dataDir = GetCalcIconDataDir(allowLocalFallback: true);
        if (dataDir == null) {
            Console.WriteLine("未找到计算器 raw 数据，也未找到本地 calc json；请先执行选项 3 生成数据。");
            return;
        }

        RebuildCalcIconsFromGame(dataDir, syncToCalcAssets: IsDspCalcProjectAvailable());
    }

    private static void RebuildCalcIconsFromGame(string dataDir, bool syncToCalcAssets) {
        Dictionary<string, string> requiredIconNames = CollectRequiredIconNames(dataDir);
        if (requiredIconNames.Count == 0) {
            Console.WriteLine($"未在计算器数据目录中读取到计算器实际需要的图标：{dataDir}");
            return;
        }

        bool cleanupWorkDir = false;
        PrepareCalcIconExportDirs();
        try {
            CopyStaticCalcIconFallbacks();

            Dictionary<string, MissingCalcIcon> missingIcons = CollectMissingRequiredIconsFromFull(dataDir);
            if (missingIcons.Count == 0) {
                if (syncToCalcAssets) {
                    SyncRequiredIconsToCalcAssets(dataDir);
                }
                Console.WriteLine("本地图标已覆盖所有计算器所需图标。");
                cleanupWorkDir = true;
                return;
            }

            Console.WriteLine($"开始启动游戏提取 {missingIcons.Count} 个缺失图标。");
            foreach (MissingCalcIcon missingIcon in missingIcons.Values) {
                Console.WriteLine($"缺少图标：{missingIcon.IconName}（{missingIcon.DataFiles.Count} 个数据文件；{string.Join("; ", missingIcon.Examples)}）");
            }

            ExportMissingCalcIconsFromGame(dataDir, missingIcons);
            if (syncToCalcAssets) {
                SyncRequiredIconsToCalcAssets(dataDir);
            } else {
                Console.WriteLine("未检测到计算器项目，跳过同步图标到计算器。");
            }
            missingIcons = CollectMissingRequiredIconsFromFull(dataDir);
            Console.WriteLine($"图标提取结束，剩余缺图：{missingIcons.Count}");
            foreach (MissingCalcIcon missingIcon in missingIcons.Values) {
                Console.WriteLine($"仍缺图标：{missingIcon.IconName}（{missingIcon.DataFiles.Count} 个数据文件；{string.Join("; ", missingIcon.Examples)}）");
            }
            cleanupWorkDir = missingIcons.Count == 0;
        }
        finally {
            if (cleanupWorkDir) {
                DeleteDirectoryIfExists(CalcIconWorkDir);
                Console.WriteLine($"已清理图标临时目录：{CalcIconWorkDir}");
            } else if (Directory.Exists(CalcIconWorkDir)) {
                Console.WriteLine($"图标流程未完全成功，保留临时目录用于排查：{CalcIconWorkDir}");
            }
        }
    }

    private static string GetCalcIconDataDir(bool allowLocalFallback) {
        if (IsDspCalcProjectAvailable()) {
            return DspCalcRawDataDir;
        }

        Console.WriteLine($"未检测到完整计算器项目：{DspCalcDir}");
        if (allowLocalFallback && Directory.Exists(CalcJsonLocalDir)) {
            Console.WriteLine($"改用本地 calc json 作为图标需求来源：{CalcJsonLocalDir}");
            return CalcJsonLocalDir;
        }

        return null;
    }

    private static bool IsDspCalcProjectAvailable() {
        return File.Exists(Path.Combine(DspCalcDir, "package.json"))
               && Directory.Exists(DspCalcRawDataDir);
    }

    private static void PrepareCalcIconExportDirs() {
        DeleteDirectoryIfExists(CalcIconWorkDir);
        DeleteDirectoryIfExists(DspCalcFullIconDir);
        Directory.CreateDirectory(DspCalcFullIconDir);
        Directory.CreateDirectory(CalcIconWorkDir);
    }

    private static void ExportCalcIconsOffline(IReadOnlyDictionary<string, string> requiredIconNames) {
        using CmdProcess cmd = new();
        string assetStudioCli = EnsureAssetStudioCli();
        foreach (CalcIconExportTarget target in GetCalcIconExportTargets()) {
            ExportCalcIconsWithAssetStudio(cmd, assetStudioCli, target, requiredIconNames);
            ExportCalcIconsFromEmbeddedDll(cmd, target, requiredIconNames);
        }
    }

    private static string EnsureAssetStudioCli() {
        if (File.Exists(AssetStudioCliPath)) {
            return AssetStudioCliPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(AssetStudioZipPath) ?? ".");
        Console.WriteLine($"下载 AssetStudio CLI：{AssetStudioDownloadUrl}");
        DownloadAssetStudioZip();

        Console.WriteLine($"解压 AssetStudio CLI：{AssetStudioToolDir}");
        Directory.CreateDirectory(AssetStudioToolDir);
        new FastZip().ExtractZip(AssetStudioZipPath, AssetStudioToolDir, null);
        if (!File.Exists(AssetStudioCliPath)) {
            throw new FileNotFoundException("AssetStudio CLI 解压后未找到可执行文件", AssetStudioCliPath);
        }

        return AssetStudioCliPath;
    }

    private static void DownloadAssetStudioZip() {
        using Process process = Process.Start(new ProcessStartInfo("curl.exe",
                   $"-L --retry 3 --connect-timeout 20 --max-time 300 --fail -o \"{AssetStudioZipPath}\" \"{AssetStudioDownloadUrl}\"") {
                   UseShellExecute = false,
                   CreateNoWindow = true,
               });
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(AssetStudioZipPath)) {
            throw new InvalidOperationException($"curl.exe 下载 AssetStudio 失败，错误码 {process.ExitCode}");
        }
    }

    private static void ExportCalcIconsWithAssetStudio(
        CmdProcess cmd,
        string assetStudioCli,
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames) {
        List<string> inputPaths = GetAssetStudioInputPaths(target).Distinct().ToList();
        if (inputPaths.Count == 0) {
            return;
        }

        int totalCopied = 0;
        foreach (string inputPath in inputPaths) {
            List<string> nameFilters = GetAssetStudioNameFilters(target, requiredIconNames);
            for (int i = 0; i < nameFilters.Count; i++) {
                string outputDir = Path.Combine(
                    CalcIconWorkDir,
                    "assetstudio",
                    target.TargetMod,
                    MakeSafePathSegment(Path.GetFileName(inputPath)),
                    $"batch-{i + 1}");
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"开始离线提取 Texture2D：{target.TargetMod} <- {inputPath}");
                string arguments =
                    $"\"{inputPath}\" \"{outputDir}\" --game Normal --types Texture2D --image_format Png --group_assets ByType";
                if (!string.IsNullOrWhiteSpace(nameFilters[i])) {
                    arguments += $" --names \"{nameFilters[i]}\"";
                }

                int exitCode = cmd.Run(assetStudioCli, arguments, Path.GetDirectoryName(assetStudioCli));
                if (exitCode != 0) {
                    Console.WriteLine($"AssetStudio 提取失败：{target.TargetMod}，错误码 {exitCode}");
                    continue;
                }

                totalCopied += CopyRequiredPngIcons(outputDir, target, requiredIconNames, require80x80: true);
            }
        }

        if (totalCopied > 0) {
            Console.WriteLine($"{target.TargetMod} AssetStudio 离线图标：复制 {totalCopied}");
        }
    }

    private static List<string> GetAssetStudioNameFilters(
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames) {
        // 原版资源会连带扫描 sharedassets，必须按名字过滤，避免每轮导出数千张无关贴图。
        if (!target.TargetMod.Equals("Vanilla", StringComparison.OrdinalIgnoreCase)) {
            return [""];
        }

        const int maxPatternLength = 3000;
        List<string> result = [];
        List<string> currentNames = [];
        int currentLength = 4;
        foreach (string iconName in requiredIconNames.Values
                     .Select(Regex.Escape)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) {
            int nextLength = currentLength + iconName.Length + 1;
            if (currentNames.Count > 0 && nextLength > maxPatternLength) {
                result.Add($"^({string.Join("|", currentNames)})$");
                currentNames.Clear();
                currentLength = 4;
            }

            currentNames.Add(iconName);
            currentLength += iconName.Length + 1;
        }

        if (currentNames.Count > 0) {
            result.Add($"^({string.Join("|", currentNames)})$");
        }
        return result.Count == 0 ? [""] : result;
    }

    private static IEnumerable<string> GetAssetStudioInputPaths(CalcIconExportTarget target) {
        foreach (string relativePath in target.AssetStudioGameDataRelativePaths) {
            string inputPath = ResolveExistingPathRespectingOld(Path.Combine(DSPGameDir, relativePath));
            if (inputPath != null) {
                yield return inputPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(target.AssetStudioPackageName)) {
            string pluginDir = Path.Combine(R2PluginsDir, target.AssetStudioPackageName);
            foreach (string relativePath in target.AssetStudioRelativePaths) {
                string inputPath = ResolveExistingPathRespectingOld(Path.Combine(pluginDir, relativePath));
                if (inputPath != null) {
                    yield return inputPath;
                }
            }
        }
    }

    private static void ExportCalcIconsFromEmbeddedDll(
        CmdProcess cmd,
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames) {
        if (string.IsNullOrWhiteSpace(target.EmbeddedDllPackageName)
            || string.IsNullOrWhiteSpace(target.EmbeddedDllRelativePath)) {
            return;
        }

        string sourceDll = ResolveExistingPathRespectingOld(
            Path.Combine(R2PluginsDir, target.EmbeddedDllPackageName, target.EmbeddedDllRelativePath));
        if (sourceDll == null) {
            Console.WriteLine($"未找到 {target.TargetMod} embedded PNG DLL：{Path.Combine(R2PluginsDir, target.EmbeddedDllPackageName, target.EmbeddedDllRelativePath)}");
            return;
        }

        string workDll = PrepareDllPathForTool(sourceDll, target.TargetMod);
        string outputDir = Path.Combine(CalcIconWorkDir, "ilspy", target.TargetMod);
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"开始离线提取 embedded PNG：{target.TargetMod} <- {sourceDll}");
        int exitCode = cmd.Run("ilspycmd", $"-p --nested-directories -o \"{outputDir}\" \"{workDll}\"");
        if (exitCode != 0) {
            Console.WriteLine($"ilspycmd 提取 embedded PNG 失败：{target.TargetMod}，错误码 {exitCode}");
            return;
        }

        int copied = CopyRequiredPngIcons(outputDir, target, requiredIconNames, require80x80: true);
        Console.WriteLine($"{target.TargetMod} embedded PNG 离线图标：复制 {copied}");
    }

    private static string PrepareDllPathForTool(string sourceDll, string targetMod) {
        if (sourceDll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            return sourceDll;
        }

        string outputDir = Path.Combine(CalcIconWorkDir, "dll-input", targetMod);
        Directory.CreateDirectory(outputDir);
        string fileName = Path.GetFileName(sourceDll);
        if (fileName.EndsWith(".old", StringComparison.OrdinalIgnoreCase)) {
            fileName = fileName.Substring(0, fileName.Length - ".old".Length);
        }
        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            fileName += ".dll";
        }

        string targetDll = Path.Combine(outputDir, fileName);
        File.Copy(sourceDll, targetDll, true);
        return targetDll;
    }

    private static string ResolveExistingPathRespectingOld(string basePath) {
        if (File.Exists(basePath)) {
            return basePath;
        }

        string oldPath = basePath + ".old";
        if (File.Exists(oldPath)) {
            return oldPath;
        }

        return null;
    }

    private static int CopyRequiredPngIcons(
        string sourceDir,
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, string> requiredIconNames,
        bool require80x80) {
        string outputDir = Path.Combine(DspCalcFullIconDir, target.TargetMod);
        Directory.CreateDirectory(outputDir);

        int copied = 0;
        int skippedSize = 0;
        int skippedExisting = 0;
        foreach (string sourceFile in Directory.GetFiles(sourceDir, "*.png", SearchOption.AllDirectories)) {
            if (require80x80 && !IsPng80x80(sourceFile)) {
                skippedSize++;
                continue;
            }

            string sourceIconName = GetSourceIconName(sourceFile, target.SourcePrefix);
            string key = NormalizeIconNameForMatch(sourceIconName);
            if (!requiredIconNames.TryGetValue(key, out string requiredIconName)) {
                continue;
            }

            string targetFile = Path.Combine(outputDir, $"{SanitizeFileName(requiredIconName)}.png");
            if (File.Exists(targetFile)) {
                skippedExisting++;
                continue;
            }

            File.Copy(sourceFile, targetFile, false);
            copied++;
        }

        if (skippedSize > 0 || skippedExisting > 0) {
            Console.WriteLine($"{target.TargetMod} 离线图标跳过：非 80x80 {skippedSize}，已有 {skippedExisting}");
        }
        return copied;
    }

    private static bool IsPng80x80(string filePath) {
        byte[] header = new byte[24];
        using FileStream stream = File.OpenRead(filePath);
        if (stream.Read(header, 0, header.Length) < header.Length) {
            return false;
        }

        return header[0] == 0x89
            && header[1] == 0x50
            && header[2] == 0x4E
            && header[3] == 0x47
            && ReadBigEndianInt32(header, 16) == 80
            && ReadBigEndianInt32(header, 20) == 80;
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset) {
        return (bytes[offset] << 24)
            | (bytes[offset + 1] << 16)
            | (bytes[offset + 2] << 8)
            | bytes[offset + 3];
    }

    private static void DeleteDirectoryIfExists(string dir) {
        if (!Directory.Exists(dir)) {
            return;
        }

        Directory.Delete(dir, true);
    }

    private static string MakeSafePathSegment(string value) {
        foreach (char invalidChar in Path.GetInvalidFileNameChars()) {
            value = value.Replace(invalidChar, '_');
        }
        return string.IsNullOrWhiteSpace(value) ? "input" : value;
    }

    private static void CopyStaticCalcIconFallbacks() {
        CopyStaticCalcIconFallback("Vanilla", "伊卡洛斯", Path.Combine(DspCalcIconAssetsDir, "Vanilla", "伊卡洛斯.png"));
        CopyStaticCalcIconFallback("Vanilla", "行星基地", Path.Combine(DspCalcIconAssetsDir, "Vanilla", "行星基地.png"));
        CopyStaticCalcIconFallback("Vanilla", "巨构星际组装厂", Path.Combine(DspCalcIconAssetsDir, "Vanilla", "巨构星际组装厂.png"));
    }

    private static void CopyStaticCalcIconFallback(string targetMod, string iconName, string sourceFile) {
        if (!File.Exists(sourceFile)) {
            return;
        }

        string outputDir = Path.Combine(DspCalcFullIconDir, targetMod);
        Directory.CreateDirectory(outputDir);
        string targetFile = Path.Combine(outputDir, $"{SanitizeFileName(iconName)}.png");
        if (File.Exists(targetFile) && new FileInfo(targetFile).Length == new FileInfo(sourceFile).Length) {
            return;
        }

        File.Copy(sourceFile, targetFile, true);
        Console.WriteLine($"同步静态兜底图标：{targetMod}/{iconName}");
    }

    private static List<CalcIconExportTarget> GetCalcIconExportTargets() {
        return [
            new() {
                TargetMod = "Vanilla",
                AssetStudioGameDataRelativePaths = [
                    @"DSPGAME_Data\resources.assets",
                    @"DSPGAME_Data\sharedassets0.assets",
                ],
                EnabledMods = [],
                LowerPriorityMods = [],
            },
            new() {
                TargetMod = "MoreMegaStructure",
                AssetStudioPackageName = "jinxOAO-MoreMegaStructure",
                AssetStudioRelativePaths = ["mmstabicon"],
                EnabledMods = ["jinxOAO-MoreMegaStructure"],
                LowerPriorityMods = ["Vanilla"],
            },
            new() {
                TargetMod = "TheyComeFromVoid",
                AssetStudioPackageName = "ckcz123-TheyComeFromVoid",
                AssetStudioRelativePaths = ["dspbattletex"],
                EnabledMods = ["jinxOAO-MoreMegaStructure", "ckcz123-TheyComeFromVoid"],
                LowerPriorityMods = ["Vanilla", "MoreMegaStructure"],
            },
            new() {
                TargetMod = "GenesisBook",
                AssetStudioPackageName = "HiddenCirno-GenesisBook",
                EmbeddedDllPackageName = "HiddenCirno-GenesisBook",
                EmbeddedDllRelativePath = "ProjectGenesis.dll",
                SourcePrefix = "ProjectGenesis.assets.sprite.",
                EnabledMods = ["jinxOAO-MoreMegaStructure", "HiddenCirno-GenesisBook"],
                LowerPriorityMods = ["Vanilla", "MoreMegaStructure"],
            },
            new() {
                TargetMod = "OrbitalRing",
                AssetStudioPackageName = "ProfessorCat-OrbitalRing",
                EmbeddedDllPackageName = "ProfessorCat-OrbitalRing",
                EmbeddedDllRelativePath = "ProjectOrbitalRing.dll",
                SourcePrefix = "ProjectOrbitalRing.assets.sprite.",
                EnabledMods = ["jinxOAO-MoreMegaStructure", "ProfessorCat-OrbitalRing"],
                LowerPriorityMods = ["Vanilla", "MoreMegaStructure"],
            },
            new() {
                TargetMod = "FractionateEverything",
                AssetStudioPackageName = "MengLei-FractionateEverything",
                AssetStudioRelativePaths = ["fe"],
                EnabledMods = ["MengLei-FractionateEverything"],
                LowerPriorityMods = ["Vanilla"],
            },
        ];
    }

    private static Dictionary<string, string> CollectRequiredIconNames(string dataDir) {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dataDir)) {
            return result;
        }

        foreach (string jsonFile in Directory.GetFiles(dataDir, "*.json")) {
            JObject root;
            try {
                root = JObject.Parse(File.ReadAllText(jsonFile));
            }
            catch (Exception ex) {
                Console.WriteLine($"读取计算器 json 失败：{jsonFile}，{ex.Message}");
                continue;
            }

            foreach ((string iconName, _) in EnumerateRequiredCalcIcons(root)) {
                if (string.IsNullOrWhiteSpace(iconName)) {
                    continue;
                }

                string key = NormalizeIconNameForMatch(iconName);
                if (!result.ContainsKey(key)) {
                    result.Add(key, iconName);
                }
            }
        }

        return result;
    }

    private static IEnumerable<(string IconName, string ItemName)> EnumerateRequiredCalcIcons(JObject root) {
        Dictionary<int, JObject> itemById = [];
        foreach (JObject item in (root["items"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()) {
            int? id = item.Value<int?>("ID");
            if (id.HasValue && !itemById.ContainsKey(id.Value)) {
                itemById.Add(id.Value, item);
            }
        }

        HashSet<int> requiredItemIds = [];
        foreach (JObject recipe in (root["recipes"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>()) {
            AddIds(requiredItemIds, recipe["Items"]);
            AddIds(requiredItemIds, recipe["Results"]);
            AddIds(requiredItemIds, recipe["Factories"]);
        }

        foreach (int id in requiredItemIds) {
            if (!itemById.TryGetValue(id, out JObject item)) {
                continue;
            }

            string iconName = item.Value<string>("IconName");
            if (string.IsNullOrWhiteSpace(iconName)) {
                continue;
            }

            yield return (iconName, item.Value<string>("Name") ?? id.ToString());
        }
    }

    private static void AddIds(HashSet<int> target, JToken token) {
        if (token is not JArray array) {
            return;
        }

        foreach (JToken item in array) {
            int? id = item.Value<int?>();
            if (id.HasValue) {
                target.Add(id.Value);
            }
        }
    }

    private static void ExportMissingCalcIconsFromGame(string dataDir, Dictionary<string, MissingCalcIcon> missingIcons) {
        using CmdProcess cmd = new();
        LoadModInfos();
        ModInfo getDspData = GetModInfo("MengLei-GetDspData");
        ModInfo errorAnalyzer = GetModInfo("starfi5h-ErrorAnalyzer");
        if (getDspData == null || errorAnalyzer == null) {
            Console.WriteLine("未找到 MengLei-GetDspData 或 starfi5h-ErrorAnalyzer，无法启动游戏兜底提取图标！");
            return;
        }

        KillDspGameForIconExportSync();
        SyncGetDspDataToR2ForIconExport();
        PrepareR2Doorstop();
        HashSet<string> handledTargets = new(StringComparer.OrdinalIgnoreCase);
        try {
            while (missingIcons.Count > 0) {
                CalcIconExportTarget target = GetNextMissingIconTarget(missingIcons, handledTargets);
                if (target == null) {
                    break;
                }

                handledTargets.Add(target.TargetMod);
                if (!TryBuildIconExportModList(target, getDspData, errorAnalyzer, out List<string> enabledModNames)) {
                    continue;
                }

                Console.WriteLine($"开始启动游戏提取 {target.TargetMod} 图标...");
                WriteIconExportRequest(target, missingIcons);
                if (File.Exists(IconExportMarkerPath)) {
                    File.Delete(IconExportMarkerPath);
                }

                cmd.Exec(KillDSP);
                OnlyEnableInputMods(enabledModNames);
                cmd.Exec(RunDSP);
                if (!WaitForFile(IconExportMarkerPath, TimeSpan.FromMinutes(5))) {
                    Console.WriteLine($"等待 {target.TargetMod} 图标导出超时，跳过。");
                    continue;
                }

                Console.WriteLine(File.ReadAllText(IconExportMarkerPath));
                missingIcons = CollectMissingRequiredIconsFromFull(dataDir);
            }
        }
        finally {
            if (File.Exists(IconExportRequestPath)) {
                File.Delete(IconExportRequestPath);
            }
            EnableModsByConfig();
            cmd.Exec(KillDSP);
        }
    }

    private static void SyncGetDspDataToR2ForIconExport() {
#if DEBUG
        string configuration = "Debug";
#else
        string configuration = "Release";
#endif
        string sourceDll = Path.Combine(SolutionFullDir, "GetDspData", "bin", "win", configuration, "GetDspData.dll");
        string sourceJsonDll = Path.Combine(SolutionFullDir, "lib", "Newtonsoft.Json.dll");
        CopyToR2RespectingOld(sourceDll, Path.Combine(R2PluginsDir, "MengLei-GetDspData", "GetDspData.dll"));
        CopyToR2RespectingOld(sourceJsonDll, Path.Combine(R2PluginsDir, "MengLei-GetDspData", "Newtonsoft.Json.dll"), false);
        Console.WriteLine("已同步图标导出用 GetDspData 到 R2");
    }

    private static void KillDspGameForIconExportSync() {
        try {
            using Process process = Process.Start(new ProcessStartInfo("taskkill.exe", "/F /IM DSPGAME.exe") {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process?.WaitForExit(5000);
        }
        catch {
        }
    }

    private static CalcIconExportTarget GetNextMissingIconTarget(
        Dictionary<string, MissingCalcIcon> missingIcons,
        HashSet<string> handledTargets) {
        HashSet<string> candidateTargets = new(StringComparer.OrdinalIgnoreCase);
        foreach (MissingCalcIcon missingIcon in missingIcons.Values) {
            candidateTargets.UnionWith(missingIcon.CandidateTargets);
        }

        return GetCalcIconExportTargets()
            .FirstOrDefault(target =>
                candidateTargets.Contains(target.TargetMod) && !handledTargets.Contains(target.TargetMod));
    }

    private static bool TryBuildIconExportModList(
        CalcIconExportTarget target,
        ModInfo getDspData,
        ModInfo errorAnalyzer,
        out List<string> enabledModNames) {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        AddModAndDependencies(names, getDspData.name);
        AddModAndDependencies(names, errorAnalyzer.name);

        foreach (string modName in target.EnabledMods) {
            ModInfo modInfo = GetModInfo(modName);
            if (modInfo == null) {
                Console.WriteLine($"mods.yml 中未找到模组信息：{modName}，跳过 {target.TargetMod} 图标提取。");
                enabledModNames = [];
                return false;
            }

            AddModAndDependencies(names, modInfo.name);
        }

        enabledModNames = names.ToList();
        return true;
    }

    private static void AddModAndDependencies(HashSet<string> names, string modName) {
        names.Add(modName);
        foreach (string dependency in GetDependencies(modName)) {
            names.Add(dependency);
        }
    }

    private static void WriteIconExportRequest(
        CalcIconExportTarget target,
        IReadOnlyDictionary<string, MissingCalcIcon> missingIcons) {
        JArray requestedIconNames = new();
        foreach (MissingCalcIcon missingIcon in missingIcons.Values
                     .Where(missingIcon => missingIcon.CandidateTargets.Contains(target.TargetMod))) {
            requestedIconNames.Add(missingIcon.IconName);
        }

        JArray existingIconDirs = new() {
            Path.Combine(DspCalcFullIconDir, target.TargetMod),
        };
        foreach (string lowerPriorityMod in target.LowerPriorityMods) {
            existingIconDirs.Add(Path.Combine(DspCalcFullIconDir, lowerPriorityMod));
        }

        JObject request = new() {
            { "TargetMod", target.TargetMod },
            { "OutputDir", Path.Combine(DspCalcFullIconDir, target.TargetMod) },
            { "LowerPriorityDirs", existingIconDirs },
            { "IconNames", requestedIconNames },
            { "MarkerPath", IconExportMarkerPath },
        };
        Directory.CreateDirectory(Path.GetDirectoryName(IconExportRequestPath) ?? ".");
        File.WriteAllText(IconExportRequestPath, request.ToString(), Encoding.UTF8);
    }

    private static bool WaitForFile(string filePath, TimeSpan timeout) {
        DateTime deadline = DateTime.Now + timeout;
        while (DateTime.Now < deadline) {
            if (File.Exists(filePath)) {
                return true;
            }
            Thread.Sleep(500);
        }
        return false;
    }

    private static void SyncRequiredIconsToCalcAssets(string dataDir) {
        if (!IsDspCalcProjectAvailable()) {
            Console.WriteLine("未检测到完整计算器项目，跳过同步图标到计算器。");
            return;
        }

        Dictionary<string, Dictionary<string, string>> requiredIconCopies = BuildRequiredCalcIconAssetCopies(dataDir);
        int removedStale = RemoveStaleCalcIconAssets(requiredIconCopies);
        int copied = 0;
        int skippedSameSize = 0;
        foreach (KeyValuePair<string, Dictionary<string, string>> modPair in requiredIconCopies) {
            string sourceMod = modPair.Key;
            Dictionary<string, string> iconFiles = modPair.Value;
            string outputDir = Path.Combine(DspCalcIconAssetsDir, sourceMod);
            Directory.CreateDirectory(outputDir);
            foreach (KeyValuePair<string, string> iconPair in iconFiles) {
                string iconName = iconPair.Key;
                string sourceFile = iconPair.Value;
                string targetFile = Path.Combine(outputDir, $"{SanitizeFileName(iconName)}.png");
                if (File.Exists(targetFile) && new FileInfo(targetFile).Length == new FileInfo(sourceFile).Length) {
                    skippedSameSize++;
                    continue;
                }

                File.Copy(sourceFile, targetFile, true);
                copied++;
            }
        }

        int requiredCount = requiredIconCopies.Values.Sum(iconFiles => iconFiles.Count);
        Console.WriteLine($"同步计算器所需图标：需要 {requiredCount}，复制 {copied}，已有同尺寸 {skippedSameSize}，删除多余 {removedStale}");
    }

    private static int RemoveStaleCalcIconAssets(Dictionary<string, Dictionary<string, string>> requiredIconCopies) {
        if (!Directory.Exists(DspCalcIconAssetsDir)) {
            return 0;
        }

        HashSet<string> requiredFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Dictionary<string, string>> modPair in requiredIconCopies) {
            string sourceMod = modPair.Key;
            foreach (string iconName in modPair.Value.Keys) {
                requiredFiles.Add(Path.GetFullPath(Path.Combine(DspCalcIconAssetsDir, sourceMod, $"{SanitizeFileName(iconName)}.png")));
            }
        }

        int removed = 0;
        foreach (string iconFile in Directory.GetFiles(DspCalcIconAssetsDir, "*.png", SearchOption.AllDirectories)) {
            if (requiredFiles.Contains(Path.GetFullPath(iconFile))) {
                continue;
            }

            File.Delete(iconFile);
            removed++;
        }
        return removed;
    }

    private static Dictionary<string, Dictionary<string, string>> BuildRequiredCalcIconAssetCopies(string dataDir) {
        Dictionary<string, Dictionary<string, string>> fullIconIndex = BuildIconFileIndex(DspCalcFullIconDir);
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dataDir)) {
            return result;
        }

        foreach (string jsonFile in Directory.GetFiles(dataDir, "*.json")) {
            JObject root = JObject.Parse(File.ReadAllText(jsonFile));
            List<string> lookupOrder = GetIconLookupOrder(Path.GetFileName(jsonFile));
            foreach ((string iconName, _) in EnumerateRequiredCalcIcons(root)) {
                if (string.IsNullOrWhiteSpace(iconName)) {
                    continue;
                }

                string sourceMod = lookupOrder.FirstOrDefault(modName =>
                    fullIconIndex.TryGetValue(modName, out Dictionary<string, string> iconFiles)
                    && iconFiles.ContainsKey(SanitizeFileName(iconName)));
                if (sourceMod == null) {
                    continue;
                }

                if (!result.TryGetValue(sourceMod, out Dictionary<string, string> requiredIconFiles)) {
                    requiredIconFiles = new(StringComparer.OrdinalIgnoreCase);
                    result.Add(sourceMod, requiredIconFiles);
                }
                requiredIconFiles[iconName] = fullIconIndex[sourceMod][SanitizeFileName(iconName)];
            }
        }

        return result;
    }

    private static Dictionary<string, MissingCalcIcon> CollectMissingRequiredIconsFromFull(string dataDir) {
        Dictionary<string, Dictionary<string, string>> fullIconIndex = BuildIconFileIndex(DspCalcFullIconDir);
        Dictionary<string, MissingCalcIcon> result = new(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dataDir)) {
            return result;
        }

        foreach (string jsonFile in Directory.GetFiles(dataDir, "*.json")) {
            JObject root;
            try {
                root = JObject.Parse(File.ReadAllText(jsonFile));
            }
            catch (Exception ex) {
                Console.WriteLine($"读取计算器 json 失败：{jsonFile}，{ex.Message}");
                continue;
            }

            string fileName = Path.GetFileName(jsonFile);
            List<string> lookupOrder = GetIconLookupOrder(fileName);
            List<string> candidateTargets = GetCandidateTargets(fileName);
            foreach ((string iconName, string itemName) in EnumerateRequiredCalcIcons(root)) {
                if (string.IsNullOrWhiteSpace(iconName)) {
                    continue;
                }

                string sanitizedIconName = SanitizeFileName(iconName);
                bool found = lookupOrder.Any(modName =>
                    fullIconIndex.TryGetValue(modName, out Dictionary<string, string> iconFiles)
                    && iconFiles.ContainsKey(sanitizedIconName));
                if (found) {
                    continue;
                }

                if (!result.TryGetValue(iconName, out MissingCalcIcon missingIcon)) {
                    missingIcon = new() {
                        IconName = iconName,
                    };
                    result.Add(iconName, missingIcon);
                }
                missingIcon.CandidateTargets.UnionWith(candidateTargets);
                missingIcon.DataFiles.Add(fileName);
                if (missingIcon.Examples.Count < 3) {
                    missingIcon.Examples.Add($"{fileName}: {itemName}");
                }
            }
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> BuildIconFileIndex(string sourceDir) {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (CalcIconExportTarget target in GetCalcIconExportTargets()) {
            string modDir = Path.Combine(sourceDir, target.TargetMod);
            Dictionary<string, string> iconFiles = new(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(modDir)) {
                foreach (string filePath in Directory.GetFiles(modDir, "*.png")) {
                    iconFiles[Path.GetFileNameWithoutExtension(filePath)] = filePath;
                }
            }
            result[target.TargetMod] = iconFiles;
        }
        return result;
    }

    private static List<string> GetIconLookupOrder(string jsonFileName) {
        List<string> enabledMods = GetEnabledCalcModsFromFileName(jsonFileName);
        enabledMods.Reverse();
        enabledMods.Add("Vanilla");
        return enabledMods;
    }

    private static List<string> GetCandidateTargets(string jsonFileName) {
        List<string> enabledMods = GetEnabledCalcModsFromFileName(jsonFileName);
        return enabledMods.Count == 0 ? ["Vanilla"] : enabledMods;
    }

    private static List<string> GetEnabledCalcModsFromFileName(string jsonFileName) {
        return GetCalcIconExportTargets()
            .Select(target => target.TargetMod)
            .Where(modName => modName != "Vanilla" && jsonFileName.Contains(modName))
            .ToList();
    }

    private static string GetSourceIconName(string sourceFile, string sourcePrefix) {
        string fileName = Path.GetFileNameWithoutExtension(sourceFile);
        if (fileName.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)) {
            return fileName.Substring(sourcePrefix.Length);
        }
        return fileName;
    }

    private static string NormalizeIconNameForMatch(string iconName) {
        StringBuilder builder = new(iconName.Length);
        foreach (char ch in iconName) {
            if (char.IsLetterOrDigit(ch)) {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }
        return builder.ToString();
    }

    private static string SanitizeFileName(string fileName) {
        foreach (char invalidChar in Path.GetInvalidFileNameChars()) {
            fileName = fileName.Replace(invalidChar, '_');
        }
        return fileName.Trim();
    }

    #endregion

    #region 生成戴森球量化计算器所需文件，并将其复制到计算器项目目录下

    private static void GetAllCalcJson() {
        using CmdProcess cmd = new();
        bool calcProjectAvailable = IsDspCalcProjectAvailable();
        //终止游戏
        cmd.Exec(KillDSP);
        if (calcProjectAvailable) {
            DeleteExistingCalcJsonFiles();
        } else {
            Console.WriteLine($"未检测到完整计算器项目：{DspCalcDir}");
            Console.WriteLine("本次只生成 C# 本地 gamedata/calc json，跳过复制到计算器和图标同步。");
        }
        PrepareR2Doorstop();
        SyncCalcJsonExportModsToR2();
        //判断所有mod是否均已存在
        List<string> names = [
            "jinxOAO-MoreMegaStructure",//mod a：更多巨构
            "ckcz123-TheyComeFromVoid",//mod b：深空来敌
            "HiddenCirno-GenesisBook",//mod c：创世之书
            "ProfessorCat-OrbitalRing",//mod d：星环
            "MengLei-FractionateEverything",//mod e：万物分馏
        ];
        foreach (string name in names) {
            string modPluginsDir = $@"{R2ProfileDir}\BepInEx\plugins\{name}";
            if (!Directory.Exists(modPluginsDir)) {
                Console.WriteLine($"未找到 {modPluginsDir}，无法生成计算器所需文件！");
                return;
            }
        }
        //载入Mod数据，然后构建ModInfo数组
        LoadModInfos();
        ModInfo[] modInfos = names.Select(GetModInfo).ToArray();
        for (int i = 0; i < modInfos.Length; i++) {
            if (modInfos[i] == null) {
                Console.WriteLine($"mods.yml 中未找到模组信息：{names[i]}，无法生成计算器所需文件！");
                return;
            }
        }
        ModInfo getDspData = GetModInfo("MengLei-GetDspData");
        ModInfo errorAnalyzer = GetModInfo("starfi5h-ErrorAnalyzer");
        if (getDspData == null || errorAnalyzer == null) {
            Console.WriteLine("未找到 MengLei-GetDspData 或 starfi5h-ErrorAnalyzer，无法生成计算器所需文件！");
            return;
        }
        //生成计算器json
        for (int r = 0; r <= modInfos.Length; r++) {
            List<List<ModInfo>> result = Combinations(modInfos, r);
            for (int index = 0; index < result.Count; index++) {
                List<ModInfo> state = result[index];
                //巨构是深空的前置依赖
                if (!state.Contains(modInfos[0]) && state.Contains(modInfos[1])) {
                    continue;
                }
                //创世、星环只能启用一个
                if (state.Contains(modInfos[2]) && state.Contains(modInfos[3])) {
                    continue;
                }
                //开始准备json相关内容
                string oriFilePath = GetJsonFilePath(state, false);
                string calcFilePath = GetJsonFilePath(state, true);
                List<ModInfo> gameState = state.ToList();
                List<ModInfo> exportState = BuildCalcJsonExportState(gameState, getDspData, errorAnalyzer);
                if (!IsCalcJsonCacheValid(oriFilePath, exportState, out string cacheReason)) {
                    Console.WriteLine($"缓存失效：{Path.GetFileName(oriFilePath)}，{cacheReason}");
                    DeleteCalcJsonCache(oriFilePath);
                    Console.WriteLine("终止游戏进程...");
                    cmd.Exec(KillDSP);
                    //仅启用指定的模组
                    HashSet<string> nameList = [];
                    foreach (ModInfo modInfo in exportState) {
                        nameList.Add(modInfo.name);
                        List<string> dependencies = GetDependencies(modInfo.name);
                        foreach (string dependency in dependencies) {
                            nameList.Add(dependency);
                        }
                    }
                    OnlyEnableInputMods(nameList.ToList());
                    StringBuilder sb = new("启动游戏，mod情况：");
                    for (int i = 0; i < modInfos.Length; i++) {
                        sb.Append(modInfos[i].displayName).Append(gameState.Contains(modInfos[i]) ? "启用 " : "禁用 ");
                    }
                    Console.WriteLine(sb.ToString());
                    cmd.Exec(RunDSP);
                    while (!File.Exists(oriFilePath)) {
                        Thread.Sleep(100);
                    }
                    //多等一会，确保文件已经全部写入
                    Thread.Sleep(500);
                    WriteCalcJsonCacheMeta(oriFilePath, exportState);
                } else {
                    Console.WriteLine($"复用缓存：{oriFilePath}");
                }
                Console.WriteLine($"已生成 {oriFilePath}");
                if (!calcProjectAvailable) {
                    continue;
                }
                DirectoryInfo info = new FileInfo(calcFilePath).Directory;
                if (info == null || !info.Exists) {
                    Console.WriteLine("未检测到戴森球计算器项目对应的文件夹，跳过复制");
                    continue;
                }
                //这里必须删除目标文件，再复制，因为windows忽略大小写，有可能导致名称有问题
                File.Delete(calcFilePath);
                File.Copy(oriFilePath, calcFilePath, true);
                if (!File.Exists(calcFilePath)
                    || new FileInfo(calcFilePath).Length != new FileInfo(oriFilePath).Length) {
                    Console.WriteLine("复制计算器json文件失败");
                    continue;
                }
                Console.WriteLine($"已复制到 {calcFilePath}");
            }
        }
        if (calcProjectAvailable) {
            RebuildCalcIconsFromGame(DspCalcRawDataDir, syncToCalcAssets: true);
        }
        //启用R2配置文件中所有enable为true的mod
        EnableModsByConfig();
        //终止游戏
        cmd.Exec(KillDSP);
    }

    private static void SyncCalcJsonExportModsToR2() {
#if DEBUG
        string configuration = "Debug";
#else
        string configuration = "Release";
#endif
        SyncProjectFileToR2("FractionateEverything", configuration, "FractionateEverything.dll");
        SyncProjectFileToR2("FractionateEverything", configuration, "FractionateEverything.dll.mdb", false);
        SyncProjectFileToR2("GetDspData", configuration, "GetDspData.dll");
        SyncProjectFileToR2("GetDspData", configuration, "GetDspData.dll.mdb", false);

        string jsonDll = Path.Combine(SolutionFullDir, "lib", "Newtonsoft.Json.dll");
        CopyToR2RespectingOld(jsonDll, Path.Combine(R2PluginsDir, "MengLei-GetDspData", "Newtonsoft.Json.dll"), false);

        string feAsset = Path.Combine(SolutionFullDir, "FractionateEverything", "Assets", "fe");
        CopyToR2RespectingOld(feAsset, Path.Combine(R2PluginsDir, "MengLei-FractionateEverything", "fe"), false);
        Console.WriteLine("已同步计算器数据导出所需本项目 DLL 到 R2");
    }

    private static void SyncProjectFileToR2(string projectName, string configuration, string fileName, bool required = true) {
        string sourceFile = Path.Combine(SolutionFullDir, projectName, "bin", "win", configuration, fileName);
        string targetFile = Path.Combine(R2PluginsDir, $"MengLei-{projectName}", fileName);
        CopyToR2RespectingOld(sourceFile, targetFile, required);
    }

    private static void CopyToR2RespectingOld(string sourceFile, string targetFile, bool required = true) {
        if (!File.Exists(sourceFile)) {
            string message = $"未找到待同步文件：{sourceFile}";
            if (required) {
                throw new FileNotFoundException(message, sourceFile);
            }
            Console.WriteLine(message);
            return;
        }

        string targetPath = File.Exists(targetFile + ".old") ? targetFile + ".old" : targetFile;
        string targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDir)) {
            Directory.CreateDirectory(targetDir);
        }
        File.Copy(sourceFile, targetPath, true);
        Console.WriteLine($"复制 {sourceFile} -> {targetPath}");
    }

    private static List<ModInfo> BuildCalcJsonExportState(
        List<ModInfo> gameState,
        ModInfo getDspData,
        ModInfo errorAnalyzer) {
        List<ModInfo> result = gameState.ToList();
        result.Add(getDspData);
        result.Add(errorAnalyzer);
        return result;
    }

    private static bool IsCalcJsonCacheValid(string jsonFilePath, List<ModInfo> exportState, out string reason) {
        if (!File.Exists(jsonFilePath)) {
            reason = "缺少 json";
            return false;
        }

        string metaFilePath = GetCalcJsonMetaFilePath(jsonFilePath);
        if (!File.Exists(metaFilePath)) {
            reason = "缺少 meta";
            return false;
        }

        JObject actual;
        try {
            actual = JObject.Parse(File.ReadAllText(metaFilePath));
        }
        catch (Exception ex) {
            reason = $"meta 读取失败：{ex.Message}";
            return false;
        }

        JObject expected = BuildCalcJsonCacheSignature(exportState);
        string actualSignature = actual.Value<string>("Signature") ?? "";
        string expectedSignature = expected.ToString(Newtonsoft.Json.Formatting.None);
        if (actualSignature != expectedSignature) {
            reason = "meta 与当前模组版本或本地 DLL 不匹配";
            return false;
        }

        reason = "命中";
        return true;
    }

    private static void WriteCalcJsonCacheMeta(string jsonFilePath, List<ModInfo> exportState) {
        JObject signature = BuildCalcJsonCacheSignature(exportState);
        JObject meta = new() {
            { "GeneratedAtUtc", DateTime.UtcNow.ToString("O") },
            { "Signature", signature.ToString(Newtonsoft.Json.Formatting.None) },
            { "SignatureData", signature },
        };
        File.WriteAllText(GetCalcJsonMetaFilePath(jsonFilePath),
            meta.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
    }

    private static JObject BuildCalcJsonCacheSignature(List<ModInfo> exportState) {
        SortedSet<string> enabledNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModInfo modInfo in exportState) {
            enabledNames.Add(modInfo.name);
            foreach (string dependency in GetDependencies(modInfo.name)) {
                enabledNames.Add(dependency);
            }
        }

        JArray mods = [];
        foreach (string name in enabledNames) {
            ModInfo modInfo = GetModInfo(name);
            mods.Add(new JObject {
                { "Name", name },
                { "DisplayName", modInfo?.displayName ?? "" },
                { "Version", modInfo?.version ?? "" },
            });
        }

        return new JObject {
            { "CacheVersion", CalcJsonCacheVersion },
            { "EnabledMods", mods },
            { "LocalDlls", BuildLocalDllSignature() },
        };
    }

    private static JArray BuildLocalDllSignature() {
        JArray result = [];
#if DEBUG
        string configuration = "Debug";
#else
        string configuration = "Release";
#endif
        foreach (string projectName in CalcJsonLocalProjectNames) {
            string dllPath = Path.Combine(SolutionFullDir, projectName, "bin", "win", configuration, $"{projectName}.dll");
            FileInfo fileInfo = new(dllPath);
            result.Add(new JObject {
                { "Project", projectName },
                { "Path", dllPath },
                { "Exists", fileInfo.Exists },
                { "Length", fileInfo.Exists ? fileInfo.Length : 0 },
                { "LastWriteTimeUtc", fileInfo.Exists ? fileInfo.LastWriteTimeUtc.ToString("O") : "" },
            });
        }
        return result;
    }

    private static void DeleteCalcJsonCache(string jsonFilePath) {
        if (File.Exists(jsonFilePath)) {
            File.Delete(jsonFilePath);
        }

        string metaFilePath = GetCalcJsonMetaFilePath(jsonFilePath);
        if (File.Exists(metaFilePath)) {
            File.Delete(metaFilePath);
        }
    }

    private static string GetCalcJsonMetaFilePath(string jsonFilePath) {
        return Path.ChangeExtension(jsonFilePath, ".meta.json");
    }

    /// <summary>
    /// 每次重新生成计算器数据前，先清空计算器项目目录中的旧 json，避免遗留无效组合。
    /// </summary>
    private static void DeleteExistingCalcJsonFiles() {
        string calcDataDir = DspCalcRawDataDir;
        if (!Directory.Exists(calcDataDir)) {
            Console.WriteLine($"未找到计算器数据目录：{calcDataDir}，跳过旧 json 清理");
            return;
        }

        foreach (string jsonFile in Directory.GetFiles(calcDataDir, "*.json")) {
            File.Delete(jsonFile);
            Console.WriteLine($"删除旧计算器 json：{jsonFile}");
        }
    }

    private static string GetJsonFilePath(List<ModInfo> state, bool isCalc) {
        string jsonFileName = "";
        foreach (ModInfo modInfo in state) {
            jsonFileName += "_" + modInfo.displayName;
            if (isCalc) {
                jsonFileName += modInfo.version;
            }
        }
        jsonFileName = jsonFileName == "" ? "Vanilla" : jsonFileName.Substring(1);
        return isCalc
            ? $@"{DspCalcRawDataDir}\{jsonFileName}.json"
            : $@"{CalcJsonLocalDir}\{jsonFileName}.json";
    }

    #endregion
}
