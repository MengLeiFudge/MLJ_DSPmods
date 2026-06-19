# AfterBuildEvent/src — Build Automation Tool

Console app. Run from IDE as post-build event or standalone. The tool is split by execution mode and
supporting workflow so `AfterBuildEvent.cs` remains the entry/router instead of owning every workflow.

## Files

| File | Lines | Role |
|---|---|---|
| `AfterBuildEvent.cs` | entry/router + shared nested models/constants | `Main` mode selection and cross-workflow shared data types |
| `Utils.cs` | 195 | Mod management helpers (combination math, r2 enable/disable) |
| `CmdProcess.cs` | 75 | Persistent cmd.exe process wrapper |
| `PathConfig.cs` | path config | All path constants, auto-detects latest nuget version, reads external mod source/cache paths |
| `Publishing/` | mode 1 publish/package workflow | R2 copy, zip packaging, package metadata, qqbot `publish-local` request |
| `LibraryUpdate/` | mode 2 DLL workflow + mode 6 external refs | Publicize/decompile game DLLs and installed R2 mod DLLs; generate cache-backed compile reference props |
| `CalcJson/` | mode 3 JSON workflow | Calculator raw JSON generation, cache signature, `gameData.ts` version sync |
| `CalcIcons/` | mode 4 icon workflow + mode 3 icon tail | Required icon discovery, offline/game icon export, asset sync |
| `DspCalcQuickUpdate/` | calculator quick update | Mode 5: source-version audit, `gameData.ts` update, raw JSON filename copy |

`AfterBuildEvent` is implemented as a `static partial class` across these files. Keep new mode-specific
methods in the owning folder instead of adding them back to `AfterBuildEvent.cs`. Cross-workflow helpers
may stay in the folder that currently owns the related behavior, but if another feature starts depending on
the same helper, prefer moving that helper into a small shared `Core/` file rather than creating duplicate
copies.

## Modes

| Option | Method | What it does |
|---|---|---|
| `1` | `UpdateModsThenStart()` | Kill DSP → copy DLLs to R2 → zip packages → launch game |
| `2` | `UpdateLibDll()` | Publicize + decompile game DLLs → scan/decompile R2 mod DLLs |
| `3` | `GetAllCalcJson()` | Enumerate all mod combos → launch game per combo → collect JSON export |
| `4` | `ExportCalcIcons()` | Rebuild calculator icons from current raw data |
| `5` | `CalcQuickUpdateRunner.Run()` | Check all configured calculator mods and quick-update versions/raw JSON filenames when source audit passes |
| `6` | `GenerateExternalModReferencesProps()` | Read profile `mods.yml` versions → resolve external mod DLLs from R2 cache → write `ExternalModReferences.generated.props` |

Mode `5` intentionally waits for Enter before returning so the user can read the audit result and copied
file list. It processes all configured calculator mods by default; an optional second argv may narrow the
run to one mod for debugging. Git network sync in mode `5` uses each mod's configured pull remote and pull
branch explicitly, instead of relying on the source repository's current branch upstream. This matters for
forked dependencies such as OrbitalRing, where the calculator version source is upstream rather than the
local fork. The sync still uses a short timeout; after the first remote timeout in a run, the remaining mods
skip remote sync and continue with local source inspection. This keeps offline quick-update checks from
spending tens of seconds per mod waiting for GitHub. Modes `1`, `3`, and `4` keep their existing completion
behavior.

Interactive usage reads the mode from stdin. An empty stdin is treated as option `1`. When a user manually chooses option `1`, the tool must still call the local qqbot admin API and dispatch the configured zip files before asking whether to launch Dyson Sphere Program.

Required publish usage passes mode `1` as argv. The latest commit body is the publish message source; Codex must commit the verified code with a body that explains why this build exists, what changed, how it was changed, and which verification commands passed. This automated publish flow applies to manual/local agent work and qqbot/Codex automation work; do not use the old no-argument interactive mode as publish completion.

```bash
./AfterBuildEvent.exe 1
# Optional single-project correction publish:
./AfterBuildEvent.exe 1 UXAEnhance
```

Mode `1` accepts optional project names after the mode argument. When present, only those projects are
packaged, copied to R2, and offered to qqbot. Use this for single-mod correction publishes so unrelated
`PublishTargets` are not resent.

Agent publish order is mandatory:
1. implement the change
2. run the required verification commands
3. commit the verified code
4. after the worktree is accepted and merged back into the Windows-mounted target branch, build the solution
5. run `AfterBuildEvent.exe 1`; the tool reads the current branch, HEAD, commit subject, and commit body from the merged checkout

