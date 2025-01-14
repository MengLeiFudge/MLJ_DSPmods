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
using static AfterBuildEvent.Utils;

namespace AfterBuildEvent {
    static class AfterBuildEvent {
        public static void Main(string[] args) {
            Console.WriteLine("本项目需要依赖于其他所有项目，且其他项目输出类型需要设定为类库");
            Console.WriteLine("输入要执行的命令（直接回车表示1）：");
            Console.WriteLine("1表示更新所有mod到R2，打包mod，然后启动游戏");
            Console.WriteLine("2表示生成计算器所需所有数据");
            string str = Console.ReadLine();
            if (str == "1" || str == "") {
                UpdateModsThenStart();
            } else if (str == "2") {
                GetAllCalcJson();
            } else {
                Console.WriteLine("输入有误！");
            }
        }

        #region 更新mod、打包、启动游戏

        static void UpdateModsThenStart() {
            using CmdProcess cmd = new();
            //强制终止游戏进程
            Console.WriteLine("终止游戏进程...");
            cmd.Exec(KillDSP);
            //等待游戏进程关闭
            Thread.Sleep(1000);
            //publicize已注入preloader的Assembly-CSharp.dll，然后将其拷贝到项目的lib
            //注：将BepInEX.cfg的DumpAssemblies设为true，就会生成已注入preloader的Assembly-CSharp.dll
            if (File.Exists(R2_DumpedDll_Origin)) {
                Console.WriteLine("开始尝试将注入preloader的Assembly-CSharp.dll公开");
                if (File.Exists(R2_DumpedDll_Publicized)) {
                    File.Delete(R2_DumpedDll_Publicized);
                }
                cmd.Exec($"cd \"{PublicizerExe.Directory}\"");//引号防止路径包含空格
                cmd.Exec($".\\{PublicizerExe.Name} \"{R2_DumpedDll_Origin}\"");//引号防止路径包含空格
                while (!File.Exists(R2_DumpedDll_Publicized)) {
                    Thread.Sleep(100);
                }
                Retry1:
                try {
                    File.Copy(R2_DumpedDll_Publicized, DSP_DumpedDll_Publicized, true);
                }
                catch (Exception) {
                    //文件刚生成不代表已经写完，所以如果仍在publicize，可能会抛出IOException
                    goto Retry1;
                }
                Console.WriteLine("已将注入preloader的Assembly-CSharp.dll公开，并复制到本地");
            } else {
                Console.WriteLine("未找到注入preloader的Assembly-CSharp.dll");
            }
            //publicize创世之书的dll，然后将其拷贝到项目的lib
            //注：如果R2禁用创世，也会出现找不到dll的情况
            if (File.Exists(R2_GenesisDll_Origin)) {
                Console.WriteLine("开始尝试将创世之书的dll公开");
                cmd.Exec($"cd \"{PublicizerExe.Directory}\"");//引号防止路径包含空格
                cmd.Exec($".\\{PublicizerExe.Name} \"{R2_GenesisDll_Origin}\"");//引号防止路径包含空格，必须绝对路径
                Retry2:
                try {
                    File.Copy(R2_GenesisDll_Publicized, DSP_GenesisDll_Publicized, true);
                }
                catch (Exception) {
                    //文件刚生成不代表已经写完，所以如果仍在publicize，可能会抛出IOException
                    Thread.Sleep(100);
                    goto Retry2;
                }
                File.Delete(R2_GenesisDll_Publicized);
                Console.WriteLine("已将创世之书的dll公开，并复制到本地");
            } else {
                Console.WriteLine("未找到创世之书的dll");
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
                    string projectModPdbFile = $@"{projectDir}\bin\debug\{projectName}.pdb";
                    string projectModMdbFile = $@"{projectDir}\bin\debug\{projectName}.dll.mdb";
#else
                    string projectModFile = $@"{projectDir}\bin\release\{projectName}.dll";
                    string projectModPdbFile = $@"{projectDir}\bin\release\{projectName}.pdb";
                    string projectModMdbFile = $@"{projectDir}\bin\release\{projectName}.dll.mdb";
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
                        cmd.Exec($"cd \"{Pdb2mdbExe.Directory}\"");//引号防止路径包含空格
                        cmd.Exec($".\\pdb2mdb \"{new FileInfo(projectModFile).FullName}\"");//引号防止路径包含空格，必须绝对路径
                        Console.WriteLine("注：如果卡在这里，说明需要调整项目设置，勾选debug symbols并且修改debug type为full");
                        while (!File.Exists(projectModMdbFile)) {
                            Thread.Sleep(100);
                        }
                        //注：mdb文件不加到fileList里面，因为它不需要打包。最后会单独处理它。
                        Console.WriteLine($"已生成{projectName}的mdb文件");
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
                    } else if (projectName == "FractionateEverything") {
                        //fracicons
                        string[] icons = [
                            "fracicons"
                        ];
                        foreach (var icon in icons) {
                            string iconPath =
                                $@"D:\project\unity\DSP_FracIcons\AssetBundles\StandaloneWindows64\{icon}";
                            if (File.Exists(iconPath)) {
                                //同时拷贝到项目
                                File.Copy(iconPath, $@"..\..\..\FractionateEverything\Assets\{icon}", true);
                                fileList.Add(iconPath);
                            }
                        }
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
                    //所有文件复制到R2，注意R2是否禁用了mod
                    //mdb也要复制到R2（pdb不需要）
                    fileList.Add(projectModMdbFile);
                    foreach (var file in fileList) {
                        string relativePath = Path.GetFileName(file);
                        string r2FilePath = $@"{R2_BepInEx}\plugins\MengLei-{projectName}\{relativePath}";
                        string r2OldFilePath = $"{r2FilePath}.old";
                        string targetPath = !File.Exists(r2OldFilePath) ? r2FilePath : r2OldFilePath;
                        FileInfo fileInfo = new FileInfo(targetPath);
                        if (!fileInfo.Directory.Exists) {
                            Directory.CreateDirectory(fileInfo.Directory.FullName);
                        }
                        File.Copy(file, targetPath, true);
                        Console.WriteLine($"复制 {file} -> {targetPath}");
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
                } else if (lines[i].StartsWith("targetAssembly=")) {
                    lines[i] = $@"targetAssembly={R2_BepInEx}\core\BepInEx.Preloader.dll";
                } else if (lines[i].StartsWith("ignoreDisableSwitch=")) {
                    lines[i] = "ignoreDisableSwitch=false";
                }
            }
            File.WriteAllLines(doorstop_config, lines);
            //启动使用R2MOD的游戏
            Console.WriteLine("是否启动游戏？1或回车表示启动，其他表示结束程序");
            string str = Console.ReadLine();
            if (str == "" || str == "1") {
                cmd.Exec(RunModded);
            }
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

        #region 生成戴森球量化计算器所需文件，并将其复制到计算器项目目录下

        private static void GetAllCalcJson() {
            using CmdProcess cmd = new();
            //终止游戏
            cmd.Exec(KillDSP);
            Console.WriteLine("确认是否已经打开矩阵分馏、燃料棒分馏？回车确认");//todo:自动修改配置文件，开启
            Console.ReadLine();
            //将R2的winhttp.dll、doorstop_config.ini复制到游戏目录
            File.Copy($@"{R2_Default}\winhttp.dll", $@"{DSPGameDir}\winhttp.dll", true);
            string doorstop_config = $@"{DSPGameDir}\doorstop_config.ini";
            File.Copy($@"{R2_Default}\doorstop_config.ini", doorstop_config, true);
            //修改doorstop_config.ini，使其目标指向R2的preloader.dll
            string[] lines = File.ReadAllLines(doorstop_config);
            for (int i = 0; i < lines.Length; i++) {
                if (lines[i].StartsWith("enabled=")) {
                    lines[i] = "enabled=true";
                } else if (lines[i].StartsWith("targetAssembly=")) {
                    lines[i] = $@"targetAssembly={R2_BepInEx}\core\BepInEx.Preloader.dll";
                } else if (lines[i].StartsWith("ignoreDisableSwitch=")) {
                    lines[i] = "ignoreDisableSwitch=false";
                }
            }
            File.WriteAllLines(doorstop_config, lines);
            //判断所有mod是否均已存在
            string[] mods = [
                "jinxOAO-MoreMegaStructure",//mod a：更多巨构
                "ckcz123-TheyComeFromVoid",//mod b：虚空来敌
                "HiddenCirno-GenesisBook",//mod c：创世之书
                "MengLei-FractionateEverything",//mod d：万物分馏
            ];
            for (int i = 0; i < mods.Length; i++) {
                string modPluginsDir = $@"{R2_BepInEx}\plugins\{mods[i]}";
                if (!Directory.Exists(modPluginsDir)) {
                    Console.WriteLine($"未找到 {modPluginsDir}，无法生成计算器所需文件！");
                    return;
                }
            }
            //禁用所有mod，加快启动速度
            ChangeAllModsEnable(false);
            //生成计算器json
            bool[] modsEnable = new bool[mods.Length];
            int[] set = [0, 1, 2, 3];
            for (int i = 0; i <= mods.Length; i++) {
                IEnumerable<IEnumerable<int>> result = Combinations(set, i);
                foreach (IEnumerable<int> combo in result) {
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
            //启用所有mod   todo：改为读取r2配置文件
            ChangeAllModsEnable(true);
            //终止游戏
            cmd.Exec(KillDSP);
        }

        private static void WriteOneJson(CmdProcess cmd, string[] mods, bool[] modsEnable) {
            string oriFilePath = GetJsonFilePath(mods, modsEnable, false);
            if (File.Exists(oriFilePath)) {
                Console.WriteLine($"{oriFilePath} 已存在，跳过生成");
                return;
            }
            Console.WriteLine("终止游戏进程...");
            cmd.Exec(KillDSP);
            for (int i = 0; i < mods.Length; i++) {
                ChangeModEnable(mods[i], modsEnable[i]);
            }
            StringBuilder sb = new("启动游戏，mod情况：");
            for (int i = 0; i < mods.Length; i++) {
                sb.Append(mods[i].Substring(mods[i].LastIndexOf("-") + 1)).Append(modsEnable[i] ? "启用 " : "禁用 ");
            }
            Console.WriteLine(sb.ToString());
            cmd.Exec(RunModded);
            while (!File.Exists(oriFilePath)) {
                Thread.Sleep(100);
            }
            //多等一会，确保文件已经全部写入
            Thread.Sleep(500);
            Console.WriteLine($"已生成 {oriFilePath}");
            string calcFilePath = GetJsonFilePath(mods, modsEnable, true);
            DirectoryInfo info = new FileInfo(calcFilePath).Directory;
            if (info == null || !info.Exists) {
                Console.WriteLine("未检测到戴森球计算器项目对应的文件夹，跳过复制");
                return;
            }
            File.Copy(oriFilePath, calcFilePath, true);
            if (!File.Exists(calcFilePath) || new FileInfo(calcFilePath).Length != new FileInfo(oriFilePath).Length) {
                Console.WriteLine("复制计算器json文件失败");
                return;
            }
            Console.WriteLine($"已复制到 {calcFilePath}");
        }

        private static string GetJsonFilePath(string[] mods, bool[] modsEnable, bool isCalc) {
            string name = "";
            for (int i = 0; i < mods.Length; i++) {
                if (modsEnable[i]) {
                    name += "_" + mods[i].Split('-')[1];
                }
            }
            name = name == "" ? "vanilla" : name.Substring(1);
            return isCalc
                ? $@"D:\project\js\dsp-calc\data\{name}.json"
                : $@"..\..\..\GetDspData\gamedata\calc json\{name}.json";
        }

        #endregion
    }
}
