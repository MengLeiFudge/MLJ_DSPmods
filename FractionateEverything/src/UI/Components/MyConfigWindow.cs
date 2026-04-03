using System;
using UnityEngine;

namespace FE.UI.Components;

public class MyConfigWindow : MyWindowWithTabs {
    // 旧版主窗口对齐新版黑色内容区 1102x787，并额外保留左侧标签区宽度。
    private const float LegacyContentWidth = 1102f;
    private const float LegacyContentHeight = 787f;
    private const float LegacyLeftNavSpan = Margin + TabWidth + Spacing;
    private const float LegacyWindowWidth = LegacyContentWidth + LegacyLeftNavSpan;
    private const float LegacyWindowHeight = LegacyContentHeight + TitleHeight;

    public static Action<MyConfigWindow, RectTransform> OnUICreated;
    public static Action OnUpdateUI;

    private RectTransform _windowTrans;

    public static MyConfigWindow CreateInstance(string name, string title = "") {
        return MyWindowManager.CreateWindow<MyConfigWindow>(name, title);
    }

    public static void DestroyInstance(MyConfigWindow win) {
        MyWindowManager.DestroyWindow(win);
    }

    public override void _OnCreate() {
        base._OnCreate();
        _windowTrans = GetComponent<RectTransform>();
        OnUICreated?.Invoke(this, _windowTrans);
        SetCurrentTab(0);
        OnUpdateUI?.Invoke();
        // Delay 500ms to run AutoFitWindowSize() after the window is created
        // Invoke(nameof(AutoFitWindowSize), 0f);
    }

    public override void _OnDestroy() {
        _windowTrans = null;
        base._OnDestroy();
    }

    public override bool _OnInit() {
        if (!base._OnInit()) return false;
        _windowTrans.anchoredPosition = new(0, 0);
        return true;
    }

    public override void _OnOpen() {
        _windowTrans.sizeDelta = new(LegacyWindowWidth, LegacyWindowHeight);
    }

    public override void _OnUpdate() {
        base._OnUpdate();
        if (VFInput.escape && !VFInput.inputing) {
            VFInput.UseEscape();
            _Close();
            return;
        }
        OnUpdateUI?.Invoke();
    }
}
