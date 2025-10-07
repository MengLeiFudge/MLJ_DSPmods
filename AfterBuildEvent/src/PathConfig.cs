using System;
using System.IO;
using System.Xml;

namespace AfterBuildEvent;

public static class PathConfig {
    private static string UserDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string _r2ProfileDir =
        $@"{UserDir}\AppData\Roaming\r2modmanPlus-local\DysonSphereProgram\profiles\Default";
    public static string R2ProfileDir => _r2ProfileDir;
    //将BepInEX.cfg的DumpAssemblies设为true，以生成此DLL
    public static readonly string R2ACDll =
        $@"{R2ProfileDir}\BepInEx\DumpedAssemblies\DSPGAME\Assembly-CSharp.dll";
    public static readonly string R2VDDll =
        $@"{R2ProfileDir}\BepInEx\plugins\\ckcz123-TheyComeFromVoid\DSP_Battle.dll";
    public static readonly string R2GBDll =
        $@"{R2ProfileDir}\BepInEx\plugins\HiddenCirno-GenesisBook\ProjectGenesis.dll";
    public static readonly string R2ORDll =
        $@"{R2ProfileDir}\BepInEx\plugins\ProfessorCat305-OrbitalRing\ProjectOrbitalRing.dll";

    private static string _dspGameDir = @"D:\Steam\steamapps\common\Dyson Sphere Program";
    public static string DSPGameDir => _dspGameDir;
    public static readonly string DSPACDll = $@"{DSPGameDir}\DSPGAME_Data\Managed\Assembly-CSharp.dll";
    public static readonly string DSPUIDll = $@"{DSPGameDir}\DSPGAME_Data\Managed\UnityEngine.UI.dll";

    private static string _nugetGameLibDir = $@"{UserDir}\.nuget\packages\dysonsphereprogram.gamelibs";
    public static string NugetGameLibNet45Dir => _nugetGameLibDir;

    public static string SolutionDir => @"..\..\..\..";
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
            string feCsprojFile = $@"{SolutionDir}\FractionateEverything\FractionateEverything.csproj";
            if (File.Exists(feCsprojFile)) {
                xmlDocument = new();
                xmlDocument.Load(feCsprojFile);
                XmlAttributeCollection xmlAttributeCollection = xmlDocument
                    .SelectSingleNode("/Project/ItemGroup/PackageReference[@Include='DysonSphereProgram.GameLibs']")
                    ?.Attributes;
                if (xmlAttributeCollection != null) {
                    string version = xmlAttributeCollection["Version"]?.Value;
                    _nugetGameLibDir += $@"\{version}\lib\net45";
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error loading from DefaultPath.props: {ex.Message}");
        }
    }
}
