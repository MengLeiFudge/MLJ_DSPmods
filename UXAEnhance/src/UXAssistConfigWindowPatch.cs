using System;
using System.Reflection;
using UnityEngine;
using UXAssist.Patches;
using UXAssist.UI;

namespace UXAEnhance;

/// <summary>
/// UXAssist 配置窗物流页增强：为自动配置滑条与布尔项挂载全局应用按钮，并同步滑条上限。
/// </summary>
internal static class UXAssistConfigWindowPatch {
    private const float ButtonWidth = 44f;
    private const float ButtonOffsetX = 470f;
    private const int ButtonFontSize = 12;

    /// <summary>
    /// 单条滑条应用绑定：UXAssist 配置字段名、本模组上限配置、应用目标与提示文案来源行号。
    /// </summary>
    private readonly struct SliderApplyBinding {
        /// <summary>
        /// UXAssist <see cref="LogisticsPatch"/> 上对应的静态 <c>ConfigEntry&lt;int&gt;</c> 字段名。
        /// </summary>
        public readonly string ConfigName;

        /// <summary>
        /// UXAEnhance 侧该滑条的可配置最大值。
        /// </summary>
        public readonly BepInEx.Configuration.ConfigEntry<int> MaxConfig;

        /// <summary>
        /// 点击应用时写入全局设施的目标字段。
        /// </summary>
        public readonly AutoConfigApplyTarget Target;

        /// <summary>
        /// 提示文案中引用的 UXAssist.UIConfigWindow.cs 源行号（仅展示，不参与逻辑）。
        /// </summary>
        public readonly int SourceLine;

        /// <summary>
        /// 组装一条滑条应用绑定。
        /// </summary>
        /// <param name="configName">UXAssist 配置字段名。</param>
        /// <param name="maxConfig">本模组滑条上限配置。</param>
        /// <param name="target">全局应用目标。</param>
        /// <param name="sourceLine">提示用来源行号。</param>
        public SliderApplyBinding(
            string configName,
            BepInEx.Configuration.ConfigEntry<int> maxConfig,
            AutoConfigApplyTarget target,
            int sourceLine) {
            ConfigName = configName;
            MaxConfig = maxConfig;
            Target = target;
            SourceLine = sourceLine;
        }
    }

