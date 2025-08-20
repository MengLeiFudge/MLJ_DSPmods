using System;
using UnityEngine;
using UnityEngine.UI;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 设置元素左上角相对于parent左上角的位置
    /// </summary>
    /// <param name="cmp">组件，类型为Component表示可以传入任何类型组件（例如Button、Image等）</param>
    /// <param name="left">cmp左上角在parent左上角的往右多少</param>
    /// <param name="top">cmp左上角在parent左上角的往下多少</param>
    /// <param name="parent">如果不为空，可以重设cmp的parent</param>
    /// <returns>cmp的transform</returns>
    public static RectTransform NormalizeRectWithTopLeft(Component cmp, float left, float top,
        Transform parent = null) {
        //只有UI相关的元素，transform才是RectTransform
        if (cmp.transform is not RectTransform rect) return null;
        //如果parent不为空，可以重设cmp的parent
        if (parent != null) {
            rect.SetParent(parent, false);
        }
        //锚点矩形的左下角是父容器的左上角，锚点矩形的右上角是父容器的左上角
        //anchorMin和anchorMax相等时，UI元素的大小不会随父容器大小变化而自动调整
        rect.anchorMin = new(0f, 1f);
        rect.anchorMax = new(0f, 1f);
        //UI元素自身的参考点为左上角。这意味着：
        //当设置位置时，是以元素的左上角为基准点
        //当旋转元素时，会围绕左上角旋转
        //当缩放元素时，左上角位置保持不变
        rect.pivot = new(0f, 1f);
        //UI元素的pivot点相对于锚点的偏移量。由于传入的是2D UI，所以z为0
        rect.anchoredPosition3D = new(left, -top, 0f);
        return rect;
    }

    public static RectTransform NormalizeRectWithMidLeft(Component cmp, float left, float top,
        Transform parent = null, float? height = null) {
        RectTransform rect = NormalizeRectWithTopLeft(cmp, left, top, parent);
        float actualHeight = height ?? rect.sizeDelta.y;
        rect.anchoredPosition3D = new(left, -top + actualHeight / 2, 0f);
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

    public static void SetPosition(this Text text, float x, float y) {
        NormalizeRectWithMidLeft(text, x, y);
    }

    public static (float, float) GetPosition(int index, int count, float totalPx = 640f) {
        //假定组件之间的间隔为20px，整行宽度为640px
        float targetLen = (totalPx - (count - 1) * 20) / count;
        float targetPx = index * (targetLen + 20);
        return (targetPx, targetLen);
    }

    public static void SetText(this UIButton btn, string notTranslateStr) {
        try {
            var l = btn.gameObject.transform.Find("button-text").GetComponent<Localizer>();
            var t = btn.gameObject.transform.Find("button-text").GetComponent<Text>();
            if (l != null) {
                l.stringKey = notTranslateStr;
                l.translation = notTranslateStr.Translate();
            }
            if (t != null) {
                t.text = notTranslateStr.Translate();
            }
        }
        catch (Exception e) {
            LogError($"SetText error: {e}");
        }
    }
}
