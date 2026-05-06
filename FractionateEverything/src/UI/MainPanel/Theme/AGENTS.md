# UI/MainPanel/Theme — Main Panel Visual System

本目录放主面板页面的视觉骨架和主题素材。

## Files

- `PageLayout.cs`：主面板页面尺寸、间距、卡片、页头、页脚等统一视觉骨架。
- `RoundedSpriteFactory.cs`：主面板卡片圆角填充和描边 sprite。

## Rules

- 这里是 MainPanel 专用主题，不是全局通用 UI 主题。
- 页面尺寸基准仍以 Analysis 黑色内容区 `1082 x 767` 设计区为准。
- 业务页面引用 `FE.UI.MainPanel.Theme.PageLayout`。
