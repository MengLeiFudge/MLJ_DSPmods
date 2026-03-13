using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using static AfterBuildEvent.Utils;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent;

static class AfterBuildEvent {
    public static void Main(string[] args) {
        Console.WriteLine("本项目需要依赖于其他所有项目，且其他项目输出类型需要设定为类库");
        Console.WriteLine("输入要执行的命令（直接回车表示1）：");
        Console.WriteLine("1表示更新所有mod到R2，打包mod，然后启动游戏");
        Console.WriteLine("2表示更新部分需要的dll类库");
        Console.WriteLine("3表示生成计算器所需所有数据");
        string str = Console.ReadLine();
        if (str == "1" || str == "") {
            UpdateModsThenStart();
        } else if (str == "2") {
            UpdateLibDll();
        } else if (str == "3") {
            GetAllCalcJson();
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
        // 下面三个mod dll，r2禁用时会自动加.old后缀，由PublizeDll内部逻辑自动识别并处理
        PublizeDll(cmd, R2VDDll, $@"{SolutionDir}\lib\DSP_Battle-publicized.dll");
        DecompileDll(cmd, "DSP_Battle-publicized.dll", $@"{SolutionDir}\lib");
        PublizeDll(cmd, R2GBDll, $@"{SolutionDir}\lib\ProjectGenesis-publicized.dll");
        DecompileDll(cmd, "ProjectGenesis-publicized.dll", $@"{SolutionDir}\lib");
        PublizeDll(cmd, R2ORDll, $@"{SolutionDir}\lib\ProjectOrbitalRing-publicized.dll");
        DecompileDll(cmd, "ProjectOrbitalRing-publicized.dll", $@"{SolutionDir}\lib");
    }

    private static void DecompileDll(CmdProcess cmd, string dllName, string sourceDir = null) {
        string dllPath = sourceDir != null
            ? $@"{sourceDir}\{dllName}"
            : $@"{NugetGameLibNet45Dir}\{dllName}";
        string dllNameNoExt = Path.GetFileNameWithoutExtension(dllName).Replace("-publicized", "");
        string outputDir = Path.GetFullPath($@"{SolutionDir}\gamedata\DecompiledSource\{dllNameNoExt}");
        string csprojPath = Path.Combine(outputDir, $"{dllNameNoExt}.csproj");
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

    #region 生成戴森球量化计算器所需文件，并将其复制到计算器项目目录下

    private static void GetAllCalcJson() {
        using CmdProcess cmd = new();
        //终止游戏
        cmd.Exec(KillDSP);
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
        //判断所有mod是否均已存在
        List<string> names = [
            "jinxOAO-MoreMegaStructure",//mod a：更多巨构
            "ckcz123-TheyComeFromVoid",//mod b：深空来敌
            "HiddenCirno-GenesisBook",//mod c1：创世之书
            "GenesisBook-GenesisBook_Experimental",//mod c2：创世之书测试版
            "ProfessorCat305-OrbitalRing",//mod d：星环
            "MengLei-FractionateEverything",//mod e：万物分馏
        ];
        bool GBExExists = false;
        for (int i = 0; i < names.Count; i++) {
            string modPluginsDir = $@"{R2ProfileDir}\BepInEx\plugins\{names[i]}";
            if (!Directory.Exists(modPluginsDir)) {
                if (i == 3) {
                    GBExExists = true;
                } else {
                    Console.WriteLine($"未找到 {modPluginsDir}，无法生成计算器所需文件！");
                    return;
                }
            }
        }
        //载入Mod数据，然后构建ModInfo数组
        LoadModInfos();
        ModInfo[] modInfos = names.Select(GetModInfo).ToArray();
        //生成计算器json
        for (int r = 0; r <= modInfos.Length; r++) {
            List<List<ModInfo>> result = Combinations(modInfos, r);
            for (int index = 0; index < result.Count; index++) {
                List<ModInfo> state = result[index];
                if (!GBExExists && state.Contains(modInfos[3])) {
                    continue;
                }
                //巨构是深空的前置依赖
                if (!state.Contains(modInfos[0]) && state.Contains(modInfos[1])) {
                    continue;
                }
                //创世、创世测试版、星环只能启用一个
                if (state.Contains(modInfos[2]) && state.Contains(modInfos[3])
                    || state.Contains(modInfos[2]) && state.Contains(modInfos[4])
                    || state.Contains(modInfos[3]) && state.Contains(modInfos[4])) {
                    continue;
                }
                //开始准备json相关内容
                string oriFilePath = GetJsonFilePath(state, false);
                string calcFilePath = GetJsonFilePath(state, true);
                if (!File.Exists(oriFilePath)) {
                    Console.WriteLine("终止游戏进程...");
                    cmd.Exec(KillDSP);
                    //仅启用指定的模组
                    HashSet<string> nameList = [];
                    state.Add(GetModInfo("MengLei-GetDspData"));
                    state.Add(GetModInfo("starfi5h-ErrorAnalyzer"));
                    foreach (ModInfo modInfo in state) {
                        nameList.Add(modInfo.name);
                        List<string> dependencies = GetDependencies(modInfo.name);
                        foreach (string dependency in dependencies) {
                            nameList.Add(dependency);
                        }
                    }
                    OnlyEnableInputMods(nameList.ToList());
                    StringBuilder sb = new("启动游戏，mod情况：");
                    for (int i = 0; i < modInfos.Length; i++) {
                        sb.Append(modInfos[i].displayName).Append(state.Contains(modInfos[i]) ? "启用 " : "禁用 ");
                    }
                    Console.WriteLine(sb.ToString());
                    cmd.Exec(RunDSP);
                    while (!File.Exists(oriFilePath)) {
                        Thread.Sleep(100);
                    }
                    //多等一会，确保文件已经全部写入
                    Thread.Sleep(500);
                }
                Console.WriteLine($"已生成 {oriFilePath}");
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
        //启用R2配置文件中所有enable为true的mod
        EnableModsByConfig();
        //终止游戏
        cmd.Exec(KillDSP);
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
            ? $@"D:\project\js\dsp-calc\data\{jsonFileName}.json"
            : $@"..\..\..\..\gamedata\calc json\{jsonFileName}.json";
    }

    #endregion
}
