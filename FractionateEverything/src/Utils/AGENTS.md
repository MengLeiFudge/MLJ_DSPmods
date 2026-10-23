# Utils — 共享底层工具

`Utils` 只放跨域可复用、无业务状态的底层工具与稳定 Proto ID 常量。功能状态、Harmony 业务 patch 和存档 owner 不放这里。

## Files

- `ProtoID.cs`：原版与 FE Item/Recipe/Model/Tech 稳定 ID。
- `SaveUtils.cs`：`WriteBlocks` / `ReadBlocks` 标签化存档和集合读写。
- `PackageUtils.cs`：背包/亚空间访问共享 flag、翻译入口和增产点拆分 helper。
- `I18NUtils.cs`：翻译注册、`Translate()` 和位置 helper。
- `RichTextUtils.cs` / `FormatUtils.cs`：显示格式 helper。
- `PatchImpl.cs`：Harmony patch 开关基础设施。
- `LogUtils.cs`、`RandomUtils.cs`、`GridIndexUtils.cs`：日志、随机和坐标工具。

## Proto ID Rules

- 原版常量使用 `I/R/M/T` 前缀；FE 原型使用 `IFE/RFE/MFE/TFE`。
- 五塔活动身份、两座基础塔配方、物流交互站和成长科技区间都在 `ProtoID.cs` 维护。
- 旧中文常量只可映射到相同数字作为源码兼容别名；不得分配新数字或用别名重新注册 Proto。
- 稳定数字进入存档、蓝图或网络后不得因命名优化改变。
- 新增 ID 前检查原版、FE 和已支持大型模组区间冲突；不为历史3.0存档保留废弃常量或注册占位原型。

## Utility Boundaries

- 亚空间库存与容量：`Logic/DataCenter`。
- 研究、探索、成长科技：`Logic/Civilization`。
- 高级塔状态与配方身份：`Logic/Fractionation`。
- UI 矩形布局：`UI/Foundation` / `UI/Layout`。
- 结构化存档使用 `SaveUtils`，不要为新状态自行拼接无标签字节；稳定块 owner 仍在业务域。
