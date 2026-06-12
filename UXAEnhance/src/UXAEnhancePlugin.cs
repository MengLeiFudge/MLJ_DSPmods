using BepInEx;
using HarmonyLib;

namespace UXAEnhance;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(UxAssistGuid, BepInDependency.DependencyFlags.HardDependency)]
public class UXAEnhancePlugin : BaseUnityPlugin {
    public const string UxAssistGuid = "org.soardev.uxassist";

    private Harmony harmony;

    private void Awake() {
        SliderLimitConfig.Bind(Config);
        harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        harmony.PatchAll(typeof(UXAEnhancePlugin).Assembly);
    }

    private void OnDestroy() {
        harmony?.UnpatchSelf();
    }
}
