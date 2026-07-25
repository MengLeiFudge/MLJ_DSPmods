using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace BlueprintPlanet;

/// <summary>
/// 为原版新建蓝图填入当前星球名称，使用原版扩展属性格式保存，避免引入自定义蓝图编码。
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class BlueprintPlanetPlugin : BaseUnityPlugin {
    /// <summary>
    /// BepInEx 插件唯一标识，同时作为 Harmony 实例 id。
    /// </summary>
    public const string PluginGuid = "com.menglei.dsp.blueprintplanet";

    /// <summary>
    /// 插件显示名称。
    /// </summary>
    public const string PluginName = "Blueprint Planet";

    /// <summary>
    /// 插件版本号，须与 csproj Version 保持一致。
    /// </summary>
    public const string PluginVersion = "1.0.0";

    /// <summary>
    /// 写入 <see cref="BlueprintData.externalFields"/> 的星球名称字段键（未转义原文）。
    /// </summary>
    private const string BlueprintPlanetFieldName = "蓝图星球";

    private static ManualLogSource log;

    /// <summary>
    /// 插件入口：初始化日志、创建 Harmony 实例并安装 CreateNew Postfix。
    /// </summary>
    private void Awake() {
        log = Logger;
        var harmony = new Harmony(PluginGuid);
        InstallCreateNewPostfix(harmony);
    }

    /// <summary>
    /// 解析并安装原版无参 <see cref="BlueprintData.CreateNew"/> 的 Postfix，含目标校验、owned 检查与异常日志。
    /// </summary>
    /// <param name="harmony">本插件专用的 Harmony 实例。</param>
    private static void InstallCreateNewPostfix(Harmony harmony) {
        MethodInfo target = AccessTools.Method(typeof(BlueprintData), nameof(BlueprintData.CreateNew), Type.EmptyTypes);
        MethodInfo patchMethod = AccessTools.Method(typeof(BlueprintPlanetPlugin), nameof(BlueprintData_CreateNew_Postfix));
        if (target == null || patchMethod == null) {
            log.LogError($"未找到补丁方法：target={target?.DeclaringType?.FullName}.{target?.Name}, patch={nameof(BlueprintData_CreateNew_Postfix)}");
            return;
        }

        try {
            harmony.Patch(target, postfix: new HarmonyMethod(patchMethod));

            bool owned = false;
            var patchInfo = Harmony.GetPatchInfo(target);
            if (patchInfo != null) {
                foreach (string owner in patchInfo.Owners) {
                    if (owner == PluginGuid) {
                        owned = true;
                        break;
                    }
                }
            }

            log.LogInfo($"已安装 Postfix：{target.DeclaringType.Name}.{target.Name} -> {nameof(BlueprintData_CreateNew_Postfix)}，owned={owned}");
        } catch (Exception exception) {
            log.LogError($"安装补丁失败：{target.DeclaringType.Name}.{target.Name} -> {nameof(BlueprintData_CreateNew_Postfix)}\n{exception}");
        }
    }

    /// <summary>
    /// 原版浏览器的新建空白蓝图直接由这个无参工厂创建。
    /// </summary>
    /// <param name="__result">工厂返回的新建蓝图实例。</param>
    public static void BlueprintData_CreateNew_Postfix(ref BlueprintData __result) {
        ApplyDefaultValuesIfNew(__result, GameMain.localPlanet, "BlueprintData.CreateNew");
    }

    /// <summary>
    /// 仅在识别为原版“新的蓝图”时写入星球扩展字段与缩略描述。
    /// </summary>
    /// <param name="blueprint">待填充的蓝图数据。</param>
    /// <param name="planet">当前本地星球；为空则跳过写入。</param>
    /// <param name="source">日志来源标签，便于区分调用入口。</param>
    private static void ApplyDefaultValuesIfNew(BlueprintData blueprint, PlanetData planet, string source) {
        if (!IsOriginalNewBlueprint(blueprint)) {
            return;
        }

        if (planet == null) {
            log?.LogInfo($"{source} 未写入蓝图星球：蓝图或当前星球为空。");
            return;
        }

        string planetName = BlueprintData.Validate(planet.displayName);
        if (string.IsNullOrEmpty(planetName)) {
            return;
        }

        string fieldName = blueprint.ExternalFieldEscape(BlueprintPlanetFieldName);
        string fieldValue = blueprint.ExternalFieldEscape(planetName);
        blueprint.externalFields = $"{fieldName}:{fieldValue};";
        blueprint.shortDesc = planetName;
        log?.LogInfo($"{source} 已写入蓝图星球：{planetName}");
    }

    /// <summary>
    /// 判断是否为尚未写入扩展字段、缩略描述仍为原版“新的蓝图”的空白蓝图。
    /// </summary>
    /// <param name="blueprint">待识别的蓝图数据。</param>
    /// <returns>符合原版新建空白蓝图特征时为 true。</returns>
    private static bool IsOriginalNewBlueprint(BlueprintData blueprint) {
        return blueprint != null && string.IsNullOrEmpty(blueprint.externalFields) &&
               string.Equals(blueprint.shortDesc, "新的蓝图".Translate(), StringComparison.Ordinal);
    }
}
