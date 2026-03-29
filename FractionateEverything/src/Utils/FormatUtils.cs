using System;
using System.Text;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 将浮点数转化为字符串，至少保留2位有效数字。
    /// </summary>
    public static string FormatF(this float value) {
        if (value < 0) {
            return "-" + (-value).FormatF();
        }
        int i = 2;
        StringBuilder sb = new StringBuilder("0.##");
        while (i < 10) {
            if (value * Math.Pow(10, i) >= 1.0) {
                return value.ToString(sb.ToString());
            }
            sb.Append("#");
            i++;
        }
        return "0";
    }

    /// <summary>
    /// 将小数转化为字符串，至少保留2位有效数字。
    /// </summary>
    public static string FormatD(this double value) {
        if (value < 0) {
            return "-" + (-value).FormatD();
        }
        int i = 2;
        StringBuilder sb = new StringBuilder("0.##");
        while (i < 10) {
            if (value * Math.Pow(10, i) >= 1.0) {
                return value.ToString(sb.ToString());
            }
            sb.Append("#");
            i++;
        }
        return "0";
    }

    /// <summary>
    /// 将浮点数转化为百分比字符串，至少保留3位有效数字。
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

    private static readonly StringBuilder countSb = new("                ", 16);

    /// <summary>
    /// 将给定的数值按照游戏的WriteKMG格式化。
    /// </summary>
    public static string FormatKMG(this long count) {
        if (count >= -10000 && count <= 10000) {
            return count.ToString();
        }
        countSb.Length = 16;
        for (int i = 0; i < 16; i++) {
            countSb[i] = ' ';
        }
        StringBuilderUtility.WriteKMG(countSb, 8, count, blank: true);
        return countSb.ToString().Trim();
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
