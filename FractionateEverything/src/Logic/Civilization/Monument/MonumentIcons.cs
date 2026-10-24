using System.Collections.Generic;

using UnityEngine;

namespace FE.Logic.Civilization.Monument;

/// <summary>启动时按来源矩阵生成丰碑帆图标；缓存与物品原型同寿命，不在切档时释放。</summary>
internal static class MonumentIcons {
    private const int Size = 80;
    private const int BadgeSize = 40;
    private static readonly Dictionary<int, Sprite> sailIcons = [];

    /// <summary>保留帆形，以矩阵主色及右下矩阵徽标区分来源，包括外部模组矩阵。</summary>
    internal static Sprite GetSailIcon(ItemProto source) {
        if (sailIcons.TryGetValue(source.ID, out Sprite cached)) { return cached; }
        Sprite template = Resources.Load<Sprite>("Assets/fe/monument-sail");
        Sprite matrixSprite = source.iconSprite;
        if (matrixSprite == null && !string.IsNullOrEmpty(source.IconPath)) {
            matrixSprite = Resources.Load<Sprite>(source.IconPath);
        }
        Texture2D matrix = matrixSprite != null ? ReadSprite(matrixSprite) : null;
        Texture2D texture = null;
        Sprite sprite = null;
        try {
            Color tone = matrix != null ? GetTone(matrix) : Color.HSVToRGB(source.ID * 0.618034f % 1f, 0.65f, 1f);
            Color tint = Color.Lerp(Color.white, tone, 0.7f);
            Color[] pixels = template.texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = new Color(pixels[i].r * tint.r, pixels[i].g * tint.g, pixels[i].b * tint.b, pixels[i].a);
            }
            if (matrix != null) {
                for (int y = 0; y < BadgeSize; y++) {
                    for (int x = 0; x < BadgeSize; x++) {
                        int index = y * Size + Size - BadgeSize + x;
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(20f, 20f));
                        float border = Mathf.Clamp01(19f - distance);
                        if (border > 0f) {
                            Color backing = distance > 17f ? new Color(0.7f, 0.65f, 0.48f, border) : new Color(0.07f, 0.09f, 0.12f, 1f);
                            pixels[index] = Over(backing, pixels[index]);
                        }
                        Color badge = matrix.GetPixelBilinear((x + 0.5f) / BadgeSize, (y + 0.5f) / BadgeSize);
                        pixels[index] = Over(badge, pixels[index]);
                    }
                }
            }
            texture = new Texture2D(Size, Size, TextureFormat.ARGB32, true) {
                name = $"monument-sail-{source.ID}", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sailIcons.Add(source.ID, sprite);
            return sprite;
        } catch {
            if (sprite != null) { Object.Destroy(sprite); }
            if (texture != null) { Object.Destroy(texture); }
            throw;
        } finally {
            if (matrix != null) { Object.Destroy(matrix); }
        }
    }

    /// <summary>读取矩阵精灵的实际矩形；不要求原版或外部纹理开启CPU读取。</summary>
    private static Texture2D ReadSprite(Sprite sprite) {
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
        Texture2D image = null;
        try {
            Rect rect = sprite.textureRect;
            Texture2D source = sprite.texture;
            Graphics.Blit(source, target, new Vector2(rect.width / source.width, rect.height / source.height),
                new Vector2(rect.x / source.width, rect.y / source.height));
            RenderTexture.active = target;
            image = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
            image.Apply(false, false);
            return image;
        } catch {
            if (image != null) { Object.Destroy(image); }
            throw;
        } finally {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    /// <summary>提高有色像素权重，保留白矩阵的中性色，避免高光把彩色矩阵洗白。</summary>
    private static Color GetTone(Texture2D matrix) {
        Color sum = Color.black;
        foreach (Color pixel in matrix.GetPixels()) {
            Color.RGBToHSV(pixel, out _, out float saturation, out _);
            sum += pixel * (pixel.a * Mathf.Max(0.05f, saturation));
        }
        float brightest = Mathf.Max(sum.r, Mathf.Max(sum.g, sum.b));
        return brightest > 0f ? new Color(sum.r / brightest, sum.g / brightest, sum.b / brightest, 1f) : Color.white;
    }

    private static Color Over(Color foreground, Color background) {
        float alpha = foreground.a + background.a * (1f - foreground.a);
        if (alpha <= 0f) { return Color.clear; }
        Color result = (foreground * foreground.a + background * (background.a * (1f - foreground.a))) / alpha;
        result.a = alpha;
        return result;
    }
}
