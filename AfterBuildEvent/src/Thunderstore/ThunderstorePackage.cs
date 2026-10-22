using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;

namespace AfterBuildEvent.Thunderstore;

/// <summary>
/// 核对指定模组的本地发布包；持有只读共享句柄，直到准备或上传结束，防止核对后被覆盖。
/// </summary>
internal sealed class ThunderstorePackage : IDisposable {
    /// <summary>与项目目录、manifest 一致的商店包名。</summary>
    public string Name { get; }
    /// <summary>csproj、manifest、DLL 和插件声明共同确认的版本。</summary>
    public string Version { get; }
    /// <summary>模式 1 已生成且内容核对通过的 ZIP 绝对路径。</summary>
    public string ZipPath { get; }
    /// <summary>待批准文件的完整 SHA256；上传时必须保持一致。</summary>
    public string Sha256 { get; }
    private readonly FileStream archiveStream;

    private ThunderstorePackage(string name, string version, string path, FileStream stream) {
        Name = name;
        Version = version;
        ZipPath = path;
        archiveStream = stream;
        Sha256 = Hash(stream);
        stream.Position = 0;
    }

    /// <summary>只接受已配置的项目，并逐文件比较模式 1 的 ZIP 与当前构建、发布素材。</summary>
    public static ThunderstorePackage Open(string name, string configuration) {
        if (!ThunderstoreApi.DefaultCategories.ContainsKey(name)) {
            throw new ArgumentException($"未知发布项目：{name}");
        }
        string projectDir = Path.Combine(PathConfig.SolutionFullDir, name);
        string manifestPath = Path.Combine(projectDir, "Assets", "manifest.json");
        JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
        string version = (string)manifest["version_number"];
        if (version == null || !Regex.IsMatch(version, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")) {
            throw new InvalidDataException("manifest 必须使用三段数字版本。");
        }
        if ((string)manifest["name"] != name || ((string)manifest["author"] ?? "MengLei") != "MengLei") {
            throw new InvalidDataException("manifest 包名或作者与 MengLei 发布目标不一致。");
        }
        XmlDocument project = new();
        project.Load(Path.Combine(projectDir, name + ".csproj"));
        if (project.SelectSingleNode("/Project/PropertyGroup/Version")?.InnerText != version) {
            throw new InvalidDataException("csproj 与 manifest 版本不一致。");
        }
        string dllPath = Path.Combine(projectDir, "bin", configuration, name + ".dll");
        RequireFile(dllPath);
        if (FileVersionInfo.GetVersionInfo(dllPath).ProductVersion != version) {
            throw new InvalidDataException("DLL 版本与 manifest 不一致，请先重新构建。");
        }
        // 其余插件使用构建生成的 PluginInfo；蓝图星球自行声明 BepInPlugin 的版本常量。
        string pluginSource = name == "BlueprintPlanet"
            ? Path.Combine(projectDir, "src", "BlueprintPlanetPlugin.cs")
            : Path.Combine(projectDir, "obj", configuration, "GeneratedFile.cs");
        string versionField = name == "BlueprintPlanet" ? "PluginVersion" : "PLUGIN_VERSION";
        string declaredVersion = Regex.Match(File.ReadAllText(pluginSource),
            @"\bconst\s+string\s+" + versionField + "\\s*=\\s*\"([^\"]+)\"").Groups[1].Value;
        if (declaredVersion != version) {
            throw new InvalidDataException("BepInEx 插件版本与 manifest 不一致。");
        }
        List<string> files = [dllPath, manifestPath, Path.Combine(projectDir, "Assets", "icon.png"),
            Path.Combine(projectDir, "README.md")];
        string changelog = Path.Combine(projectDir, "CHANGELOG.md");
        if (File.Exists(changelog)) {
            string changelogVersion = Regex.Match(File.ReadAllText(changelog),
                @"(?m)^\s*(?:[-*] |#{1,6} +)v?(\d+\.\d+\.\d+)\b").Groups[1].Value;
            if (changelogVersion != version) {
                throw new InvalidDataException("CHANGELOG 首个版本条目与 manifest 不一致。");
            }
            files.Add(changelog);
        }
        if (name == "FractionateEverything") {
            files.Add(Path.Combine(projectDir, "bin", configuration, "FractionateEverything.Resources.dll"));
        } else if (name == "GetDspData") {
            files.Add(Path.Combine(PathConfig.SolutionFullDir, "lib", "Newtonsoft.Json.dll"));
        }
        foreach (string file in files) {
            RequireFile(file);
        }
        string zipPath = Path.Combine(PathConfig.SolutionFullDir, "AfterBuildEvent", "bin", configuration,
            "ModZips", $"{name}_{version}.zip");
        RequireFile(zipPath);
        FileStream stream = new(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try {
            ValidateEntries(stream, files);
            return new ThunderstorePackage(name, version, zipPath, stream);
        } catch {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>拒绝多余、缺失、重名或与当前源文件不同的条目；不解压到磁盘。</summary>
    private static void ValidateEntries(FileStream stream, IEnumerable<string> files) {
        Dictionary<string, string> expected = files.ToDictionary(Path.GetFileName, StringComparer.Ordinal);
        using ZipFile zip = new(stream) { IsStreamOwner = false };
        foreach (ZipEntry entry in zip) {
            if (!entry.IsFile || !expected.TryGetValue(entry.Name, out string sourcePath)) {
                throw new InvalidDataException($"ZIP 含多余或重复条目：{entry.Name}");
            }
            using FileStream source = File.OpenRead(sourcePath);
            using Stream contents = zip.GetInputStream(entry);
            if (entry.Size != source.Length || Hash(contents) != Hash(source)) {
                throw new InvalidDataException($"ZIP 内容已过期：{entry.Name}，请重新构建并执行模式 1。");
            }
            expected.Remove(entry.Name);
        }
        if (expected.Count != 0) {
            throw new InvalidDataException("ZIP 缺少文件：" + string.Join(", ", expected.Keys));
        }
        stream.Position = 0;
    }

    /// <summary>缺少素材或产物时明确停止，不生成不完整的商店包。</summary>
    private static void RequireFile(string path) {
        if (!File.Exists(path)) {
            throw new FileNotFoundException("缺少发布文件：" + path, path);
        }
    }

    /// <summary>从当前位置计算内容 SHA256；调用者负责流生命周期。</summary>
    private static string Hash(Stream stream) {
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>结束对待发布 ZIP 的写入保护。</summary>
    public void Dispose() => archiveStream.Dispose();
}
