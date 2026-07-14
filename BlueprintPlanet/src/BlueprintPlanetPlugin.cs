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
    public const string PluginGuid = "com.menglei.dsp.blueprintplanet";
    public const string PluginName = "Blueprint Planet";
    public const string PluginVersion = "1.0.0";

    private const string BlueprintPlanetFieldName = "蓝图星球";
    private static ManualLogSource log;

    private void Awake() {
        log = Logger;
        var harmony = new Harmony(PluginGuid);
        InstallPostfix(harmony,
            AccessTools.Method(typeof(BlueprintData), nameof(BlueprintData.CreateNew), Type.EmptyTypes),
            nameof(BlueprintData_CreateNew_Postfix));
    }

    /// <summary>
    /// 原版浏览器的新建空白蓝图直接由这个无参工厂创建。
    /// </summary>
    public static void BlueprintData_CreateNew_Postfix(ref BlueprintData __result) {
        ApplyDefaultValuesIfNew(__result, GameMain.localPlanet, "BlueprintData.CreateNew");
    }

    private static void InstallPostfix(Harmony harmony, MethodInfo target, string patchMethodName) {
        MethodInfo patchMethod = AccessTools.Method(typeof(BlueprintPlanetPlugin), patchMethodName);
        if (target == null || patchMethod == null) {
            log.LogError($"未找到补丁方法：target={target?.DeclaringType?.FullName}.{target?.Name}, patch={patchMethodName}");
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

            log.LogInfo($"已安装 Postfix：{target.DeclaringType.Name}.{target.Name} -> {patchMethodName}，owned={owned}");
        } catch (Exception exception) {
            log.LogError($"安装补丁失败：{target.DeclaringType.Name}.{target.Name} -> {patchMethodName}\n{exception}");
        }
    }

    private static void ApplyDefaultValuesIfNew(BlueprintData blueprint, PlanetData planet, string source) {
        if (!IsOriginalNewBlueprint(blueprint)) {
            return;
        }

        if (blueprint == null || planet == null) {
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

    private static bool IsOriginalNewBlueprint(BlueprintData blueprint) {
        return blueprint != null && string.IsNullOrEmpty(blueprint.externalFields) &&
               string.Equals(blueprint.shortDesc, "新的蓝图".Translate(), StringComparison.Ordinal);
    }

}
