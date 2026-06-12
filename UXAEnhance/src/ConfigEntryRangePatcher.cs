using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;

namespace UXAEnhance;

internal static class ConfigEntryRangePatcher {
    private static readonly FieldInfo DescriptionField =
        AccessTools.Field(typeof(ConfigEntryBase), "<Description>k__BackingField") ??
        AccessTools.Field(typeof(ConfigEntryBase), "_description");

    public static bool SetRange(ConfigEntry<int> config, int minValue, int maxValue) {
        if (config == null) {
            return false;
        }

        if (DescriptionField == null) {
            return false;
        }

        try {
            ConfigDescription oldDescription = config.Description ?? ConfigDescription.Empty;
            ConfigDescription newDescription = new(
                oldDescription.Description,
                new AcceptableValueRange<int>(minValue, maxValue),
                oldDescription.Tags);
            DescriptionField.SetValue(config, newDescription);
            config.Value = Clamp(config.Value, minValue, maxValue);
            return true;
        }
        catch {
            return false;
        }
    }

    private static int Clamp(int value, int minValue, int maxValue) {
        if (value < minValue) {
            return minValue;
        }

        return value > maxValue ? maxValue : value;
    }
}
