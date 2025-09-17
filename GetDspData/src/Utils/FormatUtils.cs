namespace GetDspData.Utils;

public static partial class Utils {
    public static string Name(this Proto proto) {
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
