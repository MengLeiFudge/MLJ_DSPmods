# AGENTS.md - MLJ_DSPmods Development Guide

This document provides essential information for AI agents working on this Dyson Sphere Program mod repository.

## Project Overview

Multiple DSP mods in one solution:
- **FractionateEverything** (`FE` namespace) — Main mod: fractionators, recipes, UI, data centre
- **GetDspData** — Dev tool: exports item/recipe/model/tech data to files
- **SaveDataExporter** — Runtime mod: hotkey exports current save statistics to `.xlsx`
- **UXAEnhance** — Runtime add-on: hard-depends on UXAssist and enhances its auto-config logistics UI
- **AfterBuildEvent** — Build automation: post-build packaging and DLL publicizing
- **VanillaCurveSim** — Standalone simulator EXE: vanilla progression curve simulation

## Build Commands

**Build tool rule:** All compilation must use the local Windows environment. From WSL, run the Windows MSBuild executable at `/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe`, but only from the Windows-mounted repository path that maps to `D:\project\dsp\MLJ_DSPmods`. Do not compile from Linux-home worktrees such as `/home/mlj/.codex/worktrees/...`, because Windows tools see those as `\\wsl.localhost\...` paths and can fail to launch generated EXEs.

**Output path rule:** Debug build output is fixed to `bin\Debug`. Do not introduce OS-specific output folders, and do not allow target-framework suffixes in the output path.

