# Logic/EnginePatches — 引擎级补丁

这里只放难以归属到单一功能域的独立游戏引擎/数据加载 transpiler。

## Files

- `LDBToolPatch.cs`：LDBTool 数据加载相关 patch。
- `ModelLoadingPatch.cs`：模型加载相关 patch。

## Rules

- 能归属到分馏、站点、物品、进度等功能域的 patch，应放到对应功能域。
- 新增 transpiler 前先确认 postfix 或现有路径无法满足。
