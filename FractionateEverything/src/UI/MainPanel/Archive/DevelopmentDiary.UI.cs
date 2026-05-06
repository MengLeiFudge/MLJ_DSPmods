using System.Linq;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Archive;

public static partial class DevelopmentDiary {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static MyComboBox categoryCombo;
    private static MyComboBox fragmentCombo;
    private static UIButton btnPrevFragment;
    private static UIButton btnNextFragment;
    private static Text txtDiaryContent;
    private static Text txtNavigatorTitle;
    private static Text txtContentTitle;

    private static void RefreshSelectors() {
        if (categoryCombo == null || fragmentCombo == null) {
            return;
        }

        ClampSelection();
        DiaryFragment[] fragments = GetCurrentFragments();

        suppressSelectionCallbacks = true;
        try {
            categoryCombo.SetItems(diaryCategories.Select(static category => category.LabelKey).ToArray());
            categoryCombo.SetIndex(currentCategoryIndex);
            fragmentCombo.SetItems(fragments.Select(GetFragmentDisplayLabel).ToArray());
            fragmentCombo.SetIndex(currentFragmentIndex);
        }
        finally {
            suppressSelectionCallbacks = false;
        }

        UpdateNavigationButtons(fragments.Length);
    }

    private static void UpdateNavigationButtons(int fragmentCount) {
        if (btnPrevFragment == null || btnNextFragment == null) {
            return;
        }

        btnPrevFragment.button.interactable = fragmentCount > 0 && currentFragmentIndex > 0;
        btnNextFragment.button.interactable = fragmentCount > 0 && currentFragmentIndex < fragmentCount - 1;
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                cols: [1, 3],
                columnGap: PageLayout.Gap,
                children: [
                    Header("开发日记", objectName: "development-diary-header", pos: (0, 0), span: (1, 2),
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "development-diary-nav-card", strong: true,
                        rows: [Px(28f), Px(44f), Px(44f), 1],
                        rowGap: PageLayout.InnerGap,
                        children: [
                            CardTitleNode("分类与片段", onBuilt: text => txtNavigatorTitle = text,
                                pos: (0, 0), objectName: "development-diary-nav-title"),
                            ComboBoxNode(onBuilt: combo => categoryCombo = combo.WithOnSelChanged(index => {
                                    if (suppressSelectionCallbacks) {
                                        return;
                                    }
                                    currentCategoryIndex = Mathf.Clamp(index, 0, diaryCategories.Length - 1);
                                    currentFragmentIndex = 0;
                                    RefreshEntry();
                                }),
                                pos: (1, 0), objectName: "development-diary-category-combo"),
                            ComboBoxNode(onBuilt: combo => fragmentCombo = combo.WithOnSelChanged(index => {
                                    if (suppressSelectionCallbacks) {
                                        return;
                                    }
                                    int fragmentCount = GetCurrentFragments().Length;
                                    currentFragmentIndex = fragmentCount == 0
                                        ? 0
                                        : Mathf.Clamp(index, 0, fragmentCount - 1);
                                    RefreshEntry();
                                }),
                                pos: (2, 0), objectName: "development-diary-fragment-combo"),
                        ]),
                    ContentCard(pos: (1, 1), objectName: "development-diary-content-outer",
                        rows: [Px(28f), 1],
                        rowGap: PageLayout.InnerGap,
                        children: [
                            CardTitleNode("正文阅读", onBuilt: text => txtContentTitle = text,
                                pos: (0, 0), objectName: "development-diary-content-title"),
                            ScrollableContentCard(
                                contentHeight: 1400f,
                                pos: (1, 0),
                                objectName: "development-diary-content-scroll",
                                children: [
                                    TextNode(string.Empty, PageLayout.BodyFontSize, anchor: TextAnchor.UpperLeft,
                                        wrap: true, onBuilt: text => txtDiaryContent = text,
                                        pos: (0, 0), objectName: "txtDiaryContent"),
                                ]),
                        ]),
                    FooterCard(pos: (2, 0), span: (1, 2), objectName: "development-diary-footer-card",
                        cols: [1, 1, 4],
                        columnGap: PageLayout.InnerGap,
                        children: [
                            ButtonNode("向前", onClick: PrevFragment, onBuilt: btn => btnPrevFragment = btn,
                                pos: (0, 0), objectName: "development-diary-prev"),
                            ButtonNode("向后", onClick: NextFragment, onBuilt: btn => btnNextFragment = btn,
                                pos: (0, 1), objectName: "development-diary-next"),
                        ]),
                ]));
        RefreshEntry();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        RefreshEntry();
    }

    private static void RefreshEntry() {
        if (txtDiaryContent == null) {
            return;
        }

        DiaryFragment[] fragments = GetCurrentFragments();
        RefreshSelectors();

        if (fragments.Length == 0) {
            txtDiaryContent.text = "暂无可浏览的片段".Translate().WithColor(White);
            txtDiaryContent.alignment = TextAnchor.MiddleCenter;
            txtDiaryContent.color = PageLayout.EmptyStateTextColor;
            return;
        }

        txtDiaryContent.alignment = TextAnchor.UpperLeft;
        txtDiaryContent.color = White;

        DiaryFragment fragment = fragments[currentFragmentIndex];
        string content = fragment.ContentKey.Translate();
        header.Title.text = "开发日记".Translate().WithColor(Orange);
        header.Summary.text = $"{fragments.Length} 个片段可浏览    已解锁 {unlockedFragmentIds.Count}/{diaryFragments.Length}"
            .WithColor(White);
        txtNavigatorTitle.text = "分类与片段".Translate().WithColor(Orange);
        txtContentTitle.text = GetFragmentDisplayLabel(fragment).WithColor(Orange);
        txtDiaryContent.text = IsUnlocked(fragment) ? content : BuildLockedContent(content);
    }

    private static void PrevFragment() {
        if (currentFragmentIndex <= 0) {
            return;
        }

        currentFragmentIndex--;
        RefreshEntry();
    }

    private static void NextFragment() {
        DiaryFragment[] fragments = GetCurrentFragments();
        if (currentFragmentIndex >= fragments.Length - 1) {
            return;
        }

        currentFragmentIndex++;
        RefreshEntry();
    }
}
