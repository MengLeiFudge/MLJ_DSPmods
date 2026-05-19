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

**Packaging rule:** `FractionateEverything` and `GetDspData` are packaging-dependent projects. After the worktree change is merged back into the target branch and the Debug solution build succeeds in the main Windows-mounted checkout, start `AfterBuildEvent.exe`.
- Manual/local interactive work: start `AfterBuildEvent.exe` without arguments in `wt.exe` from `AfterBuildEvent\bin\Debug`, and do not send follow-up input. The user may choose a mode manually or close it directly.
- qqbot/Codex automation work: first make the verified atomic git commit and merge it back to the target branch, then run `AfterBuildEvent.exe 1` from `AfterBuildEvent\bin\Debug` in the main Windows-mounted checkout. This selects option 1 automatically, but must not open Explorer or launch the game. The command must carry a fresh publish summary through `AFTERBUILD_PUBLISH_SUMMARY` or extra argv; do not rely on the default summary. The summary must match the commit being uploaded and include the user-visible reason, the fix/change, the implementation path, and the verification evidence.

**Launch style rule:** Do not launch `AfterBuildEvent.exe` as a bare console process, and do not wrap it inside `powershell.exe`. On this Windows 11 machine, the closest match to the user's real double-click experience is to let `wt.exe` host `AfterBuildEvent.exe` directly, with the working directory set to the corresponding build output folder. The expected effect is: window title shows the `AfterBuildEvent.exe` path, and the content starts directly with the program's own prompt text, without any PowerShell banner.

```bash
# Run these only after the worktree branch has been merged into the target branch
# in the main Windows-mounted checkout.

# FractionateEverything / GetDspData / shared infrastructure change:
# Debug build the full solution, then start the post-build tool in Windows Terminal hosting the EXE directly
"/mnt/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  MLJ_DSPmods.sln \
  /t:Build /p:Configuration=Debug
wt.exe -d "D:\project\csharp\DSP MOD\MLJ_DSPmods\AfterBuildEvent\bin\Debug" \
  "D:\project\csharp\DSP MOD\MLJ_DSPmods\AfterBuildEvent\bin\Debug\AfterBuildEvent.exe"

# qqbot/Codex automation after Debug build:
cd "/mnt/d/project/csharp/DSP MOD/MLJ_DSPmods/AfterBuildEvent/bin/Debug"
AFTERBUILD_PUBLISH_SUMMARY="原因：用户反馈 xxx
修复：xxx
方式：xxx
验证：MSBuild 0 warning 0 error；AfterBuildEvent.exe 1 成功" ./AfterBuildEvent.exe 1
```

**Verification entry points:** root `tests/` contains lightweight Python structural checks. Run targeted Python tests when touching covered behavior, then use build verification as the release quality gate:
- Expected build result: `Build succeeded. 0 Warning(s). 0 Error(s).`
- For manual `FractionateEverything` / `GetDspData` / shared infrastructure changes, first merge the worktree branch back into the target branch in the main Windows-mounted checkout, then run the solution-level local `MSBuild.exe` command above before marking work complete, then start `AfterBuildEvent.exe` in `wt.exe` as the directly hosted command, and do not auto-select any mode.
- For qqbot/Codex automation changes, after the verified code is committed and merged back into the target branch, run the Debug solution build in the main Windows-mounted checkout, then run `AfterBuildEvent.exe 1` from `AfterBuildEvent\bin\Debug` with a non-empty publish summary. Expected behavior: copy built mod files to R2, create zip packages, write `ModZips/afterbuild-result.json`, do not open Explorer, and do not launch Dyson Sphere Program.

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
3. **Always verify build with the correct scope after merging back to the main Windows-mounted checkout** — `FractionateEverything` / `GetDspData` / shared infrastructure changes must build `MLJ_DSPmods.sln`, ensure `0 Error(s)`, then run `AfterBuildEvent.exe`.
