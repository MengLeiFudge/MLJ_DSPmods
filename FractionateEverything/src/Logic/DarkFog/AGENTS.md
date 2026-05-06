# Logic/DarkFog — 黑雾域

黑雾域负责 FE 自己的黑雾分支、战斗进度和黑雾相关存档状态。

## Files

- `DarkFogBranchManager.cs`：黑雾路线/分支相关逻辑。
- `DarkFogCombatManager.cs`：黑雾战斗进度、存档、切档清理。

## Rules

- 第三方 They Come From Void 兼容代码放 `Compatibility/DarkFog`。
- FE 自己的黑雾进度状态放本目录，并接入 `FeatureSaveRegistry`。
