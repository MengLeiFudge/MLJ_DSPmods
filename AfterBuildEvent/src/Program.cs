using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Xml;

namespace AfterBuildEvent {
    static class Program {
        private const string R2_Default =
            @"C:\Users\MLJ\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\Default";
        private const string R2_BepInEx = $@"{R2_Default}\BepInEx";
        private const string R2_DumpedDll = $@"{R2_BepInEx}\DumpedAssemblies\DSPGAME\Assembly-CSharp.dll";
        private const string PublicizerExe = @"..\..\..\lib\BepInEx.AssemblyPublicizer.Cli.exe";
        private const string Pdb2mdbExe = @"..\..\..\lib\pdb2mdb.exe";
        private const string DSPGameDir = @"D:\Steam\steamapps\common\Dyson Sphere Program";
        private const string KillDSP = "taskkill /f /im DSPGAME.exe";
        private const string RunModded = "start steam://rungameid/1366540";//可以修改游戏目录下的的效果

        public static void Main(string[] args) {
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
                    //要打包的所有文件
                    List<string> fileList = [];
                    var projectName = AssemblyName.InnerText;
                    string r2ModDir = $@"{R2_BepInEx}\plugins\MengLei-{projectName}";
                    string r2ModFile = $@"{r2ModDir}\{projectName}.dll";
                    string projectDir = $@"..\..\..\{projectName}";
#if DEBUG
                    string projectModFile = $@"..\..\..\{projectName}\bin\debug\{projectName}.dll";
#else
                    string projectModFile = $@"{projectDir}\bin\release\{projectName}.dll";
#endif
                    if (!Directory.Exists(r2ModDir)) {
                        Directory.CreateDirectory(r2ModDir);
                    }
                    if (File.Exists(projectModFile)) {
                        File.Copy(projectModFile, r2ModFile, true);
                        //用dll生成mdb文件，用于调试
                        cmd.Exec($"\"{Pdb2mdbExe}\" \"{r2ModFile}\"");//引号防止路径包含空格
                        Console.WriteLine($"已复制{projectName}.dll到BepInEx中，并生成mdb调试文件");
                        //添加dll到打包列表
                        fileList.Add(r2ModFile);
                    }
                    if (projectName == "FractionateEverything") {
                        //万物分馏还需要将图标文件拷贝至项目目录、r2Mod目录
                        string fracicons = @"D:\project\unity\DSP_FracIcons\AssetBundles\StandaloneWindows64\fracicons";
                        if (File.Exists(fracicons)) {
                            File.Copy(fracicons, @"..\..\..\FractionateEverything\Assets\fracicons", true);
                            File.Copy(fracicons, $@"{R2_BepInEx}\plugins\MengLei-FractionateEverything\fracicons",
                                true);
                            //添加资源文件到打包列表
                            fileList.Add(fracicons);
                        }
                    }
                    //打包ZIP
                    string projectReadme = $@"{projectDir}\README.md";
                    if (File.Exists(projectReadme)) {
                        fileList.Add(projectReadme);
                    }
                    string projectChangeLog = $@"{projectDir}\CHANGELOG.md";
                    if (File.Exists(projectChangeLog)) {
                        fileList.Add(projectChangeLog);
                    }
                    string projectManifest = $@"{projectDir}\Assets\manifest.json";
                    string version = null;
                    if (File.Exists(projectManifest)) {
                        fileList.Add(projectManifest);
                        var obj = JObject.Parse(File.ReadAllText(projectManifest));
                        if (obj.TryGetValue("version_number", out JToken token) && token.Type == JTokenType.String) {
                            string version_number = token.Value<string>();
                            if (!string.IsNullOrWhiteSpace(token.Value<string>())) {
                                if (version_number.StartsWith("v") || version_number.StartsWith("V")) {
                                    version = "_" + version_number;
                                }
                                else {
                                    version = "_v" + version_number;
                                }
                            }
                        }
                    }
                    string projectIcon = $@"{projectDir}\Assets\icon.png";
                    if (File.Exists(projectIcon)) {
                        fileList.Add(projectIcon);
                    }
                    ZipMod(fileList, $@".\ModZips\{projectName}{version}.zip");
                    if (projectName == "FractionateEverything") {
                        //给群友提供的测试版本，包含了如何使用R2导入的视频
                        string techVideo = $@"{projectDir}\Assets\如何从R2导入本地MOD.mp4";
                        if (File.Exists(techVideo)) {
                            fileList.Add(techVideo);
                            ZipMod(fileList, $@".\ModZips\{projectName}{version}（附带R2导入教学）.zip");
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
    }
}
