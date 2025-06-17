using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems.ModLocalization;
using static FE.Utils.LogUtils;

namespace FE.Utils;

public static class I18NUtils {
    /// <summary>
    /// 为某个字符串添加橙色标签。
    /// </summary>
    public static string AddOrangeLabel(this string s) {
        return $"<color=\"#FD965ECC\">{s}</color>";
    }

    // /// <summary>
    // /// 启用此patch后，可以得知红色是FF5D4Cb7，蓝色是61D8FFB8
    // /// </summary>
    // [HarmonyPatch(typeof(UIItemTip), nameof(UIItemTip.SetTip))]
    // [HarmonyPostfix]
    // public static void LogColorGameUsed(ref UIItemTip __instance) {
    //     var text = __instance.valuesText.text;
    //     var color = __instance.valuesText.color;
    //     LogError($"text={text} argb={Math.Round(color.a * 255.0)},{Math.Round(color.r * 255.0)},{Math.Round(color.b * 255.0)},{Math.Round(color.g * 255.0)}");
    //     //text=<color=#FF5D4Cb7>不能手动制造</color> argb=255,150,150,150
    //     //text=310<color=#61D8FFB8> + 62</color> hp argb=255,150,150,150
    //     //这个argb是浅灰色，显然不是需要的，颜色是通过text里面的标签改的
    // }

    /// <summary>
    /// 为某个字符串添加红色标签。
    /// </summary>
    public static string AddRedLabel(this string s) {
        return $"<color=\"#FF5D4CB7\">{s}</color>";
    }

    /// <summary>
    /// 为某个字符串添加蓝色标签。
    /// </summary>
    public static string AddBlueLabel(this string s) {
        return $"<color=\"#61D8FFB8\">{s}</color>";
    }

    public static string AddLabelWithQualityColor(this string s, int quality) {
        switch (quality) {
            case 0:// 褐色
                return $"<color=\"#A0522DAA\">{s}</color>";
            case 1:// 白色
                return $"<color=\"#FFFFFFAA\">{s}</color>";
            case 2:// 绿色
                return $"<color=\"#7CFC00AA\">{s}</color>";
            case 3:// 蓝色
                return $"<color=\"#61D8FFB8\">{s}</color>";
            case 4:// 紫色
                return $"<color=\"#DA70D6AA\">{s}</color>";
            case 5:// 红色
                return $"<color=\"#FF5D4CB7\">{s}</color>";
            case 7:// 金色
                return $"<color=\"#FFD700AA\">{s}</color>";
            default:
                return s;
        }
    }

    /// <summary>
    /// 添加翻译，仅在Awake结束前可用。
    /// </summary>
    public static void Register(string key, string enTrans, string cnTrans = null) {
        LocalizationModule.RegisterTranslation(key, enTrans, cnTrans ?? key, enTrans);
    }

    /// <summary>
    /// 修改翻译，仅在Awake结束前可用。
    /// </summary>
    public static void Edit(string key, string enTrans, string cnTrans = null) {
        LocalizationModule.EditTranslation(key, enTrans, cnTrans ?? key, enTrans);
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
