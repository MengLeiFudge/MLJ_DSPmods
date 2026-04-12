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

    public static readonly Color HeaderColor = new(0.08f, 0.11f, 0.16f, 0.92f);
    public static readonly Color CardColor = new(0f, 0f, 0f, 0.34f);
    public static readonly Color CardColorStrong = new(0f, 0f, 0f, 0.48f);
    public static readonly Color CardColorSoft = new(0.06f, 0.08f, 0.12f, 0.72f);
    public static readonly Color AccentStripColor = new(0.38f, 0.73f, 1f, 0.22f);

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
        RectTransform header = CreateCard(parent, objectName, 0f, 0f, DesignWidth, HeaderHeight, HeaderColor);
        CreateAccentStrip(header, "page-header-accent");

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
        float height, Color color) {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);

        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

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
        return CreateCard(parent, objectName, left, top, width, height, strong ? CardColorStrong : CardColor);
    }

    public static RectTransform CreateFooterCard(RectTransform parent, string objectName, float top) {
        return CreateCard(parent, objectName, 0f, top, DesignWidth, FooterHeight, CardColorSoft);
    }

    private static void CreateAccentStrip(RectTransform parent, string objectName) {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, 0f, 0f, parent);
        rect.sizeDelta = new Vector2(parent.rect.width, 4f);

        Image image = obj.GetComponent<Image>();
        image.color = AccentStripColor;
        image.raycastTarget = false;
    }
}
