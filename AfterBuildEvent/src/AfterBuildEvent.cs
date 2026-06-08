using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using AfterBuildEvent.DspCalcQuickUpdate;
using static AfterBuildEvent.Utils;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent;

static partial class AfterBuildEvent {
    private static readonly Regex SoftDependencyRegex =
        new(@"\[BepInDependency\(([^,\)]+),\s*BepInDependency\.DependencyFlags\.SoftDependency\)\]",
            RegexOptions.Compiled);
    private static readonly Regex GuidLiteralRegex =
        new(@"public\s+const\s+string\s+GUID\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly string[] IgnoredModDllPrefixes =
        ["System.", "Newtonsoft.Json", "0Harmony", "BepInEx.", "Mono.", "MonoMod.", "K4os.", "Unity."];
    private static readonly string[] IgnoredModDllNames =
        ["websocket-sharp", "discord_game_sdk", "discord_game_sdk_dotnet", "Open.Nat", "HarmonyXInterop"];
    private static readonly string[] CalcJsonLocalProjectNames = ["FractionateEverything", "GetDspData"];
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private const int CalcJsonCacheVersion = 2;
    private const int R2CopyRetryCount = 60;
    private const int R2CopyRetryDelayMs = 500;
    private const int QqbotArtifactUploadTimeoutMs = 60000;
    private const string AutoUploadProjectId = "mlj_dspmods";
    private const int AutoUploadGroupId = 319567534;
    private const string QqbotArtifactUploadUrl = "http://127.0.0.1:8080/admin/api/artifacts/publish-local";
#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    private sealed class ModDecompileTarget {
        public string DependencyExpression { get; set; } = "";
        public string SourceName { get; set; } = "";
        public List<string> Keywords { get; set; } = [];
    }

    private sealed class GeneratedPackageInfo {
        public string ProjectName { get; set; } = "";
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public long SizeBytes { get; set; }
        public string LastWriteTimeUtc { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }

    private sealed class PublishTarget {
        public string ProjectName { get; set; } = "";
        public int[] GroupIds { get; set; } = [];
    }

    private static readonly PublishTarget[] PublishTargets = [
        new() {
            ProjectName = "FractionateEverything",
            GroupIds = [AutoUploadGroupId],
        },
        new() {
            ProjectName = "SaveDataExporter",
            GroupIds = [AutoUploadGroupId],
        },
    ];

    private sealed class CalcIconExportTarget {
        public string TargetMod { get; set; } = "";
        public string SourceDirName { get; set; } = "";
        public string SourcePrefix { get; set; } = "";
        public string AssetStudioPackageName { get; set; } = "";
        public List<string> AssetStudioRelativePaths { get; set; } = [];
        public List<string> AssetStudioGameDataRelativePaths { get; set; } = [];
        public string EmbeddedDllPackageName { get; set; } = "";
        public string EmbeddedDllRelativePath { get; set; } = "";
        public List<string> EnabledMods { get; set; } = [];
        public List<string> LowerPriorityMods { get; set; } = [];
    }

    private sealed class MissingCalcIcon {
        public string IconName { get; set; } = "";
        public HashSet<string> CandidateTargets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> DataFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Examples { get; set; } = [];
    }

    public static void Main(string[] args) {
        bool automationMode = args.Length > 0;
        Console.WriteLine("本项目需要依赖于其他所有项目，且其他项目输出类型需要设定为类库");
        Console.WriteLine(automationMode ? "自动模式：使用命令行参数选择执行模式" : "输入要执行的命令（直接回车表示1）：");
        Console.WriteLine("1表示更新所有mod到R2，打包mod，通知qqbot并上传配置的压缩包；交互模式可继续选择是否启动游戏");
        Console.WriteLine("2表示更新部分需要的dll类库");
        Console.WriteLine("3表示生成计算器 JSON + 图标 + 同步所需图标");
        Console.WriteLine("4表示仅重建计算器所需图标资源（排障用，游戏内提取）");
        Console.WriteLine("5表示快速更新计算器模组版本和 raw JSON 文件名");
        string str = automationMode ? args[0].Trim() : Console.ReadLine();
        if (str == "1" || str == "") {
            UpdateModsThenStart(automationMode, args);
        } else if (str == "2") {
            UpdateLibDll();
        } else if (str == "3") {
            GetAllCalcJson();
        } else if (str == "4") {
            ExportCalcIcons();
        } else if (str == "5") {
            CalcQuickUpdateRunner.Run(args);
        } else {
            Console.WriteLine("输入有误！");
        }
    }
}
