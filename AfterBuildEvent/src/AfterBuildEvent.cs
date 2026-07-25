using System;
using System.Text;
using AfterBuildEvent.DspCalcQuickUpdate;

namespace AfterBuildEvent;

/// <summary>
/// Build automation entry and mode router. Mode-specific DTOs and workflows live in owning folders.
/// </summary>
static partial class AfterBuildEvent {
    /// <summary>
    /// UTF-8 without BOM for deterministic text/hash payloads shared across workflows.
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    /// <summary>
    /// Maximum attempts when R2 profile files are locked by another process.
    /// </summary>
    private const int R2CopyRetryCount = 60;

    /// <summary>
    /// Delay between R2 copy retries in milliseconds.
    /// </summary>
    private const int R2CopyRetryDelayMs = 500;

#if DEBUG
    /// <summary>
    /// Local project output configuration selected by the current AfterBuildEvent build.
    /// </summary>
    private const string BuildConfiguration = "Debug";
#else
    /// <summary>
    /// Local project output configuration selected by the current AfterBuildEvent build.
    /// </summary>
    private const string BuildConfiguration = "Release";
#endif

    /// <summary>
    /// Prints mode help and dispatches CLI/interactive selection to the owning workflow.
    /// </summary>
    /// <param name="args">
    /// Optional argv. First value is the mode; remaining values are mode-specific filters.
    /// </param>
    public static void Main(string[] args) {
        bool automationMode = args.Length > 0;
        Console.WriteLine("本项目需要依赖于其他所有项目，且其他项目输出类型需要设定为类库");
        Console.WriteLine(automationMode ? "自动模式：使用命令行参数选择执行模式" : "输入要执行的命令（直接回车表示1）：");
        Console.WriteLine("1表示更新所有mod到R2，打包mod，通知qqbot并上传配置的压缩包；交互模式可继续选择是否启动游戏");
        Console.WriteLine("2表示更新部分需要的dll类库");
        Console.WriteLine("3表示生成计算器 JSON + 图标 + 同步所需图标");
        Console.WriteLine("4表示仅重建计算器所需图标资源（排障用，游戏内提取）");
        Console.WriteLine("5表示快速更新计算器模组版本和 raw JSON 文件名");
        Console.WriteLine("6表示根据 mods.yml 与 R2 cache 生成外部模组编译引用配置");
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
        } else if (str == "6") {
            GenerateExternalModReferencesProps();
        } else {
            Console.WriteLine("输入有误！");
        }
    }
}
