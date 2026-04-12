using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.Components;

/// <summary>
/// 统一页面卡片骨架，目标是让各页面先形成稳定版式，再填具体业务控件。
/// </summary>
public static class PageLayout {
    public const float DesignWidth = 1082f;
    public const float DesignHeight = 767f;
    public const float HeaderHeight = 86f;
    public const float FooterHeight = 56f;
    public const float Gap = 16f;
    public const float InnerGap = 14f;
    private const float BorderInset = 2f;
    private const float BorderThickness = 2f;

    public static readonly Color TransparentColor = new(0f, 0f, 0f, 0f);
    public static readonly Color HeaderBorderColor = new(0.40f, 0.73f, 1f, 0.58f);
    public static readonly Color CardBorderColor = new(1f, 1f, 1f, 0.18f);
    public static readonly Color CardBorderColorStrong = new(0.72f, 0.86f, 1f, 0.28f);
    public static readonly Color FooterBorderColor = new(1f, 1f, 1f, 0.16f);

    public readonly struct HeaderRefs {
        public readonly RectTransform Root;
        public readonly Text Title;
        public readonly Text Summary;

        public HeaderRefs(RectTransform root, Text title, Text summary) {
            Root = root;
            Title = title;
            Summary = summary;
        }
    }

    public static HeaderRefs CreatePageHeader(MyWindow wnd, RectTransform parent, string title, string summary = "",
        string objectName = "page-header") {
        RectTransform header = CreateCard(parent, objectName, 0f, 0f, DesignWidth, HeaderHeight, TransparentColor,
            HeaderBorderColor);

        Text titleText = MyWindow.AddText(20f, 15f, header, title, 20, $"{objectName}-title");
        titleText.supportRichText = true;
        titleText.color = Orange;

        Text summaryText = MyWindow.AddText(20f, 49f, header, summary, 13, $"{objectName}-summary");
        summaryText.supportRichText = true;
        summaryText.color = White;
        summaryText.rectTransform.sizeDelta = new Vector2(DesignWidth - 40f, 26f);

        return new(header, titleText, summaryText);
    }

    public static RectTransform CreateCard(RectTransform parent, string objectName, float left, float top, float width,
        float height, Color fillColor, Color borderColor) {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, left + BorderInset, top + BorderInset, parent);
        rect.sizeDelta = new Vector2(width - BorderInset * 2f, height - BorderInset * 2f);

        Image image = obj.GetComponent<Image>();
        image.color = fillColor;
        image.raycastTarget = false;

        CreateBorderLines(rect, borderColor);
        return rect;
    }

    public static Text AddCardTitle(MyWindow wnd, RectTransform parent, float left, float top, string title,
        int fontSize = 15, string objectName = "card-title") {
        Text text = MyWindow.AddText(left, top, parent, title, fontSize, objectName);
        text.supportRichText = true;
        text.color = Orange;
        return text;
    }

    public static Text AddCardSummary(MyWindow wnd, RectTransform parent, float left, float top, string textValue = "",
        float width = 360f, int fontSize = 13, string objectName = "card-summary") {
        Text text = MyWindow.AddText(left, top, parent, textValue, fontSize, objectName);
        text.supportRichText = true;
        text.color = White;
        text.rectTransform.sizeDelta = new Vector2(width, 24f);
        return text;
    }

    public static RectTransform CreateContentCard(RectTransform parent, string objectName, float left, float top,
        float width, float height, bool strong = false) {
        return CreateCard(parent, objectName, left, top, width, height, TransparentColor,
            strong ? CardBorderColorStrong : CardBorderColor);
    }

    public static RectTransform CreateFooterCard(RectTransform parent, string objectName, float top) {
        return CreateCard(parent, objectName, 0f, top, DesignWidth, FooterHeight, TransparentColor, FooterBorderColor);
    }

    private static void CreateBorderLines(RectTransform parent, Color borderColor) {
        CreateBorderLine(parent, "border-top", 0f, 0f, parent.sizeDelta.x, BorderThickness, borderColor);
        CreateBorderLine(parent, "border-bottom", 0f, parent.sizeDelta.y - BorderThickness, parent.sizeDelta.x,
            BorderThickness, borderColor);
        CreateBorderLine(parent, "border-left", 0f, 0f, BorderThickness, parent.sizeDelta.y, borderColor);
        CreateBorderLine(parent, "border-right", parent.sizeDelta.x - BorderThickness, 0f, BorderThickness,
            parent.sizeDelta.y, borderColor);
    }

    private static void CreateBorderLine(RectTransform parent, string objectName, float left, float top, float width,
        float height, Color color) {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);

        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
