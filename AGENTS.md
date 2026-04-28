# AGENTS.md - MLJ_DSPmods Development Guide

This document provides essential information for AI agents working on this Dyson Sphere Program mod repository.

## Project Overview

Multiple DSP mods in one solution:
- **FractionateEverything** (`FE` namespace) — Main mod: fractionators, recipes, UI, data centre
- **GetDspData** — Dev tool: exports item/recipe/model/tech data to files
- **AfterBuildEvent** — Build automation: post-build packaging and DLL publicizing
- **VanillaCurveSim** — Standalone simulator EXE: vanilla progression curve simulation

## Build Commands

**Build tool rule:** In the current WSL environment, every compilation must use the local MSBuild at `/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe`.

**Build scope rule:** Build scope depends on the project that changed:
- If any file under `FractionateEverything/` or `GetDspData/` changes, build the full solution `MLJ_DSPmods.sln`.
- If shared build infrastructure changes, including `AfterBuildEvent/`, `Directory.Build.props`, `DefaultPath.props*`, or `MLJ_DSPmods.sln`, also build the full solution `MLJ_DSPmods.sln`.
- If only `VanillaCurveSim/` changes, it may be built separately via `VanillaCurveSim/VanillaCurveSim.csproj`.

**Packaging rule:** `FractionateEverything` and `GetDspData` are packaging-dependent projects. After a successful solution build for either of them, always start `AfterBuildEvent.exe`, but do not send any follow-up input. The user may choose a mode manually or close it directly.

**Simulator rule:** `VanillaCurveSim` is a standalone simulator project. When only it changes, do not start `AfterBuildEvent.exe`; instead, it may be built and run directly.

**Launch style rule:** Do not launch `AfterBuildEvent.exe` as a bare console process, and do not wrap it inside `powershell.exe`. On this Windows 11 machine, the closest match to the user's real double-click experience is to let `wt.exe` host `AfterBuildEvent.exe` directly, with the working directory set to the corresponding build output folder. The expected effect is: window title shows the `AfterBuildEvent.exe` path, and the content starts directly with the program's own prompt text, without any PowerShell banner.

```bash
# FractionateEverything / GetDspData / shared infrastructure change:
# Debug build the full solution, then start the post-build tool in Windows Terminal hosting the EXE directly
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  MLJ_DSPmods.sln \
  /t:Build /p:Configuration=Debug
wt.exe -d "D:\project\csharp\DSP MOD\MLJ_DSPmods\AfterBuildEvent\bin\win\Debug" \
  "D:\project\csharp\DSP MOD\MLJ_DSPmods\AfterBuildEvent\bin\win\Debug\AfterBuildEvent.exe"

# FractionateEverything / GetDspData / shared infrastructure change:
# Release build the full solution, then start the post-build tool in Windows Terminal hosting the EXE directly
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  MLJ_DSPmods.sln \
  /t:Build /p:Configuration=Release
wt.exe -d "D:\project\csharp\DSP MOD\MLJ_DSPmods\AfterBuildEvent\bin\win\Release" \
  "D:\project\csharp\DSP MOD\MLJ_DSPmods\AfterBuildEvent\bin\win\Release\AfterBuildEvent.exe"

# VanillaCurveSim-only change: standalone Debug build and run
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  VanillaCurveSim/VanillaCurveSim.csproj \
  /t:Build /p:Configuration=Debug
"/mnt/c/Windows/System32/cmd.exe" /c \
  "D:\project\csharp\DSP MOD\MLJ_DSPmods\VanillaCurveSim\bin\win\Debug\VanillaCurveSim.exe"

# VanillaCurveSim-only change: standalone Release build and run
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  VanillaCurveSim/VanillaCurveSim.csproj \
  /t:Build /p:Configuration=Release
"/mnt/c/Windows/System32/cmd.exe" /c \
  "D:\project\csharp\DSP MOD\MLJ_DSPmods\VanillaCurveSim\bin\win\Release\VanillaCurveSim.exe"
```

