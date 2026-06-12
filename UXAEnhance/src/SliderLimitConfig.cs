using BepInEx.Configuration;

namespace UXAEnhance;

internal static class SliderLimitConfig {
    public static ConfigEntry<int> DispenserChargePowerMax { get; private set; }
    public static ConfigEntry<int> DispenserCourierCountMax { get; private set; }
    public static ConfigEntry<int> BattleBaseChargePowerMax { get; private set; }
    public static ConfigEntry<int> PlsChargePowerMax { get; private set; }
    public static ConfigEntry<int> PlsMaxTripDroneMax { get; private set; }
    public static ConfigEntry<int> PlsDroneMinDeliverMax { get; private set; }
    public static ConfigEntry<int> PlsMinPilerValueMax { get; private set; }
    public static ConfigEntry<int> PlsDroneCountMax { get; private set; }
    public static ConfigEntry<int> IlsChargePowerMax { get; private set; }
    public static ConfigEntry<int> IlsMaxTripDroneMax { get; private set; }
    public static ConfigEntry<int> IlsMaxTripShipMax { get; private set; }
    public static ConfigEntry<int> IlsWarperDistanceMax { get; private set; }
    public static ConfigEntry<int> IlsDroneMinDeliverMax { get; private set; }
    public static ConfigEntry<int> IlsShipMinDeliverMax { get; private set; }
    public static ConfigEntry<int> IlsMinPilerValueMax { get; private set; }
    public static ConfigEntry<int> IlsDroneCountMax { get; private set; }
    public static ConfigEntry<int> IlsShipCountMax { get; private set; }
    public static ConfigEntry<int> VeinCollectorHarvestSpeedMax { get; private set; }
    public static ConfigEntry<int> VeinCollectorMinPilerValueMax { get; private set; }

    public static void Bind(ConfigFile config) {
        DispenserChargePowerMax = BindMax(config, "DispenserChargePowerMax", 30, 3, "物流配送器最大充电功率滑条上限。UXA 原始上限为 30。");
        DispenserCourierCountMax = BindMax(config, "DispenserCourierCountMax", 10, 0, "物流配送器填充配送机数量滑条上限。UXA 原始上限为 10。");
        BattleBaseChargePowerMax = BindMax(config, "BattleBaseChargePowerMax", 40, 4, "战场分析基站最大充电功率滑条上限。UXA 原始上限为 40。");
        PlsChargePowerMax = BindMax(config, "PLSChargePowerMax", 20, 2, "行星物流站最大充电功率滑条上限。UXA 原始上限为 20。");
        PlsMaxTripDroneMax = BindMax(config, "PLSMaxTripDroneMax", 180, 1, "行星物流站运输机最远路程滑条上限，单位为角度。UXA 原始上限为 180。");
        PlsDroneMinDeliverMax = BindMax(config, "PLSDroneMinDeliverMax", 10, 0, "行星物流站运输机起送量滑条上限。UXA 原始上限为 10。");
        PlsMinPilerValueMax = BindMax(config, "PLSMinPilerValueMax", 4, 0, "行星物流站输出集装数量滑条上限。UXA 原始上限为 4。");
        PlsDroneCountMax = BindMax(config, "PLSDroneCountMax", 50, 0, "行星物流站填充运输机数量滑条上限。UXA 原始上限为 50。");
        IlsChargePowerMax = BindMax(config, "ILSChargePowerMax", 20, 2, "星际物流站最大充电功率滑条上限。UXA 原始上限为 20。");
        IlsMaxTripDroneMax = BindMax(config, "ILSMaxTripDroneMax", 180, 1, "星际物流站运输机最远路程滑条上限，单位为角度。UXA 原始上限为 180。");
        IlsMaxTripShipMax = BindMax(config, "ILSMaxTripShipMax", 41, 1, "星际物流站运输船最远路程滑条上限。UXA 原始上限为 41，41 表示无限。");
        IlsWarperDistanceMax = BindMax(config, "ILSWarperDistanceMax", 21, 2, "星际物流站曲速启用路程滑条上限。UXA 原始上限为 21。");
        IlsDroneMinDeliverMax = BindMax(config, "ILSDroneMinDeliverMax", 10, 0, "星际物流站运输机起送量滑条上限。UXA 原始上限为 10。");
        IlsShipMinDeliverMax = BindMax(config, "ILSShipMinDeliverMax", 10, 0, "星际物流站运输船起送量滑条上限。UXA 原始上限为 10。");
        IlsMinPilerValueMax = BindMax(config, "ILSMinPilerValueMax", 4, 0, "星际物流站输出集装数量滑条上限。UXA 原始上限为 4。");
        IlsDroneCountMax = BindMax(config, "ILSDroneCountMax", 100, 0, "星际物流站填充运输机数量滑条上限。UXA 原始上限为 100。");
        IlsShipCountMax = BindMax(config, "ILSShipCountMax", 10, 0, "星际物流站填充运输船数量滑条上限。UXA 原始上限为 10。");
        VeinCollectorHarvestSpeedMax = BindMax(config, "VeinCollectorHarvestSpeedMax", 20, 0, "大型采矿机开采速度滑条上限。UXA 原始上限为 20。");
        VeinCollectorMinPilerValueMax = BindMax(config, "VeinCollectorMinPilerValueMax", 4, 0, "大型采矿机输出集装数量滑条上限。UXA 原始上限为 4。");
    }

    private static ConfigEntry<int> BindMax(ConfigFile config, string key, int defaultValue, int minValue, string description) {
        return config.Bind("SliderLimits", key, defaultValue,
            new ConfigDescription(description, new AcceptableValueRange<int>(minValue, 1000000)));
    }
}
