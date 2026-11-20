using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static AfterBuildEvent.PathConfig;
using static AfterBuildEvent.Utils;

namespace AfterBuildEvent;

static partial class AfterBuildEvent {
    #region 更新类库需要的部分DLL

    /// <summary>
    /// 匹配 CheckPlugins.cs 中的软依赖特性。只用于提示 FE 新增了未列入固定清单的模组，
    /// 不参与反编译目标解析。
    /// </summary>
    private static readonly Regex SoftDependencyRegex =
        new(@"\[BepInDependency\(([^,\)]+),\s*BepInDependency\.DependencyFlags\.SoftDependency\)\]",
            RegexOptions.Compiled);

    /// <summary>
    /// 记录源 DLL 内容哈希与 ilspycmd 版本的状态文件名，用于跳过未变化的 DLL。
    /// </summary>
    private const string DecompileStateFileName = ".decompile-state.txt";

    /// <summary>
    /// 模式 2 的固定反编译清单，是反编译目标的唯一真源。
    /// FE 的 CheckPlugins 软依赖新增或改名时，必须同步在这里补充条目；
    /// 未列入清单的软依赖会在运行时打印警告。
    /// </summary>
    private static readonly ModDecompileTarget[] ModDecompileTargets = [
        new("xiaoye97-LDBTool", "LDBTool.dll", "", "LDBToolPlugin"),
        new("CommonAPI-CommonAPI", "CommonAPI.dll", "CommonAPI", "CommonAPIPlugin"),
        new("CommonAPI-DSPModSave", "DSPModSave.dll", "CommonAPI", "DSPModSavePlugin"),
        new("jinxOAO-BuildBarTool", "BuildBarTool.dll", "", "BuildBarToolPlugin"),
        new("nebula-NebulaMultiplayerModApi", "NebulaAPI.dll", "nebula", "NebulaMultiplayerModAPI"),
        new("PhantomGamers-AchievementsEnabler", "AchievementsEnabler.dll", "", "AchievementsEnabler"),
        new("blacksnipebiu-Auxilaryfunction", "Auxilaryfunction.dll", "", "Auxilaryfunction"),
        new("cat_yummi-BigTower", "BigTower.dll", "", "BigTower"),
        new("starfi5h-BuildToolOpt", "BuildToolOpt.dll", @"DSP_Mod\BuildToolOpt", "BuildToolOpt"),
        new("soarqin-CheatEnabler", "CheatEnabler.dll", @"DSP_Mods\CheatEnabler", "CheatEnabler"),
        // 未安装：包名按 BepInEx GUID 推断，尚未与 Thunderstore 核对。
        new("kumor-CustomCreateBirthStar", "CustomCreateBirthStar.dll", "", "CustomCreateBirthStar"),
        new("starfi5h-DeliverySlotsTweaks", "DeliverySlotsTweaks.dll", @"DSP_Mod\DeliverySlotsTweaks",
            "DeliverySlotsTweaks"),
        new("jinxOAO-DSPCalculator", "DSPCalculator.dll", "DSPCalculator", "DspCalculator"),
        new("appuns-DSPRecipeFinder", "DSPRecipeFinder.dll", "", "DspRecipeFinder"),
        new("HiddenCirno-GenesisBook", "ProjectGenesis.dll", "ProjectGenesis", "GenesisBook"),
        new("jinxOAO-MoreMegaStructure", "MoreMegaStructure.dll", "DSPmod_MoreMegaStructures",
            "MoreMegaStructure"),
        new("blacksnipebiu-Multfuntion_mod", "Multifunction_mod.dll", "", "Multfunction_mod"),
        new("ProfessorCat-OrbitalRing", "ProjectOrbitalRing.dll", "OrbitalRing-MOD", "OrbitalRing"),
        // 未安装：包名按 BepInEx GUID 推断，尚未与 Thunderstore 核对。
        new("qlvlp-PackageLogistic", "PackageLogistic.dll", "", "PackageLogistic"),
        new("jinxOAO-SmelterMiner", "SmelterMiner.dll", "", "SmelterMiner"),
        new("ckcz123-TheyComeFromVoid", "DSP_Battle.dll", "DSP_Battle", "TheyComeFromVoid"),
        new("cat_yummi-TowerFactory", "TowerFactory.dll", "", "TowerFactory"),
        new("soarqin-UXAssist", "UXAssist.dll", @"DSP_Mods\UXAssist", "UxAssist"),
        // 非 FE 软依赖，保留用于排查第三方模组。
        new("Galactic_Scale-GalacticScale", "GalacticScale.dll", "", ""),
    ];

