# AfterBuildEvent/src — Build Automation Tool

Console app. Run from IDE as post-build event or standalone. 4 files, ~826 lines.

## Files

| File | Lines | Role |
|---|---|---|
| `AfterBuildEvent.cs` | 490 | `Main` entry, 3 feature implementations |
| `Utils.cs` | 195 | Mod management helpers (combination math, r2 enable/disable) |
| `CmdProcess.cs` | 75 | Persistent cmd.exe process wrapper |
| `PathConfig.cs` | 66 | All path constants, auto-detects latest nuget version |

## Modes

| Option | Method | What it does |
|---|---|---|
| `1` | `UpdateModsThenStart()` | Kill DSP → copy DLLs to R2 → zip packages → launch game |
| `2` | `UpdateLibDll()` | Publicize + decompile game DLLs → scan/decompile R2 mod DLLs |
| `3` | `GetAllCalcJson()` | Enumerate all mod combos → launch game per combo → collect JSON export |

Interactive usage reads the mode from stdin. An empty stdin is treated as option `1`.

qqbot/Codex automation usage passes the mode as argv. Codex may pass an optional publish summary as additional argv, or via `AFTERBUILD_PUBLISH_SUMMARY`:

```bash
./AfterBuildEvent.exe 1
./AfterBuildEvent.exe 1 "原因：用户反馈启动崩溃
修复：避免 ProcessManager 静态初始化读取未就绪字段
方式：使用固定建筑类型数量替代跨 partial 字段长度"
```

In automation mode, option `1` keeps the packaging/R2 sync behavior but changes the user-facing side effects:
- copy built mod files to the R2 profile
- create zip packages under `ModZips`
- write generated package paths to `ModZips/afterbuild-result.json`
- include a concise publish summary in `afterbuild-result.json` when provided; the summary should explain why this build exists, what was fixed or changed, and how it was fixed
- push `afterbuild-result.json` to the local qqbot admin API, which publishes only `FractionateEverything_*.zip` to QQ group `319567534`
- qqbot should use the provided summary as the group message content, instead of file-level diff statistics
- qqbot deletes old bot-uploaded `FractionateEverything_*.zip` group files before uploading the new package
- if upload succeeds, do not open Explorer
- if qqbot is unavailable or upload fails, open Explorer at `ModZips` so the package is still visible
- do not ask whether to launch Dyson Sphere Program
- do not launch Dyson Sphere Program

Codex final replies for automation runs must include the `AfterBuildEvent.exe 1` command result, generated zip paths, and whether local qqbot FE package publishing succeeded or fell back to opening `ModZips`.

## Option 2 — UpdateLibDll Detail

```
# Game DLLs (from game install → nuget → decompile)
PublizeDll(DSPACDll → NugetGameLibNet45Dir\Assembly-CSharp.dll)
    → DecompileDll(ilspycmd → gamedata/DecompiledSource/Assembly-CSharp/)
PublizeDll(DSPUIDll → NugetGameLibNet45Dir\UnityEngine.UI.dll)
    → DecompileDll(ilspycmd → gamedata/DecompiledSource/UnityEngine.UI/)

# Mod DLLs (from CheckPlugins soft dependencies → mods.yml → R2 plugins → decompile)
Parse `FractionateEverything/src/Compatibility/CheckPlugins.cs`
    → collect `[BepInDependency(..., SoftDependency)]`
Read `mods.yml`
    → confirm the user actually installed the package through R2
Inspect `BepInEx/plugins/<package>/`
    → find the primary mod DLL (`.dll` or `.dll.old`, skipping companion libs)
DecompileDll(ilspycmd → gamedata/DecompiledSource/<AssemblyName>/)
```

Requires `ilspycmd` globally installed: `dotnet tool install -g ilspycmd`

## CmdProcess — Async cmd.exe Wrapper

`Exec(string)` writes to a **shared persistent cmd.exe stdin** — it does NOT wait for completion.
Completion is detected by polling for expected output files:
```csharp
while (!File.Exists(expectedFile)) { Thread.Sleep(100); }
```
`Dispose()` sends `exit` and blocks until process exits — all queued commands complete.

## PathConfig — Key Properties

| Property | Value |
|---|---|
| `DSPGameDir` | From `DefaultPath.props` or hardcoded default |
| `NugetGameLibNet45Dir` | Auto-scanned: latest subdirectory of nuget gamelibs by `LastWriteTime` |
| `SolutionDir` | `@"..\..\..\..\"` (relative from bin output) |
| `PublicizerExe` | `lib\BepInEx.AssemblyPublicizer.Cli.exe` |

Path overrides live in `DefaultPath.props` (gitignored). Copy from `DefaultPath.props.example`.
