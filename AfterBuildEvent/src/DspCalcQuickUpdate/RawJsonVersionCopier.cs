using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal sealed class RawJsonCopyResult {
    public List<string> CreatedFiles { get; } = [];
    public List<string> SkippedSameFiles { get; } = [];
    public List<string> KeptOldFiles { get; } = [];
}

internal sealed class RawJsonVersionCopier {
    public RawJsonCopyResult CopyVersionFiles(string calcName, string oldVersion, string newVersion) {
        if (!Directory.Exists(DspCalcRawDataDir)) {
            throw new DirectoryNotFoundException($"未找到计算器 raw 目录：{DspCalcRawDataDir}");
        }

        string oldToken = calcName + oldVersion;
        string newToken = calcName + newVersion;
        RawJsonCopyResult result = new();
        foreach (string sourceFile in Directory.GetFiles(DspCalcRawDataDir, "*.json")
                     .Where(file => Path.GetFileNameWithoutExtension(file).Contains(oldToken))) {
            string sourceName = Path.GetFileName(sourceFile);
            string targetName = sourceName.Replace(oldToken, newToken);
            string targetFile = Path.Combine(DspCalcRawDataDir, targetName);
            result.KeptOldFiles.Add(sourceName);

            if (File.Exists(targetFile)) {
                if (!FileEquals(sourceFile, targetFile)) {
                    throw new IOException($"目标 raw JSON 已存在且内容不同：{targetFile}");
                }
                result.SkippedSameFiles.Add(targetName);
                continue;
            }

            File.Copy(sourceFile, targetFile);
            result.CreatedFiles.Add(targetName);
        }

        if (result.CreatedFiles.Count == 0 && result.SkippedSameFiles.Count == 0) {
            throw new InvalidOperationException($"未找到包含 {oldToken} 的 raw JSON 文件");
        }
        return result;
    }

    private static bool FileEquals(string left, string right) {
        FileInfo leftInfo = new(left);
        FileInfo rightInfo = new(right);
        if (leftInfo.Length != rightInfo.Length) {
            return false;
        }
        byte[] leftBytes = File.ReadAllBytes(left);
        byte[] rightBytes = File.ReadAllBytes(right);
        return leftBytes.SequenceEqual(rightBytes);
    }
}
