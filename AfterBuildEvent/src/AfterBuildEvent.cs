using System;
using System.Text;
using AfterBuildEvent.DspCalcQuickUpdate;
using AfterBuildEvent.Thunderstore;

namespace AfterBuildEvent;

/// <summary>
/// 构建自动化入口和模式路由。特定模式的 DTO 与工作流位于各自所属目录中。
/// </summary>
static partial class AfterBuildEvent {
    /// <summary>
    /// 使用无 BOM 的 UTF-8，以便各工作流生成确定性的文本/哈希载荷。
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    /// <summary>
    /// R2 配置文件被其他进程锁定时的最大尝试次数。
    /// </summary>
    private const int R2CopyRetryCount = 60;

    /// <summary>
    /// R2 复制重试的间隔时间，单位为毫秒。
    /// </summary>
    private const int R2CopyRetryDelayMs = 500;

#if DEBUG
    /// <summary>
    /// 当前 AfterBuildEvent 构建所选择的本地项目输出配置。
    /// </summary>
    private const string BuildConfiguration = "Debug";
#else
    /// <summary>
    /// 当前 AfterBuildEvent 构建所选择的本地项目输出配置。
    /// </summary>
    private const string BuildConfiguration = "Release";
#endif

    /// <summary>
    /// 输出模式帮助，并将 CLI/交互式选择分派给所属工作流。
    /// </summary>
    /// <param name="args">
    /// 可选 argv。第一个值为模式，其余值为特定模式的筛选条件。
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
        Console.WriteLine("7表示独立准备或发布到雷霆商店；上传必须先获得明确批准并指定包、版本和 SHA256");
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
        } else if (str == "7") {
            ThunderstorePublisher.Run(args, BuildConfiguration);
        } else {
            Console.WriteLine("输入有误！");
        }
    }
}
