using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FE.Utils;

/// <summary>
/// 翻译 JSON 的最小解析器，只接受两层对象：{ "键": { "语言": "译文" } }。
/// 游戏运行时不附带 Newtonsoft.Json，因此自行实现以避免额外依赖。
/// </summary>
public static class TranslationJsonParser {
    /// <summary>解析翻译表。</summary>
    /// <param name="json">JSON 文本。</param>
    /// <returns>键到（语言缩写到译文）的映射，保持文件顺序无关。</returns>
    /// <exception cref="FormatException">格式不符合预期时抛出，消息包含出错位置。</exception>
    public static Dictionary<string, Dictionary<string, string>> Parse(string json) {
        int i = 0;
        var result = new Dictionary<string, Dictionary<string, string>>();
        SkipWs(json, ref i);
        Expect(json, ref i, '{');
        SkipWs(json, ref i);
        if (Peek(json, i) == '}') { i++; return result; }
        while (true) {
            SkipWs(json, ref i);
            string key = ReadString(json, ref i);
            SkipWs(json, ref i);
            Expect(json, ref i, ':');
            SkipWs(json, ref i);
            var langs = new Dictionary<string, string>();
            Expect(json, ref i, '{');
            SkipWs(json, ref i);
            if (Peek(json, i) == '}') {
                i++;
            } else {
                while (true) {
                    SkipWs(json, ref i);
                    string lang = ReadString(json, ref i);
                    SkipWs(json, ref i);
                    Expect(json, ref i, ':');
                    SkipWs(json, ref i);
                    langs[lang] = ReadString(json, ref i);
                    SkipWs(json, ref i);
                    char c = Next(json, ref i);
                    if (c == '}') { break; }
                    if (c != ',') { throw Error(i - 1, "expected ',' or '}'"); }
                }
            }
            if (result.ContainsKey(key)) { throw Error(i, $"duplicate key '{key}'"); }
            result[key] = langs;
            SkipWs(json, ref i);
            char end = Next(json, ref i);
            if (end == '}') { break; }
            if (end != ',') { throw Error(i - 1, "expected ',' or '}'"); }
        }
        SkipWs(json, ref i);
        if (i != json.Length) { throw Error(i, "trailing content"); }
        return result;
    }

    private static string ReadString(string s, ref int i) {
        Expect(s, ref i, '"');
        var sb = new StringBuilder();
        while (true) {
            char c = Next(s, ref i);
            if (c == '"') { return sb.ToString(); }
            if (c != '\\') { sb.Append(c); continue; }
            char e = Next(s, ref i);
            switch (e) {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 > s.Length
                        || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                            out int code)) {
                        throw Error(i, "invalid \\u escape");
                    }
                    sb.Append((char)code);
                    i += 4;
                    break;
                default: throw Error(i - 1, $"invalid escape '\\{e}'");
            }
        }
    }

    private static void SkipWs(string s, ref int i) {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n' || s[i] == '\uFEFF')) {
            i++;
        }
    }

    private static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

    private static char Next(string s, ref int i) {
        if (i >= s.Length) { throw Error(i, "unexpected end of input"); }
        return s[i++];
    }

    private static void Expect(string s, ref int i, char c) {
        if (Next(s, ref i) != c) { throw Error(i - 1, $"expected '{c}'"); }
    }

    private static FormatException Error(int pos, string message) => new($"{message} at offset {pos}");
}
