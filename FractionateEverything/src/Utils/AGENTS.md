# Utils — 共享底层工具

`Utils` 只放跨域可复用、无业务状态的底层工具。新增功能状态、Harmony patch、存档块不要放这里。

## Files

- `ProtoID.cs`：所有原版和 FE 新增 item/recipe/model/tech ID 常量。
- `PackageUtils.cs`：数据中心/背包访问共享 flag、翻译入口，以及原版同名 `split_inc` 增产点拆分 helper。
- `I18NUtils.cs`：翻译注册、`Translate()`、常用位置 helper。
- `RichTextUtils.cs`：富文本颜色/字号 helper。
- `FormatUtils.cs`：百分比、名称等格式化。
- `PatchImpl.cs`：Harmony patch 开关基础设施。
- `SaveUtils.cs`：`WriteBlocks/ReadBlocks` 标签化存档 API。
- `LogUtils.cs`：BepInEx/Unity 日志封装。
- `RandomUtils.cs`：随机数 helper。
- `GridIndexUtils.cs`：GridIndex 坐标转换。

## Moved Out

- 数据中心库存和背包访问：`Logic/DataCenter`。
- 物品访问重定向 patch：`Logic/DataCenter/Patches`。
- 增产点池：`Logic/Station/ProliferatorPool.cs`。
- UI 矩形布局 helper：`UI/Foundation/RectTransformUtils.cs`。

## Proto ID Naming

```
I铁块        = 1101
R精炼铁      = ...
M电弧熔炉    = ...
IFE转化塔    = ...
RFE分馏配方  = ...
MFE转化塔    = ...
TFE分馏数据中心 = ...
```

新增 mod 物品时，把 ID 追加到 `ProtoID.cs` 对应 `IFE/RFE/MFE/TFE` 分组。
