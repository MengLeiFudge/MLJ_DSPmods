# UI/MainPanel/Shell — Main Panel Window Shells

本目录放 FE 主面板窗口外壳。Shell 负责承载页面、导航容器和窗口风格，不负责具体业务页面逻辑。

## Structure

```
Shell/
├── MessageBox/
│   └── MessageBoxMainPanelWindow.cs
└── Analysis/
    └── AnalysisMainPanelWindow/
        ├── AnalysisMainPanelWindow.cs
        ├── ButtonStyle.cs
        ├── Layout.cs
        ├── NativeShell.cs
        └── Navigation.cs
```

## Rules

- `MessageBox/` 是消息框风格主面板窗口。
- `Analysis/` 是分析面板风格主面板窗口。
- Shell 可以依赖 `MainPanel` 路由契约，但不要直接写具体页面业务。
