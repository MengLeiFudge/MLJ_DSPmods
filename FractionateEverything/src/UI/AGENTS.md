# UI — Unity UI Layer

34 files, ~8000 lines. All Views are **static classes** managed centrally by `MainWindow`.

## Structure

```
UI/
├── Components/   # Reusable widgets (MyWindow, MyImageButton, MySlider, …)
├── Patches/      # UI-specific Harmony patches (icon injection, combo box, etc.)
└── View/         # Feature panels — one subdir per feature domain
    ├── MainWindow.cs               # Central hub: routes lifecycle to all Views
    ├── CoreOperate/                # Recipe/building operate panels
    ├── GetItemRecipe/              # Raffle + LimitedTimeStore
    ├── ProgressSystem/             # Quests, achievements, dev diary
    ├── RuneSystem/                 # Rune menu (精华)
    ├── Setting/                    # VIP, sandbox mode, miscellaneous config
    ├── Statistic/                  # Recipe gallery, fractionation stats
    └── ModPackage/                 # Important items, item interaction
```

## View Lifecycle (MainWindow delegates to each View)

```csharp
// Every static View class must implement:
static void AddTranslations()          // register i18n strings
static void LoadConfig(ConfigFile cfg) // bind ConfigEntry<T>
static void CreateUI(MyConfigWindow wnd, RectTransform tab)
static void UpdateUI()                 // called each frame when tab visible
// + IModCanSave: Import / Export / IntoOtherSave
```

## Adding a New View Tab

1. Create `UI/View/MyFeature/MyFeatureView.cs` as a `public static class`
2. Implement all 4 lifecycle methods + IModCanSave
3. In `MainWindow`: add calls in `AddTranslations`, `LoadConfig`, `CreateUI`, `UpdateUI`
4. Translations registered here, not in Logic

## Unity UI Patterns Used

**Positioning** — always `NormalizeRectUtils`:
```csharp
wnd.AddImageButton(x, y, tab, itemProto);   // x/y relative to tab origin
wnd.AddText2(x + 40, y, tab, "text");
```

**Proto binding** — image buttons bind directly to game protos:
```csharp
exchangeImages[i].Proto = LDB.items.Select(itemId);  // auto-sets sprite
```

**Frame update guard** — always gate UpdateUI on tab visibility:
```csharp
public static void UpdateUI() {
    if (!tab.gameObject.activeSelf) return;
    ...
}
```

## Patches Folder

- `IconSetPatch.cs` — injects mod item icons into DSP's icon atlas
- `UIRecipeEntryPatch.cs` / `UIComboBoxPatch.cs` / `UIButtonPatch.cs` — minor UI compatibility fixes

These patches are **UI-only**. Game-logic patches belong in `Logic/Manager/`.
