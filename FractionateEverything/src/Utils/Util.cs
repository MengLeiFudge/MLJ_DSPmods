using UnityEngine;

namespace FE.Utils;

public static class UIUtils {
    public static RectTransform
        NormalizeRectWithTopLeft(Component cmp, float left, float top, Transform parent = null) {
        if (cmp.transform is not RectTransform rect) return null;
        if (parent != null) {
            rect.SetParent(parent, false);
        }
        rect.anchorMax = new(0f, 1f);
        rect.anchorMin = new(0f, 1f);
        rect.pivot = new(0f, 1f);
        rect.anchoredPosition3D = new(left, -top, 0f);
        return rect;
    }

    public static RectTransform NormalizeRectWithTopRight(Component cmp, float right, float top,
        Transform parent = null) {
        if (cmp.transform is not RectTransform rect) return null;
        if (parent != null) {
            rect.SetParent(parent, false);
        }
        rect.anchorMax = new(1f, 1f);
        rect.anchorMin = new(1f, 1f);
        rect.pivot = new(1f, 1f);
        rect.anchoredPosition3D = new(-right, -top, 0f);
        return rect;
    }

    public static RectTransform NormalizeRectWithBottomLeft(Component cmp, float left, float bottom,
        Transform parent = null) {
        if (cmp.transform is not RectTransform rect) return null;
        if (parent != null) {
            rect.SetParent(parent, false);
        }
        rect.anchorMax = new(0f, 0f);
        rect.anchorMin = new(0f, 0f);
        rect.pivot = new(0f, 0f);
        rect.anchoredPosition3D = new(left, bottom, 0f);
        return rect;
    }

    public static RectTransform NormalizeRectWithMargin(Component cmp, float top, float left, float bottom, float right,
        Transform parent = null) {
        if (cmp.transform is not RectTransform rect) return null;
        if (parent != null) {
            rect.SetParent(parent, false);
        }
        rect.anchoredPosition3D = Vector3.zero;
        rect.localScale = Vector3.one;
        rect.anchorMax = Vector2.one;
        rect.anchorMin = Vector2.zero;
        rect.pivot = new(0.5f, 0.5f);
        rect.offsetMax = new(-right, -top);
        rect.offsetMin = new(left, bottom);
        return rect;
    }

    public static RectTransform NormalizeRectCenter(GameObject go, float width = 0, float height = 0) {
        if (go.transform is not RectTransform rect) return null;
        rect.anchorMax = new(0.5f, 0.5f);
        rect.anchorMin = new(0.5f, 0.5f);
        rect.pivot = new(0.5f, 0.5f);
        if (width > 0 && height > 0) {
            rect.sizeDelta = new(width, height);
        }
        return rect;
    }
}
