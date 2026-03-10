# UI/View — Feature Panels

18 files across 7 feature subdirs + `MainWindow.cs`. Each subdir = one self-contained feature.

## MainWindow.cs — Central Hub

Routes all lifecycle calls to every View. When adding a new View, register it here only:
```csharp
public static void AddTranslations() { FracRecipeOperate.AddTranslations(); MyNewView.AddTranslations(); ... }
public static void LoadConfig(ConfigFile cfg) { ... }
public static void CreateUI(MyConfigWindow wnd) { ... }
public static void UpdateUI() { ... }
```

## Subdirectory Reference

| Dir | Files | Key classes | Purpose |
|---|---|---|---|
| `CoreOperate/` | 3 | `FracRecipeOperate` (570L), `BuildingOperate` (417L), `VanillaRecipeOperate` | Recipe/building upgrade panels |
| `GetItemRecipe/` | 2 | `TicketRaffle` (1262L), `LimitedTimeStore` (509L) | Raffle + timed shop |
| `RuneSystem/` | 1 | `RuneMenu` (645L) | Rune (精华) management |
| `ProgressSystem/` | 4 | `DevelopmentDiary`, `Achievements`, `MainTask`, `RecurringTask` | Quest + diary |
| `Setting/` | 3 | `VipFeatures`, `SandboxMode`, `Miscellaneous` | Config/cheat options |
| `Statistic/` | 2 | `FracStatistic`, `RecipeGallery` | Stats + recipe browser |
| `ModPackage/` | 2 | `ItemInteraction`, `ImportantItem` | DataCentre item ops |

## Conventions Unique to View

**Harmony patches inside Views** — `TicketRaffle` and `LimitedTimeStore` both patch `GameMain.FixedUpdate` as postfix for background tick logic. If the feature needs a per-tick callback, add the patch inline in the View class (not in `Logic/Manager/`).

**ExchangeInfo pattern** — `LimitedTimeStore` uses `ExchangeInfo` data class with `Import/Export` for per-slot save data. Follow this pattern for any new per-slot state.

**Raffle vs LimitedTimeStore** — Both in `GetItemRecipe/` but fully independent:
- `TicketRaffle` — ticket-based random draw, managed by `TicketRaffle.GameMain_FixedUpdate_Postfix`
- `LimitedTimeStore` — timed shop refresh, managed by `LimitedTimeStore.GameData_FixedUpdate_Postfix`

## Large File Warning

`TicketRaffle.cs` (1262 lines) — do not add more features; extract to a helper class if needed.
