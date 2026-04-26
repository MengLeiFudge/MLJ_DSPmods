using System;
using System.Collections.Generic;
using UnityEngine;

namespace FE.UI.Components;

public static class GridDsl {
    public static LayoutTrack Px(float value) {
        return LayoutTrack.Px(value);
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
            RootFactory = (parent, rect) => PageLayout.CreateContentCard(parent, objectName, rect.Left, rect.Top, rect.Width,
                rect.Height, strong),
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
