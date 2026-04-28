using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems.ModLocalization;

namespace FE.Utils;

public static partial class Utils {
    private static readonly Dictionary<string, (string enTrans, string cnTrans)> registeredTranslations = [];

    /// <summary>
    /// 添加翻译，仅在Awake结束前可用。
    /// </summary>
    /// <param name="key">翻译键</param>
    /// <param name="enTrans">英文翻译</param>
    /// <param name="cnTrans">中文翻译，留空时回退到 key</param>
    public static void Register(string key, string enTrans, string cnTrans = null) {
        string resolvedCn = cnTrans ?? key;
        if (registeredTranslations.TryGetValue(key, out var existing)) {
            if (existing.enTrans != enTrans || existing.cnTrans != resolvedCn) {
                LogError(
                    $"Duplicate translation key '{key}' ignored. Existing=({existing.enTrans}, {existing.cnTrans}), New=({enTrans}, {resolvedCn})");
            }
            return;
        }

        registeredTranslations[key] = (enTrans, resolvedCn);
        // LocalizationModule.RegisterTranslation(key, enTrans, cnTrans ?? key, enTrans);
        // 对于当前CommonAPI版本（1.6.7），RegisterTranslation(key, dic)不会检测trans为null或空字符串的情况
        Dictionary<string, string> dic = [];
        dic["enUS"] = enTrans;
        dic["zhCN"] = resolvedCn;
        dic["frFR"] = enTrans;
        LocalizationModule.RegisterTranslation(key, dic);
    }

    /// <summary>
    /// 修改翻译，仅在Awake结束前可用。
    /// </summary>
    /// <param name="key">翻译键</param>
    /// <param name="enTrans">英文翻译</param>
    /// <param name="cnTrans">中文翻译，留空时回退到 key</param>
    public static void Edit(string key, string enTrans, string cnTrans = null) {
        // LocalizationModule.EditTranslation(key, enTrans, cnTrans ?? key, enTrans);
        // 对于当前CommonAPI版本（1.6.7），EditTranslation(key, dic)不会检测trans为null或空字符串的情况
        Dictionary<string, string> dic = [];
        dic["enUS"] = enTrans;
        dic["zhCN"] = cnTrans ?? key;
        dic["frFR"] = enTrans;
        LocalizationModule.EditTranslation(key, dic);
    }

    private record struct ModStr {
        public string key;
        public string enTrans;
        public string cnTrans;
    }

    private static readonly List<ModStr> modStringList = [];

    /// <summary>
    /// 添加或修改翻译，仅在Awake结束后可用。
    /// 翻译将会暂存到modStringList中，只有在调用LoadLanguagePostfixAfterCommonApi后才生效。
    /// </summary>
    /// <param name="key">翻译键</param>
    /// <param name="enTrans">英文翻译</param>
    /// <param name="cnTrans">中文翻译，留空时回退到 key</param>
    public static void RegisterOrEditAsync(string key, string enTrans, string cnTrans = null) {
        ModStr modStr = new() { key = key, enTrans = enTrans, cnTrans = cnTrans ?? key };
        foreach (ModStr m in modStringList) {
            if (m.key == key) {
                modStringList.Remove(m);
                break;
            }
        }
        modStringList.Add(modStr);
    }

    /// <summary>
    /// 立即添加或修改翻译，并立刻刷新本地语言表。
    /// </summary>
    /// <param name="key">翻译键</param>
    /// <param name="enTrans">英文翻译</param>
    /// <param name="cnTrans">中文翻译，留空时回退到 key</param>
    public static void RegisterOrEditImmediately(string key, string enTrans, string cnTrans = null) {
        RegisterOrEditAsync(key, enTrans, cnTrans);
        LoadLanguagePostfixAfterCommonApi();
    }

    /// <summary>
    /// 将modStringList所有内容添加到翻译列表，之后即可使用.Translate()获取翻译。
    /// </summary>
    public static void LoadLanguagePostfixAfterCommonApi() {
        try {
            //执行此方法需要确保数组不为null
            if (modStringList.Count == 0
                || Localization.strings == null
                || Localization.CurrentLanguageIndex >= Localization.strings.Length
                || Localization.strings[Localization.CurrentLanguageIndex] == null
                || Localization.currentStrings == null
                || Localization.floats == null
                || Localization.CurrentLanguageIndex >= Localization.floats.Length
                || Localization.floats[Localization.CurrentLanguageIndex] == null
                || Localization.currentFloats == null) {
                return;
            }
            List<string> stringsList = [..Localization.strings[Localization.CurrentLanguageIndex]];
            List<float> floatsList = [..Localization.floats[Localization.CurrentLanguageIndex]];
            bool isZHCN = Localization.CurrentLanguageLCID == Localization.LCID_ZHCN;
            foreach (var p in modStringList) {
                if (!Localization.namesIndexer.TryGetValue(p.key, out int index)) {
                    index = Localization.namesIndexer.Count;
                    Localization.namesIndexer.Add(p.key, index);
                }
                //可能出现List长度不够的情况，所以先用namesIndexer对应位置的key占位，最后再修改而非Add，就可以避免异常
                while (stringsList.Count <= index) {
                    stringsList.Add(Localization.namesIndexer.ElementAt(stringsList.Count).Key);
                    floatsList.Add(0);
                }
                stringsList[index] = isZHCN ? p.cnTrans : p.enTrans;
            }
            Localization.strings[Localization.CurrentLanguageIndex] = stringsList.ToArray();
            Localization.currentStrings = Localization.strings[Localization.CurrentLanguageIndex];
            Localization.floats[Localization.CurrentLanguageIndex] = floatsList.ToArray();
            Localization.currentFloats = Localization.floats[Localization.CurrentLanguageIndex];
            LogInfo($"Modify translations finish, currentStrings.len={Localization.currentStrings.Length}");
        }
        catch (Exception ex) {
            LogError("LoadLanguagePostfixAfterCommonApi Error: " + ex);
        }
    }
}
