﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace AfterBuildEvent;

public static class Utils {
    public const string R2_Default =
        @"C:\Users\MLJ\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\Default";
    public const string R2_Mods_Config = $@"{R2_Default}\mods.yml";
    public const string R2_BepInEx = $@"{R2_Default}\BepInEx";
    public const string DSPGameDir = @"D:\Steam\steamapps\common\Dyson Sphere Program";

    public static FileInfo PublicizerExe => new(@"..\..\..\..\lib\BepInEx.AssemblyPublicizer.Cli.exe");
    public const string R2_DumpedDll_Origin = $@"{R2_BepInEx}\DumpedAssemblies\DSPGAME\Assembly-CSharp.dll";
    public const string R2_DumpedDll_Publicized =
        $@"{R2_BepInEx}\DumpedAssemblies\DSPGAME\Assembly-CSharp-publicized.dll";
    public const string Project_DumpedDll_Publicized = @"..\..\..\..\lib\Assembly-CSharp-publicized.dll";
    public const string R2_GenesisDll_Origin = $@"{R2_BepInEx}\plugins\HiddenCirno-GenesisBook\ProjectGenesis.dll";
    public const string R2_GenesisDll_Publicized =
        $@"{R2_BepInEx}\plugins\HiddenCirno-GenesisBook\ProjectGenesis-publicized.dll";
    public const string Project_GenesisDll_Publicized = @"..\..\..\..\lib\ProjectGenesis-publicized.dll";

    public static FileInfo Pdb2mdbExe => new(@"..\..\..\..\lib\pdb2mdb.exe");

    public const string KillDSP = "taskkill /f /im DSPGAME.exe";
    public const string RunModded = "start steam://rungameid/1366540";

    #region C(n,r)

    /// <summary>
    /// 设集合为set，集合长度为n，要取出长度为r的集合，则返回的集合长度为C(n,r)
    /// </summary>
    /// <param name="set">原始集合</param>
    /// <param name="r">要取出长度为多少的集合</param>
    /// <returns>所有指定长度、不同元素、顺序无关的集合的合集</returns>
    public static List<List<T>> Combinations<T>(IReadOnlyList<T> set, int r) {
        return Combinations(set, r, 0);
    }

    private static List<List<T>> Combinations<T>(IReadOnlyList<T> set, int r, int index) {
        if (r == 0) {
            return [[]];
        }
        List<List<T>> combos = [];
        for (int i = index; i <= set.Count - r; i++) {
            IReadOnlyList<T> head = new List<T> { set[i] };
            List<List<T>> tailCombinations = Combinations(set, r - 1, i + 1);
            foreach (List<T> tail in tailCombinations) {
                combos.Add(head.Concat(tail).ToList());
            }
        }
        return combos;
    }

    #endregion

    #region 切换单个模组启用/禁用

    /// <summary>
    /// 切换单个模组的启用/禁用状态
    /// </summary>
    /// <param name="mod">要处理的模组名称，格式为作者名字-模组名字</param>
    /// <param name="enable">是否启用</param>
    public static void ChangeModEnable(string mod, bool enable) {
        string modPatchersDir = $@"{R2_BepInEx}\patchers\{mod}";
        string modPluginsDir = $@"{R2_BepInEx}\plugins\{mod}";
        ChangeEnable(modPatchersDir, enable);
        ChangeEnable(modPluginsDir, enable);
    }

