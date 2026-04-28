using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace AfterBuildEvent;

public static class PathConfig {
    private static string UserDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string _r2ProfileDir =
        $@"{UserDir}\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\Default";
    public static string R2ProfileDir => _r2ProfileDir;
    public static string ModsConfigPath => $@"{R2ProfileDir}\mods.yml";
    public static string R2PluginsDir => $@"{R2ProfileDir}\BepInEx\plugins";
    public static string CompatibilityDir => $@"{SolutionDir}\FractionateEverything\src\Compatibility";
    public static string CheckPluginsSourcePath => $@"{CompatibilityDir}\CheckPlugins.cs";
    public static string DspCalcDir => @"D:\project\js\dsp-calc";
    public static string DspCalcRawDataDir => $@"{DspCalcDir}\src\engine\data\raw";
    public static string DspCalcIconAssetsDir => $@"{DspCalcDir}\src\ui\components\icons\assets";
    public static string DspCalcFullIconDir => $@"{SolutionFullDir}\gamedata\icons";
    public static string CalcJsonLocalDir => $@"{SolutionFullDir}\gamedata\calc json";
    public static string CalcIconWorkDir => $@"{SolutionFullDir}\gamedata\test";
    public static string AssetStudioToolDir => $@"{SolutionFullDir}\lib\tools\AssetStudio-net8.0-win";
    public static string AssetStudioZipPath => $@"{SolutionFullDir}\lib\tools\AssetStudio-net8.0-win.zip";
    public static string AssetStudioCliPath => $@"{AssetStudioToolDir}\AssetStudio.CLI.exe";
    public const string AssetStudioDownloadUrl =
        "https://github.com/Razviar/assetstudio/releases/download/v2.4.1/AssetStudio-net8.0-win.zip";
    public static string IconExportRequestPath => $@"{SolutionFullDir}\gamedata\calc-icon-export-request.json";
    public static string IconExportMarkerPath => $@"{SolutionFullDir}\gamedata\calc-icon-export-done.json";

    private static string _dspGameDir = @"D:\Steam\steamapps\common\Dyson Sphere Program";
    public static string DSPGameDir => _dspGameDir;
    public static readonly string DSPACDll = $@"{DSPGameDir}\DSPGAME_Data\Managed\Assembly-CSharp.dll";
    public static readonly string DSPUIDll = $@"{DSPGameDir}\DSPGAME_Data\Managed\UnityEngine.UI.dll";

    private static string _nugetGameLibDir = $@"{UserDir}\.nuget\packages\dysonsphereprogram.gamelibs";
    public static string NugetGameLibNet45Dir;

    public static string SolutionDir => @"..\..\..\..";
    public static string SolutionFullDir => ResolveSolutionFullDir();
    public static FileInfo PublicizerExe => new($@"{SolutionDir}\lib\BepInEx.AssemblyPublicizer.Cli.exe");
    public static FileInfo Pdb2mdbExe => new($@"{SolutionDir}\lib\pdb2mdb.exe");

    static PathConfig() {
        LoadPath();
    }

    private static void LoadPath() {
        try {
            XmlDocument xmlDocument;
            string defaultPathFile = $@"{SolutionDir}\DefaultPath.props";
            if (File.Exists(defaultPathFile)) {
                xmlDocument = new();
                xmlDocument.Load(defaultPathFile);
                _r2ProfileDir = xmlDocument.SelectSingleNode("/Project/PropertyGroup/ProfileDir")?.InnerText;
                _dspGameDir = xmlDocument.SelectSingleNode("/Project/PropertyGroup/DSPGameDir")?.InnerText;
                _nugetGameLibDir = xmlDocument.SelectSingleNode("/Project/PropertyGroup/NugetGameLibDir")?.InnerText;
            }
            // 扫描 nuget 包目录，自动找最新已安装版本（兼容 Version="*-*" 通配符写法）
            var nugetBaseDir = new DirectoryInfo(_nugetGameLibDir);
            if (nugetBaseDir.Exists) {
                DirectoryInfo latestDir = nugetBaseDir.GetDirectories()
                    .OrderByDescending(d => d.LastWriteTime)
                    .FirstOrDefault();
                if (latestDir != null) {
                    NugetGameLibNet45Dir = $@"{latestDir.FullName}\lib\net45";
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error loading from DefaultPath.props: {ex.Message}");
        }
    }

    private static string ResolveSolutionFullDir() {
        DirectoryInfo dir = new(AppContext.BaseDirectory);
        while (dir != null) {
            if (File.Exists(Path.Combine(dir.FullName, "MLJ_DSPmods.sln"))) {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return Path.GetFullPath(SolutionDir);
    }
}
