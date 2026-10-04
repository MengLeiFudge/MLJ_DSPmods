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
    /// 在 Awake 阶段读取 Locale/translations.json，完整校验后交给 CommonAPI 注册。
    /// 译文和格式模板只在资源中维护；不得用英文回退掩盖游戏语言缺失。
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
            foreach (var entry in table) {
                foreach (string language in TranslationLanguages) {
                    if (!entry.Value.TryGetValue(language, out string text) || string.IsNullOrWhiteSpace(text)) {
                        throw new FormatException($"Translation '{entry.Key}' has no {language} text.");
                    }
                }
                if (entry.Value.Count != TranslationLanguages.Length) {
                    throw new FormatException($"Translation '{entry.Key}' contains an unsupported language.");
                }
            }
        }
        catch (FormatException ex) {
            LogError($"Failed to load {resourceName}: {ex.Message}");
            return;
        }
        foreach (var entry in table) {
            LocalizationModule.RegisterTranslation(entry.Key, entry.Value);
        }
        translationsLoaded = true;
        LogInfo($"Loaded {table.Count} translations from {resourceName}");
    }
}
