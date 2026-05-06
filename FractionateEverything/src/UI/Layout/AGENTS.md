# UI/Layout — Declarative Layout

本目录放声明式网格布局 DSL 和运行时，不放具体页面，也不放窗口 Shell。

## Files

- `GridDsl.cs`
- `GridLayoutNodes.cs`
- `GridLayoutPrimitives.cs`
- `GridLayoutRuntime.cs`

## Rules

- Layout 可以使用 `UI/Foundation` 和 `UI/Controls`。
- 当前 DSL 仍复用 `UI/MainPanel/Theme/PageLayout` 的卡片节点；如后续要通用化，再拆出主面板专用 DSL。
- 页面代码使用 `using static FE.UI.Layout.GridDsl;`。