**Intermediate output rule:** Windows MSBuild/NuGet must keep project intermediates under `obj\`, while Linux `dotnet` and `csharp-ls` use `obj/linux/`. Do not point both operating systems at the same `project.assets.json`; their restore graphs contain platform-specific paths and targets and will invalidate the other toolchain.

**Assembly metadata rule:** `Directory.Build.props` must keep `IncludeSourceRevisionInInformationalVersion=false`. Do not embed the current Git revision into `AssemblyInformationalVersion`, because full-solution Debug builds recompile every packaged project and would otherwise make unrelated DLLs change whenever `HEAD` changes.

**External mod reference rule:** `FractionateEverything` compile-time references for external R2 mods should resolve from `ExternalModReferences.generated.props` when possible. Generate it by running `AfterBuildEvent.exe 6` in the main Windows-mounted checkout after changing R2 mod versions. The generator reads `ProfileDir\mods.yml` for the selected package versions, then resolves DLLs from `R2CacheDir\<package>\<version>\...` so disabled profile files such as `.dll.old` do not become the primary compile reference. `ExternalModReferences.generated.props` is local/generated and must not be committed. `FractionateEverything.csproj` may keep profile plugin `.dll/.dll.old` lookup only as a fallback.

**Build scope rule:** Build scope depends on the project that changed:
- If any file under `FractionateEverything/`, `GetDspData/`, `SaveDataExporter/`, or `UXAEnhance/` changes, build the full solution `MLJ_DSPmods.sln`.
- If shared build infrastructure changes, including `AfterBuildEvent/`, `Directory.Build.props`, `DefaultPath.props*`, or `MLJ_DSPmods.sln`, also build the full solution `MLJ_DSPmods.sln`.
- If only `VanillaCurveSim/` changes, it may be built separately via `VanillaCurveSim/VanillaCurveSim.csproj`.

**Worktree build rule:** Code changes may be edited, tested with non-Windows structural checks, and committed inside a Codex worktree, but Windows compilation and all EXE launches must wait until the worktree branch is merged back into the target branch in the main Windows-mounted checkout. Do not start `AfterBuildEvent.exe` from a worktree.

**Packaging and publish rule:** `FractionateEverything`, `GetDspData`, `SaveDataExporter`, and `UXAEnhance` are packaging-dependent projects. After the verified worktree change is committed, accepted, merged back into the target branch, and the Debug solution build succeeds in the main Windows-mounted checkout, always run `AfterBuildEvent.exe 1` from `AfterBuildEvent\bin\Debug`. This is required for both manual/local work and qqbot/Codex automation work.
- User wording such as "改一下", "修一下", "优化一下", "调整一下", or any other request that leads to modifying `FractionateEverything` / `GetDspData` / `SaveDataExporter` / `UXAEnhance` / shared packaging-dependent code is already sufficient authorization for the required build + package + publish chain. Do not infer "publish was not requested" from the absence of the word "发布"; only skip `AfterBuildEvent.exe 1` for read-only discussion, explicit user pause/stop, `VanillaCurveSim`-only work, or a real blocking failure that must be reported.
- The latest commit body is the publish message source. Include the user-visible reason, the fix/change, the implementation path, the verification evidence, and the impact in the commit body before running `AfterBuildEvent.exe 1`.
- Running `AfterBuildEvent.exe 1` is not enough by itself. The required completion state is: built mod files copied to R2, zip packages created under `ModZips`, unchanged package contents skipped locally, changed configured zips sent to qqbot through the generic local `publish-local` admin API, and qqbot either independently skips unchanged content or delivers changed files to the target QQ group.
- `AfterBuildEvent` owns the publish target list. To publish another mod or group, edit the tool-side publish target configuration; do not add MLJ_DSPmods-specific rules to qqbot.
- If the user manually starts `AfterBuildEvent.exe` and selects option `1`, the tool must still notify qqbot and dispatch the configured zip files before any optional game launch prompt.
- A publish run with qqbot upload failure is not complete. Report the real qqbot/HTTP error and keep the package paths visible for manual recovery instead of claiming the change was dispatched.
- `AfterBuildEvent.exe 1` must use two independent content guards for QQ group uploads. First, `AfterBuildEvent` records a local content-hash cache under `ModZips\publish-content-sha256\` and must not notify qqbot for configured zip packages whose content hash is unchanged. Second, qqbot independently recalculates package content from the zip file and compares it with its own cache before deleting, uploading, or sending any group message. `sha256` validates the zip file bytes; `content_sha256` is still sent as metadata, but neither side may rely on timestamps.

**Thunderstore 授权边界：** 上述模式 `1` 的发布仅指本地 R2 替换和 QQ 群文件推送，不包含 Thunderstore。雷霆商店使用独立模式 `7`，只有用户明确批准指定包及版本后才允许执行 `publish`；修复、构建、打包、群推送或准备清单均不能代替商店发布批准。

- 先按 [README 的发布流程](README.md#thunderstore-发布) 执行 `7 prepare <项目>`，核对清单、版本、ZIP SHA256 和分类。
- 用户批准后才能执行 `7 publish <项目> <版本> --approve <ZIP SHA256>`；不自动发布全部模组，不自动增版，不对失败或超时盲目重传。
- 认证复用 Windows User/Machine 环境变量 `THUNDERSTORE_API_TOKEN`，仅向 CLI 子进程映射 `TCLI_AUTH_TOKEN`；不得输出、落盘或放入命令参数。

**Simulator rule:** `VanillaCurveSim` is a standalone simulator project. When only it changes, do not start `AfterBuildEvent.exe`; instead, it may be built and run directly.

**Publish launch rule:** Required packaging/publish verification uses the automated command form from WSL in the main Windows-mounted checkout: change to `AfterBuildEvent\bin\Debug`, and run `./AfterBuildEvent.exe 1`. Do not wrap the automated publish command in `powershell.exe`, do not wait for stdin, and do not use the old interactive no-argument launch for publish completion.

```bash
# 仅在工作树分支已经合并回目标分支后运行以下命令，
# 并且必须在 Windows 挂载的主检出目录中执行。

# FractionateEverything、GetDspData、SaveDataExporter、UXAEnhance 或共享基础设施发生变化时：
# 完整构建解决方案，再运行自动打包、发布和 qqbot 压缩包发送流程
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  MLJ_DSPmods.sln \
  /t:Build /p:Configuration=Debug
cd "/mnt/d/project/dsp/MLJ_DSPmods/AfterBuildEvent/bin/Debug"
./AfterBuildEvent.exe 1

# 发布单个模组的修正：把模式 1 限制为指定项目，避免重新打包、复制到 R2
# 或发送其他发布目标。
./AfterBuildEvent.exe 1 UXAEnhance

# 仅 VanillaCurveSim 变化时：单独构建并运行 Debug 版本
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  VanillaCurveSim/VanillaCurveSim.csproj \
  /t:Build /p:Configuration=Debug
"/mnt/c/Windows/System32/cmd.exe" /c \
  "D:\project\dsp\MLJ_DSPmods\VanillaCurveSim\bin\Debug\VanillaCurveSim.exe"

