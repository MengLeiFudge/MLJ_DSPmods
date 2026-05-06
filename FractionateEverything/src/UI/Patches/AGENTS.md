# UI/Patches — 通用 UI 补丁

`UI/Patches` 只保留与具体功能域无关的通用 Unity/DSP UI 控件 patch。

## Structure

```
Patches/
└── Common/
    ├── UIButtonPatch.cs
    └── UIComboBoxPatch.cs
```

## Rules

- 通用按钮、下拉框等控件兼容放 `Common`。
- 分馏塔窗口、配方显示、图标注入、信号选择等 patch 归对应逻辑功能域的 `Presentation`。
- 主面板页面不放这里，放 `UI/MainPanel`。