    /// <summary>
    /// 一个反编译目标：R2 包名（等同 plugins 目录名）、主 DLL 文件名、
    /// 本地源码目录（相对 ModSourcesRootDir，空表示没有本地源码）与对应的 FE 软依赖标识符。
    /// </summary>
    private sealed class ModDecompileTarget(string package, string dllName, string sourceDir, string feDependency) {
        public string Package { get; } = package;
        public string DllName { get; } = dllName;
        public string SourceDir { get; } = sourceDir;
        public string FeDependency { get; } = feDependency;
    }

    /// <summary>
    /// 一条反编译状态：生成当前源码时源 DLL 的内容哈希与 ilspycmd 版本。
    /// </summary>
    private sealed class DecompileStateEntry {
        public string SourceSha256 { get; set; } = "";
        public string IlspyVersion { get; set; } = "";
    }

    /// <summary>
    /// 反编译状态文件路径，与反编译产物同目录。
    /// </summary>
    private static string DecompileStatePath => $@"{DecompiledSourceDir}\{DecompileStateFileName}";

    private static void UpdateLibDll() {
        using CmdProcess cmd = new();
        LoadModInfos();
        WarnUnlistedSoftDependencies();
        Dictionary<string, DecompileStateEntry> state = LoadDecompileState();
        string ilspyVersion = GetIlspyVersion();
        DecompileGameDlls(cmd, ilspyVersion, state);
        DecompileModsFromR2(cmd, ilspyVersion, state);
        SaveDecompileState(state);
    }

    /// <summary>
    /// 反编译游戏 DLL。源 DLL 内容与 ilspycmd 版本都没变化时跳过 publicize 和反编译。
    /// </summary>
    private static void DecompileGameDlls(
        CmdProcess cmd, string ilspyVersion, Dictionary<string, DecompileStateEntry> state) {
        if (NugetGameLibNet45Dir == null) {
            Console.WriteLine("NugetGameLibNet45Dir为空，跳过Publize游戏dll");
            return;
        }

        PublizeAndDecompileGameDll(cmd, ilspyVersion, state, DSPACDll, "Assembly-CSharp.dll");
        PublizeAndDecompileGameDll(cmd, ilspyVersion, state, DSPUIDll, "UnityEngine.UI.dll");
    }

    private static void PublizeAndDecompileGameDll(
        CmdProcess cmd, string ilspyVersion, Dictionary<string, DecompileStateEntry> state,
        string sourceDllPath, string dllName) {
        string actualSourcePath = ResolveExistingDll(sourceDllPath);
        if (actualSourcePath == null) {
            Console.WriteLine($"未找到 {sourceDllPath} (且无 .old 备份)，跳过反编译");
            return;
        }

        string assemblyName = Path.GetFileNameWithoutExtension(dllName);
        if (ShouldSkipDecompile(state, assemblyName, actualSourcePath, ilspyVersion)) {
            return;
        }

        string targetPath = $@"{NugetGameLibNet45Dir}\{dllName}";
        PublizeDll(cmd, actualSourcePath, targetPath);
        if (!File.Exists(targetPath)) {
            Console.Error.WriteLine($"publicize 未产出 {targetPath}，跳过反编译");
            return;
        }

        bool succeeded = DecompileDll(cmd, dllName);
        RecordDecompileState(state, assemblyName, actualSourcePath, ilspyVersion, succeeded);
    }

