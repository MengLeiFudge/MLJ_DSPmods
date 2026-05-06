using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.NormalizeRectUtils;
using static FE.Utils.Utils;

namespace FE.UI.Components;

public class MyWindowWithTabs : MyWindow {
    private sealed class TabState {
        public RectTransform Content;
        public UIButton Button;
        public string Label;
    }

    private sealed class TabGroupState {
        public string Label;
        public int StartTabIndex;
        public UIButton HeaderButton;
        public bool Collapsed;
        public int LastSelectedTabIndex = -1;
    }

    private readonly List<TabState> _tabs = [];
    private readonly List<TabGroupState> _tabGroups = [];
    private RectTransform _tabParent;
    private float _tabY = 66f;
    private int _currentTabIndex = -1;

    public override void TryClose() {
        _Close();
    }

    public override bool IsWindowFunctional() {
        return true;
    }

    private RectTransform AddTabInternal(float y, int index, RectTransform parent, string label) {
        var tab = new GameObject();
        var tabRect = tab.AddComponent<RectTransform>();
        NormalizeRectWithMargin(tabRect, TitleHeight, Margin + TabWidth + Spacing, 0f, 0f, parent);
        tab.name = "tab-" + index;
        var swarmPanel = UIRoot.instance.uiGame.dysonEditor.controlPanel.hierarchy.swarmPanel;
        var src = swarmPanel.orbitButtons[0];
        var btn = Instantiate(src);
        var btnRect = NormalizeRectWithMidLeft(btn, Margin, y, parent);
        btn.name = "tab-btn-" + index;
        btnRect.sizeDelta = new(TabWidth, TabHeight);
        btn.transform.Find("frame").gameObject.SetActive(false);
        if (btn.transitions.Length >= 3) {
            btn.transitions[0].normalColor = new(0.1f, 0.1f, 0.1f, 0.68f);
            btn.transitions[0].highlightColorOverride = new(0.9906f, 0.5897f, 0.3691f, 0.4f);
            btn.transitions[1].normalColor = new(1f, 1f, 1f, 0.6f);
            btn.transitions[1].highlightColorOverride = new(0.2f, 0.1f, 0.1f, 0.9f);
        }

        var btnText = btn.transform.Find("Text").GetComponent<Text>();
        btnText.text = label.Translate();
        btnText.fontSize = 16;
        // var srcText = UIRoot.instance.uiGame.assemblerWindow.stateText;
        // btnText.font = srcText.font;
        // btnText.fontStyle = srcText.fontStyle;
        btn.data = index;

        _tabs.Add(new() {
            Content = tabRect,
            Button = btn,
            Label = label
        });
        btn.onClick += OnTabButtonClick;

        MaxY = Math.Max(MaxY, y + TabHeight);
        return tabRect;
    }

    public RectTransform AddTab(RectTransform parent, string label) {
        _tabParent = parent;
        var result = AddTabInternal(_tabY, _tabs.Count, parent, label);
        _tabY += 28f;
        if (_tabGroups.Count > 0) {
            RefreshTabLayout();
        }
        return result;
    }

    public void AddSplitter(RectTransform parent, float spacing) {
        var img = Instantiate(UIRoot.instance.optionWindow.transform.Find("tab-line").Find("bar"));
        Destroy(img.Find("tri").gameObject);
        _tabY += spacing;
        var rect = NormalizeRectWithMidLeft(img, 28, _tabY, parent);
        rect.sizeDelta = new(107, 2);
        _tabY += 2;
    }

    public void AddTabGroup(RectTransform parent, string label, string objName = "tabl-group-label") {
        _tabParent = parent;
        int groupIndex = _tabGroups.Count;
        var header = AddButton(28f, _tabY, 107f, parent, "", 14, objName,
            () => ToggleTabGroup(groupIndex));
        _tabGroups.Add(new() {
            Label = label,
            StartTabIndex = _tabs.Count,
            HeaderButton = header,
            Collapsed = true
        });
        _tabY += 28f;
        RefreshTabLayout();
    }

    private int GetGroupEndTabIndex(int groupIndex) {
        if (groupIndex < 0 || groupIndex >= _tabGroups.Count) return -1;
        int nextStart = groupIndex + 1 < _tabGroups.Count
            ? _tabGroups[groupIndex + 1].StartTabIndex
            : _tabs.Count;
        return nextStart - 1;
    }

    private void ToggleTabGroup(int groupIndex) {
        if (groupIndex < 0 || groupIndex >= _tabGroups.Count) return;
        bool willExpand = _tabGroups[groupIndex].Collapsed;
        CollapseAllGroupsExcept(willExpand ? groupIndex : -1);
        _tabGroups[groupIndex].Collapsed = !willExpand;
        RefreshTabLayout();
        if (willExpand) {
            SelectRememberedTabInGroup(groupIndex);
        }
    }