Do not run `AfterBuildEvent.exe 1` before the commit. The qqbot publish message reads the latest git commit from the repository; running before commit will publish the previous commit title even if the built DLL already contains local changes.

In automation mode, option `1` keeps the packaging/R2 sync behavior but changes the user-facing side effects:
- copy built mod files to the R2 profile
- create zip packages under `ModZips`
- before creating a package, delete only the current target zip path for the same project/version; do not clear other versions from `ModZips`
- build a JSON request in memory and post it to qqbot's localhost-only `/admin/api/artifacts/publish-local`
- include `timestamp`, `project_id`, current `branch`, current `commit_hash`, current `commit_subject`, current `commit_detail`, and a `files` array
- publish only zip files explicitly listed by `PublishTargets`; qqbot must not hard-code which MLJ_DSPmods packages are publishable
- each file entry includes path, upload name, SHA256, and target QQ groups
- `sha256` is the zip file integrity checksum; `content_sha256` is the stable hash of zip entry names, sizes, and bytes, and qqbot uses it to skip unchanged package contents
- qqbot skips delete/upload/message when the same target group and upload name already published the same `content_sha256`; otherwise it deletes only bot-uploaded files with the exact same name in the target group before uploading the new file
- successful publish logs must report qqbot's returned `uploaded`, `deleted`, and `skipped` counts; do not use the request file count as the actual pushed count
- if upload succeeds, do not open Explorer
- if qqbot is unavailable or upload fails, open Explorer at `ModZips` so the package is still visible, report the real failure, and do not claim the package was delivered
- do not ask whether to launch Dyson Sphere Program
- do not launch Dyson Sphere Program

Codex final replies for publish runs must include the `AfterBuildEvent.exe 1` command result, generated zip paths, R2 copy status, and whether local qqbot package publishing succeeded or fell back to opening `ModZips`.

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

## Option 6 — External Compile References

Mode `6` generates the local `ExternalModReferences.generated.props` file used by
`FractionateEverything.csproj` for external mod compile references.

Source of truth:
- `ProfileDir\mods.yml` selects the package and version currently targeted by the developer profile.
- `R2CacheDir\<package>\<version>\...` provides the actual DLL file, independent of whether that mod is enabled in the profile.

This avoids using profile plugin files like `DSP_Battle.dll.old` as the primary compile reference. The generated props file is machine-local and gitignored. Run `AfterBuildEvent.exe 6` again after updating external mod versions in R2. Keep the profile plugin `.dll/.dll.old` lookup only as fallback behavior in project files.

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
| `R2CacheDir` | From `DefaultPath.props` or r2modman local cache default |
| `NugetGameLibNet45Dir` | Auto-scanned: latest subdirectory of nuget gamelibs by `LastWriteTime` |
| `SolutionDir` | resolved by walking upward from `AppContext.BaseDirectory` until `MLJ_DSPmods.sln` is found |
| `PublicizerExe` | `lib\BepInEx.AssemblyPublicizer.Cli.exe` |

Path overrides live in `DefaultPath.props` (gitignored). Copy from `DefaultPath.props.example`.

## Project Output Layout

`Directory.Build.props` fixes project output to `bin\<Configuration>` without OS or target-framework suffixes.
It also keeps `IncludeSourceRevisionInInformationalVersion=false` so `AssemblyInformationalVersion` stays at the
declared package version instead of appending the current Git revision. Do not re-enable Git revision embedding:
mode `1` compares package contents, and a full solution build would otherwise make unrelated project DLLs change
solely because `HEAD` changed.
AfterBuildEvent must use that layout when copying local project DLLs:

```
<ProjectName>\bin\Debug\<ProjectName>.dll
<ProjectName>\bin\Release\<ProjectName>.dll
```

Do not reintroduce `bin\win\<Configuration>` or `bin\<Configuration>\net472` in AfterBuildEvent paths.
`AfterBuildEvent.csproj` should keep `ProjectReference` entries with `ReferenceOutputAssembly="false"` for every
local mod/tool project it packages or uses for calculator export, and those referenced projects should remain
`OutputType=Library`.

## Deterministic Zip Rule

Mode `1` package zips must be deterministic for unchanged contents. `ZipMod()` sorts entries by file name and
uses a fixed zip entry timestamp so rerunning `AfterBuildEvent.exe 1` does not change zip SHA256 solely because
the command ran at a different time. Do not reintroduce current-time entry metadata into package zips.
