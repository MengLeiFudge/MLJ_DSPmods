using System;
using System.Collections.Generic;
using FE.UI.Foundation.Window;
using UnityEngine;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.Layout;

/// <summary>
/// 声明式网格布局的 RectTransform 构建与定位运行时。
/// </summary>
internal static class GridLayoutRuntime {
    /// <summary>
    /// 在 <paramref name="parent"/> 的左上坐标系中创建固定尺寸容器，并按给定布局矩形设置位置与大小。
    /// </summary>
    /// <param name="objectName">新建 GameObject 的名称。</param>
    /// <param name="parent">容器的直接父 RectTransform。</param>
    /// <param name="rect">相对父节点左上角的布局矩形。</param>
    /// <returns>已挂到父节点并完成定位的 RectTransform。</returns>
    public static RectTransform CreateContainerRect(string objectName, RectTransform parent, LayoutRect rect) {
        var obj = new GameObject(objectName, typeof(RectTransform));
        RectTransform trans = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(trans, rect.Left, rect.Top, parent);
        trans.sizeDelta = new(rect.Width, rect.Height);
        return trans;
    }

    /// <summary>
    /// 以父节点当前矩形构建根网格；布局尚未刷新而宽高非正时回退到 <c>sizeDelta</c>。
    /// </summary>
    /// <param name="wnd">用于创建根网格子节点内控件的主窗口。</param>
    /// <param name="parent">根网格直接使用的父 RectTransform。</param>
    /// <param name="root">提供轨道、间距与子节点的根网格定义。</param>
    public static void BuildRoot(MyWindow wnd, RectTransform parent, LayoutGrid root) {
        LayoutRect rootRect = new(0f, 0f, parent.rect.width, parent.rect.height);
        if (rootRect.Width <= 0f || rootRect.Height <= 0f) {
            rootRect = new(0f, 0f, parent.sizeDelta.x, parent.sizeDelta.y);
        }

        BuildChildren(wnd, parent, rootRect, root);
    }

    /// <summary>
    /// 按 <paramref name="grid"/> 的行列轨道与间距，把其子节点排布进 <paramref name="rect"/>。
    /// </summary>
    /// <param name="wnd">用于创建子节点内控件的主窗口。</param>
    /// <param name="parent">子节点的直接父 RectTransform。</param>
    /// <param name="rect">本次排布的可用矩形（已扣除外层 margin/padding）。</param>
    /// <param name="grid">提供行/列轨道、行列间距与子节点列表的网格定义。</param>
    public static void BuildChildren(MyWindow wnd, RectTransform parent, LayoutRect rect, LayoutGrid grid) {
        // 入口一次性捕获，等价于原 8 参数调用的调用时取值：child.Build 委托理论上可改写同一 grid，捕获后循环内不再访问 grid.*。
        IReadOnlyList<LayoutTrack> rows = grid.Rows;
        IReadOnlyList<LayoutTrack> cols = grid.Cols;
        float rowGap = grid.RowGap;
        float columnGap = grid.ColumnGap;
        IReadOnlyList<LayoutNode> children = grid.Children;

        IReadOnlyList<LayoutTrack> effectiveRows = rows.Count > 0 ? rows : [1];
        IReadOnlyList<LayoutTrack> effectiveCols = cols.Count > 0 ? cols : [1];
        float[] rowHeights = ResolveTracks(rect.Height, effectiveRows, rowGap);
        float[] colWidths = ResolveTracks(rect.Width, effectiveCols, columnGap);

        for (int i = 0; i < children.Count; i++) {
            LayoutNode child = children[i];
            LayoutPlacement placement = child.ResolvePlacement();
            ValidatePlacement(placement, effectiveRows.Count, effectiveCols.Count, child.ObjectName);
            LayoutRect childRect = ResolveChildRect(rect, placement, rowHeights, colWidths, rowGap, columnGap);
            child.Build(wnd, parent, childRect);
        }
    }

