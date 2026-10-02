using System;
using System.Collections.Generic;
using FE.UI.Foundation;
using FE.UI.Foundation.Window;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Theme;

/// <summary>卡片浏览页的一项显示数据；操作由页面显式提供，不保存业务状态。</summary>
public sealed class PageCard {
    /// <summary>用于保持选择态的内部键，不直接显示。</summary>
    public string Key;
    /// <summary>卡片标题。</summary>
    public string Title;
    /// <summary>状态标签。</summary>
    public string Status;
    /// <summary>详情正文。</summary>
    public string Detail;
    /// <summary>物品图标身份，0使用未知图标。</summary>
    public int ItemId;
    /// <summary>0～1进度，负数表示不显示。</summary>
    public float Progress = -1;
    /// <summary>详情中的输入、输出物品图标。</summary>
    public int[] DetailItems = [];
    /// <summary>明确操作的按钮文案。</summary>
    public string ActionLabel;
    /// <summary>点击时调用领域命令或页面路由。</summary>
    public Action Action;
}

/// <summary>共享窄目录、分页与宽正文的页面骨架；每个业务页只提供展示数据。</summary>
public abstract class CardBrowserPage {
    private const int PageSize = 8;
    private const float ListWidth = 270f;
    private const float DetailWidth = 796f;
    private readonly List<(RectTransform Root, Text Title, Text Status, Image Icon, Image Progress, Button Button)> slots = [];
    private readonly List<Image> detailIcons = [];
    private RectTransform root;
    private RectTransform detailRoot;
    private Text detailText;
    private Text summaryText;
    private Text pagerText;
    private Text actionText;
    private Button actionButton;
    private IReadOnlyList<PageCard> cards = Array.Empty<PageCard>();
    private string selectedKey;
    /// <summary>已排版的条目身份，仅在选择改变时重置阅读位置。</summary>
    private string renderedKey;
    private int pageIndex;
    private float nextRefresh;

    /// <summary>页面标题。</summary>
    protected abstract string Title { get; }
    /// <summary>页头的一句叙事提示。</summary>
    protected abstract string Summary { get; }
    /// <summary>读取本页数据；业务状态仍归逻辑域。</summary>
    /// <returns>本页卡片列表。</returns>
    protected abstract IReadOnlyList<PageCard> ReadCards();

    /// <summary>按统一1082×767画布创建可点击卡片与滚动详情。</summary>
    /// <param name="window">当前外壳。</param>
    /// <param name="parent">页面父节点。</param>
    public void CreateUI(MyWindow window, RectTransform parent) {
        slots.Clear(); detailIcons.Clear();
        root = PageLayout.CreateCard(parent, "card-browser", 0, 0, 1082, 767,
            PageLayout.TransparentColor, PageLayout.TransparentColor);
        var header = PageLayout.CreatePageHeader(window, root, Title, Summary);
        summaryText = header.Summary;
        RectTransform list = PageLayout.CreateContentCard(root, "cards", 0, 88, ListWidth, 679);
        RectTransform detail = PageLayout.CreateContentCard(root, "detail", ListWidth + 16, 88, DetailWidth, 679);
        for (int i = 0; i < PageSize; i++) {
            int slot = i;
            RectTransform card = PageLayout.CreateContentCard(list, $"entry-{i}", 10,
                10 + i * 75, ListWidth - 20, 69);
            Image icon = CreateIcon(card, 8, 12, 36);
            Text title = PageLayout.AddCenteredText(card, "", 14, White, TextAnchor.MiddleLeft, 52, 4, 188, 34, "title", true);
            Text status = PageLayout.AddCenteredText(card, "", 12, White, TextAnchor.MiddleLeft, 52, 37, 188, 24, "status", true);
            Image progress = PageLayout.CreateProgressBar(card, "progress", 8, 63, ListWidth - 36, 3);
            Button button = card.gameObject.AddComponent<Button>();
            card.GetComponent<Image>().raycastTarget = true;
            button.targetGraphic = card.GetComponent<Image>();
            button.onClick.AddListener(() => {
                int index = pageIndex * PageSize + slot;
                if (index >= cards.Count) { return; }
                selectedKey = cards[index].Key;
                Render();
            });
            slots.Add((card, title, status, icon, progress, button));
        }
        AddButton(list, "上一页", 10, 622, 80, () => { pageIndex = Math.Max(0, pageIndex - 1); Render(); });
        AddButton(list, "下一页", 180, 622, 80, () => { pageIndex = Math.Min(Math.Max(0, (cards.Count - 1) / PageSize), pageIndex + 1); Render(); });
        pagerText = PageLayout.AddCenteredText(list, "", 13, White, TextAnchor.MiddleCenter, 90, 622, 90, 36, "page-count");
        for (int i = 0; i < 10; i++) { detailIcons.Add(CreateIcon(detail, 22 + i * 46, 20, 40)); }
        detailRoot = PageLayout.CreateScrollableArea(detail, "description-scroll", 22, 22, DetailWidth - 44, 635, 635);
        detailText = PageLayout.AddCenteredText(detailRoot, "", 16, White, TextAnchor.UpperLeft, 0, 0, DetailWidth - 57, 635, "description", true);
        // 默认 UI 字体材质支持 RectMask2D，建筑状态文字的专用材质不保证执行视口裁剪。
        detailText.material = null;
        detailText.maskable = true;
        detailText.raycastTarget = false;
        detailText.resizeTextForBestFit = false;
        actionButton = AddButton(detail, "", 22, 622, DetailWidth - 44, () => {
            foreach (PageCard card in cards) {
                if (card.Key == selectedKey) { card.Action?.Invoke(); break; }
            }
            nextRefresh = 0;
        });
        actionText = actionButton.GetComponentInChildren<Text>();
        nextRefresh = 0;
        UpdateUI();
    }

