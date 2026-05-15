# AfterBuildEvent/src — Build Automation Tool

Console app. Run from IDE as post-build event or standalone. The tool is now split between the legacy
`AfterBuildEvent.cs` entry/large workflows and focused subfolders such as `DspCalcQuickUpdate/`.

## Files

| File | Lines | Role |
|---|---|---|
| `AfterBuildEvent.cs` | large legacy workflow file | `Main` entry, packaging, DLL update, calculator JSON/icon workflows |
| `Utils.cs` | 195 | Mod management helpers (combination math, r2 enable/disable) |
| `CmdProcess.cs` | 75 | Persistent cmd.exe process wrapper |
| `PathConfig.cs` | path config | All path constants, auto-detects latest nuget version, reads external mod source paths |
| `DspCalcQuickUpdate/` | calculator quick update | Mode 5: source-version audit, `gameData.ts` update, raw JSON filename copy |

## Modes

| Option | Method | What it does |
|---|---|---|
| `1` | `UpdateModsThenStart()` | Kill DSP → copy DLLs to R2 → zip packages → launch game |
| `2` | `UpdateLibDll()` | Publicize + decompile game DLLs → scan/decompile R2 mod DLLs |
| `3` | `GetAllCalcJson()` | Enumerate all mod combos → launch game per combo → collect JSON export |
| `4` | `ExportCalcIcons()` | Rebuild calculator icons from current raw data |
| `5` | `CalcQuickUpdateRunner.Run()` | Check all configured calculator mods and quick-update versions/raw JSON filenames when source audit passes |

Mode `5` intentionally waits for Enter before returning so the user can read the audit result and copied
file list. It processes all configured calculator mods by default; an optional second argv may narrow the
run to one mod for debugging. Git network sync in mode `5` uses a short timeout; after the first remote
timeout in a run, the remaining mods skip remote sync and continue with local source inspection. This keeps
offline quick-update checks from spending tens of seconds per mod waiting for GitHub. Modes `1`, `3`, and
`4` keep their existing completion behavior.

Interactive usage reads the mode from stdin. An empty stdin is treated as option `1`.

qqbot/Codex automation usage passes the mode as argv. Codex must pass a fresh publish summary as additional argv, or via `AFTERBUILD_PUBLISH_SUMMARY`. The summary is not optional for Codex automation because the fallback text is generic and does not tell the QQ group what changed.

```bash
./AfterBuildEvent.exe 1
./AfterBuildEvent.exe 1 "原因：用户反馈启动崩溃
修复：避免 ProcessManager 静态初始化读取未就绪字段
方式：使用固定建筑类型数量替代跨 partial 字段长度"
```

Codex automation order is mandatory:
1. implement the change
2. run the required verification commands
3. commit the verified code
4. run `AfterBuildEvent.exe 1` with a publish summary that describes that exact commit

Do not run `AfterBuildEvent.exe 1` before the commit. The qqbot publish message reads the latest git commit from the repository; running before commit will publish the previous commit title even if the built DLL already contains local changes.

In automation mode, option `1` keeps the packaging/R2 sync behavior but changes the user-facing side effects:
- copy built mod files to the R2 profile
- create zip packages under `ModZips`
- write generated package paths to `ModZips/afterbuild-result.json`
- include a concise publish summary in `afterbuild-result.json`; the summary should explain why this build exists, what was fixed or changed, how it was fixed, and which verification commands passed
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
