# Logic/DataCenter — 数据中心域

数据中心域负责 FE 全局物品库存、玩家背包访问和把原版物品消耗/查询重定向到数据中心的 patch。

## Structure

```
DataCenter/
├── DataCenterInventory.cs      # centerItemCount/Inc、手动上传/提取统计、原子扣除、存档
├── DataCenterUploadRouter.cs   # 实体上传边界：Frag/Memory 包裹规范化、普通库存、塔型注册
├── PlayerInventoryAccess.cs    # 玩家背包 add/take/count、TakeItemWithTip
├── PackageAccessRules.cs       # ArchitectMode、科技开关、货币包裹上传/下载边界
└── Patches/                    # 物品计数/提取/建造/制造/排序等数据中心访问 patch
```

## Rules

- 数据中心库存状态只放 `DataCenterInventory`；Frag/Memory 基础槽是双货币唯一余额真源。
- 玩家、培养塔、物流站和兼容模组发起的实体上传统一调用 `DataCenterUploadRouter`；内部奖励和联机库存同步仍直接写库存。
- Frag/Memory 的 10/100/1000 包裹上传时按面额折算并丢弃增产点，永不占用自身 `centerItemCount` 槽。
- 协议解锁等多物品扣款使用 `TryConsumeItemsAtomic`：先聚合预检，再同一锁内一次扣除，失败零改动。
- 玩家包裹和物流包裹访问只放 `PlayerInventoryAccess`。
- `Patches` 只放让原版/外部系统从数据中心读取、写入或统计物品的 patch。
- 纯 UI 防御、战斗显示修复、引擎安全兜底不放 `DataCenter/Patches`，应归属到对应功能域或 `EnginePatches`。
- `Utils/PackageUtils.cs` 只保留共享 flag 和翻译入口。
