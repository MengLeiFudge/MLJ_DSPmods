using System;
using System.Text;

namespace FractionateEverything.Utils;

public static class FormatUtils {
    public static string FormatP(this float value) {
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
