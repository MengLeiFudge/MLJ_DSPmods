# Utils — Shared Utilities (partial class)

12 files, ~3200 lines. All declare `public static partial class Utils` in `namespace FE.Utils`.
Import with `using static FE.Utils.Utils;` — then call helpers directly without prefix.

## File Breakdown

| File | Lines | Contains |
|---|---|---|
| `ProtoID.cs` | 1236 | ALL proto ID constants (`I铁块`, `RFE分馏配方`, `MFE转化塔`, `TFE分馏数据中心`, …) |
| `PackageUtils.cs` | 1273 | Inventory ops, belt ops, item-take/give, building-mode checks, `GetItemTotalCount` |
| `I18NUtils.cs` | 105 | `Register(zh, en)`, `Translate()` extension, `GetPosition(col, totalCols)` |
| `QualitySystem.cs` | 129 | Quality tier logic (white/green/blue/purple/red/gold) |
| `NormalizeRectUtils.cs` | 131 | `NormalizeRectWithMidLeft`, `NormalizeRectWithTopLeft` layout helpers |
| `RichTextUtils.cs` | 74 | Rich text color/size helpers |
| `FormatUtils.cs` | 66 | `FormatP(percent)`, `FormatName(item)` |
| `PatchImpl.cs` | 56 | Harmony patch infrastructure helpers |
| `SaveUtils.cs` | 77 | `w.WriteBlocks(...)` / `r.ReadBlocks(...)` — tag-dispatch save API |
| `LogUtils.cs` | 52 | `LogDebug/Info/Warning/Error` (wraps BepInEx logger) |
| `RandomUtils.cs` | 27 | `GetRandDouble()`, `GetRandInt(min, max)` |
| `GridIndexUtils.cs` | 27 | Grid index ↔ coordinate conversion |

## Proto ID Naming Scheme

```
I铁块        = 1101   // item ID:  I + Chinese name
R精炼铁      = ?      // recipe ID: R + Chinese name
M电弧熔炉    = ?      // model ID:  M + Chinese name
IFE转化塔    = ?      // mod item:  IFE + Chinese name
RFE分馏配方  = ?      // mod recipe: RFE + name
MFE转化塔    = ?      // mod model:  MFE + name
TFE分馏数据中心 = ?   // mod tech:  TFE + name
```

When adding new mod items, append to `ProtoID.cs` in the appropriate `IFE/RFE/MFE/TFE` block.

## Save API (SaveUtils.cs)

Only two **public** extension methods. `WriteBlock` / `ReadBlock` / `ReadAndHandleBlock` are **private** — never call them directly.

```csharp
// Export — WriteBlocks auto-writes block count; no manual version number needed
w.WriteBlocks(
    ("Tag1", bw => { bw.Write(field1); }),
    ("Tag2", bw => { bw.Write(field2); })
);

// Import — ReadBlocks auto-reads block count; dispatches by tag (dictionary lookup);
// unknown tags are skipped with a warning (forward-compatible by design)
r.ReadBlocks(
    ("Tag1", br => { field1 = br.ReadInt32(); }),
    ("Tag2", br => { field2 = br.ReadSingle(); })
);
```

**Top-level exception**: `FractionateEverything.Import` reads one `int version` first.
If `version < 10` → seeks to end, shows UIMessageBox warning, and returns (no data read).
Otherwise delegates to managers via `ReadBlocks`.
## Key Utility: TakeItemWithTip

```csharp
// Takes items from player inventory, shows UI tip if insufficient
bool success = TakeItemWithTip(itemId, count, out int taken, showMessage: true);
```

## AddItemToModData

```csharp
// Adds item to DataCentre bag (not player inventory)
AddItemToModData(itemId, count, inc, showTip: true);
```
