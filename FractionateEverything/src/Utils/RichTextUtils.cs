using UnityEngine;

namespace FE.Utils;

/// <summary>
/// 富文本颜色、字号与数值显示辅助方法。
/// </summary>
public static partial class Utils {
    // Blue/Red/Orange 等值取自 DSP UIItemTip 富文本色码，不是 Text.color 本体。
    public static Color Gray = new(150 / 255f, 150 / 255f, 150 / 255f, 255 / 255f);
    public static Color Gray2 = new(255 / 255f, 255 / 255f, 255 / 255f, 102 / 255f);//UX使用的颜色
    public static Color Gray3 = new(178 / 255f, 178 / 255f, 178 / 255f, 168 / 255f);//UX使用的颜色
    public static Color White = new(0xE0 / 255f, 0xE0 / 255f, 0xE0 / 255f, 0xB7 / 255f);
    public static Color Green = new(0x60 / 255f, 0xC0 / 255f, 0x00 / 255f, 0xCC / 255f);
    public static Color Blue = new(0x61 / 255f, 0xD8 / 255f, 0xFF / 255f, 0xB8 / 255f);
    public static Color Purple = new(0xB0 / 255f, 0x60 / 255f, 0xC0 / 255f, 0xB7 / 255f);
    public static Color Red = new(0xFF / 255f, 0x5D / 255f, 0x4C / 255f, 0xB7 / 255f);
    public static Color Orange = new(0xFD / 255f, 0x96 / 255f, 0x5E / 255f, 0xCC / 255f);
    public static Color Gold = new(0xE0 / 255f, 0xB0 / 255f, 0x00 / 255f, 0xCC / 255f);

    /// <summary>
    /// 为字符串添加指定颜色的富文本标签。
    /// </summary>
    public static string WithColor(this string s, Color color) {
        string hexColor =
            $"#{(byte)(color.r * 255):X2}{(byte)(color.g * 255):X2}{(byte)(color.b * 255):X2}{(byte)(color.a * 255):X2}";
        return $"<color={hexColor}>{s}</color>";
    }

    /// <summary>
    /// 根据颜色档位为字符串添加对应富文本颜色。
    /// </summary>
    public static string WithColor(this string s, int colorIdx) {
        return colorIdx switch {
            <= 0 => s.WithColor(Gray),
            1 => s.WithColor(White),
            2 => s.WithColor(Green),
            3 => s.WithColor(Blue),
            4 => s.WithColor(Purple),
            5 => s.WithColor(Red),
            6 => s.WithColor(Orange),
            >= 7 => s.WithColor(Gold),
        };
    }

    /// <summary>
    /// 根据物品价值为字符串添加对应颜色的富文本标签。
    /// </summary>
    public static string WithColor(this string s, float itemValue) {
        return itemValue switch {
            <= 5 => s.WithColor(Gray),
            <= 20 => s.WithColor(White),
            <= 100 => s.WithColor(Green),
            <= 500 => s.WithColor(Blue),
            <= 2500 => s.WithColor(Purple),
            <= 10000 => s.WithColor(Red),
            <= 100000 => s.WithColor(Orange),
            _ => s.WithColor(Gold)
        };
    }
}
