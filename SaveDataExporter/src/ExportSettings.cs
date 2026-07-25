using BepInEx.Configuration;

namespace SaveDataExporter;

/// <summary>
/// 导出相关配置项的绑定与语义化访问入口，不向外暴露 ConfigEntry 实现。
/// </summary>
internal static class ExportSettings {
    /// <summary>
    /// 默认统计周期档位（10 分钟）。
    /// </summary>
    private const int DefaultTimeLevel = 1;

    /// <summary>
    /// 默认目标物品：增产剂 Mk.III（1143）和宇宙矩阵（6006）。
    /// </summary>
    private const string DefaultTargetItems = "1143,6006";

    /// <summary>
    /// 目标物品配置绑定实例，由 Bind 初始化。
    /// </summary>
    private static ConfigEntry<string> targetItemsEntry;

    /// <summary>
    /// 统计周期配置绑定实例，由 Bind 初始化。
    /// </summary>
    private static ConfigEntry<int> timeLevelEntry;

    /// <summary>
    /// 输出目录配置绑定实例，由 Bind 初始化。
    /// </summary>
    private static ConfigEntry<string> outputDirectoryEntry;

    /// <summary>
    /// 输出文件命名模式配置绑定实例，由 Bind 初始化。
    /// </summary>
    private static ConfigEntry<OutputFileNameMode> outputFileNameModeEntry;

    /// <summary>
    /// 导出目标物品原始配置字符串。
    /// </summary>
    public static string TargetItems => targetItemsEntry.Value;

    /// <summary>
    /// 统计周期配置值（0-4）。
    /// </summary>
    public static int TimeLevel => timeLevelEntry.Value;

    /// <summary>
    /// 输出目录配置；空字符串表示使用默认目录。
    /// </summary>
    public static string OutputDirectory => outputDirectoryEntry.Value;

    /// <summary>
    /// 将配置项绑定到插件 ConfigFile。
    /// </summary>
    /// <param name="config">BepInEx 插件配置文件。</param>
    public static void Bind(ConfigFile config) {
        targetItemsEntry = config.Bind(
            "Export",
            "TargetItems",
            DefaultTargetItems,
            "导出的目标物品，支持物品 ID 或物品名，用逗号分隔。默认 1143,6006 表示增产剂 Mk.III 和宇宙矩阵。");
        timeLevelEntry = config.Bind(
            "Export",
            "TimeLevel",
            DefaultTimeLevel,
            "统计周期：0=1分钟，1=10分钟，2=1小时，3=10小时，4=100小时。");
        outputDirectoryEntry = config.Bind(
            "Export",
            "OutputDirectory",
            "",
            "导出目录。留空时使用 BepInEx/config/SaveDataExporter。");
        outputFileNameModeEntry = config.Bind(
            "Miscellaneous",
            "OutputFileNameMode",
            OutputFileNameMode.TimestampedNewFile,
            "输出文件命名模式。TimestampedNewFile=文件名包含导出时间，每次生成新文件；SaveNameOverwrite=固定为 SaveDataExporter_<存档名>.xlsx，已有同名文件时覆盖。");
    }

    /// <summary>
    /// 读取有效的输出文件命名模式；非法配置值会写回并回退为时间戳新文件。
    /// </summary>
    /// <returns>可用于路径生成与设置页同步的命名模式。</returns>
    public static OutputFileNameMode GetOutputFileNameMode() {
        OutputFileNameMode mode = outputFileNameModeEntry.Value;
        if (System.Enum.IsDefined(typeof(OutputFileNameMode), mode)) {
            return mode;
        }

        outputFileNameModeEntry.Value = OutputFileNameMode.TimestampedNewFile;
        return OutputFileNameMode.TimestampedNewFile;
    }

    /// <summary>
    /// 将输出文件命名模式写入原 Miscellaneous.OutputFileNameMode 配置项。
    /// </summary>
    /// <param name="mode">要持久化的命名模式。</param>
    public static void SetOutputFileNameMode(OutputFileNameMode mode) {
        outputFileNameModeEntry.Value = mode;
    }
}
