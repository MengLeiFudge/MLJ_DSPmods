# UI/Foundation — UI Infrastructure

本目录放 UI 基础设施：窗口生命周期、窗口基类和低层 RectTransform 工具。

## Structure

```
Foundation/
├── RectTransformUtils.cs
└── Window/
    ├── MyWindow.cs
    ├── MyWindowManager.cs
    └── MyWindowWithTabs.cs
```

## Rules

- Foundation 不知道 FE 主面板页面、分类或业务功能。
- `Window/` 只处理窗口创建、销毁、生命周期、通用窗口基类。
- `RectTransformUtils.cs` 是低层坐标/尺寸工具，可被 Controls、Layout、MainPanel 使用。
