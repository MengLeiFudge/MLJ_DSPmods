using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace FE.Compatibility.Mods;

/// <summary>
/// 星环模组检测、指引布局与兼容状态入口。
/// </summary>
public static class OrbitalRing {
    public const string GUID = "org.ProfessorCat305.OrbitalRing";
    public static bool Enable;
    public static Assembly assembly;

    /// <summary>兼容初始化时解析的星环布局识别方法，随进程缓存。</summary>
    private static MethodInfo isTutorialLayoutMethod;
    /// <summary>兼容初始化时解析的星环正文生成方法，保留星环自身的语言选择。</summary>
    private static MethodInfo getTutorialLayoutMethod;

    /// <summary>
    /// 检测星环并缓存指引布局接口，再注册兼容补丁。
    /// </summary>
    public static void Compatible() {
        isTutorialLayoutMethod = null;
        getTutorialLayoutMethod = null;
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out BepInEx.PluginInfo pluginInfo);
        if (!Enable || pluginInfo == null) {
            return;
        }
        assembly = pluginInfo.Instance.GetType().Assembly;
        var tutorialPatchType = assembly.GetType("ProjectOrbitalRing.Patches.UITutorialWindowPatches");
        if (tutorialPatchType != null) {
            isTutorialLayoutMethod = AccessTools.Method(tutorialPatchType, "IsOrbitalRingLayout", [typeof(TutorialProto)]);
            getTutorialLayoutMethod = AccessTools.Method(tutorialPatchType, "GetOrbitalRingLayoutStr", [typeof(TutorialProto)]);
        }
        if (isTutorialLayoutMethod == null || !isTutorialLayoutMethod.IsStatic
            || isTutorialLayoutMethod.ReturnType != typeof(bool)
            || getTutorialLayoutMethod == null || !getTutorialLayoutMethod.IsStatic
            || getTutorialLayoutMethod.ReturnType != typeof(string)) {
            isTutorialLayoutMethod = null;
            getTutorialLayoutMethod = null;
            CheckPlugins.LogWarning("星环指引布局接口缺失或签名不兼容，已跳过指引布局适配。");
        }
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.OrbitalRing");
        harmony.PatchAll(typeof(OrbitalRing));
        CheckPlugins.LogInfo("OrbitalRing Compat finish.");
    }

    /// <summary>
    /// 使用星环自身的识别规则判断教程归属；接口不完整时不接管布局。
    /// </summary>
    /// <param name="proto">当前选中的教程原型。</param>
    /// <returns>星环已启用、接口完整且教程属于星环时返回 true。</returns>
    internal static bool IsTutorialLayout(TutorialProto proto) {
        if (!Enable || proto == null || isTutorialLayoutMethod == null || getTutorialLayoutMethod == null) {
            return false;
        }
        return isTutorialLayoutMethod.Invoke(null, [proto]) is bool isLayout && isLayout;
    }

    /// <summary>
    /// 委派星环生成教程正文，保留其 JSON 翻译与布局标记。
    /// </summary>
    /// <param name="proto">已识别为星环布局的教程原型。</param>
    /// <returns>星环生成的布局文本；未启用、参数或接口不可用时返回空字符串。</returns>
    internal static string GetTutorialLayoutStr(TutorialProto proto) {
        if (!Enable || proto == null || getTutorialLayoutMethod == null) {
            return string.Empty;
        }
        return getTutorialLayoutMethod.Invoke(null, [proto]) as string ?? string.Empty;
    }
}