    /// <summary>
    /// 把像素轨道与比例轨道解析为实际像素长度；先扣除非负间距和固定像素，剩余空间按 fr 权重分配。
    /// </summary>
    /// <param name="totalSize">可用总长度，单位为像素。</param>
    /// <param name="tracks">按布局顺序排列的像素/fr 轨道。</param>
    /// <param name="gap">相邻轨道间距，负值按 0 处理。</param>
    /// <returns>与 <paramref name="tracks"/> 等长的像素长度数组；fr 总权重为 0 时比例轨道长度为 0。</returns>
    private static float[] ResolveTracks(float totalSize, IReadOnlyList<LayoutTrack> tracks, float gap) {
        int count = tracks.Count;
        var resolved = new float[count];
        float pxTotal = 0f;
        float frTotal = 0f;
        for (int i = 0; i < count; i++) {
            if (tracks[i].Kind == LayoutTrackKind.Px) {
                pxTotal += tracks[i].Value;
            } else {
                frTotal += tracks[i].Value;
            }
        }

        float gapTotal = Math.Max(0f, count - 1) * Math.Max(0f, gap);
        float remaining = Math.Max(0f, totalSize - pxTotal - gapTotal);
        for (int i = 0; i < count; i++) {
            LayoutTrack track = tracks[i];
            resolved[i] = track.Kind switch {
                LayoutTrackKind.Px => track.Value,
                LayoutTrackKind.Fr when frTotal > 0f => remaining * track.Value / frTotal,
                _ => 0f,
            };
        }

        return resolved;
    }

    /// <summary>
    /// 在父矩形左上坐标系中累计轨道长度与间距，计算一个已校验 placement 跨越的子矩形。
    /// </summary>
    /// <param name="parentRect">当前网格的可用父矩形。</param>
    /// <param name="placement">已经过范围校验的行列起点与跨度。</param>
    /// <param name="rowHeights">各行实际像素高度。</param>
    /// <param name="colWidths">各列实际像素宽度。</param>
    /// <param name="rowGap">相邻行间距，单位为像素。</param>
    /// <param name="columnGap">相邻列间距，单位为像素。</param>
    /// <returns>相对父节点左上角定位、包含完整行列跨度的子矩形。</returns>
    private static LayoutRect ResolveChildRect(LayoutRect parentRect, LayoutPlacement placement, float[] rowHeights,
        float[] colWidths, float rowGap, float columnGap) {
        float left = parentRect.Left;
        for (int i = 0; i < placement.Col; i++) {
            left += colWidths[i] + columnGap;
        }

        float top = parentRect.Top;
        for (int i = 0; i < placement.Row; i++) {
            top += rowHeights[i] + rowGap;
        }

        float width = 0f;
        for (int i = 0; i < placement.ColSpan; i++) {
            width += colWidths[placement.Col + i];
        }
        width += Math.Max(0, placement.ColSpan - 1) * columnGap;

        float height = 0f;
        for (int i = 0; i < placement.RowSpan; i++) {
            height += rowHeights[placement.Row + i];
        }
        height += Math.Max(0, placement.RowSpan - 1) * rowGap;
        return new(left, top, width, height);
    }

    /// <summary>
    /// 校验 placement 起点非负且行列跨度不越过当前网格边界。
    /// </summary>
    /// <param name="placement">待校验的行列起点与跨度。</param>
    /// <param name="rowCount">当前网格行数。</param>
    /// <param name="colCount">当前网格列数。</param>
    /// <param name="objectName">写入越界异常消息的布局节点名。</param>
    /// <exception cref="InvalidOperationException">行列起点为负或任一跨度越界。</exception>
    private static void ValidatePlacement(LayoutPlacement placement, int rowCount, int colCount, string objectName) {
        if (placement.Row < 0 || placement.Col < 0) {
            throw new InvalidOperationException($"{objectName} 的 row/col 不能为负数。");
        }

        if (placement.Row + placement.RowSpan > rowCount) {
            throw new InvalidOperationException($"{objectName} 的 rowSpan 超出 Grid 行范围。");
        }

        if (placement.Col + placement.ColSpan > colCount) {
            throw new InvalidOperationException($"{objectName} 的 colSpan 超出 Grid 列范围。");
        }
    }
}
