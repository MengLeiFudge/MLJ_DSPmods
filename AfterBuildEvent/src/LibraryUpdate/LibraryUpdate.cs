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
        return IgnoredModDllNames.Any(name =>
            string.Equals(assemblyBaseName, name, StringComparison.OrdinalIgnoreCase));
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
}
