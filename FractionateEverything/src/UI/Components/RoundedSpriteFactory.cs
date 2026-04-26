using UnityEngine;

namespace FE.UI.Components;

/// <summary>
/// 运行时程序化生成圆角九宫格 Sprite，避免额外美术资源。
/// 两张 Sprite：
///   - Fill：圆角实心，配合 <c>Image.color</c> 作为卡片填充色
///   - Border：圆角 1 px 描边环，配合 <c>Image.color</c> 作为卡片边线
/// 均为 Sliced 模式，任意尺寸拉伸不变形。
/// </summary>
internal static class RoundedSpriteFactory {
    private const int Radius = 8;
    private const int Size = Radius * 2 + 1;

    private static Sprite fillSprite;
    private static Sprite borderSprite;

    public static Sprite GetFillSprite() {
        if (fillSprite == null) {
            fillSprite = BuildFill();
        }
        return fillSprite;
    }

    public static Sprite GetBorderSprite() {
        if (borderSprite == null) {
            borderSprite = BuildBorder();
        }
        return borderSprite;
    }

    private static Sprite BuildFill() {
        Texture2D tex = NewTexture();
        Color solid = Color.white;
        Color clear = new(1f, 1f, 1f, 0f);
        for (int y = 0; y < Size; y++) {
            for (int x = 0; x < Size; x++) {
                tex.SetPixel(x, y, IsInsideRoundedRect(x, y, 0f) ? solid : clear);
            }
        }
        tex.Apply();
        return CreateSlicedSprite(tex);
    }

    private static Sprite BuildBorder() {
        Texture2D tex = NewTexture();
        Color solid = Color.white;
        Color clear = new(1f, 1f, 1f, 0f);
        for (int y = 0; y < Size; y++) {
            for (int x = 0; x < Size; x++) {
                bool outside = !IsInsideRoundedRect(x, y, 0f);
                bool inside = IsInsideRoundedRect(x, y, 1f);
                tex.SetPixel(x, y, outside || inside ? clear : solid);
            }
        }
        tex.Apply();
        return CreateSlicedSprite(tex);
    }

    private static Texture2D NewTexture() {
        return new Texture2D(Size, Size, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "FE-RoundedSprite",
        };
    }

    private static Sprite CreateSlicedSprite(Texture2D tex) {
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0u,
            SpriteMeshType.FullRect, new Vector4(Radius, Radius, Radius, Radius));
        sprite.name = "FE-Rounded";
        Object.DontDestroyOnLoad(tex);
        Object.DontDestroyOnLoad(sprite);
        return sprite;
    }

    /// <summary>
    /// 以 <paramref name="inset"/> 像素向内收缩后判断点 (x+0.5, y+0.5) 是否在圆角矩形内。
    /// </summary>
    private static bool IsInsideRoundedRect(int x, int y, float inset) {
        float px = x + 0.5f;
        float py = y + 0.5f;
        float left = inset;
        float right = Size - inset;
        float bottom = inset;
        float top = Size - inset;
        if (px < left || px > right || py < bottom || py > top) {
            return false;
        }
        float r = Radius - inset;
        if (r <= 0f) {
            return true;
        }
        float cx = px < left + r ? left + r : px > right - r ? right - r : px;
        float cy = py < bottom + r ? bottom + r : py > top - r ? top - r : py;
        float dx = px - cx;
        float dy = py - cy;
        return dx * dx + dy * dy <= r * r;
    }
}
