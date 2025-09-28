using UnityEngine;
using static FE.Logic.Manager.ItemManager;

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

    public static Color Gray = new(150 / 255f, 150 / 255f, 150 / 255f, 255 / 255f);
    public static Color Gray2 = new(255 / 255f, 255 / 255f, 255 / 255f, 102 / 255f);//UX使用的颜色
    public static Color Gray3 = new(178 / 255f, 178 / 255f, 178 / 255f, 168 / 255f);//UX使用的颜色
    public static Color White = new(0xE0 / 255f, 0xE0 / 255f, 0xE0 / 255f, 0xB7 / 255f);
    public static Color Green = new(0x60 / 255f, 0xC0 / 255f, 0x00 / 255f, 0xB7 / 255f);
    public static Color Blue = new(0x61 / 255f, 0xD8 / 255f, 0xFF / 255f, 0xB8 / 255f);
    public static Color Purple = new(0xB0 / 255f, 0x60 / 255f, 0xC0 / 255f, 0xB7 / 255f);
    public static Color Red = new(0xFF / 255f, 0x5D / 255f, 0x4C / 255f, 0xB7 / 255f);
    public static Color Orange = new(0xFD / 255f, 0x96 / 255f, 0x5E / 255f, 0xCC / 255f);
    public static Color Gold = new(0xE0 / 255f, 0xB0 / 255f, 0x00 / 255f, 0xB7 / 255f);

    /// <summary>
    /// 为字符串添加指定颜色的富文本标签。
    /// </summary>
    public static string WithColor(this string s, Color color) {
        string hexColor =
            $"#{(byte)(color.r * 255):X2}{(byte)(color.g * 255):X2}{(byte)(color.b * 255):X2}{(byte)(color.a * 255):X2}";
        return $"<color={hexColor}>{s}</color>";
    }

    /// <summary>
    /// 根据品质等级为字符串添加对应颜色的富文本标签。
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
    public static string WithValueColor(this string s, int itemID) {
        return itemValue[itemID] switch {
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
