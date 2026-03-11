# AfterBuildEvent/src — Build Automation Tool

Console app. Run from IDE as post-build event or standalone. 4 files, ~820 lines.

## Files

| File | Lines | Role |
|---|---|---|
| `AfterBuildEvent.cs` | 483 | `Main` entry, 3 feature implementations |
| `Utils.cs` | 195 | Mod management helpers (combination math, r2 enable/disable) |
| `CmdProcess.cs` | 75 | Persistent cmd.exe process wrapper |
| `PathConfig.cs` | 66 | All path constants, auto-detects latest nuget version |

## Three Modes (console prompt 1/2/3)

| Option | Method | What it does |
|---|---|---|
| `1` | `UpdateModsThenStart()` | Kill DSP → copy DLLs to R2 → zip packages → launch game |
| `2` | `UpdateLibDll()` | Publicize game DLLs → decompile Assembly-CSharp → publicize mod DLLs |
| `3` | `GetAllCalcJson()` | Enumerate all mod combos → launch game per combo → collect JSON export |

## Option 2 — UpdateLibDll Detail

```
PublizeDll(DSPACDll → NugetGameLibNet45Dir\Assembly-CSharp.dll)
    → DecompileAcDll(ilspycmd -p --nested-directories → gamedata/DecompiledSource/)
PublizeDll(DSPUIDll → NugetGameLibNet45Dir\UnityEngine.UI.dll)
PublizeDll(R2VDDll  → lib\DSP_Battle-publicized.dll)
PublizeDll(R2GBDll  → lib\ProjectGenesis-publicized.dll)
PublizeDll(R2ORDll  → lib\ProjectOrbitalRing-publicized.dll)
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
