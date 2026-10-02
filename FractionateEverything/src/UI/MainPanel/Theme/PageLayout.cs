using FE.UI.Foundation.Window;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel.Theme;

/// <summary>
/// 统一页面卡片骨架，负责把各页面收敛为"圆角 + 填充 + 描边 + 标题带"的一致版式。
/// </summary>
public static class PageLayout {
    public const float DesignWidth = 1082f;
    public const float DesignHeight = 767f;
    public const float HeaderHeight = 72f;
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

    /// <summary>卡内统一边距：DSL 底层 ContentCard / ScrollableContentCard / FooterCard / Header 默认内边距。</summary>
    public const float CardInnerPadding = 10f;

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

    /// <summary>选中态：比普通卡更亮的蓝白色描边，配合 hover/激活态使用。</summary>
    public static readonly Color CardBorderColorSelected = new(1f, 0.78f, 0.32f, 0.72f);

    /// <summary>鼠标悬停态：普通卡基础上亮度抬升的描边。</summary>
    public static readonly Color CardBorderColorHover = new(1f, 1f, 1f, 0.36f);

    /// <summary>空态提示文本颜色：比正文再低一档的灰白，避免抢视觉。</summary>
    public static readonly Color EmptyStateTextColor = new(1f, 1f, 1f, 0.45f);

    /// <summary>进度条底色。</summary>
    public static readonly Color ProgressTrackColor = new(1f, 1f, 1f, 0.10f);

    /// <summary>进度条主填充色。</summary>
    public static readonly Color ProgressFillColor = new(0.32f, 0.76f, 0.92f, 0.92f);

    /// <summary>滚动条轨道与滑块颜色。</summary>
    public static readonly Color ScrollbarTrackColor = new(1f, 1f, 1f, 0.08f);
    public static readonly Color ScrollbarHandleColor = new(0.72f, 0.82f, 0.88f, 0.62f);

