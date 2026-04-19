using System;

namespace FE.UI.Components;

public enum LayoutTrackKind {
    Fr,
    Px,
}

public readonly struct LayoutTrack {
    public LayoutTrackKind Kind { get; }
    public float Value { get; }

    public LayoutTrack(LayoutTrackKind kind, float value) {
        Kind = kind;
        Value = value;
    }

    public static LayoutTrack Fr(float value) {
        return new(LayoutTrackKind.Fr, Math.Max(0f, value));
    }

    public static LayoutTrack Px(float value) {
        return new(LayoutTrackKind.Px, Math.Max(0f, value));
    }

    public static implicit operator LayoutTrack(int value) {
        return Fr(value);
    }

    public static implicit operator LayoutTrack(float value) {
        return Fr(value);
    }
}

public readonly struct LayoutInsets {
    public float Left { get; }
    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }

    public LayoutInsets(float all) : this(all, all, all, all) { }

    public LayoutInsets(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    public LayoutInsets(float left, float top, float right, float bottom) {
        Left = Math.Max(0f, left);
        Top = Math.Max(0f, top);
        Right = Math.Max(0f, right);
        Bottom = Math.Max(0f, bottom);
    }

    public static readonly LayoutInsets Zero = new(0f);
}

public readonly struct LayoutRect {
    public float Left { get; }
    public float Top { get; }
    public float Width { get; }
    public float Height { get; }

    public LayoutRect(float left, float top, float width, float height) {
        Left = left;
        Top = top;
        Width = Math.Max(0f, width);
        Height = Math.Max(0f, height);
    }

    public LayoutRect Inset(LayoutInsets insets) {
        float width = Math.Max(0f, Width - insets.Left - insets.Right);
        float height = Math.Max(0f, Height - insets.Top - insets.Bottom);
        return new(Left + insets.Left, Top + insets.Top, width, height);
    }
}

internal readonly struct LayoutPlacement {
    public int Row { get; }
    public int Col { get; }
    public int RowSpan { get; }
    public int ColSpan { get; }

    public LayoutPlacement(int row, int col, int rowSpan, int colSpan) {
        Row = row;
        Col = col;
        RowSpan = rowSpan;
        ColSpan = colSpan;
    }
}
