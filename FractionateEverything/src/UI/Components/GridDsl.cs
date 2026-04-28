using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.Components;

public static class GridDsl {
    public static LayoutTrack Px(float value) {
        return LayoutTrack.Px(value);
    }

    public static LayoutTrack Fr(float value) {
        return LayoutTrack.Fr(value);
    }

    public static LayoutInsets Inset(float all) {
        return new(all);
    }

    public static LayoutInsets Inset(float horizontal, float vertical) {
        return new(horizontal, vertical);
    }

    public static LayoutInsets Inset(float left, float top, float right, float bottom) {
        return new(left, top, right, bottom);
    }

    public static LayoutGrid Grid((int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, IReadOnlyList<LayoutTrack> rows = null,
        IReadOnlyList<LayoutTrack> cols = null, LayoutInsets? margin = null, LayoutInsets? padding = null,
        float rowGap = 0f, float columnGap = 0f, string objectName = "layout-grid",
        IReadOnlyList<LayoutNode> children = null) {
        return new() {
            Pos = pos,
            Span = span,
            Row = row,
            Col = col,
            RowSpan = rowSpan,
            ColSpan = colSpan,
            Rows = rows ?? Array.Empty<LayoutTrack>(),
            Cols = cols ?? Array.Empty<LayoutTrack>(),
            Margin = margin ?? LayoutInsets.Zero,
            Padding = padding ?? LayoutInsets.Zero,
            RowGap = rowGap,
            ColumnGap = columnGap,
            ObjectName = objectName,
            Children = children ?? Array.Empty<LayoutNode>(),
        };
    }

    public static LayoutLeaf Node((int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, LayoutInsets? margin = null, LayoutInsets? padding = null,
        string objectName = "layout-node", Action<MyWindow, RectTransform> build = null) {
        return new() {
            Pos = pos,
            Span = span,
            Row = row,
            Col = col,
            RowSpan = rowSpan,
            ColSpan = colSpan,
            Margin = margin ?? LayoutInsets.Zero,
            Padding = padding ?? LayoutInsets.Zero,
            ObjectName = objectName,
            BuildAction = build,
        };
    }

    public static LayoutGrid ContentCard((int, int)? pos = null, (int, int)? span = null, int? row = null,
        int? col = null, int? rowSpan = null, int? colSpan = null, bool strong = false, string objectName = "content-card",
        IReadOnlyList<LayoutTrack> rows = null, IReadOnlyList<LayoutTrack> cols = null, LayoutInsets? margin = null,
        LayoutInsets? padding = null, float rowGap = 0f, float columnGap = 0f,
        IReadOnlyList<LayoutNode> children = null) {
        // 默认 10px 周边内边距；强调卡额外在左侧让出橙色条 (橙色条宽 + 9px)，子节点不会与橙色条重叠。
        LayoutInsets userPadding = padding ?? new LayoutInsets(PageLayout.CardInnerPadding);
        LayoutInsets actualPadding = strong
            ? new LayoutInsets(userPadding.Left + PageLayout.StrongAccentWidth + 9f, userPadding.Top, userPadding.Right,
                userPadding.Bottom)
            : userPadding;
        return new() {
            Pos = pos,
            Span = span,
            Row = row,
            Col = col,
            RowSpan = rowSpan,
            ColSpan = colSpan,
            Rows = rows ?? Array.Empty<LayoutTrack>(),
            Cols = cols ?? Array.Empty<LayoutTrack>(),
            Margin = margin ?? LayoutInsets.Zero,
            Padding = actualPadding,
            RowGap = rowGap,
            ColumnGap = columnGap,
            ObjectName = objectName,
            Children = children ?? Array.Empty<LayoutNode>(),
            RootFactory = (parent, rect) => PageLayout.CreateContentCard(parent, objectName, rect.Left, rect.Top, rect.Width,
                rect.Height, strong),
        };
    }

    /// <summary>
    /// 一段填满给定单元的文本节点。默认左中对齐、不折行、白色。
    /// </summary>
    public static LayoutLeaf TextNode(string text, int fontSize = PageLayout.BodyFontSize, Color? color = null,
        TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false, Action<Text> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null, int? rowSpan = null,
        int? colSpan = null, LayoutInsets? margin = null, string objectName = "text-node") {
        return new() {
            Pos = pos,
            Span = span,
            Row = row,
            Col = col,
            RowSpan = rowSpan,
            ColSpan = colSpan,
            Margin = margin ?? LayoutInsets.Zero,
            ObjectName = objectName,
            BuildAction = (wnd, parent) => {
                Text built = PageLayout.AddCenteredText(parent, text, fontSize,
                    color ?? White, anchor, 0f, 0f, parent.sizeDelta.x, parent.sizeDelta.y,
                    objectName, wrap);
                onBuilt?.Invoke(built);
            },
        };
    }

    /// <summary>
    /// 可滚动的 ContentCard。外层卡片物理高度来自 Grid 轨道，内部逻辑高度由
    /// <paramref name="contentHeight"/> 决定；大于外层时自动启用垂直滚动。
    /// </summary>
    public static LayoutGrid ScrollableContentCard(float contentHeight, (int, int)? pos = null,
        (int, int)? span = null, int? row = null, int? col = null, int? rowSpan = null, int? colSpan = null,
        bool strong = false, string objectName = "scroll-card", IReadOnlyList<LayoutTrack> rows = null,
        IReadOnlyList<LayoutTrack> cols = null, LayoutInsets? margin = null, LayoutInsets? padding = null,
        float rowGap = 0f, float columnGap = 0f, IReadOnlyList<LayoutNode> children = null) {
        // 与 ContentCard 保持一致：默认 10px 周边内边距，strong 卡额外让出橙色条 + 9px 间距。
        LayoutInsets userPadding = padding ?? new LayoutInsets(PageLayout.CardInnerPadding);
        LayoutInsets actualPadding = strong
            ? new LayoutInsets(userPadding.Left + PageLayout.StrongAccentWidth + 9f, userPadding.Top, userPadding.Right,
                userPadding.Bottom)
            : userPadding;
        return new() {
            Pos = pos,
            Span = span,
            Row = row,
            Col = col,
            RowSpan = rowSpan,
            ColSpan = colSpan,
            Rows = rows ?? Array.Empty<LayoutTrack>(),
            Cols = cols ?? Array.Empty<LayoutTrack>(),
            Margin = margin ?? LayoutInsets.Zero,
            Padding = actualPadding,
            RowGap = rowGap,
            ColumnGap = columnGap,
            ObjectName = objectName,
            Children = children ?? Array.Empty<LayoutNode>(),
            ContentHeight = contentHeight,
            RootFactory = (parent, rect) => PageLayout.CreateScrollableContentCard(parent, objectName, rect.Left,
                rect.Top, rect.Width, rect.Height, contentHeight, strong),
        };
    }

    public static LayoutGrid FooterCard((int, int)? pos = null, (int, int)? span = null, int? row = null,
        int? col = null, int? rowSpan = null, int? colSpan = null, string objectName = "footer-card",
        IReadOnlyList<LayoutTrack> rows = null, IReadOnlyList<LayoutTrack> cols = null, LayoutInsets? margin = null,
        LayoutInsets? padding = null, float rowGap = 0f, float columnGap = 0f,
        IReadOnlyList<LayoutNode> children = null) {
        // 与 ContentCard 保持一致：默认 10px 周边内边距。
        LayoutInsets actualPadding = padding ?? new LayoutInsets(PageLayout.CardInnerPadding);
        return new() {
            Pos = pos,
            Span = span,
            Row = row,
            Col = col,
            RowSpan = rowSpan,
            ColSpan = colSpan,
            Rows = rows ?? Array.Empty<LayoutTrack>(),
            Cols = cols ?? Array.Empty<LayoutTrack>(),
            Margin = margin ?? LayoutInsets.Zero,
            Padding = actualPadding,
            RowGap = rowGap,
            ColumnGap = columnGap,
            ObjectName = objectName,
            Children = children ?? Array.Empty<LayoutNode>(),
            RootFactory = (parent, rect) => PageLayout.CreateFooterCard(parent, objectName, rect.Top),
        };
    }

    public static LayoutLeaf Header(string title, string summary = "", string objectName = "page-header",
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null, int? rowSpan = null,
        int? colSpan = null, Action<PageLayout.HeaderRefs> onBuilt = null) {
        return new() {
            Pos = pos,
            Span = span,
            Row = row,
            Col = col,
            RowSpan = rowSpan,
            ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, parent) => {
                PageLayout.HeaderRefs header = PageLayout.CreatePageHeader(wnd, parent, title, summary, objectName);
                onBuilt?.Invoke(header);
            },
        };
    }

    public static void BuildLayout(MyWindow wnd, RectTransform parent, LayoutGrid root) {
        GridLayoutRuntime.BuildRoot(wnd, parent, root);
    }
}
