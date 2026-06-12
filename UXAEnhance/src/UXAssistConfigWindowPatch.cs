using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UXAssist;
using UXAssist.Patches;
using UXAssist.UI;

namespace UXAEnhance;

[HarmonyPatch]
internal static class UXAssistConfigWindowPatch {
    private const float ButtonWidth = 44f;
    private const float ButtonOffsetX = 470f;
    private const int ButtonFontSize = 12;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIConfigWindow), "CreateUI")]
    private static void UIConfigWindow_CreateUI_Postfix(MyConfigWindow wnd, RectTransform trans) {
        RectTransform tab = FindLogisticsTab(trans);
        if (tab == null) {
            return;
        }

        MySideSlider[] sliders = FindAutoConfigSliders(tab);
        if (sliders.Length < 19) {
            return;
        }

        AddApplyButton(wnd, tab, sliders[0], "AutoConfigDispenserChargePower", SliderLimitConfig.DispenserChargePowerMax, AutoConfigApplyTarget.DispenserChargePower, 647f);
        AddApplyButton(wnd, tab, sliders[1], "AutoConfigDispenserCourierCount", SliderLimitConfig.DispenserCourierCountMax, AutoConfigApplyTarget.DispenserCourierCount, 649f);
        AddApplyButton(wnd, tab, sliders[2], "AutoConfigBattleBaseChargePower", SliderLimitConfig.BattleBaseChargePowerMax, AutoConfigApplyTarget.BattleBaseChargePower, 651f);
        AddApplyButton(wnd, tab, sliders[3], "AutoConfigPLSChargePower", SliderLimitConfig.PlsChargePowerMax, AutoConfigApplyTarget.PlsChargePower, 653f);
        AddApplyButton(wnd, tab, sliders[4], "AutoConfigPLSMaxTripDrone", SliderLimitConfig.PlsMaxTripDroneMax, AutoConfigApplyTarget.PlsMaxTripDrone, 655f);
        AddApplyButton(wnd, tab, sliders[5], "AutoConfigPLSDroneMinDeliver", SliderLimitConfig.PlsDroneMinDeliverMax, AutoConfigApplyTarget.PlsDroneMinDeliver, 657f);
        AddApplyButton(wnd, tab, sliders[6], "AutoConfigPLSMinPilerValue", SliderLimitConfig.PlsMinPilerValueMax, AutoConfigApplyTarget.PlsMinPilerValue, 659f);
        AddApplyButton(wnd, tab, sliders[7], "AutoConfigPLSDroneCount", SliderLimitConfig.PlsDroneCountMax, AutoConfigApplyTarget.PlsDroneCount, 661f);
        AddApplyButton(wnd, tab, sliders[8], "AutoConfigILSChargePower", SliderLimitConfig.IlsChargePowerMax, AutoConfigApplyTarget.IlsChargePower, 663f);
        AddApplyButton(wnd, tab, sliders[9], "AutoConfigILSMaxTripDrone", SliderLimitConfig.IlsMaxTripDroneMax, AutoConfigApplyTarget.IlsMaxTripDrone, 665f);
        AddApplyButton(wnd, tab, sliders[10], "AutoConfigILSMaxTripShip", SliderLimitConfig.IlsMaxTripShipMax, AutoConfigApplyTarget.IlsMaxTripShip, 667f);
        AddApplyButton(wnd, tab, sliders[11], "AutoConfigILSWarperDistance", SliderLimitConfig.IlsWarperDistanceMax, AutoConfigApplyTarget.IlsWarperDistance, 669f);
        AddApplyButton(wnd, tab, sliders[12], "AutoConfigILSDroneMinDeliver", SliderLimitConfig.IlsDroneMinDeliverMax, AutoConfigApplyTarget.IlsDroneMinDeliver, 671f);
        AddApplyButton(wnd, tab, sliders[13], "AutoConfigILSShipMinDeliver", SliderLimitConfig.IlsShipMinDeliverMax, AutoConfigApplyTarget.IlsShipMinDeliver, 673f);
        AddApplyButton(wnd, tab, sliders[14], "AutoConfigILSMinPilerValue", SliderLimitConfig.IlsMinPilerValueMax, AutoConfigApplyTarget.IlsMinPilerValue, 675f);
        AddApplyButton(wnd, tab, sliders[15], "AutoConfigILSDroneCount", SliderLimitConfig.IlsDroneCountMax, AutoConfigApplyTarget.IlsDroneCount, 677f);
        AddApplyButton(wnd, tab, sliders[16], "AutoConfigILSShipCount", SliderLimitConfig.IlsShipCountMax, AutoConfigApplyTarget.IlsShipCount, 679f);
        AddApplyButton(wnd, tab, sliders[17], "AutoConfigVeinCollectorHarvestSpeed", SliderLimitConfig.VeinCollectorHarvestSpeedMax, AutoConfigApplyTarget.VeinCollectorHarvestSpeed, 681f);
        AddApplyButton(wnd, tab, sliders[18], "AutoConfigVeinCollectorMinPilerValue", SliderLimitConfig.VeinCollectorMinPilerValueMax, AutoConfigApplyTarget.VeinCollectorMinPilerValue, 683f);
    }

    private static RectTransform FindLogisticsTab(RectTransform trans) {
        if (trans == null) {
            return null;
        }

        for (int i = 0; i < trans.childCount; i++) {
            Transform child = trans.GetChild(i);
            if (child is RectTransform rect && child.name == "tab-2") {
                return rect;
            }
        }

        return null;
    }

    private static void AddApplyButton(MyConfigWindow wnd, RectTransform tab, MySideSlider slider, string configName,
        BepInEx.Configuration.ConfigEntry<int> maxConfig, AutoConfigApplyTarget target, float sourceLine) {
        if (slider == null) {
            return;
        }

        BepInEx.Configuration.ConfigEntry<int> uxaConfig = GetUxAssistConfig(configName);
        int minValue = Mathf.RoundToInt(slider.slider.minValue);
        int maxValue = Math.Max(minValue, maxConfig.Value);
        ConfigEntryRangePatcher.SetRange(uxaConfig, minValue, maxValue);
        slider.slider.maxValue = maxValue;
        if (slider.Value > maxValue) {
            slider.Value = maxValue;
        }

        float y = -slider.rectTrans.anchoredPosition.y;
        UIButton button = wnd.AddButton(ButtonOffsetX, y, ButtonWidth, tab, "应用", ButtonFontSize,
            $"uxaenhance-apply-{configName}", () => ApplyAndNotify(target));
        button.tips.tipTitle = "UXAEnhance";
        button.tips.tipText = $"应用此项到全局已有设施。来源 UXAssist.UIConfigWindow.cs:{sourceLine:0}";
        button.UpdateTip();

        maxConfig.SettingChanged += OnMaxConfigChanged;
        wnd.OnFree += () => maxConfig.SettingChanged -= OnMaxConfigChanged;
        return;

        void OnMaxConfigChanged(object sender, EventArgs args) {
            int nextMax = Math.Max(minValue, maxConfig.Value);
            ConfigEntryRangePatcher.SetRange(uxaConfig, minValue, nextMax);
            slider.slider.maxValue = nextMax;
            if (slider.Value > nextMax) {
                slider.Value = nextMax;
            }
        }
    }

    private static BepInEx.Configuration.ConfigEntry<int> GetUxAssistConfig(string configName) {
        return (BepInEx.Configuration.ConfigEntry<int>)AccessTools.Field(typeof(LogisticsPatch), configName).GetValue(null);
    }

    private static MySideSlider[] FindAutoConfigSliders(RectTransform tab) {
        System.Collections.Generic.List<MySideSlider> sliders = [];
        for (int i = 0; i < tab.childCount; i++) {
            MySideSlider slider = tab.GetChild(i).GetComponent<MySideSlider>();
            if (slider == null) {
                continue;
            }

            sliders.Add(slider);
        }

        return sliders.ToArray();
    }

    private static void ApplyAndNotify(AutoConfigApplyTarget target) {
        int changedCount = AutoConfigGlobalApplyService.Apply(target);
        UIRealtimeTip.Popup($"UXAEnhance 已应用 {changedCount} 个设施", sound: false);
    }
}
