using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Events;
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
        IReadOnlyList<LayoutNode> children = null, Action<RectTransform> onBuilt = null) {
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
            OnBuilt = onBuilt,
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
        int? col = null, int? rowSpan = null, int? colSpan = null, bool strong = false,
        string objectName = "content-card",
        IReadOnlyList<LayoutTrack> rows = null, IReadOnlyList<LayoutTrack> cols = null, LayoutInsets? margin = null,
        LayoutInsets? padding = null, float rowGap = 0f, float columnGap = 0f,
        IReadOnlyList<LayoutNode> children = null, Action<RectTransform> onBuilt = null) {
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
            OnBuilt = onBuilt,
            RootFactory = (parent, rect) => PageLayout.CreateContentCard(parent, objectName, rect.Left, rect.Top,
                rect.Width,
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
        float rowGap = 0f, float columnGap = 0f, IReadOnlyList<LayoutNode> children = null,
        Action<RectTransform> onBuilt = null) {
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
            OnBuilt = onBuilt,
            RootFactory = (parent, rect) => PageLayout.CreateScrollableContentCard(parent, objectName, rect.Left,
                rect.Top, rect.Width, rect.Height, contentHeight, strong),
        };
    }

    public static LayoutGrid FooterCard((int, int)? pos = null, (int, int)? span = null, int? row = null,
        int? col = null, int? rowSpan = null, int? colSpan = null, string objectName = "footer-card",
        IReadOnlyList<LayoutTrack> rows = null, IReadOnlyList<LayoutTrack> cols = null, LayoutInsets? margin = null,
        LayoutInsets? padding = null, float rowGap = 0f, float columnGap = 0f,
        IReadOnlyList<LayoutNode> children = null, Action<RectTransform> onBuilt = null) {
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
            OnBuilt = onBuilt,
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

    // ==================== 控件节点 ====================

    /// <summary>
    /// 卡片标题节点：橙色标题文本，垂直居中、水平靠左。
    /// </summary>
    public static LayoutLeaf CardTitleNode(string title, int fontSize = PageLayout.CardTitleFontSize,
        Action<Text> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "card-title-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                Text text = PageLayout.AddCenteredText(root, title, fontSize,
                    Orange, TextAnchor.MiddleLeft, 0f, 0f, root.sizeDelta.x, root.sizeDelta.y,
                    objectName);
                onBuilt?.Invoke(text);
            },
        };
    }

    /// <summary>
    /// 按钮节点：填满格子宽度，垂直居中。
    /// </summary>
    public static LayoutLeaf ButtonNode(string text, UnityAction onClick = null,
        int fontSize = PageLayout.BodyFontSize, Action<UIButton> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "button-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float w = root.sizeDelta.x;
                float h = root.sizeDelta.y;
                UIButton btn = wnd.AddButton(0f, h / 2f, w, root, text, fontSize, objectName, onClick);
                onBuilt?.Invoke(btn);
            },
        };
    }

    /// <summary>
    /// 标签 + 下拉框节点：左侧标签，右侧 ComboBox 填满剩余空间，整行垂直居中。
    /// </summary>
    public static LayoutLeaf LabeledComboBoxNode(string label, string[] items, ConfigEntry<int> config,
        int fontSize = PageLayout.BodyFontSize, Action<MyComboBox> onBuilt = null,
        string tipTitle = null, string tipContent = null, Action<UIButton> onTipBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "labeled-combo-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellW = root.sizeDelta.x;
                float cellH = root.sizeDelta.y;
                float cy = cellH / 2f;
                const float gap = 10f;
                Text txt = wnd.AddText2(0f, cy, root, label, fontSize);
                float labelW = txt.preferredWidth + 8f;
                float comboW = cellW - labelW - gap;
                if (tipTitle != null) comboW -= 30f;
                var combo = wnd.AddComboBox(labelW + gap, cy, root, fontSize)
                    .WithItems(items).WithSize(comboW, 0).WithConfigEntry(config);
                onBuilt?.Invoke(combo);
                if (tipTitle != null) {
                    var tip = wnd.AddTipsButton2(cellW - 24f, cy, root, tipTitle, tipContent ?? "");
                    onTipBuilt?.Invoke(tip);
                }
            },
        };
    }

    public static LayoutLeaf LabeledComboBoxNode(string label, string[] items, int selectedIndex,
        Action<int> onSelChanged, int fontSize = PageLayout.BodyFontSize, Action<MyComboBox> onBuilt = null,
        string tipTitle = null, string tipContent = null, Action<UIButton> onTipBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "labeled-combo-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellW = root.sizeDelta.x;
                float cellH = root.sizeDelta.y;
                float cy = cellH / 2f;
                const float gap = 10f;
                Text txt = wnd.AddText2(0f, cy, root, label, fontSize);
                float labelW = txt.preferredWidth + 8f;
                float comboW = cellW - labelW - gap;
                if (tipTitle != null) comboW -= 30f;
                var combo = wnd.AddComboBox(labelW + gap, cy, root, fontSize)
                    .WithItems(items).WithSize(comboW, 0).WithIndex(selectedIndex);
                if (onSelChanged != null) {
                    combo.WithOnSelChanged(onSelChanged);
                }
                onBuilt?.Invoke(combo);
                if (tipTitle != null) {
                    var tip = wnd.AddTipsButton2(cellW - 24f, cy, root, tipTitle, tipContent ?? "");
                    onTipBuilt?.Invoke(tip);
                }
            },
        };
    }

    public static LayoutLeaf ComboBoxNode(int fontSize = PageLayout.BodyFontSize, Action<MyComboBox> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "combo-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellW = root.sizeDelta.x;
                float cellH = root.sizeDelta.y;
                var combo = wnd.AddComboBox(0f, cellH / 2f, root, fontSize).WithSize(cellW, 0f);
                onBuilt?.Invoke(combo);
            },
        };
    }

    public static LayoutLeaf SliderNode<T>(ConfigEntry<T> config, MyWindow.ValueMapper<T> mapper,
        string format = "G", float width = 0f, Action<MySlider> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "slider-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellW = root.sizeDelta.x;
                float cellH = root.sizeDelta.y;
                var slider = wnd.AddSlider(0f, cellH / 2f, root, config, mapper, format,
                    width > 0f ? Math.Min(width, cellW) : cellW);
                onBuilt?.Invoke(slider);
            },
        };
    }

    public static LayoutLeaf SliderNode<T>(ConfigEntry<T> config, T[] valueList,
        string format = "G", float width = 0f, Action<MySlider> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "slider-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellW = root.sizeDelta.x;
                float cellH = root.sizeDelta.y;
                var slider = wnd.AddSlider(0f, cellH / 2f, root, config, valueList, format,
                    width > 0f ? Math.Min(width, cellW) : cellW);
                onBuilt?.Invoke(slider);
            },
        };
    }

    /// <summary>
    /// 标签 + 滑条节点：左侧标签，右侧 Slider 填满剩余空间，整行垂直居中。
    /// 可选 TipsButton 在最右侧。
    /// </summary>
    public static LayoutLeaf LabeledSliderNode<T>(string label, ConfigEntry<T> config,
        MyWindow.ValueMapper<T> mapper, string format = "G",
        int fontSize = PageLayout.BodyFontSize,
        string tipTitle = null, string tipContent = null,
        Action<MySlider> onSliderBuilt = null, Action<UIButton> onTipBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "labeled-slider-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellW = root.sizeDelta.x;
                float cellH = root.sizeDelta.y;
                float cy = cellH / 2f;
                const float gap = 10f;
                Text txt = wnd.AddText2(0f, cy, root, label, fontSize);
                float labelW = txt.preferredWidth + 8f;
                float sliderW = cellW - labelW - gap;
                if (tipTitle != null) sliderW -= 30f;
                var slider = wnd.AddSlider(labelW + gap, cy, root, config, mapper, format, sliderW);
                onSliderBuilt?.Invoke(slider);
                if (tipTitle != null) {
                    var tip = wnd.AddTipsButton2(cellW - 24f, cy, root, tipTitle, tipContent ?? "");
                    onTipBuilt?.Invoke(tip);
                }
            },
        };
    }

    /// <summary>
    /// 复选框节点：CheckBox 垂直居中、水平靠左。可选 TipsButton 在右侧。
    /// </summary>
    public static LayoutLeaf CheckBoxNode(ConfigEntry<bool> config, string label,
        int fontSize = PageLayout.BodyFontSize,
        string tipTitle = null, string tipContent = null,
        Action<MyCheckBox> onBuilt = null, Action<UIButton> onTipBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "checkbox-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellH = root.sizeDelta.y;
                float cy = cellH / 2f;
                var cb = wnd.AddCheckBox(0f, cy, root, config, label, fontSize);
                onBuilt?.Invoke(cb);
                if (tipTitle != null) {
                    var tip = wnd.AddTipsButton2(cb.Width + 10f, cy, root, tipTitle, tipContent ?? "");
                    onTipBuilt?.Invoke(tip);
                }
            },
        };
    }

    public static LayoutLeaf CheckBoxNode(bool initialValue, string label,
        int fontSize = PageLayout.BodyFontSize,
        Action<MyCheckBox> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "checkbox-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellH = root.sizeDelta.y;
                float cy = cellH / 2f;
                var cb = MyCheckBox.CreateCheckBox(0f, cy, root, initialValue, label, fontSize);
                onBuilt?.Invoke(cb);
            },
        };
    }

    /// <summary>
    /// 图标按钮节点：垂直居中、水平靠左。
    /// </summary>
    public static LayoutLeaf ImageButtonNode(Proto proto = null, float size = 40f,
        Action<MyImageButton> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "image-button-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellW = root.sizeDelta.x;
                float cellH = root.sizeDelta.y;
                float cy = cellH / 2f;
                float x = Math.Max(0f, (cellW - size) / 2f);
                var btn = wnd.AddImageButton(x, cy, root, proto, objectName).WithSize(size, size);
                onBuilt?.Invoke(btn);
            },
        };
    }

    public static LayoutLeaf TipsButtonNode(string tipTitle, string tipContent, Action<UIButton> onBuilt = null,
        (int, int)? pos = null, (int, int)? span = null, int? row = null, int? col = null,
        int? rowSpan = null, int? colSpan = null, string objectName = "tips-button-node") {
        return new() {
            Pos = pos, Span = span, Row = row, Col = col, RowSpan = rowSpan, ColSpan = colSpan,
            ObjectName = objectName,
            BuildAction = (wnd, root) => {
                float cellH = root.sizeDelta.y;
                float cy = cellH / 2f;
                var tip = wnd.AddTipsButton2(0f, cy, root, tipTitle, tipContent);
                onBuilt?.Invoke(tip);
            },
        };
    }

    public static void BuildLayout(MyWindow wnd, RectTransform parent, LayoutGrid root) {
        GridLayoutRuntime.BuildRoot(wnd, parent, root);
    }
}