```

**Verification entry points:** this repository does not use a root `tests/` suite. Do not add or run project-level Python tests for MLJ_DSPmods unless the user explicitly reinstates them. Use code review, structural probes, the required Windows build, and the packaging/publish flow as the release quality gate:
- Expected build result: `Build succeeded. 0 Warning(s). 0 Error(s).`
- For any `FractionateEverything` / `GetDspData` / `SaveDataExporter` / `UXAEnhance` / shared infrastructure change, after the verified code is committed, accepted, and merged back into the target branch, run the Debug solution build in the main Windows-mounted checkout, then run `AfterBuildEvent.exe 1` from `AfterBuildEvent\bin\Debug`. Expected behavior: copy built mod files to R2, create zip packages under `ModZips`, skip QQ upload locally when configured package content is unchanged, otherwise notify qqbot through the generic local `publish-local` admin API and let qqbot run its own content-hash guard before group delivery. A no-change run should finish successfully without opening Explorer or launching Dyson Sphere Program. The final Codex reply must include the build command/result, AfterBuildEvent command/result, generated zip file paths, R2 copy status, qqbot delivery status or local skip status, the uploaded commit hash when an upload occurred, and the commit body used as the publish message.
- For `VanillaCurveSim`-only changes, build `VanillaCurveSim/VanillaCurveSim.csproj` and run `VanillaCurveSim.exe` directly.

## Key Files

| File | Purpose |
|---|---|
| `MLJ_DSPmods.sln` | Solution entry point |
| `FractionateEverything/FractionateEverything.csproj` | Main mod project (net472, LangVersion latest) |
| `GetDspData/GetDspData.csproj` | DSP data export tool; depends on `FractionateEverything` |
| `SaveDataExporter/SaveDataExporter.csproj` | Runtime save statistics exporter mod |
| `UXAEnhance/UXAEnhance.csproj` | Runtime UXAssist add-on; depends on `UXAssist` |
| `VanillaCurveSim/VanillaCurveSim.csproj` | Standalone simulator EXE; can build/run without `AfterBuildEvent` |
| `DefaultPath.props` / `DefaultPath.props.example` | Game library path config (copy example, fill paths) |
| `ExternalModReferences.generated.props` | Local generated external R2 mod reference paths; created by `AfterBuildEvent.exe 6`, gitignored |
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

## Game Design Documentation Boundary

- `FractionateEverything/GAME_DESIGN.md` is the FE author/AI-facing game-design source of truth. It records stable experience goals, player decisions, system relationships, mechanism contracts, responsibility boundaries, and red lines; it is not a marketing summary, numeric implementation spec, or audit report.
- `FractionateEverything/GAME_DESIGN_SPEC.md` is the matching executable game-design specification. Keep formulas, probabilities, thresholds, units, applicability, trigger timing, settlement order, state fields, persistence/network semantics, UI operations, and necessary code entry points there.
- The two files must use the same numbered chapter names and order. `GAME_DESIGN_SPEC.md` refines `GAME_DESIGN.md` but never overrides it. If a detailed rule changes a system's purpose, player decision, responsibility boundary, or invariant, update `GAME_DESIGN.md` first, then the matching `GAME_DESIGN_SPEC.md` chapter and code.
- Confirmed parameters have one source of truth in `GAME_DESIGN_SPEC.md`; do not duplicate numeric tables in `GAME_DESIGN.md`. Structural counts that define the design itself, such as four tower types, may remain in both when needed for comprehension.
- Do not add candidate designs, unconfirmed balance, implementation gaps, audit reasoning, build/publish commands, validation logs, commit history, AI workflow instructions, task plans, or chat-derived status reports to either game-design document.
- AI workflow and repository rules belong in this `AGENTS.md` or the relevant domain `AGENTS.md`; user-facing installation or public behavior belongs in `README.md`; temporary reasoning belongs in `.codex/drafts/` or `.codex/plans/`.
- When updating FE game design, preserve the direction -> mechanism contract -> executable specification hierarchy and remove obsolete or duplicated rules instead of accumulating historical alternatives.

## Project Structure

```
FractionateEverything/src/
├── FractionateEverything.cs    # BepInEx plugin entry point (Awake, config, Harmony)
├── Lifecycle/                  # Feature bootstrap and save/load dispatch
├── Compatibility/              # Per-mod detection + integration → Compatibility/AGENTS.md
│   ├── LegacySave/             # FE 2.x physical Item/Model/Recipe reference migration (vanilla containers only)
│   ├── Mods/                   # General external mod integrations
│   └── Nebula/                 # Multiplayer packets and sync hooks
├── Logic/                      # → Logic/AGENTS.md
│   ├── Civilization/           # Ancient civilization: analysis, protocols, recovery, tech tree, achievements, dark fog, tutorial
│   ├── DataCenter/             # Data centre inventory + package access patches
│   ├── Enhancements/           # Stacking and vanilla recipe adjustment (Stacking/, VanillaRecipes/)
│   ├── EnginePatches/          # Standalone engine/data loading transpilers
│   ├── Fractionation/          # Recipes, runtime, tower definitions and presentation
│   ├── Items/                  # Item prototypes/value/matrix helpers + item presentation patches
│   └── Station/                # Data-centre station interaction and station UI patches
├── UI/                         # → UI/AGENTS.md
│   ├── Controls/               # Reusable widgets → Controls/AGENTS.md
│   ├── Foundation/             # Window lifecycle and RectTransform helpers
│   ├── Layout/                 # Grid DSL and layout runtime
│   ├── Patches/Common/         # UI-specific common control patches
│   └── MainPanel/              # FE main panel pages → MainPanel/AGENTS.md
│       ├── MainWindow.cs       # Dual-panel lifecycle hub (MessageBox + Analysis)
│       ├── MainWindowPageRegistry.cs # Page registry + category filtering
│       ├── Archive/            # Recipe gallery and development diary
│       ├── Civilization/       # Overview, protocol recovery and ancient tech tree
│       ├── CoreOperate/        # Fractionation and vanilla recipe operations
│       ├── ProgressTask/       # Recovery guide and achievements
│       ├── ResourceInteraction/ # Data-centre item interaction
│       ├── Setting/            # Miscellaneous settings
│       ├── Shell/              # MessageBox and Analysis panel shells
│       └── Theme/              # Shared page framing and visual assets
└── Utils/                      # Shared low-level utilities and proto IDs → Utils/AGENTS.md
    ├── ProtoID.cs              # ALL proto ID constants (I/R/M/T prefix + IFE/RFE/MFE/TFE for mod)
    └── PackageUtils.cs         # Package/data-centre shared flags and translations
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
| Namespaces | `FE.*` (root `FE`) | `FE.Logic.Fractionation.Fractionators`, `FE.Utils` |

