# AGENTS.md - MLJ_DSPmods Development Guide

This document provides essential information for AI agents working on this Dyson Sphere Program mod repository branch.

## Project Overview

Multiple DSP mods/tools in one solution:
- **FractionateEverything** (`FE` namespace) — Main mod
- **GetDspData** — Dev tool for DSP data export
- **AfterBuildEvent** — Build automation and post-build packaging/publicizing

## Build Commands

**Build tool rule:** All compilation must use the local Windows environment. From WSL, run the Windows MSBuild executable at `/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe`, but only from the Windows-mounted repository path that maps to `D:\project\csharp\DSP MOD\MLJ_DSPmods`. Do not compile from Linux-home worktrees such as `/home/mlj/.codex/worktrees/...`, because Windows tools see those as `\\wsl.localhost\...` paths and can fail to launch generated EXEs.

**Output path rule:** Debug build output is fixed to `bin\Debug`. Do not introduce OS-specific output folders, and do not allow target-framework suffixes in the output path.

**Build scope rule:** Build scope depends on the project that changed:
- If any file under `FractionateEverything/` or `GetDspData/` changes, build the full solution `MLJ_DSPmods.sln`.
- If shared build infrastructure changes, including `AfterBuildEvent/`, `Directory.Build.props`, `DefaultPath.props*`, or `MLJ_DSPmods.sln`, also build the full solution `MLJ_DSPmods.sln`.

**Worktree build rule:** Code changes may be edited, tested with non-Windows structural checks, and committed inside a Codex worktree, but Windows compilation and all EXE launches must wait until the worktree branch is merged back into the target branch in the main Windows-mounted checkout. Do not start `AfterBuildEvent.exe` from a worktree.

**Packaging and publish rule:** `FractionateEverything` and `GetDspData` are packaging-dependent projects. After the verified worktree change is committed, accepted, merged back into the target branch, and the Debug solution build succeeds in the main Windows-mounted checkout, always run `AfterBuildEvent.exe 1` from `AfterBuildEvent\bin\Debug`. This is required for both manual/local work and qqbot/Codex automation work.
- The latest commit body is the publish message source. Include the user-visible reason, the fix/change, the implementation path, the verification evidence, and the impact in the commit body before running `AfterBuildEvent.exe 1`.
- Running `AfterBuildEvent.exe 1` is not enough by itself. The required completion state is: built mod files copied to R2, zip packages created under `ModZips`, qqbot notified through the generic local `publish-local` admin API, and every configured zip delivered to its target QQ group.
- `AfterBuildEvent` owns the publish target list. To publish another mod or group, edit the tool-side publish target configuration; do not add MLJ_DSPmods-specific rules to qqbot.
- If the user manually starts `AfterBuildEvent.exe` and selects option `1`, the tool must still notify qqbot and dispatch the configured zip files before any optional game launch prompt.
- A publish run with qqbot upload failure is not complete. Report the real qqbot/HTTP error and keep the package paths visible for manual recovery instead of claiming the change was dispatched.

**Publish launch rule:** Required packaging/publish verification uses the automated command form from WSL in the main Windows-mounted checkout: change to `AfterBuildEvent\bin\Debug`, and run `./AfterBuildEvent.exe 1`. Do not wrap the automated publish command in `powershell.exe`, do not wait for stdin, and do not use the old interactive no-argument launch for publish completion.

```bash
# Run these only after the worktree branch has been merged into the target branch
# in the main Windows-mounted checkout.

# FractionateEverything / GetDspData / shared infrastructure change:
# Debug build the full solution, then run the automated publish + qqbot zip delivery flow
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  MLJ_DSPmods.sln \
  /t:Build /p:Configuration=Debug
cd "/mnt/d/project/csharp/DSP MOD/MLJ_DSPmods/AfterBuildEvent/bin/Debug"
./AfterBuildEvent.exe 1
```

**Verification entry points:** root `tests/` contains lightweight Python structural checks. Run targeted Python tests when touching covered behavior, then use build verification as the release quality gate:
- Expected build result: `Build succeeded. 0 Warning(s). 0 Error(s).`
- For any `FractionateEverything` / `GetDspData` / shared infrastructure change, after the verified code is committed, accepted, and merged back into the target branch, run the Debug solution build in the main Windows-mounted checkout, then run `AfterBuildEvent.exe 1` from `AfterBuildEvent\bin\Debug`. Expected behavior: copy built mod files to R2, create zip packages under `ModZips`, notify qqbot through the generic local `publish-local` admin API, and deliver every configured zip to its target QQ group without opening Explorer or launching Dyson Sphere Program on success. The final Codex reply must include the build command/result, AfterBuildEvent command/result, generated zip file paths, R2 copy status, qqbot delivery status, the uploaded commit hash, and the commit body used as the publish message.

## Key Files

| File | Purpose |
|---|---|
| `MLJ_DSPmods.sln` | Solution entry point |
| `Directory.Build.props` | Shared target framework, build output, and language settings |
| `FractionateEverything/FractionateEverything.csproj` | Main mod project |
| `GetDspData/GetDspData.csproj` | DSP data export tool; depends on `FractionateEverything` |
| `AfterBuildEvent/AfterBuildEvent.csproj` | Post-build automation EXE |
| `DefaultPath.props` / `DefaultPath.props.example` | Game library path config |
| `lib/` | Custom binaries kept in-repo |

**Build notes:**
- Target framework: `net472` (Unity/.NET Framework compatibility)
- `AllowUnsafeBlocks: true`, `LangVersion: latest`
- Game libraries are publicized for mod access

## Project Structure

```
FractionateEverything/src/
├── Compatibility/
├── Logic/
│   ├── Building/
│   ├── Manager/
│   ├── Patches/
│   └── Recipe/
├── UI/
│   ├── Components/
│   ├── Patches/
│   └── View/
└── Utils/
```

## Git Practices

- Commit messages use simplified Chinese conventional style, such as `功能：`, `修复：`, `重构：`, `杂项：`, `文档：`, `构建：`.
- Atomic commits are required.
- Do not push unless the user explicitly approves it.
- Git operations must be serialized; do not run concurrent `git add`, `git commit`, `git merge`, `git rebase`, `git stash`, or `git checkout`.

## Critical Pitfalls

1. **Never modify `BaseRecipe.GetOutputs` directly** — it is shared; subclass instead.
2. **Avoid new Harmony patches** when existing code paths suffice.
3. **Always verify build with the correct scope after merging back to the main Windows-mounted checkout** — `FractionateEverything` / `GetDspData` / shared infrastructure changes must build `MLJ_DSPmods.sln`, ensure `0 Error(s)`, then run `AfterBuildEvent.exe 1` and confirm qqbot zip delivery.