    /// <summary>
    /// UXAssist 配置窗创建后回调：在物流 Tab 为自动配置项追加应用按钮并接线上限。
    /// </summary>
    /// <param name="wnd">UXAssist 配置窗实例。</param>
    /// <param name="trans">配置窗内容根节点。</param>
    public static void OnUxAssistConfigWindowCreated(MyConfigWindow wnd, RectTransform trans) {
        RectTransform tab = FindLogisticsTab(trans);
        if (tab == null) {
            return;
        }

        MySideSlider[] sliders = FindAutoConfigSliders(tab);
        if (sliders.Length < 19) {
            return;
        }

        SliderApplyBinding[] bindings = [
            new("AutoConfigDispenserChargePower", SliderLimitConfig.DispenserChargePowerMax, AutoConfigApplyTarget.DispenserChargePower, 647),
            new("AutoConfigDispenserCourierCount", SliderLimitConfig.DispenserCourierCountMax, AutoConfigApplyTarget.DispenserCourierCount, 649),
            new("AutoConfigBattleBaseChargePower", SliderLimitConfig.BattleBaseChargePowerMax, AutoConfigApplyTarget.BattleBaseChargePower, 651),
            new("AutoConfigPLSChargePower", SliderLimitConfig.PlsChargePowerMax, AutoConfigApplyTarget.PlsChargePower, 653),
            new("AutoConfigPLSMaxTripDrone", SliderLimitConfig.PlsMaxTripDroneMax, AutoConfigApplyTarget.PlsMaxTripDrone, 655),
            new("AutoConfigPLSDroneMinDeliver", SliderLimitConfig.PlsDroneMinDeliverMax, AutoConfigApplyTarget.PlsDroneMinDeliver, 657),
            new("AutoConfigPLSMinPilerValue", SliderLimitConfig.PlsMinPilerValueMax, AutoConfigApplyTarget.PlsMinPilerValue, 659),
            new("AutoConfigPLSDroneCount", SliderLimitConfig.PlsDroneCountMax, AutoConfigApplyTarget.PlsDroneCount, 661),
            new("AutoConfigILSChargePower", SliderLimitConfig.IlsChargePowerMax, AutoConfigApplyTarget.IlsChargePower, 663),
            new("AutoConfigILSMaxTripDrone", SliderLimitConfig.IlsMaxTripDroneMax, AutoConfigApplyTarget.IlsMaxTripDrone, 665),
            new("AutoConfigILSMaxTripShip", SliderLimitConfig.IlsMaxTripShipMax, AutoConfigApplyTarget.IlsMaxTripShip, 667),
            new("AutoConfigILSWarperDistance", SliderLimitConfig.IlsWarperDistanceMax, AutoConfigApplyTarget.IlsWarperDistance, 669),
            new("AutoConfigILSDroneMinDeliver", SliderLimitConfig.IlsDroneMinDeliverMax, AutoConfigApplyTarget.IlsDroneMinDeliver, 671),
            new("AutoConfigILSShipMinDeliver", SliderLimitConfig.IlsShipMinDeliverMax, AutoConfigApplyTarget.IlsShipMinDeliver, 673),
            new("AutoConfigILSMinPilerValue", SliderLimitConfig.IlsMinPilerValueMax, AutoConfigApplyTarget.IlsMinPilerValue, 675),
            new("AutoConfigILSDroneCount", SliderLimitConfig.IlsDroneCountMax, AutoConfigApplyTarget.IlsDroneCount, 677),
            new("AutoConfigILSShipCount", SliderLimitConfig.IlsShipCountMax, AutoConfigApplyTarget.IlsShipCount, 679),
            new("AutoConfigVeinCollectorHarvestSpeed", SliderLimitConfig.VeinCollectorHarvestSpeedMax, AutoConfigApplyTarget.VeinCollectorHarvestSpeed, 681),
            new("AutoConfigVeinCollectorMinPilerValue", SliderLimitConfig.VeinCollectorMinPilerValueMax, AutoConfigApplyTarget.VeinCollectorMinPilerValue, 683),
        ];

        for (int i = 0; i < bindings.Length; i++) {
            AddApplyButton(wnd, tab, sliders[i], in bindings[i]);
        }

        AddBooleanApplyButton(wnd, tab, AutoConfigApplyTarget.IlsIncludeOrbitCollector, "AutoConfigILSIncludeOrbitCollector", 628f);
        AddBooleanApplyButton(wnd, tab, AutoConfigApplyTarget.IlsWarperNecessary, "AutoConfigILSWarperNecessary", 646f);
    }

    /// <summary>
    /// 在配置窗子节点中定位物流 Tab（名称 <c>tab-2</c>）。
    /// </summary>
    /// <param name="trans">配置窗内容根节点。</param>
    /// <returns>物流 Tab；未找到时返回 <c>null</c>。</returns>
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

