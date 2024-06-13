using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace AfterBuildEvent {
    static class AfterBuildEvent {
        private const string R2_Default =
            @"C:\Users\MLJ\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\Default";
        private const string R2_BepInEx = $@"{R2_Default}\BepInEx";
        private const string R2_DumpedDll = $@"{R2_BepInEx}\DumpedAssemblies\DSPGAME\Assembly-CSharp.dll";
        private const string PublicizerExe = @"..\..\..\lib\BepInEx.AssemblyPublicizer.Cli.exe";
        private const string Pdb2mdbExe = @"..\..\..\lib\pdb2mdb.exe";
        private const string DSPGameDir = @"D:\Steam\steamapps\common\Dyson Sphere Program";
        private const string KillDSP = "taskkill /f /im DSPGAME.exe";
        private const string RunModded = "start steam://rungameid/1366540";

        public static void Main(string[] args) {
            Console.WriteLine("输入要执行的命令（直接回车表示1）：");
            Console.WriteLine("1表示更新所有mod到R2，打包mod，然后启动游戏");
            Console.WriteLine("2表示生成计算器所需所有数据");
            string cmdStr = Console.ReadLine();
            if (cmdStr == "1" || cmdStr == "") {
                UpdateModsThenStart();
            }
            else if (cmdStr == "2") {
                GetAllClacJson();
            }
            else {
                Console.WriteLine("输入有误！");
            }
        }

        static void UpdateModsThenStart() {
            using CmdProcess cmd = new();
            //强制终止游戏进程
            Console.WriteLine("终止游戏进程...");
            cmd.Exec(KillDSP);
            //等待游戏进程关闭
            Thread.Sleep(1000);
            //将注入preloader的Assembly-CSharp.dll的所有内容公开，生成的文件放在项目目录下
            //注1：需要将BepInEX.cfg的DumpAssemblies设为true，才有注入preloader的Assembly-CSharp.dll
            //注2：正常使用Publicizer的前提是存在注入preloader的Assembly-CSharp.dll
            if (File.Exists(R2_DumpedDll)) {
                cmd.Exec($"\"{PublicizerExe}\" \"{R2_DumpedDll}\"");//引号防止路径包含空格
                Console.WriteLine("已将注入preloader的Assembly-CSharp.dll公开");
            }
            else {
                Console.WriteLine("未找到注入preloader的Assembly-CSharp.dll");
            }
            //遍历所有csproj，拷贝dll（本程序Debug则仅拷贝所有debug的dll，Release则仅拷贝release的dll）
            foreach (var dirInfo in new DirectoryInfo(@"..\..\..").GetDirectories()) {
                string csproj = $@"{dirInfo.FullName}\{dirInfo.Name}.csproj";
                if (!File.Exists(csproj)) {
                    continue;
                }
                XmlDocument xml = new();
                XmlReader reader = XmlReader.Create(csproj);
                xml.Load(reader);
                reader.Close();
                XmlNamespaceManager nsMgr = new XmlNamespaceManager(xml.NameTable);
                nsMgr.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003");
                var OutputType = xml.SelectSingleNode("/ns:Project/ns:PropertyGroup/ns:OutputType", nsMgr);
                if (OutputType == null) {
                    continue;
                }
                if (OutputType.InnerText == "Library") {
                    var AssemblyName = xml.SelectSingleNode("/ns:Project/ns:PropertyGroup/ns:AssemblyName", nsMgr);
                    if (AssemblyName == null) {
                        continue;
                    }
                    //要打包的所有文件，也是要复制到R2_BepInEx的文件
                    List<string> fileList = [];
                    var projectName = AssemblyName.InnerText;
                    string r2ModDir = $@"{R2_BepInEx}\plugins\MengLei-{projectName}";
                    string projectDir = $@"..\..\..\{projectName}";
                    //mod.dll
#if DEBUG
                    string projectModFile = $@"{projectDir}\bin\debug\{projectName}.dll";
#else
                    string projectModFile = $@"{projectDir}\bin\release\{projectName}.dll";
#endif
                    if (!File.Exists(projectModFile)) {
                        continue;
                    }
                    fileList.Add(projectModFile);
                    //mod.dll.mdb
                    // cmd.Exec($"\"{Pdb2mdbExe}\" \"{projectModFile}\"");//引号防止路径包含空格
                    // string projectMdb = $@"{projectModFile}.mdb";
                    // if (File.Exists(projectMdb)) {
                    //     fileList.Add(projectMdb);
                    // }
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
                    if (File.Exists(projectManifest)) {
                        fileList.Add(projectManifest);
                        var obj = JObject.Parse(File.ReadAllText(projectManifest));
                        if (obj.TryGetValue("version_number", out JToken value)) {
                            version = "_" + value;
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
                        string jsonDll = @"..\..\..\packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll";
                        if (File.Exists(jsonDll)) {
                            fileList.Add(jsonDll);
                        }
                    }
                    else if (projectName == "FractionateEverything") {
                        //fracicons
                        string fracicons = @"D:\project\unity\DSP_FracIcons\AssetBundles\StandaloneWindows64\fracicons";
                        if (File.Exists(fracicons)) {
                            File.Copy(fracicons, @"..\..\..\FractionateEverything\Assets\fracicons", true);//拷贝到项目
                            fileList.Add(fracicons);
                        }
                    }
                    //打包
                    if (!Directory.Exists(r2ModDir)) {
                        Directory.CreateDirectory(r2ModDir);
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
                    foreach (var file in fileList) {
                        string r2FilePath = $@"{R2_BepInEx}\plugins\MengLei-{projectName}\{Path.GetFileName(file)}";
                        string r2OldFilePath = $"{r2FilePath}.old";
                        string targetPath = !File.Exists(r2OldFilePath) ? r2FilePath : r2OldFilePath;
                        File.Copy(file, targetPath, true);
                        Console.WriteLine($"复制 {file} -> {targetPath}");
                    }
                    //额外打包
                    if (projectName == "FractionateEverything") {
                        //给群友提供的测试版本，包含了如何使用R2导入的视频
                        string techVideo = $@"{projectDir}\Assets\如何从R2导入本地MOD.mp4";
                        if (File.Exists(techVideo)) {
                            fileList.Add(techVideo);
                            zipFile = $@".\ModZips\{projectName}{version}（附带R2导入教学）.zip";
                            ZipMod(fileList, zipFile);
                            Console.WriteLine($"创建 {zipFile}");
                        }
                    }
                }
            }
            //打开所有压缩包的文件夹
            Process.Start("explorer", @".\ModZips");
            //将R2的winhttp.dll、doorstop_config.ini复制到游戏目录
            File.Copy($@"{R2_Default}\winhttp.dll", $@"{DSPGameDir}\winhttp.dll", true);
            string doorstop_config = $@"{DSPGameDir}\doorstop_config.ini";
            File.Copy($@"{R2_Default}\doorstop_config.ini", doorstop_config, true);
            //修改doorstop_config.ini，使其目标指向R2的preloader.dll
            string[] lines = File.ReadAllLines(doorstop_config);
            for (int i = 0; i < lines.Length; i++) {
                if (lines[i].StartsWith("enabled=")) {
                    lines[i] = "enabled=true";
                }
                else if (lines[i].StartsWith("targetAssembly=")) {
                    lines[i] = $@"targetAssembly={R2_BepInEx}\core\BepInEx.Preloader.dll";
                }
                else if (lines[i].StartsWith("ignoreDisableSwitch=")) {
                    lines[i] = "ignoreDisableSwitch=false";
                }
            }
            File.WriteAllLines(doorstop_config, lines);
            //启动使用R2MOD的游戏
            cmd.Exec(RunModded);
        }

        static void ZipMod(List<string> fileList, string zipPath) {
            string zipParentDir = new FileInfo(zipPath).DirectoryName;
            if (!Directory.Exists(zipParentDir)) {
                Directory.CreateDirectory(zipParentDir);
            }
            if (File.Exists(zipPath)) {
                File.Delete(zipPath);
            }
            using var zipStream = new ZipOutputStream(File.Create(zipPath));
            foreach (string file in fileList) {
                //MOD上传至R2的时候，文件要直接打包在里面，不能嵌套文件夹，所以相对路径直接使用文件名
                string relativePath = Path.GetFileName(file);
                var entry = new ZipEntry(relativePath);
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

        static void GetAllClacJson() {
            using CmdProcess cmd = new();
            string[] mods = [
                "jinxOAO-MoreMegaStructure",//mod a：更多巨构
                "ckcz123-TheyComeFromVoid",//mod b：虚空来敌
                "HiddenCirno-GenesisBook",//mod c：创世之书
                "MengLei-FractionateEverything",//mod d：万物分馏
            ];
            //判断所有mod是否均已存在
            for (int i = 0; i < mods.Length; i++) {
                string modPluginsDir = $@"{R2_BepInEx}\plugins\{mods[i]}";
                if (!Directory.Exists(modPluginsDir)) {
                    Console.WriteLine($"未找到 {modPluginsDir}，无法生成计算器所需文件！");
                    return;
                }
            }
            //生成计算器json
            bool[] modsEnable = new bool[mods.Length];
            int[] set = [0, 1, 2, 3];
            for (int i = 0; i <= mods.Length; i++) {
                IEnumerable<IEnumerable<int>> result = Combinations(set, i);
                foreach (var combo in result) {
                    for (int j = 0; j < mods.Length; j++) {
                        modsEnable[j] = combo.Contains(j);
                    }
                    //战斗的前置依赖为巨构，需要特别处理一下
                    if (modsEnable[1] && !modsEnable[0]) {
                        continue;
                    }
                    WriteOneJson(cmd, mods, modsEnable);
                }
            }
            //恢复mod启用禁用状态
            //todo：改为读取r2配置文件
            // for (int i = 0; i < mods.Length; i++) {
            //     ChangeModEnable(mods[i], modsEnableFirstState[i]);
            // }
            //终止游戏
            cmd.Exec(KillDSP);
        }

        public static IEnumerable<IEnumerable<int>> Combinations(int[] set, int r) {
            return Combinations(set, r, 0);
        }

        private static IEnumerable<IEnumerable<int>> Combinations(int[] set, int r, int index) {
            if (r == 0) {
                return new[] { new int[0] };
            }
            var combos = new List<IEnumerable<int>>();
            for (int i = index; i <= set.Length - r; i++) {
                var head = new[] { set[i] };
                var tailCombinations = Combinations(set, r - 1, i + 1);
                foreach (var tail in tailCombinations) {
                    combos.Add(head.Concat(tail));
                }
            }
            return combos;
        }

        static void WriteOneJson(CmdProcess cmd, string[] mods, bool[] modsEnable) {
            string filePath = GetJsonFilePath(mods, modsEnable);
            if (File.Exists(filePath)) {
                Console.WriteLine($"{filePath} 已存在，跳过生成");
                return;
            }
            Console.WriteLine("终止游戏进程...");
            cmd.Exec(KillDSP);
            for (int i = 0; i < mods.Length; i++) {
                ChangeModEnable(mods[i], modsEnable[i]);
            }
            StringBuilder sb = new("启动游戏，mod情况：");
            for (int i = 0; i < mods.Length; i++) {
                sb.Append(modsEnable[i] ? "启用 " : "禁用 ");
            }
            Console.WriteLine(sb.ToString());
            cmd.Exec(RunModded);
            while (true) {
                Thread.Sleep(500);
                if (!File.Exists(filePath)) {
                    continue;
                }
                FileInfo info = new FileInfo(filePath);
                if (info.LastWriteTime > DateTime.Now.AddSeconds(-10)) {
                    Console.WriteLine($"已生成{filePath}");
                    Thread.Sleep(1000);
                    break;
                }
            }
        }

        static string GetJsonFilePath(string[] mods, bool[] modsEnable) {
            string name = "";
            for (int i = 0; i < mods.Length; i++) {
                if (modsEnable[i]) {
                    name += "_" + mods[i].Split('-')[1];
                }
            }
            name = name == "" ? "vanilla" : name.Substring(1);
            return $@"..\..\..\GetDspData\gamedata\calc json\{name}.json";
        }

        static void ChangeModEnable(string mod, bool enable) {
            string modPatchersDir = $@"{R2_BepInEx}\patchers\{mod}";
            string modPluginsDir = $@"{R2_BepInEx}\plugins\{mod}";
            if (Directory.Exists(modPatchersDir)) {
                foreach (var file in Directory.GetFiles(modPatchersDir)) {
                    if (enable) {
                        if (file.EndsWith(".old")) {
                            File.Move(file, file.Substring(0, file.Length - 4));
                        }
                    }
                    else {
                        if (!file.EndsWith(".old")) {
                            File.Move(file, file + ".old");
                        }
                    }
                }
            }
            foreach (var file in Directory.GetFiles(modPluginsDir)) {
                if (enable) {
                    if (file.EndsWith(".old")) {
                        File.Move(file, file.Substring(0, file.Length - 4));
                    }
                }
                else {
                    if (!file.EndsWith(".old")) {
                        File.Move(file, file + ".old");
                    }
                }
            }
        }
    }
}
