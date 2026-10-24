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
- 结构化存档使用 `SaveUtils`，不要为新状态自行拼接无标签字节；稳定块 owner 仍在业务域。3.x 顶层版本遵循[仓库存档规则](../../../AGENTS.md#fe-存档兼容边界强制)。

## 保存块的独立增删

- 每项可独立移除的数据使用独立、稳定且在同级唯一的标签；由 `WriteBlocks` 统一写入块数量、标签、长度和内容，由 `ReadBlocks` 按标签选择处理器。不得依赖块顺序、邻块字节数或邻块存在来确定读取位置。
- 多个可独立增删的状态放入同一业务块时，继续使用嵌套子块；`WriteList` / `ReadList` 保存集合数量并为每个元素设置标签与长度边界，复杂元素内部仍须用命名子块隔离字段。数组按存储数量和稳定身份恢复，不按本地数组长度消费字节。字典的键和值可作为一个不可拆分的记录，但多个字典不得直接顺序拼接。
- 改名用读取端 `Field("CurrentName", reader => ..., "OldName", "OlderName")` 注册历史别名，再交给 `ReadBlocks`；写入端只写当前名。当前名优先，其次按别名声明顺序选择，同一字段只执行一次；跨字段名称或别名冲突属于代码错误。别名只适用于含义与载荷相同的字段，不用于伪装类型变化。
- `WriteBlocks` 同级字段可以换序；`ReadBlocks` 按当前注册顺序调用处理器，不按磁盘顺序。原始 `Write` / `Read` 序列、复合键及向量仍有固定内部布局，不能直接交换其中两行。读取器的业务依赖须由业务层处理，不能把字节顺序无关误认为任意执行顺序都无关。
- 块缺失的默认值与切档清理由所属业务负责；删除某块后，其余块仍须能够读取、校验和完成运行状态同步，不能沿用上一存档的残留状态。
- 移除数据时可以删除写入和读取注册，旧存档中的未知块由 `ReadBlocks` 按长度跳过，不保留无意义的占位读取器。已有标签不得改作其他数据含义。
- 新增或修改保存逻辑时，静态核对缺块、未知块、块顺序变化和单块读取失败四条路径，确认不会使其余块错位或阻断处理。
