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
    #region 生成戴森球量化计算器所需文件，并将其复制到计算器项目目录下

    private static void GetAllCalcJson() {
        using CmdProcess cmd = new();
        bool calcProjectAvailable = IsDspCalcProjectAvailable();
        //终止游戏
        KillDspGameAndWaitForExit();
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
                    KillDspGameAndWaitForExit();
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
            SyncDspCalcGameDataInfoList(modInfos);
            KillDspGameAndWaitForExit();
            RebuildCalcIconsFromGame(DspCalcRawDataDir, syncToCalcAssets: true, syncGetDspDataToR2: false);
        }
        //启用R2配置文件中所有enable为true的mod
        EnableModsByConfig();
        //终止游戏
        KillDspGameAndWaitForExit();
    }

    private static void SyncCalcJsonExportModsToR2() {
        SyncProjectFileToR2("FractionateEverything", BuildConfiguration, "FractionateEverything.dll");
        SyncProjectFileToR2("FractionateEverything", BuildConfiguration, "FractionateEverything.dll.mdb", false);
        SyncProjectFileToR2("GetDspData", BuildConfiguration, "GetDspData.dll");
        SyncProjectFileToR2("GetDspData", BuildConfiguration, "GetDspData.dll.mdb", false);

        string jsonDll = Path.Combine(SolutionFullDir, "lib", "Newtonsoft.Json.dll");
        CopyToR2RespectingOld(jsonDll, Path.Combine(R2PluginsDir, "MengLei-GetDspData", "Newtonsoft.Json.dll"), false);

        string feAsset = Path.Combine(SolutionFullDir, "FractionateEverything", "Assets", "fe");
        CopyToR2RespectingOld(feAsset, Path.Combine(R2PluginsDir, "MengLei-FractionateEverything", "fe"), false);
        Console.WriteLine("已同步计算器数据导出所需本项目 DLL 到 R2");
    }

    private static void SyncProjectFileToR2(string projectName, string configuration, string fileName,
        bool required = true) {
        string sourceFile = GetProjectOutputPath(projectName, configuration, fileName);
        string targetFile = Path.Combine(R2PluginsDir, $"MengLei-{projectName}", fileName);
        CopyToR2RespectingOld(sourceFile, targetFile, required);
    }

    private static string GetProjectOutputPath(string projectName, string configuration, string fileName) {
        return Path.Combine(SolutionFullDir, projectName, "bin", configuration, fileName);
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
        Exception lastException = null;
        for (int attempt = 1; attempt <= R2CopyRetryCount; attempt++) {
            try {
                File.Copy(sourceFile, targetPath, true);
                Console.WriteLine($"复制 {sourceFile} -> {targetPath}");
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                lastException = ex;
                if (attempt == R2CopyRetryCount) {
                    break;
                }

                Console.WriteLine(
                    $"复制 R2 文件被占用，{R2CopyRetryDelayMs}ms 后重试 {attempt}/{R2CopyRetryCount}：{targetPath}；{ex.Message}");
                Thread.Sleep(R2CopyRetryDelayMs);
            }
        }

        throw new IOException($"复制 R2 文件失败，目标文件仍被占用：{targetPath}", lastException);
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
        foreach (string projectName in CalcJsonLocalProjectNames) {
            string dllPath = GetProjectOutputPath(projectName, BuildConfiguration, $"{projectName}.dll");
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

    private static void SyncDspCalcGameDataInfoList(ModInfo[] modInfos) {
        if (!File.Exists(DspCalcGameDataPath)) {
            Console.WriteLine($"未找到计算器 gameData.ts，跳过同步模组版本：{DspCalcGameDataPath}");
            return;
        }

        Dictionary<string, string> versions = new(StringComparer.OrdinalIgnoreCase) {
            ["Vanilla"] = GetDspGameVersion(),
        };
        foreach (ModInfo modInfo in modInfos) {
            if (!string.IsNullOrWhiteSpace(modInfo.displayName) && !string.IsNullOrWhiteSpace(modInfo.version)) {
                versions[modInfo.displayName] = modInfo.version;
            }
        }

        string content = File.ReadAllText(DspCalcGameDataPath, Encoding.UTF8);
        string updated = content;
        List<string> synced = [];
        foreach (KeyValuePair<string, string> pair in versions) {
            if (string.IsNullOrWhiteSpace(pair.Value)) {
                Console.WriteLine($"跳过同步计算器版本：{pair.Key} 缺少版本号");
                continue;
            }

            string next = ReplaceGameDataInfoVersion(updated, pair.Key, pair.Value, out bool changed);
            if (changed) {
                synced.Add($"{pair.Key} -> {pair.Value}");
                updated = next;
            }
        }

        if (updated == content) {
            Console.WriteLine("计算器 game_data_info_list 版本已是最新。");
            return;
        }

        File.WriteAllText(DspCalcGameDataPath, updated, Utf8NoBom);
        Console.WriteLine($"已同步计算器 game_data_info_list 版本：{string.Join(", ", synced)}");
    }

    private static string ReplaceGameDataInfoVersion(
        string content,
        string nameEn,
        string version,
        out bool changed) {
        string pattern =
            $@"(\{{(?:(?!\}}\s*,?\s*\{{).)*?""name_en""\s*:\s*""{Regex.Escape(nameEn)}""(?:(?!\}}\s*,?\s*\{{).)*?""version""\s*:\s*"")([^""]*)("")";
        string result = Regex.Replace(
            content,
            pattern,
            match => {
                if (match.Groups[2].Value == version) {
                    return match.Value;
                }
                return match.Groups[1].Value + version + match.Groups[3].Value;
            },
            RegexOptions.Singleline);
        changed = result != content;
        return result;
    }

    private static string GetDspGameVersion() {
        string versionsFile = Path.Combine(DSPGameDir, "Updates", "Versions.txt");
        if (!File.Exists(versionsFile)) {
            Console.WriteLine($"未找到游戏版本列表：{versionsFile}");
            return "";
        }

        string result = "";
        foreach (string line in File.ReadLines(versionsFile)) {
            string version = line.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(version)) {
                result = version;
            }
        }
        return result;
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