    /// <summary>
    /// 按固定清单反编译已安装的 R2 模组。已有本地源码或 DLL 未变化时跳过。
    /// </summary>
    private static void DecompileModsFromR2(
        CmdProcess cmd, string ilspyVersion, Dictionary<string, DecompileStateEntry> state) {
        IReadOnlyList<ModInfo> installedMods = GetAllModInfos();
        if (installedMods.Count == 0) {
            Console.WriteLine("mods.yml 中没有可用模组信息，跳过 mod 反编译。");
            return;
        }

        foreach (ModDecompileTarget target in ModDecompileTargets) {
            if (TryResolveLocalSource(target, out string sourcePath, out string installedVersion)) {
                Console.WriteLine(
                    $"跳过反编译 {target.Package}：已有本地源码 {sourcePath}（已安装版本 {installedVersion}）");
                continue;
            }

            ModInfo modInfo = GetModInfo(target.Package);
            if (modInfo == null) {
                Console.WriteLine($"mods.yml 中未找到 {target.Package}，跳过反编译。");
                continue;
            }

            string pluginDir = Path.Combine(R2PluginsDir, modInfo.name);
            string dllPath = FindConfiguredDll(pluginDir, target.DllName);
            if (dllPath == null) {
                Console.WriteLine($"插件目录 {pluginDir} 中未找到 {target.DllName}，跳过反编译。");
                continue;
            }

            string assemblyName = Path.GetFileNameWithoutExtension(target.DllName);
            if (ShouldSkipDecompile(state, assemblyName, dllPath, ilspyVersion)) {
                continue;
            }

            Console.WriteLine($"开始处理模组 {modInfo.name} -> {dllPath}");
            DecompileModDll(cmd, dllPath);
            RecordDecompileState(state, assemblyName, dllPath, ilspyVersion, HasDecompiledSource(assemblyName));
        }
    }

