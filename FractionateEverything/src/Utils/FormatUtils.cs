using System;
using System.Text;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 将浮点数转化为百分数，至少保留一位有效数字。
    /// </summary>
    public static string FormatP(this float value) {
        if (value < 0) {
            return "-" + (-value).FormatP();
        }
        int i = 3;
        StringBuilder sb = new StringBuilder("0.###");
        while (i < 10) {
            if (value * Math.Pow(10, i) >= 1.0) {
                return value.ToString(sb.Append("%").ToString());
            }
            sb.Append("#");
            i++;
        }
        return "0%";
    }

    /// <summary>
    ///     将浮点数转化为百分数，至少保留一位有效数字。
    /// </summary>
    public static string FormatPWithSymbol(this float value) {
        if (value < 0) {
            return (-value).FormatPWithSymbol().Replace("+", "-");
        }
        int i = 3;
        var sb = new StringBuilder("+0.###");
        while (i < 10) {
            if (value * Math.Pow(10, i) >= 1.0) {
                return value.ToString(sb.Append("%").ToString());
            }
            sb.Append("#");
            i++;
        }
        return "+0%";
    }

    /// <summary>
    /// 格式化物品/配方名称，去除多余的空格、特殊字符等。
    /// </summary>
    public static string FormatName(string s) {
        if (s == null) {
            return "null";
        }
        return s.Translate()
            .Replace(" ", "")
            .Replace(" ", "")
            .Replace(" ", "")
            .Replace("“", "")
            .Replace("”", "")
            .Replace(":", "")
            .Replace("：", "")
            .Replace("!", "")
            .Replace("-", "")
            .Replace(".", "")
            .Replace("（", "")
            .Replace("）", "");
    }
}
