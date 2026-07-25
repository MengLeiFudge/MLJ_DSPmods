using System;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;

namespace SaveDataExporter;

/// <summary>
/// 根据配置与存档名解析导出 xlsx 的完整输出路径。
/// </summary>
internal static class ExportPathResolver {
    /// <summary>
    /// 创建输出目录（必要时）并返回本次导出文件的完整路径。
    /// </summary>
    /// <param name="gameData">当前游戏数据，用于读取存档名。</param>
    /// <returns>目标 xlsx 绝对或相对完整路径。</returns>
    public static string BuildOutputPath(GameData gameData) {
        string outputDirectory = ExportSettings.OutputDirectory;
        if (string.IsNullOrWhiteSpace(outputDirectory)) {
            string modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Paths.PluginPath;
            outputDirectory = Path.Combine(
                Directory.GetParent(modDirectory)?.Parent?.FullName ?? Paths.ConfigPath,
                "config",
                "SaveDataExporter");
        }

        Directory.CreateDirectory(outputDirectory);
        string saveName = SanitizeFileName(string.IsNullOrWhiteSpace(gameData.gameName) ? "save" : gameData.gameName);
        string fileName = ExportSettings.GetOutputFileNameMode() == OutputFileNameMode.SaveNameOverwrite
            ? $"SaveDataExporter_{saveName}.xlsx"
            : $"SaveDataExporter_{saveName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return Path.Combine(outputDirectory, fileName);
    }

    /// <summary>
    /// 将文件名中非法字符替换为下划线，确保结果可在 Windows 文件系统上安全使用。
    /// </summary>
    /// <param name="value">原始文件名（可能含 \ / : * ? " < > | 等字符）。</param>
    /// <returns>清理后不含非法字符的文件名。</returns>
    private static string SanitizeFileName(string value) {
        string invalidChars = new(Path.GetInvalidFileNameChars());
        StringBuilder builder = new(value.Length);
        foreach (char c in value) {
            builder.Append(invalidChars.IndexOf(c) >= 0 ? '_' : c);
        }
        return builder.ToString();
    }
}
