using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AfterBuildEvent {
    public static class Utils {
        public const string R2_Default =
            @"C:\Users\MLJ\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\Default";
        public const string R2_BepInEx = $@"{R2_Default}\BepInEx";
        public const string DSPGameDir = @"D:\Steam\steamapps\common\Dyson Sphere Program";

        public static FileInfo PublicizerExe => new(@"..\..\..\lib\BepInEx.AssemblyPublicizer.Cli.exe");
        public const string R2_DumpedDll_Origin = $@"{R2_BepInEx}\DumpedAssemblies\DSPGAME\Assembly-CSharp.dll";
        public const string R2_DumpedDll_Publicized =
            $@"{R2_BepInEx}\DumpedAssemblies\DSPGAME\Assembly-CSharp-publicized.dll";
        public const string Project_DumpedDll_Publicized = @"..\..\..\lib\Assembly-CSharp-publicized.dll";
        public const string R2_GenesisDll_Origin = $@"{R2_BepInEx}\plugins\HiddenCirno-GenesisBook\ProjectGenesis.dll";
        public const string R2_GenesisDll_Publicized =
            $@"{R2_BepInEx}\plugins\HiddenCirno-GenesisBook\ProjectGenesis-publicized.dll";
        public const string Project_GenesisDll_Publicized = @"..\..\..\lib\ProjectGenesis-publicized.dll";

        public static FileInfo Pdb2mdbExe => new(@"..\..\..\lib\pdb2mdb.exe");

        public const string KillDSP = "taskkill /f /im DSPGAME.exe";
        public const string RunModded = "start steam://rungameid/1366540";

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

        public static void ChangeModEnable(string mod, bool enable) {
            string modPatchersDir = $@"{R2_BepInEx}\patchers\{mod}";
            string modPluginsDir = $@"{R2_BepInEx}\plugins\{mod}";
            ChangeEnable(modPatchersDir, enable);
            ChangeEnable(modPluginsDir, enable);
        }

        private static void ChangeEnable(string path, bool enable) {
            if (Directory.Exists(path)) {
                foreach (var file in Directory.GetFiles(path)) {
                    ChangeEnable(file, enable);
                }
                foreach (var dir in Directory.GetDirectories(path)) {
                    ChangeEnable(dir, enable);
                }
            } else if (File.Exists(path)) {
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
            }
        }

        public static void ChangeAllModsEnable(bool enable) {
            //不启用的mod
            List<string> enableIgnore =
                ["Galactic_Scale-GalacticScale", "essium-PlanetWormhole", "jinxOAO-SmelterMiner"];
            //不禁用的mod
            List<string> disableIgnore = [
                "xiaoye97-LDBTool", "CommonAPI-CommonAPI", "CommonAPI-DSPModSave", "nebula-NebulaMultiplayerModApi",
                "jinxOAO-BuildBarTool", "starfi5h-ErrorAnalyzer", "MengLei-GetDspData"
            ];
            string pluginsDir = $@"{R2_BepInEx}\plugins";
            foreach (var dir in Directory.GetDirectories(pluginsDir)) {
                if (enable && enableIgnore.Contains(new DirectoryInfo(dir).Name)) {
                    continue;
                }
                if (!enable && disableIgnore.Contains(new DirectoryInfo(dir).Name)) {
                    continue;
                }
                ChangeModEnable(new DirectoryInfo(dir).Name, enable);
            }
        }
    }
}
