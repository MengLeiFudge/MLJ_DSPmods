using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CommonAPI.Systems.ModLocalization;

namespace FE.Utils;

/// <summary>
/// 从嵌入资源统一注册万物分馏的五语翻译。
/// </summary>
public static partial class Utils {
    /// <summary>CommonAPI 接受的游戏语言代码；德语和日语使用数字 LCID。</summary>
    private static readonly string[] TranslationLanguages = ["enUS", "zhCN", "frFR", "1031", "1041"];
    /// <summary>依赖检查插件和主插件共用资源，只向 CommonAPI 注册一次。</summary>
    private static bool translationsLoaded;

    /// <summary>
    /// 在 Awake 阶段读取 Locale/translations.json，逐条注册；缺失译文回退英文并记录错误。
    /// 没有英文时保留其他有效译文，没有可用译文的词条单独跳过。
    /// </summary>
    public static void LoadTranslationsFromJson() {
        if (translationsLoaded) { return; }
        const string resourceName = "FE.Locale.translations.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null) {
            LogError($"Embedded resource '{resourceName}' not found.");
            return;
        }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Dictionary<string, Dictionary<string, string>> table;
        try {
            table = TranslationJsonParser.Parse(reader.ReadToEnd());
        }
        catch (FormatException ex) {
            LogError($"Failed to load {resourceName}: {ex.Message}");
            return;
        }
        int registeredCount = 0;
        foreach (var entry in table) {
            if (string.IsNullOrWhiteSpace(entry.Key)) {
                LogError("Skipping translation with an empty key.");
                continue;
            }
            bool hasEnglish = entry.Value.TryGetValue("enUS", out string english) && !string.IsNullOrWhiteSpace(english);
            var translations = new Dictionary<string, string>();
            foreach (string language in TranslationLanguages) {
                if (!entry.Value.TryGetValue(language, out string text) || string.IsNullOrWhiteSpace(text)) {
                    LogError($"Translation '{entry.Key}' has no {language} text; "
                             + (hasEnglish ? "using enUS fallback." : "no enUS fallback is available; skipping this language."));
                    if (!hasEnglish) { continue; }
                    text = english;
                }
                translations.Add(language, text);
            }
            foreach (string language in entry.Value.Keys) {
                if (Array.IndexOf(TranslationLanguages, language) < 0) {
                    LogError($"Translation '{entry.Key}' contains unsupported language '{language}'; ignoring this language.");
                }
            }
            if (translations.Count == 0) { continue; }
            LocalizationModule.RegisterTranslation(entry.Key, translations);
            registeredCount++;
        }
        translationsLoaded = true;
        LogInfo($"Loaded {registeredCount} of {table.Count} translations from {resourceName}");
    }
}
