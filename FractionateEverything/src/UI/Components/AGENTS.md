# UI/Components — Reusable Widget Library

12 files, ~2200 lines. Wraps Unity UI + DSP's ManualBehaviour. Do not construct directly — use factory methods on `MyWindow`.

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
| `MyWindow` | 635 | `MyWindow.Create<T>(name, title)` |
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