    /// <summary>
    /// 为一条自动配置滑条接线上限同步，并在其右侧创建全局应用按钮。
    /// </summary>
    /// <param name="wnd">配置窗，用于创建按钮并订阅释放清理。</param>
    /// <param name="tab">物流 Tab 根节点。</param>
    /// <param name="slider">UXAssist 已创建的侧滑条。</param>
    /// <param name="binding">该滑条的配置字段、上限、应用目标与提示来源。</param>
    private static void AddApplyButton(MyConfigWindow wnd, RectTransform tab, MySideSlider slider,
        in SliderApplyBinding binding) {
        if (slider == null) {
            return;
        }

        BepInEx.Configuration.ConfigEntry<int> uxaConfig = GetUxAssistConfig(binding.ConfigName);
        BepInEx.Configuration.ConfigEntry<int> maxConfig = binding.MaxConfig;
        int minValue = Mathf.RoundToInt(slider.slider.minValue);
        int maxValue = Math.Max(minValue, maxConfig.Value);
        ConfigEntryRangePatcher.SetRange(uxaConfig, minValue, maxValue);
        slider.slider.maxValue = maxValue;
        if (slider.Value > maxValue) {
            slider.Value = maxValue;
        }

        float y = -slider.rectTrans.anchoredPosition.y;
        CreateApplyButton(wnd, tab, y, binding.ConfigName, binding.Target, BuildApplyTip(binding.Target, binding.SourceLine));

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

    /// <summary>
    /// 为物流页布尔自动配置项创建固定坐标的全局应用按钮。
    /// </summary>
    /// <param name="wnd">配置窗。</param>
    /// <param name="tab">物流 Tab 根节点。</param>
    /// <param name="target">布尔项对应的应用目标。</param>
    /// <param name="objectName">按钮对象名后缀，对应 UXAssist 配置对象名。</param>
    /// <param name="y">按钮锚定 Y（与 UXAssist 原布局对齐的绝对值坐标）。</param>
    private static void AddBooleanApplyButton(MyConfigWindow wnd, RectTransform tab, AutoConfigApplyTarget target,
        string objectName, float y) {
        string tipText = target switch {
            AutoConfigApplyTarget.IlsIncludeOrbitCollector => "应用到全局已有星际物流运输站：是否包含轨道采集器。",
            AutoConfigApplyTarget.IlsWarperNecessary => "应用到全局已有星际物流运输站：是否需要翘曲器。",
            _ => "应用此项到全局已有设施。",
        };
        CreateApplyButton(wnd, tab, y, objectName, target, tipText);
    }

    /// <summary>
    /// 按固定宽度/偏移创建「应用」按钮，挂上提示与点击回调。
    /// </summary>
    /// <param name="wnd">配置窗。</param>
    /// <param name="tab">父级 Tab。</param>
    /// <param name="y">按钮锚定 Y。</param>
    /// <param name="objectName">用于生成唯一按钮名的后缀。</param>
    /// <param name="target">点击时应用的目标。</param>
    /// <param name="tipText">按钮悬停提示正文。</param>
    private static void CreateApplyButton(MyConfigWindow wnd, RectTransform tab, float y, string objectName,
        AutoConfigApplyTarget target, string tipText) {
        UIButton button = wnd.AddButton(ButtonOffsetX, y, ButtonWidth, tab, "应用", ButtonFontSize,
            $"uxaenhance-apply-{objectName}", () => ApplyAndNotify(target));
        button.tips.tipTitle = "UXAEnhance";
        button.tips.tipText = tipText;
        button.UpdateTip();
    }

    /// <summary>
    /// 构建滑条应用按钮的悬停提示（含 UXAssist 来源行与充电功率单位说明）。
    /// </summary>
    /// <param name="target">应用目标。</param>
    /// <param name="sourceLine">UXAssist 源码行号展示值。</param>
    /// <returns>提示正文。</returns>
    private static string BuildApplyTip(AutoConfigApplyTarget target, int sourceLine) {
        string tip = $"应用此项到全局已有设施。来源 UXAssist.UIConfigWindow.cs:{sourceLine}";
        return target switch {
            AutoConfigApplyTarget.IlsChargePower => tip + "\n星际物流运输站：1 = 15MW。",
            AutoConfigApplyTarget.PlsChargePower => tip + "\n行星物流运输站：1 = 3MW。",
            AutoConfigApplyTarget.DispenserChargePower => tip + "\n物流配送器：1 = 0.3MW。",
            _ => tip,
        };
    }

    /// <summary>
    /// 通过反射读取 UXAssist <see cref="LogisticsPatch"/> 上的整型配置项。
    /// </summary>
    /// <param name="configName">静态字段名。</param>
    /// <returns>对应配置项；字段不存在时为 <c>null</c>。</returns>
    private static BepInEx.Configuration.ConfigEntry<int> GetUxAssistConfig(string configName) {
        FieldInfo field = typeof(LogisticsPatch).GetField(configName, BindingFlags.Static | BindingFlags.Public);
        return (BepInEx.Configuration.ConfigEntry<int>)field?.GetValue(null);
    }

    /// <summary>
    /// 按子节点顺序收集物流 Tab 上全部 <see cref="MySideSlider"/>。
    /// </summary>
    /// <param name="tab">物流 Tab。</param>
    /// <returns>滑条数组（顺序与 UXAssist 创建顺序一致）。</returns>
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

    /// <summary>
    /// 将指定自动配置目标应用到全局已有设施，并弹出变更数量提示。
    /// </summary>
    /// <param name="target">应用目标。</param>
    private static void ApplyAndNotify(AutoConfigApplyTarget target) {
        int changedCount = AutoConfigGlobalApplyService.Apply(target);
        UIRealtimeTip.Popup($"UXAEnhance 已应用 {changedCount} 个设施", sound: false);
    }
}
