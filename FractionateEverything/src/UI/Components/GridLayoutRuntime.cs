using System;
using System.Collections.Generic;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.Components;

internal static class GridLayoutRuntime {
    public static RectTransform CreateContainerRect(string objectName, RectTransform parent, LayoutRect rect) {
        var obj = new GameObject(objectName, typeof(RectTransform));
        RectTransform trans = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(trans, rect.Left, rect.Top, parent);
        trans.sizeDelta = new(rect.Width, rect.Height);
        return trans;
    }

    public static void BuildRoot(MyWindow wnd, RectTransform parent, LayoutGrid root) {
        LayoutRect rootRect = new(0f, 0f, parent.rect.width, parent.rect.height);
        if (rootRect.Width <= 0f || rootRect.Height <= 0f) {
            rootRect = new(0f, 0f, parent.sizeDelta.x, parent.sizeDelta.y);
        }

        BuildChildren(wnd, parent, rootRect, root.Rows, root.Cols, root.RowGap, root.ColumnGap, root.Children);
    }

    public static void BuildChildren(MyWindow wnd, RectTransform parent, LayoutRect rect,
        IReadOnlyList<LayoutTrack> rows,
        IReadOnlyList<LayoutTrack> cols, float rowGap, float columnGap, IReadOnlyList<LayoutNode> children) {
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
