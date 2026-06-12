using BepInEx;
using UXAssist.UI;

namespace UXAEnhance;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(UxAssistGuid, BepInDependency.DependencyFlags.HardDependency)]
public class UXAEnhancePlugin : BaseUnityPlugin {
    public const string UxAssistGuid = "org.soardev.uxassist";

    private void Awake() {
        SliderLimitConfig.Bind(Config);
        MyConfigWindow.OnUICreated += UXAssistConfigWindowPatch.OnUxAssistConfigWindowCreated;
    }

    private void OnDestroy() {
        MyConfigWindow.OnUICreated -= UXAssistConfigWindowPatch.OnUxAssistConfigWindowCreated;
    }
}