**No unit tests exist.** Build verification is the quality gate:
- Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`
- For `FractionateEverything` / `GetDspData` / shared infrastructure changes, always run the solution-level local `MSBuild.exe` command above before marking work complete, then start `AfterBuildEvent.exe` in `wt.exe` as the directly hosted command, and do not auto-select any mode.
- For `VanillaCurveSim`-only changes, build `VanillaCurveSim/VanillaCurveSim.csproj` and run `VanillaCurveSim.exe` directly.

## Key Files

| File | Purpose |
|---|---|
| `MLJ_DSPmods.sln` | Solution entry point |
| `FractionateEverything/FractionateEverything.csproj` | Main mod project (net472, LangVersion latest) |
| `GetDspData/GetDspData.csproj` | DSP data export tool; depends on `FractionateEverything` |
| `VanillaCurveSim/VanillaCurveSim.csproj` | Standalone simulator EXE; can build/run without `AfterBuildEvent` |
| `DefaultPath.props` / `DefaultPath.props.example` | Game library path config (copy example, fill paths) |
| `lib/` | Custom binaries kept in-repo (`Newtonsoft.Json.dll`, publicizer tools, misc helpers) |

**Build notes:**
- Target framework: `net472` (Unity/.NET Framework compatibility)
- `AllowUnsafeBlocks: true`, `LangVersion: latest` (C# 12 features available)
- Game libraries are "publicized" (all members made public) for mod access
- BepInEx NuGet feed: `https://nuget.bepinex.dev/v3/index.json`

## Calculator Icon Export Notes

- `AfterBuildEvent` 的计算器图标正式流程应以游戏内 `ItemProto.iconSprite` / `RecipeProto.iconSprite` 导出为准。
- AssetStudio 离线导出只适合作为辅助排查或已知资源包提取，不应作为“图标是否存在”的最终判断。
- 已验证原版 `resources.assets` 中存在 `Texture2D: diamond`，但 AssetStudio 导出的 `diamond.png` 是 `56x56`；当前计算器图标需要 `80x80`，所以离线 80x80 过滤会跳过它。游戏内导出会把同一个图标渲染到 `80x80` 画布，才是计算器应使用的结果。
- R2 禁用模组时会把文件改成 `.old`。图标工具查找已知资源文件时必须同时兼容 `foo` 和 `foo.old`，不要只硬编码其中一种。

## Project Structure

```
FractionateEverything/src/
├── FractionateEverything.cs    # BepInEx plugin entry point (Awake, config, Harmony)
├── Logic/                      # → Logic/AGENTS.md
│   ├── Building/               # Static building definition classes → Building/AGENTS.md
│   ├── Manager/                # Static game-state managers → Manager/AGENTS.md
│   ├── Patches/                # Standalone Harmony transpiler patches (IL-level only)
│   └── Recipe/                 # BaseRecipe hierarchy → Recipe/AGENTS.md
├── Compatibility/              # Per-mod detection + integration → Compatibility/AGENTS.md
├── UI/                         # → UI/AGENTS.md
│   ├── Components/             # Reusable widgets (MyWindow, MyImageButton, …) → Components/AGENTS.md
│   ├── Patches/                # UI-specific Harmony patches
│   └── View/                   # Feature panels → View/AGENTS.md
│       ├── MainWindow.cs       # Dual-panel lifecycle hub (Legacy + Analysis)
│       ├── MainWindowPageRegistry.cs # Page registry + category filtering + Analysis availability
│       ├── Archive/            # Stats, recipe gallery, dev diary
│       ├── CoreOperate/        # Recipe/building operate panels
│       ├── DrawGrowth/         # Gacha (Raffle + Store), exchange system
│       ├── ProgressTask/       # Quests, achievements, main tasks
│       ├── ResourceInteraction/ # DataCentre item interaction
│       └── Setting/            # VIP, sandbox, misc config
└── Utils/                      # `partial class Utils` split across 12 files → Utils/AGENTS.md
    ├── ProtoID.cs              # ALL proto ID constants (I/R/M/T prefix + IFE/RFE/MFE/TFE for mod)
    └── PackageUtils.cs         # Inventory/belt ops, TakeItemWithTip, AddItemToModData
```

## Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes, methods, properties | `PascalCase` | `ProcessManager`, `GetOutputs`, `EnableVoidSpray` |
| Private/local fields | `camelCase` | `item`, `recipe`, `fluidInputCount` |
| Public static fields | `PascalCase` | `Level`, `MaxStack` |
| Constants (C# string names) | Chinese inline or `UPPER_SNAKE_CASE` | `IFE转化塔`, `RFE转化塔` |
| Local variables | `camelCase` | `outputList`, `fluidInputInc` |
| Proto ID constants | `I` prefix + Chinese item name | `I铁块`, `I电磁矩阵` |
| Harmony patch methods | `ClassName_MethodName_Suffix` | `FractionatorComponent_Import_Postfix` |
| Namespaces | `FE.*` (root `FE`) | `FE.Logic.Building`, `FE.Utils` |

## Import Organization

```csharp
// 1. System namespaces
using System;
using System.Collections.Generic;
using System.IO;

// 2. Third-party / game libraries (alphabetical within group)
using BepInEx;
using HarmonyLib;
using UnityEngine;

// 3. Internal FE namespaces (alphabetical)
using FE.Compatibility;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;

// 4. Static imports last
using static FE.FractionateEverything;
using static FE.Logic.Manager.ProcessManager;
using static FE.Utils.Utils;
```

## Code Style

**Indentation:** 4 spaces (no tabs)  
**Braces:** Same-line opening brace (K&R style): `public static class Foo {`  
**Line endings:** CRLF (Windows) — maintained by `.gitattributes`  
**File encoding:** UTF-8 with BOM (auto-generated by VS)  

### Switch Expressions (preferred for level-based values)
```csharp
public static int MaxStack => Level switch {
    < 6 => 1,
    < 9 => 4,
    < 12 => 8,
    _ => 12,
};
```

### Array Initializers (C# 12 collection expressions)
```csharp
[IFE分馏塔定向原胚], [2], [IFE转化塔], [5]   // preferred over new int[] { }
public static readonly List<ProductOutputInfo> emptyOutputs = [];
```

### String Interpolation (preferred over concatenation)
```csharp
public string TypeName => $"{RecipeType.GetName()}-{LDB.items.Select(InputID).name} +{Level}";
```

### XML Documentation
```csharp
/// <summary>
/// Brief description (Chinese or English acceptable).
/// </summary>
public static void SomeMethod() { ... }
```

## DSP/Unity-Specific Patterns

### Static Building Class Template
Every building in `Logic/Building/` follows this structure:
```csharp
namespace FE.Logic.Building;

public static class ConversionTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.7f, 0.6f, 0.8f);

    public static int Level = 0;
    public static bool EnableFluidEnhancement => Level >= 3;
    public static int MaxStack => Level switch { < 6 => 1, _ => 4 };

    public static void AddTranslations() { ... }
    public static void Create() { ... }        // Register item/recipe/model
    public static void SetMaterial() { ... }   // Apply materials/colors
    public static void UpdateHpAndEnergy() { ... }
}
```

### Harmony Patches
```csharp
// Postfix (most common)
[HarmonyPostfix]
[HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) { }

// Transpiler (IL-level, use CodeMatcher)
[HarmonyTranspiler]
[HarmonyPatch(typeof(ModelProtoSet), nameof(ModelProtoSet.OnAfterDeserialize))]
public static IEnumerable<CodeInstruction> SomeClass_Method_Transpiler(
    IEnumerable<CodeInstruction> instructions) {
    var matcher = new CodeMatcher(instructions);
    // ... patch IL
    return matcher.InstructionEnumeration();
}
```

### Recipe System
- Inherit from `BaseRecipe` (primary outputs go to `outputMain`, side products to `outputAppend`)
- Override `RecipeType` property (returns `ERecipe` enum value)
- Override `GetOutputs()` for custom distribution logic
- Use `OutputInfo` and `ProductOutputInfo` for output descriptors
- `fluidInputInc` carries proliferator points through the pipeline

### Proto ID Constants (`Utils/ProtoID.cs`)
- All game item/recipe/model IDs are `internal const int` in `partial class Utils`
- Item IDs: `I` prefix + Chinese name (e.g., `I铁块 = 1101`)
- Recipe IDs: `R` prefix + Chinese name
- Model IDs: `M` prefix + Chinese name
- Mod-added IDs: `IFE`, `RFE`, `MFE` prefixes for this mod's additions

## Error Handling

- Minimal try-catch (Unity/BepInEx catches unhandled exceptions at the game level)
- Prefer null checks: `if (LDB.items.Exist(outputId))` before accessing
- Use early returns for validation failures (guard clauses)
- Log with the shared logger utilities in `Utils/LogUtils.cs`; avoid console spam

## Common Tasks

### Adding a New Building
1. Create `Logic/Building/NewBuildingName.cs` following the static class template above
2. Add `Level`, `EnableXxx`, switch-expression properties as needed
3. Implement `AddTranslations()`, `Create()`, `SetMaterial()`, `UpdateHpAndEnergy()`
4. Register in `BuildingManager`: call new methods inside `AddTranslations()`, `AddFractionators()`, `SetFractionatorMaterial()`, `UpdateHpAndEnergy()`
5. Add Proto IDs to `Utils/ProtoID.cs`

### Adding a New Recipe Type
1. Create `Logic/Recipe/NewRecipe.cs` inheriting from `BaseRecipe`
2. Override `RecipeType` (returns an `ERecipe` value)
3. Override `GetOutputs()` with the distribution logic
4. Add a static `CreateAll()` method and call it from `RecipeManager`

### Modifying Game Logic
1. Check if an existing manager/patch covers the target method
2. Prefer adding to existing Harmony patch classes in `Logic/Manager/` or `Logic/Patches/`
3. Use `[HarmonyPostfix]` by default; use `[HarmonyTranspiler]` only when postfix is insufficient
4. Place patches as static methods directly inside the relevant manager class when cohesive

## Git Practices

- Commit messages in **Chinese**, conventional style: `功能：`, `修复：`, `重构：`, `杂项：`
- Atomic commits (one logical change per commit)
- Do **not** push unless explicitly approved by the user

### Commit Policy for Agents

**核心原则：严禁积压未提交改动。** 任何代码改动必须被记录在 Git 历史中，不允许以"改了一堆文件但零 commit"的状态结束任务。即使用户没有明确要求，也应在构建通过后自动按逻辑单元 commit。

**提交流程：** 主代理根据任务复杂度和风险决定提交方式：
- 改动独立且风险较低 → 可由子代理直接 commit，主代理审查后如有问题再提交修复性 commit
- 改动跨多个模块或风险较高 → 子代理完成后不 commit，由主代理审查通过后统一 commit

**职责要求：** 主代理下发任务时，必须在 prompt 中明确说明本轮的 commit 策略，不能让"代码已改完但暂不提交"成为默认结束状态。

**并行场景：** 多个子代理并行执行时，子代理不得各自提交；应由主代理收齐结果、完成审查后统一 commit，以避免历史冲突和责任边界不清。**所有 Git 操作都必须串行执行**，禁止并发 `git add`、`git commit`、`git rebase`、`git stash`、`git checkout`、`git merge` 等命令；即使作用文件完全不重叠，也必须等待前一个 Git 命令完成并确认仓库锁已释放后，才能开始下一个 Git 命令。

**Git 串行规则：** Git 使用单一仓库锁（如 `.git/index.lock`）；因此所有 Git 操作都必须串行执行，禁止任何形式的并发 Git 命令。只有确认前一个 Git 命令已经完成且仓库锁已释放后，才能启动下一个 Git 命令。

**commit 要求：**
- 构建无错误（`0 Error(s)`）后方可 commit；Warning 不作硬性要求（如未使用变量等无害 warning 可忽略）
- 每个逻辑单元一个 commit，不批量堆积
- **严禁 push**，除非用户明确批准

## AI Agent Notes

- Rune related content has been removed (RuneManager, RuneMenu, etc.).
- `.sisyphus/plans/` — task plans with checkboxes; update when tasks complete
- `.sisyphus/notepads/` — learnings from previous sessions; read before starting
- `.sisyphus/evidence/` — screenshots and supporting evidence
- Subdirectory `AGENTS.md` files exist for: `Logic/`, `Logic/Building/`, `Logic/Manager/`, `Logic/Recipe/`, `UI/`, `UI/Components/`, `UI/View/`, `UI/View/DrawGrowth/`, `Compatibility/`, `Utils/`, `AfterBuildEvent/src/`
- **Simulator-first workflow (mandatory):** when the user is exploring balance, pacing, or design direction and has not yet approved applying the result to the real mod, agents must restrict code changes to `VanillaCurveSim/**` (and supporting docs/plans if needed). In this stage, agents may read `FractionateEverything/**` to mirror real formulas, but must not modify FE project files until the user explicitly confirms that the simulated result should be applied to FE.

## Analysis UI Layout Baseline (Mandatory)

When rendering pages inside `FEAnalysisMainWindow` middle black area:

- Black area size is fixed to **1102 x 787**
- Keep **10px gap** on all four sides
- Unified design origin is **(10, 10)** from black area's top-left
- Unified design canvas size is **1082 x 767**

This baseline applies to both:
- Analysis-specific pages (`CreateUIInAnalysis`)
- Legacy proxied pages (`CreateUI` routed through analysis)

For multi-column layout helper `GetPosition`, default total width is aligned to this baseline (1082).

### Game Source Reference: DecompiledSource

`gamedata/DecompiledSource/` contains the full decompiled C# source of DSP's game DLLs and mod DLLs (publicized versions, one `.cs` file per type, namespace-nested directories). **This is the authoritative reference for DSP game internals and mod APIs.**

**Game DLLs:**
- `Assembly-CSharp/` — Main game logic
- `UnityEngine.UI/` — UI components

**Mod DLLs:**
- `DSP_Battle/` — 深空来敌 (They Come From Void)
- `ProjectGenesis/` — 创世之书 (Genesis Book)
- `ProjectOrbitalRing/` — 星环 (Orbital Ring)

**How it's generated** — Run `AfterBuildEvent` → select option `2`:
1. Publicizes game DLLs from game install → nuget package dir → decompiles
2. Reads FE soft dependencies from `CheckPlugins.cs`, confirms installation via `mods.yml`, then decompiles matching mod DLLs directly from `R2ProfileDir\BepInEx\plugins\`
3. Decompiles each via `ilspycmd -p --nested-directories` into `DecompiledSource/{DllName}/`
4. Requires `ilspycmd` installed globally: `dotnet tool install -g ilspycmd`

**When to use it:**
- Verifying whether a Harmony patch target (`typeof(X)`, `nameof(X.Y)`) actually exists
- Understanding the original method logic before writing a patch
- Looking up field/property names, struct layouts, or enum values in game types
- Any question of the form "does `GameMain.FixedUpdate` exist / what does it do?"

**How to search it** — Use `Grep` or `mcp_grep` on the directory:
```
path: gamedata/DecompiledSource/{DllName}
pattern: class GameMain|void FixedUpdate
```

### Critical Pitfalls
1. **Never modify `BaseRecipe.GetOutputs` directly** — it's shared; subclass instead
2. **Never touch `buffBonus1/2/3`** — reserved for future use
3. **Avoid new Harmony patches** when existing code paths suffice
4. **Always verify build with the correct scope** — `FractionateEverything` / `GetDspData` / shared infrastructure changes must build `MLJ_DSPmods.sln`, ensure `0 Error(s)`, then run `AfterBuildEvent.exe`; `VanillaCurveSim`-only changes may build `VanillaCurveSim.csproj` and run `VanillaCurveSim.exe`
5. **LangVersion is `latest`** — use C# 12 features (collection expressions `[]`, primary constructors, etc.)

---

*Last updated: March 2026 | Target: net472 | C# latest | BepInEx 5.4.17*