    /// <summary>
    /// 切换指定路径的启用/禁用状态
    /// </summary>
    /// <param name="path">要处理的路径，可以是文件或文件夹</param>
    /// <param name="enable">是否启用</param>
    private static void ChangeEnable(string path, bool enable) {
        if (Directory.Exists(path)) {
            foreach (var file in Directory.GetFiles(path)) {
                ChangeEnable(file, enable);
            }
            foreach (var dir in Directory.GetDirectories(path)) {
                ChangeEnable(dir, enable);
            }
        } else if (File.Exists(path)) {
            while (true) {
                try {
                    if (enable) {
                        while (path.EndsWith(".old")) {
                            File.Move(path, path.Substring(0, path.Length - 4));
                            path = path.Substring(0, path.Length - 4);
                        }
                    } else {
                        if (!path.EndsWith(".old")) {
                            File.Move(path, path + ".old");
                        }
                    }
                    break;
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error changing enable state for {path}: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }
    }

    #endregion

    #region 从R2配置文件获取指定模组信息

    public class ModInfo {
        public string name = "";//等价于 authorName-displayName
        public string authorName = "";
        public string displayName = "";
        public List<string> dependencies = [];//虽然有名称和版本，但是版本不关心，简化一下
        public string version = "";
        public bool enabled = false;
    }

    private static List<ModInfo> modInfos = [];

    /// <summary>
    /// 从R2配置文件读取所有模组信息，然后将其保存在 modInfos 列表中
    /// </summary>
    public static void LoadModInfos() {
        modInfos.Clear();
        using StreamReader sr = File.OpenText(R2_Mods_Config);
        ModInfo modInfo = null;
        string line;
        Regex regex = new Regex("    - .+-.+-[0-9]+.[0-9]+.[0-9]+");
        while ((line = sr.ReadLine()) != null) {
            if (line.StartsWith("- manifestVersion:")) {
                modInfo = new();
                continue;
            }
            if (modInfo == null) {
                continue;
            }
            if (line.StartsWith("  name:")) {
                modInfo.name = line.Substring(line.IndexOf(':') + 2);
            } else if (line.StartsWith("  authorName:")) {
                modInfo.authorName = line.Substring(line.IndexOf(':') + 2);
            } else if (line.StartsWith("  displayName:")) {
                modInfo.displayName = line.Substring(line.IndexOf(':') + 2);
            } else if (line.StartsWith("  dependencies:")) {
                if (line == "  dependencies:") {
                    while ((line = sr.ReadLine()) != null) {
                        if (regex.IsMatch(line)) {
                            modInfo.dependencies.Add(line.Substring(6, line.LastIndexOf('-') - 6));
                        } else {
                            break;
                        }
                    }
                }
            } else if (line.StartsWith("    major:")) {
                modInfo.version = line.Substring(line.IndexOf(':') + 2);
            } else if (line.StartsWith("    minor:") || line.StartsWith("    patch:")) {
                modInfo.version += "." + line.Substring(line.IndexOf(':') + 2);
            } else if (line.StartsWith("  enabled:")) {
                modInfo.enabled = bool.Parse(line.Substring(line.IndexOf(':') + 2));
                modInfos.Add(modInfo);
            }
        }
    }

    public static ModInfo GetModInfo(string mod) {
        return modInfos.Find(m => m.name == mod || m.displayName == mod);
    }

    /// <summary>
    /// 获取某个Mod的所有前置依赖Mod
    /// </summary>
    /// <param name="mod">要查找前置依赖的Mod的名称，简写或全名均可</param>
    /// <returns>所有前置依赖Mod，包括前置依赖的前置依赖</returns>
    public static List<string> GetDependencies(string mod) {
        List<string> dependencies = [];
        GetDependencies(mod, ref dependencies);
        return dependencies;
    }

    private static void GetDependencies(string mod, ref List<string> dependencies) {
        ModInfo modInfo = GetModInfo(mod);
        if (modInfo == null) return;
        foreach (string dependency in modInfo.dependencies) {
            if (dependencies.Contains(dependency)) {
                continue;
            }
            dependencies.Add(dependency);
            GetDependencies(dependency, ref dependencies);
        }
    }

    public static void OnlyEnableInputMods(List<string> names) {
        foreach (ModInfo modInfo in modInfos) {
            ChangeModEnable(modInfo.name, names.Contains(modInfo.name));
        }
    }

    public static void EnableModsByConfig() {
        foreach (ModInfo modInfo in modInfos) {
            ChangeModEnable(modInfo.name, modInfo.enabled);
        }
    }

    #endregion
}
