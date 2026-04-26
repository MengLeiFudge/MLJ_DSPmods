using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.Components;

/// <summary>
/// 统一页面卡片骨架，负责把各页面收敛为"圆角 + 填充 + 描边 + 标题带"的一致版式。
/// </summary>
public static class PageLayout {
    public const float DesignWidth = 1082f;
    public const float DesignHeight = 767f;
    public const float HeaderHeight = 86f;
    public const float FooterHeight = 56f;
    public const float Gap = 16f;
    public const float InnerGap = 14f;

    /// <summary>字号四档：页标题 / 卡标题 / 正文 / 辅助说明。</summary>
    public const int PageTitleFontSize = 20;
    public const int CardTitleFontSize = 15;
    public const int BodyFontSize = 13;
    public const int HintFontSize = 12;

    /// <summary>卡内默认内边距 (左,上,右,下)。</summary>
    public const float CardPaddingLeft = 18f;
    public const float CardPaddingTop = 14f;
    public const float CardPaddingRight = 18f;
    public const float CardPaddingBottom = 18f;

    /// <summary>卡顶部标题带高度，CardHeader 约定用这一高度。</summary>
    public const float CardHeaderHeight = 28f;

    /// <summary>强调卡左侧橙色条宽度。</summary>
    public const float StrongAccentWidth = 3f;

    public static readonly Color TransparentColor = new(0f, 0f, 0f, 0f);

    /// <summary>卡片默认填充：深灰半透明，让卡片从黑色背景浮起。</summary>
    public static readonly Color CardFillColor = new(30f / 255f, 35f / 255f, 40f / 255f, 0.4f);

    /// <summary>Header 填充：稍亮一点，强化页顶存在感。</summary>
    public static readonly Color HeaderFillColor = new(36f / 255f, 42f / 255f, 48f / 255f, 0.45f);

    /// <summary>Footer 填充：与普通卡一致。</summary>
    public static readonly Color FooterFillColor = CardFillColor;

    public static readonly Color HeaderBorderColor = new(0.40f, 0.73f, 1f, 0.58f);
    public static readonly Color CardBorderColor = new(1f, 1f, 1f, 0.18f);
    public static readonly Color CardBorderColorStrong = new(0.72f, 0.86f, 1f, 0.32f);
    public static readonly Color FooterBorderColor = new(1f, 1f, 1f, 0.20f);

    /// <summary>强调卡左侧竖条颜色：与页面主题橙一致。</summary>
    public static readonly Color StrongAccentColor = new(1f, 0.65f, 0.18f, 0.95f);

    /// <summary>卡标题下方分隔线颜色。</summary>
    public static readonly Color CardHeaderSeparatorColor = new(1f, 1f, 1f, 0.14f);

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
        RectTransform header = CreateCard(parent, objectName, 0f, 0f, DesignWidth, HeaderHeight, HeaderFillColor,
            HeaderBorderColor);

        Text titleText = MyWindow.AddText(20f, 15f, header, title, PageTitleFontSize, $"{objectName}-title");
        titleText.supportRichText = true;
        titleText.color = Orange;

        Text summaryText = MyWindow.AddText(20f, 49f, header, summary, BodyFontSize, $"{objectName}-summary");
        summaryText.supportRichText = true;
        summaryText.color = White;
        summaryText.rectTransform.sizeDelta = new Vector2(DesignWidth - 40f, 26f);

        return new(header, titleText, summaryText);
    }

    /// <summary>
    /// 构造一张卡：圆角填充 + 1 px 描边。可选强调态（右上起一条橙色侧条）。
    /// </summary>
    public static RectTransform CreateCard(RectTransform parent, string objectName, float left, float top, float width,
        float height, Color fillColor, Color borderColor, bool strong = false) {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);

        Image fillImage = obj.GetComponent<Image>();
        fillImage.sprite = RoundedSpriteFactory.GetFillSprite();
        fillImage.type = Image.Type.Sliced;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        AddBorderOverlay(rect, borderColor);
        if (strong) {
            AddStrongAccent(rect);
        }

        return rect;
    }

    private static void AddBorderOverlay(RectTransform parent, Color borderColor) {
        var obj = new GameObject("border", typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = obj.GetComponent<Image>();
        image.sprite = RoundedSpriteFactory.GetBorderSprite();
        image.type = Image.Type.Sliced;
        image.color = borderColor;
        image.raycastTarget = false;
    }

    private static void AddStrongAccent(RectTransform parent) {
        var obj = new GameObject("accent-strip", typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, 0f, 6f, parent);
        rect.sizeDelta = new Vector2(StrongAccentWidth, parent.sizeDelta.y - 12f);

        Image image = obj.GetComponent<Image>();
        image.sprite = RoundedSpriteFactory.GetFillSprite();
        image.type = Image.Type.Sliced;
        image.color = StrongAccentColor;
        image.raycastTarget = false;
    }

    /// <summary>
    /// 约定卡内顶部 28 px 为标题带：橙色卡标题 + 底部 1 px 分隔线。
    /// </summary>
    public static Text AddCardHeader(MyWindow wnd, RectTransform parent, string title, string objectName = "card-header",
        float left = CardPaddingLeft, float right = CardPaddingRight) {
        Text text = MyWindow.AddText(left, 4f, parent, title, CardTitleFontSize, $"{objectName}-title");
        text.supportRichText = true;
        text.color = Orange;

        var separatorObj = new GameObject($"{objectName}-separator", typeof(RectTransform), typeof(Image));
        RectTransform sep = separatorObj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(sep, left, CardHeaderHeight - 1f, parent);
        sep.sizeDelta = new Vector2(parent.sizeDelta.x - left - right, 1f);

        Image sepImg = separatorObj.GetComponent<Image>();
        sepImg.color = CardHeaderSeparatorColor;
        sepImg.raycastTarget = false;

        return text;
    }

    public static Text AddCardTitle(MyWindow wnd, RectTransform parent, float left, float top, string title,
        int fontSize = CardTitleFontSize, string objectName = "card-title") {
        Text text = MyWindow.AddText(left, top, parent, title, fontSize, objectName);
        text.supportRichText = true;
        text.color = Orange;
        return text;
    }

    public static Text AddCardSummary(MyWindow wnd, RectTransform parent, float left, float top, string textValue = "",
        float width = 360f, int fontSize = BodyFontSize, string objectName = "card-summary") {
        Text text = MyWindow.AddText(left, top, parent, textValue, fontSize, objectName);
        text.supportRichText = true;
        text.color = White;
        text.rectTransform.sizeDelta = new Vector2(width, 24f);
        return text;
    }

    public static RectTransform CreateContentCard(RectTransform parent, string objectName, float left, float top,
        float width, float height, bool strong = false) {
        return CreateCard(parent, objectName, left, top, width, height, CardFillColor,
            strong ? CardBorderColorStrong : CardBorderColor, strong);
    }

    public static RectTransform CreateFooterCard(RectTransform parent, string objectName, float top) {
        return CreateCard(parent, objectName, 0f, top, DesignWidth, FooterHeight, FooterFillColor, FooterBorderColor);
    }
}
