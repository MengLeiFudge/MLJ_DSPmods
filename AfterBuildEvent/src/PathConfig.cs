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
    public static string CompatibilityDir => $@"{SolutionFullDir}\FractionateEverything\src\Compatibility";
    public static string CheckPluginsSourcePath => $@"{CompatibilityDir}\CheckPlugins.cs";
    public static string DspCalcDir => @"D:\project\js\dsp-calc";
    public static string DspCalcGameDataPath => $@"{DspCalcDir}\src\engine\data\gameData.ts";
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
    private static string _modSourcesRootDir = @"D:\project\csharp\DSP MOD";
    private static string _moreMegaStructureSourceDir = "";
    private static string _theyComeFromVoidSourceDir = "";
    private static string _genesisBookSourceDir = "";
    private static string _orbitalRingSourceDir = "";
    private static string _fractionateEverythingSourceDir = "";
    public static string ModSourcesRootDir => _modSourcesRootDir;
    public static string MoreMegaStructureSourceDir => _moreMegaStructureSourceDir;
    public static string TheyComeFromVoidSourceDir => _theyComeFromVoidSourceDir;
    public static string GenesisBookSourceDir => _genesisBookSourceDir;
    public static string OrbitalRingSourceDir => _orbitalRingSourceDir;
    public static string FractionateEverythingSourceDir => _fractionateEverythingSourceDir;

    public static string SolutionDir => SolutionFullDir;
    public static string SolutionFullDir => ResolveSolutionFullDir();
    public static FileInfo PublicizerExe => new($@"{SolutionFullDir}\lib\BepInEx.AssemblyPublicizer.Cli.exe");
    public static FileInfo Pdb2mdbExe => new($@"{SolutionFullDir}\lib\pdb2mdb.exe");

    static PathConfig() {
        LoadPath();
    }

    private static void LoadPath() {
        try {
            XmlDocument xmlDocument = null;
            string defaultPathFile = $@"{SolutionFullDir}\DefaultPath.props";
            if (File.Exists(defaultPathFile)) {
                xmlDocument = new();
                xmlDocument.Load(defaultPathFile);
                _r2ProfileDir = ReadPathValue(xmlDocument, "ProfileDir", _r2ProfileDir);
                _dspGameDir = ReadPathValue(xmlDocument, "DSPGameDir", _dspGameDir);
                _nugetGameLibDir = ReadPathValue(xmlDocument, "NugetGameLibDir", _nugetGameLibDir);
                _modSourcesRootDir = ReadPathValue(xmlDocument, "ModSourcesRootDir", _modSourcesRootDir);
            }
            _moreMegaStructureSourceDir = ReadPathValue(
                xmlDocument,
                "MoreMegaStructureSourceDir",
                Path.Combine(_modSourcesRootDir, "DSPmod_MoreMegaStructures"));
            _theyComeFromVoidSourceDir = ReadPathValue(
                xmlDocument,
                "TheyComeFromVoidSourceDir",
                Path.Combine(_modSourcesRootDir, "DSP_Battle"));
            _genesisBookSourceDir = ReadPathValue(
                xmlDocument,
                "GenesisBookSourceDir",
                Path.Combine(_modSourcesRootDir, "ProjectGenesis"));
            _orbitalRingSourceDir = ReadPathValue(
                xmlDocument,
                "OrbitalRingSourceDir",
                Path.Combine(_modSourcesRootDir, "OrbitalRing-MOD"));
            _fractionateEverythingSourceDir = ReadPathValue(
                xmlDocument,
                "FractionateEverythingSourceDir",
                Path.Combine(SolutionFullDir, "FractionateEverything"));
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

    private static string ReadPathValue(XmlDocument xmlDocument, string propertyName, string fallback) {
        string value = xmlDocument?.SelectSingleNode($"/Project/PropertyGroup/{propertyName}")?.InnerText;
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string ResolveSolutionFullDir() {
        DirectoryInfo dir = new(AppContext.BaseDirectory);
        while (dir != null) {
            if (File.Exists(Path.Combine(dir.FullName, "MLJ_DSPmods.sln"))) {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"无法从程序目录向上定位 MLJ_DSPmods.sln：{AppContext.BaseDirectory}");
    }
}
