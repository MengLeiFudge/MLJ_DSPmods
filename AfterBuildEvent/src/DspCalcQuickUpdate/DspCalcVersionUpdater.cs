using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static AfterBuildEvent.PathConfig;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal sealed class DspCalcVersionUpdater {
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public string ReadCurrentVersion(string calcName) {
        if (!File.Exists(DspCalcGameDataPath)) {
            throw new FileNotFoundException($"未找到计算器 gameData.ts：{DspCalcGameDataPath}", DspCalcGameDataPath);
        }
        string content = File.ReadAllText(DspCalcGameDataPath, Encoding.UTF8);
        Match match = BuildVersionRegex(calcName).Match(content);
        if (!match.Success) {
            throw new InvalidOperationException($"计算器 gameData.ts 中未找到 {calcName} 的版本号");
        }
        return match.Groups[2].Value;
    }

    public bool UpdateVersion(string calcName, string version) {
        string content = File.ReadAllText(DspCalcGameDataPath, Encoding.UTF8);
        Regex regex = BuildVersionRegex(calcName);
        bool changed = false;
        string updated = regex.Replace(
            content,
            match => {
                if (match.Groups[2].Value == version) {
                    return match.Value;
                }
                changed = true;
                return match.Groups[1].Value + version + match.Groups[3].Value;
            },
            1);

        if (!changed) {
            return false;
        }
        File.WriteAllText(DspCalcGameDataPath, updated, Utf8NoBom);
        return true;
    }

    private static Regex BuildVersionRegex(string calcName) {
        string pattern =
            $@"(\{{(?:(?!\}}\s*,?\s*\{{).)*?""name_en""\s*:\s*""{Regex.Escape(calcName)}""(?:(?!\}}\s*,?\s*\{{).)*?""version""\s*:\s*"")([^""]*)("")";
        return new(pattern, RegexOptions.Singleline);
    }
}
