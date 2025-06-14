using BepInEx.Configuration;

namespace FE.Utils;

public class AcceptableIntValue(int defVal, int min, int max) : AcceptableValueBase(typeof(int)) {
    private readonly int defVal = defVal >= min && defVal <= max ? defVal : min;
    public override object Clamp(object value) => IsValid(value) ? (int)value : defVal;

    public override bool IsValid(object value) =>
        value.GetType() == ValueType && (int)value >= min && (int)value <= max;

    public override string ToDescriptionString() => null;
}

public class AcceptableFloatValue(float defVal, float min, float max) : AcceptableValueBase(typeof(float)) {
    private readonly float defVal = defVal >= min && defVal <= max ? defVal : min;
    public override object Clamp(object value) => IsValid(value) ? (float)value : defVal;

    public override bool IsValid(object value) =>
        value.GetType() == ValueType && (float)value >= min && (float)value <= max;

    public override string ToDescriptionString() => null;
}

public class AcceptableLongValue(long defVal, long min, long max) : AcceptableValueBase(typeof(long)) {
    private readonly long defVal = defVal >= min && defVal <= max ? defVal : min;
    public override object Clamp(object value) => IsValid(value) ? (long)value : defVal;

    public override bool IsValid(object value) =>
        value.GetType() == ValueType && (long)value >= min && (long)value <= max;

    public override string ToDescriptionString() => null;
}

public class AcceptableDoubleValue(double defVal, double min, double max) : AcceptableValueBase(typeof(double)) {
    private readonly double defVal = defVal >= min && defVal <= max ? defVal : min;
    public override object Clamp(object value) => IsValid(value) ? (double)value : defVal;

    public override bool IsValid(object value) =>
        value.GetType() == ValueType && (double)value >= min && (double)value <= max;

    public override string ToDescriptionString() => null;
}

public class AcceptableBoolValue(bool defVal) : AcceptableValueBase(typeof(bool)) {
    public override object Clamp(object value) => IsValid(value) ? (bool)value : defVal;
    public override bool IsValid(object value) => value.GetType() == ValueType;
    public override string ToDescriptionString() => null;
}

public class AcceptableStringValue(string defVal) : AcceptableValueBase(typeof(string)) {
    public override object Clamp(object value) => IsValid(value) ? (string)value : defVal;
    public override bool IsValid(object value) => value.GetType() == ValueType;
    public override string ToDescriptionString() => null;
}
