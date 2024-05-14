using BepInEx.Configuration;

namespace FractionateEverything.Utils {
    public class AcceptableIntValue(int defVal, int min, int max) : AcceptableValueBase(typeof(int)) {
        private readonly int defVal = defVal >= min && defVal <= max ? defVal : min;
        public override object Clamp(object value) => IsValid(value) ? (int)value : defVal;

        public override bool IsValid(object value) =>
            value.GetType() == ValueType && (int)value >= min && (int)value <= max;

        public override string ToDescriptionString() => null;
    }

    public class AcceptableBoolValue(bool defVal) : AcceptableValueBase(typeof(bool)) {
        public override object Clamp(object value) => IsValid(value) ? (bool)value : defVal;
        public override bool IsValid(object value) => value.GetType() == ValueType;
        public override string ToDescriptionString() => null;
    }
}