    /// <summary>
    /// 提示 FE 软依赖中尚未列入固定清单的模组。清单才是反编译目标的唯一真源。
    /// </summary>
    private static void WarnUnlistedSoftDependencies() {
        if (!File.Exists(CheckPluginsSourcePath)) {
            Console.WriteLine($"未找到 CheckPlugins 源文件：{CheckPluginsSourcePath}");
            return;
        }

        HashSet<string> listed = ModDecompileTargets
            .Select(target => target.FeDependency)
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SoftDependencyRegex.Matches(File.ReadAllText(CheckPluginsSourcePath))) {
            string dependencyExpression = match.Groups[1].Value.Trim();
            string sourceName = dependencyExpression.Split('.')[0].Trim();
            if (listed.Contains(sourceName) || !warned.Add(sourceName)) {
                continue;
            }

            Console.WriteLine(
                $"警告：FE 软依赖 {sourceName} 未列入 ModDecompileTargets，请补充其 R2 包名与主 DLL 文件名。");
        }
    }

    /// <summary>
    /// 按清单给定的本地源码目录判断是否已有源码，并取出 mods.yml 中的已安装版本，便于发现版本漂移。
    /// </summary>
    private static bool TryResolveLocalSource(
        ModDecompileTarget target, out string sourcePath, out string installedVersion) {
        sourcePath = "";
        installedVersion = "";
        if (string.IsNullOrWhiteSpace(target.SourceDir)) {
            return false;
        }

        string fullPath = Path.Combine(ModSourcesRootDir, target.SourceDir);
        if (!Directory.Exists(fullPath)) {
            return false;
        }

        sourcePath = fullPath;
        ModInfo modInfo = GetModInfo(target.Package);
        installedVersion = modInfo == null ? "未知" : modInfo.version;
        return true;
    }

    /// <summary>
    /// 按清单给定的 DLL 文件名定位插件文件，兼容 r2modman 禁用模组后的 .dll.old。
    /// </summary>
    private static string FindConfiguredDll(string pluginDir, string dllName) {
        if (!Directory.Exists(pluginDir)) {
            return null;
        }

        string dllPath = Path.Combine(pluginDir, dllName);
        if (File.Exists(dllPath)) {
            return dllPath;
        }

        string disabledDllPath = dllPath + ".old";
        return File.Exists(disabledDllPath) ? disabledDllPath : null;
    }

    private static string ResolveExistingDll(string dllPath) {
        if (File.Exists(dllPath)) {
            return dllPath;
        }

        return File.Exists(dllPath + ".old") ? dllPath + ".old" : null;
    }

    private static bool HasDecompiledSource(string assemblyName) {
        return File.Exists(Path.Combine(DecompiledSourceDir, assemblyName, $"{assemblyName}.csproj"));
    }

    /// <summary>
    /// 源 DLL 内容、ilspycmd 版本和已生成项目都未变化时才跳过反编译。
    /// </summary>
    private static bool ShouldSkipDecompile(
        Dictionary<string, DecompileStateEntry> state, string assemblyName, string sourceDllPath,
        string ilspyVersion) {
        if (!HasDecompiledSource(assemblyName)) {
            return false;
        }

        if (!state.TryGetValue(assemblyName, out DecompileStateEntry entry)) {
            return false;
        }

        if (!string.Equals(entry.IlspyVersion, ilspyVersion, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        if (!string.Equals(entry.SourceSha256, GetFileSha256(sourceDllPath),
                StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        Console.WriteLine($"跳过反编译 {assemblyName}：源 DLL 内容与 ilspycmd 版本均未变化");
        return true;
    }

    private static void RecordDecompileState(
        Dictionary<string, DecompileStateEntry> state, string assemblyName, string sourceDllPath,
        string ilspyVersion, bool succeeded) {
        if (!succeeded) {
            return;
        }

        state[assemblyName] = new() {
            SourceSha256 = GetFileSha256(sourceDllPath),
            IlspyVersion = ilspyVersion,
        };
    }

    private static Dictionary<string, DecompileStateEntry> LoadDecompileState() {
        Dictionary<string, DecompileStateEntry> state = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(DecompileStatePath)) {
            return state;
        }

        try {
            foreach (string line in File.ReadAllLines(DecompileStatePath)) {
                string[] parts = line.Split('\t');
                if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0])) {
                    continue;
                }

                state[parts[0]] = new() {
                    SourceSha256 = parts[1],
                    IlspyVersion = parts[2],
                };
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"读取反编译状态失败，本次将重新反编译：{ex.Message}");
            state.Clear();
        }

        return state;
    }

    private static void SaveDecompileState(Dictionary<string, DecompileStateEntry> state) {
        try {
            Directory.CreateDirectory(DecompiledSourceDir);
            List<string> lines = state
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}\t{pair.Value.SourceSha256}\t{pair.Value.IlspyVersion}")
                .ToList();
            File.WriteAllLines(DecompileStatePath, lines);
        }
        catch (Exception ex) {
            Console.WriteLine($"写入反编译状态失败：{ex.Message}");
        }
    }

    private static string GetIlspyVersion() {
        try {
            string output = CmdProcess.RunCapture("ilspycmd", "--version");
            // 版本输出含 ilspycmd 与 ICSharpCode.Decompiler 两行，必须压成单行才能写进状态文件。
            string version = string.Join(" | ",
                output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0));
            if (version.Length > 0) {
                return version;
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"读取 ilspycmd 版本失败：{ex.Message}");
        }

        return "unknown";
    }

    #region 模组匹配（模式 2 与模式 6 共用）

    /// <summary>
    /// 作为附属库的程序集名称前缀，绝不会作为主要模组 DLL。
    /// </summary>
    private static readonly string[] IgnoredModDllPrefixes =
        ["System.", "Newtonsoft.Json", "0Harmony", "BepInEx.", "Mono.", "MonoMod.", "K4os.", "Unity."];

    /// <summary>
    /// 作为附属库的确切程序集基本名称，绝不会作为主要模组 DLL。
    /// </summary>
    private static readonly string[] IgnoredModDllNames =
        ["websocket-sharp", "discord_game_sdk", "discord_game_sdk_dotnet", "Open.Nat", "HarmonyXInterop"];

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

    private static bool IsIgnoredCompanionDll(string fileName) {
        string assemblyBaseName = GetAssemblyBaseName(fileName);
        if (IgnoredModDllPrefixes.Any(prefix =>
                assemblyBaseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) {
            return true;
        }
        return IgnoredModDllNames.Any(name =>
            string.Equals(assemblyBaseName, name, StringComparison.OrdinalIgnoreCase));
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

    #endregion

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

    private static bool DecompileDll(CmdProcess cmd, string dllName, string sourceDir = null) {
        string dllPath = sourceDir != null
            ? $@"{sourceDir}\{dllName}"
            : $@"{NugetGameLibNet45Dir}\{dllName}";
        string dllNameNoExt = Path.GetFileNameWithoutExtension(dllName).Replace("-publicized", "");
        string dllBaseName = Path.GetFileNameWithoutExtension(dllName);
        string outputDir = Path.GetFullPath($@"{DecompiledSourceDir}\{dllNameNoExt}");
        string csprojPath = Path.Combine(outputDir, $"{dllNameNoExt}.csproj");
        string publicizedCsprojPath = Path.Combine(outputDir, $"{dllBaseName}.csproj");
        if (!File.Exists(dllPath)) {
            Console.WriteLine($"未找到{dllPath}，跳过反编译");
            return false;
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
            return true;
        }

        Console.Error.WriteLine($"反编译失败，未生成 {csprojPath}");
        return false;
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
}