    /// <summary>仅刷新可见页，每半秒读取一次领域快照。</summary>
    public void UpdateUI() {
        if (root == null || !root.gameObject.activeInHierarchy || Time.unscaledTime < nextRefresh) { return; }
        nextRefresh = Time.unscaledTime + 0.5f;
        cards = ReadCards();
        summaryText.text = Summary;
        bool found = false;
        foreach (PageCard card in cards) { if (card.Key == selectedKey) { found = true; break; } }
        if (!found) { selectedKey = cards.Count > 0 ? cards[0].Key : null; }
        pageIndex = Math.Min(pageIndex, Math.Max(0, (cards.Count - 1) / PageSize));
        Render();
    }

    /// <summary>重用现有卡片对象绘制列表及选中详情。</summary>
    private void Render() {
        PageCard selected = null;
        foreach (PageCard card in cards) { if (card.Key == selectedKey) { selected = card; break; } }
        for (int i = 0; i < slots.Count; i++) {
            var slot = slots[i];
            int index = pageIndex * PageSize + i;
            slot.Root.gameObject.SetActive(index < cards.Count);
            if (index >= cards.Count) { continue; }
            PageCard card = cards[index];
            slot.Title.text = card.Title;
            slot.Status.text = card.Status;
            slot.Icon.sprite = LDB.items.Select(card.ItemId)?.iconSprite ?? LDB.items.Select(IFE残片)?.iconSprite;
            slot.Progress.transform.parent.gameObject.SetActive(card.Progress >= 0);
            PageLayout.SetProgress(slot.Progress, card.Progress);
            PageLayout.SetCardState(slot.Root, card.Key == selectedKey ? CardVisualState.Selected : CardVisualState.Normal);
        }
        pagerText.text = $"{pageIndex + 1} / {Math.Max(1, (cards.Count + PageSize - 1) / PageSize)}";
        detailText.text = selected == null ? "暂无记录。" : $"<b>{selected.Title}</b>\n\n{selected.Detail}";
        bool hasIcons = selected?.DetailItems.Length > 0;
        bool hasAction = selected?.Action != null;
        RectTransform scrollRoot = detailRoot.parent.parent as RectTransform;
        float top = hasIcons ? 78f : 22f;
        scrollRoot.anchoredPosition = new Vector2(22f, -top);
        scrollRoot.sizeDelta = new Vector2(DetailWidth - 44, (hasAction ? 604f : 657f) - top);
        PageLayout.ResizeScrollableText(detailRoot, detailText);
        if (renderedKey != selectedKey) {
            PageLayout.ScrollToTop(detailRoot);
            renderedKey = selectedKey;
        }
        for (int i = 0; i < detailIcons.Count; i++) {
            bool visible = selected != null && i < selected.DetailItems.Length;
            detailIcons[i].gameObject.SetActive(visible);
            if (visible) { detailIcons[i].sprite = LDB.items.Select(selected.DetailItems[i])?.iconSprite; }
        }
        actionButton.gameObject.SetActive(selected?.Action != null);
        actionText.text = selected?.ActionLabel ?? "";
    }

    /// <summary>创建不参与点击拦截的物品图标。</summary>
    /// <param name="parent">父节点。</param>
    /// <param name="x">横向位置。</param>
    /// <param name="y">纵向位置。</param>
    /// <param name="size">方形边长。</param>
    /// <returns>图标组件。</returns>
    private static Image CreateIcon(RectTransform parent, float x, float y, float size) {
        var obj = new GameObject("item-icon", typeof(RectTransform), typeof(Image));
        var rect = obj.GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(rect, x, y, parent);
        rect.sizeDelta = new Vector2(size, size);
        var image = obj.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }

    /// <summary>创建统一外观的明确操作按钮。</summary>
    /// <param name="parent">父节点。</param>
    /// <param name="label">按钮标题。</param>
    /// <param name="x">横向位置。</param>
    /// <param name="y">纵向位置。</param>
    /// <param name="width">按钮宽度。</param>
    /// <param name="action">点击动作。</param>
    /// <returns>按钮组件。</returns>
    private static Button AddButton(RectTransform parent, string label, float x, float y, float width, Action action) {
        RectTransform rect = PageLayout.CreateContentCard(parent, "action", x, y, width, 36);
        Image image = rect.GetComponent<Image>(); image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener(() => action());
        PageLayout.AddCenteredText(rect, label, 14, White, TextAnchor.MiddleCenter, 0, 0, width, 36, "label").raycastTarget = false;
        return button;
    }
}