## Import Organization

```csharp
// 1. System 命名空间
using System;
using System.Collections.Generic;
using System.IO;

// 2. 第三方与游戏库，组内按字母顺序排列
using BepInEx;
using HarmonyLib;
using UnityEngine;

// 3. FE 内部命名空间，组内按字母顺序排列
using FE.Compatibility;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Station;

// 4. 静态导入放在最后
using static FE.FractionateEverything;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;
using static FE.Utils.Utils;
```

## Code Style

**Indentation:** 4 spaces (no tabs)  
**Braces:** Same-line opening brace (K&R style): `public static class Foo {`  
**Line endings:** CRLF (Windows) — maintained by `.gitattributes`  
**Comments:** 所有自然语言代码注释、XML 文档正文和区域标题统一使用简体中文；API 名、标识符、路径、URL 与格式关键字保留原拼写。  
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
[IFE分馏塔定向原胚], [2], [IFE转化塔], [5]   // 推荐写法，不使用 new int[] { }
public static readonly List<ProductOutputInfo> emptyOutputs = [];
```

### String Interpolation (preferred over concatenation)
```csharp
public string TypeName => $"{RecipeType.GetName()}-{LDB.items.Select(InputID).name} +{Level}";
```

### XML Documentation
```csharp
/// <summary>
/// 简要说明成员的用途。
/// </summary>
public static void SomeMethod() { ... }
```

## DSP/Unity-Specific Patterns

### Static Building Class Template
Every tower definition in `Logic/Fractionation/Fractionators/Definitions/` follows this structure:
```csharp
namespace FE.Logic.Fractionation.Fractionators;

public static class ConversionTower {
    private static ItemProto item;
    private static RecipeProto recipe;
    private static ModelProto model;
    public static Color color = new(0.7f, 0.6f, 0.8f);

    public static int Level = 0;
    public static bool EnableFluidEnhancement => Level >= 3;
    public static int MaxStack => Level switch { < 6 => 1, _ => 4 };

    public static void AddTranslations() { ... }
    public static void Create() { ... }        // 注册物品、配方和模型
    public static void SetMaterial() { ... }   // 应用材质与颜色
    public static void UpdateHpAndEnergy() { ... }
}
```

### Harmony Patches
```csharp
// 后置补丁，最常用
[HarmonyPostfix]
[HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) { }