    private void CollapseAllGroupsExcept(int expandedGroupIndex) {
        for (int i = 0; i < _tabGroups.Count; i++) {
            _tabGroups[i].Collapsed = i != expandedGroupIndex;
        }
    }

    private void RefreshTabLayout() {
        if (_tabParent == null || _tabGroups.Count == 0) return;
        float y = 66f;
        for (int g = 0; g < _tabGroups.Count; g++) {
            TabGroupState group = _tabGroups[g];
            if (group.HeaderButton != null) {
                NormalizeRectWithMidLeft(group.HeaderButton, 28f, y, _tabParent);
                string arrow = group.Collapsed ? "▶" : "▼";
                group.HeaderButton.SetText($"{arrow} {group.Label.Translate()}");
            }
            y += 28f;

            int end = GetGroupEndTabIndex(g);
            for (int i = group.StartTabIndex; i <= end; i++) {
                UIButton btn = _tabs[i].Button;
                bool visible = !group.Collapsed;
                btn.gameObject.SetActive(visible);
                if (!visible) continue;
                NormalizeRectWithMidLeft(btn, Margin, y, _tabParent);
                y += 28f;
            }
        }
        _tabY = y;
    }

    public void SetCurrentTab(int index) => OnTabButtonClick(index);

    public void JumpToGroup(string label, int internalTabIndex = 0) {
        for (int i = 0; i < _tabGroups.Count; i++) {
            if (_tabGroups[i].Label == label) {
                CollapseAllGroupsExcept(i);
                _tabGroups[i].Collapsed = false;
                RefreshTabLayout();
                int targetIndex = _tabGroups[i].StartTabIndex + internalTabIndex;
                if (targetIndex >= 0 && targetIndex < _tabs.Count) {
                    SetCurrentTab(targetIndex);
                }
                return;
            }
        }
    }

    public bool JumpToPage(string groupLabel, string subpageLabel) {
        if (string.IsNullOrEmpty(groupLabel) || string.IsNullOrEmpty(subpageLabel)) {
            return false;
        }

        for (int i = 0; i < _tabGroups.Count; i++) {
            if (_tabGroups[i].Label != groupLabel) {
                continue;
            }

            CollapseAllGroupsExcept(i);
            _tabGroups[i].Collapsed = false;
            RefreshTabLayout();
            int end = GetGroupEndTabIndex(i);
            for (int tabIndex = _tabGroups[i].StartTabIndex; tabIndex <= end; tabIndex++) {
                if (_tabs[tabIndex].Label != subpageLabel) {
                    continue;
                }

                SetCurrentTab(tabIndex);
                return true;
            }

            return false;
        }

        return false;
    }

    public bool TryGetCurrentTabRoute(out string groupLabel, out string subpageLabel) {
        groupLabel = null;
        subpageLabel = null;
        if (_currentTabIndex < 0 || _currentTabIndex >= _tabs.Count) {
            return false;
        }

        for (int i = 0; i < _tabGroups.Count; i++) {
            int start = _tabGroups[i].StartTabIndex;
            int end = GetGroupEndTabIndex(i);
            if (_currentTabIndex < start || _currentTabIndex > end) {
                continue;
            }

            groupLabel = _tabGroups[i].Label;
            subpageLabel = _tabs[_currentTabIndex].Label;
            return true;
        }

        return false;
    }

    private void OnTabButtonClick(int index) {
        _currentTabIndex = index;
        int groupIndex = GetGroupIndexByTabIndex(index);
        if (groupIndex >= 0) {
            CollapseAllGroupsExcept(groupIndex);
            _tabGroups[groupIndex].Collapsed = false;
            _tabGroups[groupIndex].LastSelectedTabIndex = index;
            RefreshTabLayout();
        }
        foreach (TabState tab in _tabs) {
            if (tab.Button.data != index) {
                tab.Button.highlighted = false;
                tab.Content.gameObject.SetActive(false);
                continue;
            }

            tab.Button.highlighted = true;
            tab.Content.gameObject.SetActive(true);
        }
    }

    private int GetGroupIndexByTabIndex(int tabIndex) {
        for (int i = 0; i < _tabGroups.Count; i++) {
            int start = _tabGroups[i].StartTabIndex;
            int end = GetGroupEndTabIndex(i);
            if (tabIndex >= start && tabIndex <= end) {
                return i;
            }
        }

        return -1;
    }

    private void SelectRememberedTabInGroup(int groupIndex) {
        if (groupIndex < 0 || groupIndex >= _tabGroups.Count) {
            return;
        }

        int start = _tabGroups[groupIndex].StartTabIndex;
        int end = GetGroupEndTabIndex(groupIndex);
        if (start < 0 || start >= _tabs.Count || end < start) {
            return;
        }

        int targetIndex = _tabGroups[groupIndex].LastSelectedTabIndex;
        if (targetIndex < start || targetIndex > end) {
            targetIndex = start;
        }

        SetCurrentTab(targetIndex);
    }
}
