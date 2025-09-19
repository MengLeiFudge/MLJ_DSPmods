using static GetDspData.GetDspData;

namespace GetDspData.Utils;

public static partial class Utils {
    public static string Name(this Proto proto) {
        if (proto == null) {
            return "null";
        }
        if (OrbitalRingEnable && proto is ItemProto item && item.ID == IOR终末螺旋) {
            return "终末螺旋";
        }
        return string.IsNullOrEmpty(proto.Name) ? proto.name : proto.Name;
    }

    public static string FName(this Proto proto) {
        return proto.Name().Translate()
            .Replace(" ", "")
            .Replace(" ", "")
            .Replace(" ", "")
            .Replace("“", "")
            .Replace("”", "")
            .Replace(":", "")
            .Replace("：", "")
            .Replace("!", "")
            .Replace("-", "")
            .Replace(".", "")
            .Replace("（", "")
            .Replace("）", "")
            .Replace("「", "")
            .Replace("」", "")
            .Replace("『", "")
            .Replace("』", "")
            .Replace("Recipe", "");
    }
}
