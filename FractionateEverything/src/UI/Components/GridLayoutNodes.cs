using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.Components;

public abstract class LayoutNode {
    public (int row, int col)? Pos { get; set; }
    public (int rows, int cols)? Span { get; set; }
    public int? Row { get; set; }
    public int? Col { get; set; }
    public int? RowSpan { get; set; }
    public int? ColSpan { get; set; }
    public LayoutInsets Margin { get; set; } = LayoutInsets.Zero;
    public string ObjectName { get; set; } = "layout-node";

    internal LayoutPlacement ResolvePlacement(bool allowDefault = false) {
        bool useCompactSyntax = Pos != null || Span != null;
        bool useExplicitSyntax = Row != null || Col != null || RowSpan != null || ColSpan != null;
        if (useCompactSyntax && useExplicitSyntax) {
            throw new InvalidOperationException($"{ObjectName} 同时使用了 pos/span 与 row/col/rowSpan/colSpan。");
        }

        if (useCompactSyntax) {
            (int row, int col) pos = Pos ?? throw new InvalidOperationException($"{ObjectName} 缺少 pos。");
            (int rows, int cols) span = Span ?? (1, 1);
            return new(pos.row, pos.col, Math.Max(1, span.rows), Math.Max(1, span.cols));
        }

        if (useExplicitSyntax) {
            int row = Row ?? throw new InvalidOperationException($"{ObjectName} 缺少 row。");
            int col = Col ?? throw new InvalidOperationException($"{ObjectName} 缺少 col。");
            return new(row, col, Math.Max(1, RowSpan ?? 1), Math.Max(1, ColSpan ?? 1));
        }

        if (allowDefault) {
            return new(0, 0, 1, 1);
        }

        throw new InvalidOperationException($"{ObjectName} 缺少布局定位信息。");
    }

    internal abstract RectTransform Build(MyWindow wnd, RectTransform parent, LayoutRect rect);
}

public class LayoutGrid : LayoutNode {
    public IReadOnlyList<LayoutTrack> Rows { get; set; } = Array.Empty<LayoutTrack>();
    public IReadOnlyList<LayoutTrack> Cols { get; set; } = Array.Empty<LayoutTrack>();
    public LayoutInsets Padding { get; set; } = LayoutInsets.Zero;
    public float RowGap { get; set; }
    public float ColumnGap { get; set; }
    public IReadOnlyList<LayoutNode> Children { get; set; } = Array.Empty<LayoutNode>();

    /// <summary>
    /// 可选：子节点排布使用的逻辑高度。为 0 时与外层卡片物理高度一致；
    /// 大于物理高度时，需要 <see cref="RootFactory"/> 返回一个受裁剪的内部 Content RectTransform，
    /// 从而把溢出部分放入滚动视口内（见 ScrollableContentCard）。
    /// </summary>
    public float ContentHeight { get; set; }

    internal Func<RectTransform, LayoutRect, RectTransform> RootFactory { get; set; }

    internal override RectTransform Build(MyWindow wnd, RectTransform parent, LayoutRect rect) {
        LayoutRect outerRect = rect.Inset(Margin);
        float logicalHeight = ContentHeight > 0f ? ContentHeight : outerRect.Height;
        LayoutRect factoryRect = new(outerRect.Left, outerRect.Top, outerRect.Width, outerRect.Height);
        RectTransform root = RootFactory != null
            ? RootFactory(parent, factoryRect)
            : GridLayoutRuntime.CreateContainerRect(ObjectName, parent, outerRect);
        LayoutRect contentRect = new LayoutRect(0f, 0f, outerRect.Width, logicalHeight).Inset(Padding);
        GridLayoutRuntime.BuildChildren(wnd, root, contentRect, Rows, Cols, RowGap, ColumnGap, Children);
        return root;
    }
}

public sealed class LayoutLeaf : LayoutNode {
    public LayoutInsets Padding { get; set; } = LayoutInsets.Zero;
    public Action<MyWindow, RectTransform> BuildAction { get; set; }
    internal Func<RectTransform, LayoutRect, RectTransform> RootFactory { get; set; }

    internal override RectTransform Build(MyWindow wnd, RectTransform parent, LayoutRect rect) {
        LayoutRect outerRect = rect.Inset(Margin);
        RectTransform root = RootFactory != null
            ? RootFactory(parent, outerRect)
            : GridLayoutRuntime.CreateContainerRect(ObjectName, parent, outerRect);
        RectTransform contentRoot = Padding.Equals(LayoutInsets.Zero)
            ? root
            : GridLayoutRuntime.CreateContainerRect($"{ObjectName}-content", root,
                new LayoutRect(0f, 0f, outerRect.Width, outerRect.Height).Inset(Padding));
        BuildAction?.Invoke(wnd, contentRoot);
        return root;
    }
}