// IL 转译器，使用 CodeMatcher
[HarmonyTranspiler]
[HarmonyPatch(typeof(ModelProtoSet), nameof(ModelProtoSet.OnAfterDeserialize))]
public static IEnumerable<CodeInstruction> SomeClass_Method_Transpiler(
    IEnumerable<CodeInstruction> instructions) {
    var matcher = new CodeMatcher(instructions);
    // ... 修改 IL
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

### Adding a New Tower
1. Create `Logic/Fractionation/Fractionators/Definitions/NewTowerName.cs` following the static class template above
2. Add `Level`, `EnableXxx`, switch-expression properties as needed
3. Implement `AddTranslations()`, `Create()`, `SetMaterial()`, `UpdateHpAndEnergy()`
4. Register in `Logic/Fractionation/Fractionators/FractionatorManager.cs`
5. Add Proto IDs to `Utils/ProtoID.cs`

### Adding a New Recipe Type
1. Create `Logic/Fractionation/Recipes/Definitions/NewRecipe.cs` inheriting from `BaseRecipe`
2. Override `RecipeType` (returns an `ERecipe` value)
3. Override `GetOutputs()` with the distribution logic
4. Add a static `CreateAll()` method and call it from `Logic/Fractionation/Recipes/RecipeManager.cs`

### Modifying Game Logic
1. Check if an existing manager/patch covers the target method
2. Prefer adding to an existing feature-domain patch class under `Logic/*` or to `Logic/EnginePatches/` for standalone engine/data loading transpilers
3. Use `[HarmonyPostfix]` by default; use `[HarmonyTranspiler]` only when postfix is insufficient
4. Place patches as static methods directly inside the relevant manager class when cohesive

## Git Practices

- Commit messages in **Chinese**, using this format: `<前缀>[可选作用域][!]: <描述>`
- Supported prefixes:
  - `功能：` 新增能力、机制、命令、页面或入口
  - `调整：` 有意改变既有行为，且不是 bug fix 或纯新增
  - `修复：` 修正崩溃、丢状态、错误路径、错误文案、兼容问题或其他错误行为
  - `优化：` 性能、内存、加载速度、构建速度、包体积或查询次数改善，行为原则上不变
  - `重构：` 内部结构调整，外部行为不变
  - `构建：` 构建系统、输出路径、打包、发布、依赖、CI、工具链或自动化流程
  - `文档：` README、AGENTS、设计说明、长期规则、教程文案等纯文本变更
  - `测试：` 仅测试用例、测试脚本、测试数据、夹具或回归守卫变更
  - `杂项：` 格式化、忽略文件整理等小维护；低频使用，不作为垃圾桶
- Optional scope goes in parentheses when it improves one-line log scanning, for example `修复(UI): ...` or `构建(deps): ...`; scope describes the module or domain, not the change type.
- Use `!` for breaking changes, for example `调整!: ...` or `功能(API)!: ...`; the commit body must explain impact and migration.
- Commit body/details should record `原因：`, `实现：`, `验证：`, and `影响：` when the title alone is not enough.
- Atomic commits are one complete functional or behavioral change; code, tests, config, and docs for the same change should usually stay in one commit.
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
- Subdirectory `AGENTS.md` files exist for major FE domains such as `Logic/Civilization/`, `Logic/Fractionation/`, `Logic/Station/`, `Logic/DataCenter/`, `Logic/Items/`, `Logic/Enhancements/`, `UI/`, `UI/Controls/`, `UI/MainPanel/`, `Compatibility/`, `Utils/`, and `AfterBuildEvent/src/`
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
4. **Always verify build with the correct scope after merging back to the main Windows-mounted checkout** — `FractionateEverything` / `GetDspData` / `SaveDataExporter` / `UXAEnhance` / shared infrastructure changes must build `MLJ_DSPmods.sln`, ensure `0 Error(s)`, then run `AfterBuildEvent.exe 1` and confirm qqbot zip delivery; `VanillaCurveSim`-only changes may build `VanillaCurveSim.csproj` and run `VanillaCurveSim.exe`
5. **LangVersion is `latest`** — use C# 12 features (collection expressions `[]`, primary constructors, etc.)

---

*Last updated: March 2026 | Target: net472 | C# latest | BepInEx 5.4.17*