    /// <summary>
    /// 主面板页头根节点与标题文本引用。
    /// </summary>
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
        string objectName = "page-header", float width = DesignWidth, float height = HeaderHeight) {
        RectTransform header = CreateCard(parent, objectName, 0f, 0f, width, height, HeaderFillColor,
            HeaderBorderColor);

        // 使用 1:1 fr 行比例：上下等高，上方放橙色页标题（垂直居中），下方放白色说明（垂直居中）。
        const float padX = 22f;
        float innerWidth = width - padX * 2f;
        float titleHeight = height * 0.5f;
        float summaryHeight = height - titleHeight;

        Text titleText = AddCenteredText(header, title, PageTitleFontSize, Orange, TextAnchor.MiddleLeft,
            padX, 0f, innerWidth, titleHeight, $"{objectName}-title", wrap: false);
        Text summaryText = AddCenteredText(header, summary, BodyFontSize, White, TextAnchor.MiddleLeft,
            padX, titleHeight, innerWidth, summaryHeight, $"{objectName}-summary", wrap: true);

        return new(header, titleText, summaryText);
    }

    /// <summary>
    /// 创建一段填充给定矩形并按 anchor 对齐的 Text。用于在 fr 网格单元里放一段居中文字的统一入口。
    /// </summary>
    public static Text AddCenteredText(RectTransform parent, string label, int fontSize, Color color,
        TextAnchor anchor, float left, float top, float width, float height, string objectName, bool wrap = false) {
        Text text = MyWindow.AddText(0f, 0f, parent, label, fontSize, objectName);
        text.supportRichText = true;
        text.color = color;
        text.alignment = anchor;
        if (wrap) {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
        RectTransform rt = text.rectTransform;
        NormalizeRectWithTopLeft(rt, left, top, parent);
        rt.sizeDelta = new Vector2(width, height);
        return text;
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
    public static Text AddCardHeader(MyWindow wnd, RectTransform parent, string title,
        string objectName = "card-header",
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

    /// <summary>
    /// 添加一个居中的"暂无内容"空态提示。与常规正文文本共用卡片父节点。
    /// </summary>
    public static Text AddEmptyStateHint(MyWindow wnd, RectTransform parent, float left, float top, float width,
        float height, string textValue = "暂无内容", string objectName = "empty-state-hint") {
        Text text = MyWindow.AddText(left, top, parent, textValue, BodyFontSize, objectName);
        text.supportRichText = true;
        text.color = EmptyStateTextColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.rectTransform.sizeDelta = new Vector2(width, height);
        return text;
    }

    /// <summary>
    /// 切换卡片 border 颜色以反映选中/悬停/普通三态。目标是调用者在事件回调里统一用此方法，
    /// 避免在各页面散写 Image.color =。
    /// </summary>
    public static void SetCardState(RectTransform card, CardVisualState state) {
        if (card == null) {
            return;
        }

        Transform border = card.Find("border");
        if (border == null) {
            return;
        }

        if (!border.TryGetComponent(out Image image)) {
            return;
        }

        image.color = state switch {
            CardVisualState.Selected => CardBorderColorSelected,
            CardVisualState.Hover => CardBorderColorHover,
            CardVisualState.Strong => CardBorderColorStrong,
            _ => CardBorderColor,
        };
    }

    /// <summary>
    /// 创建一条可动态更新的横向进度条，并返回填充图像。
    /// </summary>
    public static Image CreateProgressBar(RectTransform parent, string objectName, float left, float top,
        float width, float height, Color? fillColor = null) {
        var trackObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(trackRect, left, top, parent);
        trackRect.sizeDelta = new Vector2(width, height);

        Image trackImage = trackObject.GetComponent<Image>();
        trackImage.sprite = RoundedSpriteFactory.GetFillSprite();
        trackImage.type = Image.Type.Sliced;
        trackImage.color = ProgressTrackColor;
        trackImage.raycastTarget = false;

        var fillObject = new GameObject($"{objectName}-fill", typeof(RectTransform), typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(trackRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.localScale = Vector3.one;

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.sprite = RoundedSpriteFactory.GetFillSprite();
        fillImage.type = Image.Type.Sliced;
        fillImage.color = fillColor ?? ProgressFillColor;
        fillImage.raycastTarget = false;
        return fillImage;
    }

    /// <summary>
    /// 更新进度条填充比例。
    /// </summary>
    public static void SetProgress(Image fillImage, float progress) {
        if (fillImage == null) {
            return;
        }
        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 构造一张可滚动的圆角卡片。需要在现有卡片内部滚动时应使用
    /// <see cref="CreateScrollableArea"/>，避免重复卡片边框。
    /// </summary>
    public static RectTransform CreateScrollableContentCard(RectTransform parent, string objectName, float left,
        float top, float width, float height, float contentHeight, bool strong = false) {
        RectTransform card = CreateContentCard(parent, objectName, left, top, width, height, strong);
        return CreateScrollableArea(card, $"{objectName}-scroll-area", 2f, 2f,
            width - 4f, height - 4f, contentHeight);
    }

    /// <summary>
    /// 创建带裁剪、鼠标滚轮和自动隐藏右侧滚动条的无边框滚动区域。
    /// </summary>
    public static RectTransform CreateScrollableArea(RectTransform parent, string objectName, float left,
        float top, float width, float height, float contentHeight) {
        const float scrollbarWidth = 8f;
        const float scrollbarGap = 5f;

        var rootObject = new GameObject(objectName, typeof(RectTransform), typeof(ScrollRect));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(root, left, top, parent);
        root.sizeDelta = new Vector2(width, height);
        rootObject.AddComponent<UIScrollZone>();

        var viewportObject = new GameObject($"{objectName}-viewport",
            typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(root, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = new Vector2(-(scrollbarWidth + scrollbarGap), 0f);
        viewport.localScale = Vector3.one;

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = TransparentColor;
        viewportImage.raycastTarget = true;

        var contentObject = new GameObject($"{objectName}-content", typeof(RectTransform));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, height));
        content.localScale = Vector3.one;

        Scrollbar scrollbar = CreateVerticalScrollbar(root, $"{objectName}-scrollbar", scrollbarWidth);
        ScrollRect scroll = rootObject.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing = scrollbarGap;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.verticalNormalizedPosition = 1f;
        return content;
    }

    /// <summary>
    /// 按文本实际首选高度扩展滚动内容，同时保持内容至少覆盖视口。
    /// </summary>
    public static void ResizeScrollableText(RectTransform contentRoot, Text text, float verticalPadding = 20f) {
        if (contentRoot == null || text == null || contentRoot.parent is not RectTransform viewport) {
            return;
        }

        float height = Mathf.Max(viewport.rect.height, text.preferredHeight + verticalPadding);
        contentRoot.sizeDelta = new Vector2(contentRoot.sizeDelta.x, height);
        text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, height - verticalPadding);
        if (text.rectTransform.parent is RectTransform textContainer && textContainer != contentRoot) {
            textContainer.sizeDelta = new Vector2(textContainer.sizeDelta.x, height - verticalPadding);
        }
    }

    /// <summary>
    /// 将滚动区域恢复到顶部。
    /// </summary>
    public static void ScrollToTop(RectTransform contentRoot) {
        ScrollRect scroll = contentRoot?.GetComponentInParent<ScrollRect>();
        if (scroll != null) {
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    private static Scrollbar CreateVerticalScrollbar(RectTransform parent, string objectName, float width) {
        var scrollbarObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.SetParent(parent, false);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.sizeDelta = new Vector2(width, 0f);
        scrollbarRect.localScale = Vector3.one;

        Image trackImage = scrollbarObject.GetComponent<Image>();
        trackImage.sprite = RoundedSpriteFactory.GetFillSprite();
        trackImage.type = Image.Type.Sliced;
        trackImage.color = ScrollbarTrackColor;

        var slidingObject = new GameObject("sliding-area", typeof(RectTransform));
        RectTransform slidingRect = slidingObject.GetComponent<RectTransform>();
        slidingRect.SetParent(scrollbarRect, false);
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = Vector2.one;
        slidingRect.offsetMax = -Vector2.one;
        slidingRect.localScale = Vector3.one;

        var handleObject = new GameObject("handle", typeof(RectTransform), typeof(Image));
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.SetParent(slidingRect, false);
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        handleRect.localScale = Vector3.one;

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.sprite = RoundedSpriteFactory.GetFillSprite();
        handleImage.type = Image.Type.Sliced;
        handleImage.color = ScrollbarHandleColor;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }
}

/// <summary>
/// 页面卡片视觉状态。
/// </summary>
public enum CardVisualState {
    Normal,
    Hover,
    Selected,
    Strong,
}
