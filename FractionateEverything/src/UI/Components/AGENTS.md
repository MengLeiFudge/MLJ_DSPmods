# UI/Components — Reusable Widget Library

通用组件库包装 Unity UI + DSP `ManualBehaviour`。窗口类和布局 helper 归这里，业务页面归 `UI/MainPanel`。

## Class Hierarchy

```
ManualBehaviour (DSP base)
└── MyWindow (abstract)
    └── MyWindowWithTabs
        └── MyConfigWindow          ← used by all mod panels
```

## Widget Catalogue

| Class | Lines | Created via |
|---|---|---|
| `MyWindow` | base | `MyWindow.Create<T>(name, title)` |
| `MyWindowWithTabs` | tabs | inherited by `MyConfigWindow` |
| `MyWindowManager` | lifecycle | `MyWindowManager.CreateWindow<T>()` |
| `MyAnalysisWindow*.cs` | ~1400 | `MyAnalysisWindow.CreateInstance(name, title)` |
| `MyImageButton` | 206 | `wnd.AddImageButton(x, y, tab [, proto])` |
| `MyCheckButton` | 232 | `wnd.AddCheckButton(x, y, tab, ...)` |
| `MyKeyBinder` | 205 | `wnd.AddKeyBinder(x, y, tab, config)` |
| `MyComboBox` | 148 | `wnd.AddComboBox(x, y, tab)` |
| `MyCornerComboBox` | 132 | `wnd.AddCornerComboBox(x, y, tab)` |
| `MySlider` | 140 | `wnd.AddSlider(x, y, tab, ...)` |
| `MySideSlider` | 145 | `wnd.AddSideSlider(x, y, tab, ...)` |
| `MyCheckbox` | 172 | `wnd.AddCheckBox(x, y, tab, ...)` |
| `MyFlatButton` | 87 | `wnd.AddButton(col, cols, y, tab, label)` |
| `MyImageButtonGroup` | 66 | `wnd.AddImageButtonGroup(x, y, tab)` |
| `MyConfigWindow` | 50 | `MyConfigWindow.CreateWindow(...)` |
| `NormalizeRectUtils` | layout | static layout helpers |

## MyAnalysisWindow Split

- `MyAnalysisWindow.cs`：共享字段、嵌套类型、实例创建、窗口生命周期。
- `MyAnalysisWindow.NativeShell.cs`：复制并改造原生统计窗口、隐藏原生控件、解析内容宿主。
- `MyAnalysisWindow.Navigation.cs`：顶部分类、左侧子页、跨页跳转、页面内容显示。
- `MyAnalysisWindow.ButtonStyle.cs`：按钮克隆容量、位置恢复、拖拽转发、状态高亮、文本与隐藏内嵌导航。
- `MyAnalysisWindow.Layout.cs`：Analysis 内容根、设计根、窗口尺寸和容器创建 helper。

## Fluent API Pattern

```csharp
// Most components support chaining after creation:
wnd.AddComboBox(x, y, tab)
   .WithItems(new[] { "A", "B", "C" })
   .WithSize(200, 0)
   .WithConfigEntry(myConfigEntry);

wnd.AddImageButton(x, y, tab)
   .WithClickEvent(onLeft, onRight)
   .WithTip("tooltip text");
```

## Layout Conventions

- `x`, `y` are pixel offsets relative to the tab's `RectTransform` origin (top-left)
- `GetPosition(col, totalCols)` helper returns evenly-spaced `(x, y)` for multi-column rows
- `y` increments by `36f + 7f` (row height = 36, gap = 7) between rows
- Columns in `AddButton(col, cols, y, tab, label)` auto-distribute width

## MyWindow Lifecycle Hooks

Override these (not Unity's `Start`/`Update`):
```csharp
protected override void _OnCreate()  { /* init */ }
protected override void _OnOpen()   { /* show */ }
protected override void _OnFree()   { /* hide */ }
protected override void _OnDestroy(){ /* cleanup */ }
protected override void _OnUpdate() { /* per-frame */ }
```
