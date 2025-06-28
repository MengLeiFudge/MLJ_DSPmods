using UnityEngine;

namespace FE.Utils;

public static partial class Utils {
    // /// <summary>
    // /// 启用此patch后，可以得知红色是FF5D4CB7，蓝色是61D8FFB8
    // /// </summary>
    // [HarmonyPatch(typeof(UIItemTip), nameof(UIItemTip.SetTip))]
    // [HarmonyPostfix]
    // public static void LogColorGameUsed(ref UIItemTip __instance) {
    //     var text = __instance.valuesText.text;
    //     var color = __instance.valuesText.color;
    //     LogError($"text={text} argb={Math.Round(color.r * 255.0)},{Math.Round(color.b * 255.0)},{Math.Round(color.g * 255.0)},{Math.Round(color.a * 255.0)}");
    //     //log结果：
    //     //text=<color=#FF5D4Cb7>不能手动制造</color> rgba=150,150,150,255
    //     //text=310<color=#61D8FFB8> + 62</color> hp rgba=150,150,150,255
    //     //可以看出，这是用富文本控制的（text.supportRichText=true）
    //     //且text.color为浅灰色（rgba=150,150,150,255），富文本的红色是FF5D4CB7，蓝色是61D8FFB8
    //     //同样原理，橙色是FD965ECC
    // }

    // 游戏基础颜色
    public static Color Gray2 = new(255 / 255f, 255 / 255f, 255 / 255f, 102 / 255f);//UX使用的颜色
    public static Color Gray = new(150 / 255f, 150 / 255f, 150 / 255f, 255 / 255f);
    public static Color Orange = new(0xFD / 255f, 0x96 / 255f, 0x5E / 255f, 0xCC / 255f);
    public static Color Red = new(0xFF / 255f, 0x5D / 255f, 0x4C / 255f, 0xB7 / 255f);
    public static Color Blue = new(0x61 / 255f, 0xD8 / 255f, 0xFF / 255f, 0xB8 / 255f);

    // 品质颜色
    public static Color QualityBrown = new(0xA0 / 255f, 0x52 / 255f, 0x2D / 255f, 0xAA / 255f);// 褐色 #A0522DAA
    public static Color QualityWhite = new(0xFF / 255f, 0xFF / 255f, 0xFF / 255f, 0xAA / 255f);// 白色 #FFFFFFAA
    public static Color QualityGreen = new(0x7C / 255f, 0xFC / 255f, 0x00 / 255f, 0xAA / 255f);// 绿色 #7CFC00AA
    public static Color QualityBlue = new(0x61 / 255f, 0xD8 / 255f, 0xFF / 255f, 0xB8 / 255f);// 蓝色 #61D8FFB8
    public static Color QualityPurple = new(0xDA / 255f, 0x70 / 255f, 0xD6 / 255f, 0xAA / 255f);// 紫色 #DA70D6AA
    public static Color QualityRed = new(0xFF / 255f, 0x5D / 255f, 0x4C / 255f, 0xB7 / 255f);// 红色 #FF5D4CB7
    public static Color QualityGold = new(0xFF / 255f, 0xD7 / 255f, 0x00 / 255f, 0xAA / 255f);// 金色 #FFD700AA

    /// <summary>
    /// 为字符串添加指定颜色标签。
    /// </summary>
    public static string WithColor(this string s, Color color) {
        string hexColor =
            $"#{(byte)(color.r * 255):X2}{(byte)(color.g * 255):X2}{(byte)(color.b * 255):X2}{(byte)(color.a * 255):X2}";
        return $"<color={hexColor}>{s}</color>";
    }

    /// <summary>
    /// 根据品质等级为字符串添加对应颜色标签。
    /// </summary>
    public static string WithQualityColor(this string s, int quality) {
        return quality switch {
            0 => s.WithColor(QualityBrown),// 褐色
            1 => s.WithColor(QualityWhite),// 白色
            2 => s.WithColor(QualityGreen),// 绿色
            3 => s.WithColor(QualityBlue),// 蓝色
            4 => s.WithColor(QualityPurple),// 紫色
            5 => s.WithColor(QualityRed),// 红色
            7 => s.WithColor(QualityGold),// 金色
            _ => ("invalid quality level" + quality).WithColor(QualityRed)
        };
    }
}
