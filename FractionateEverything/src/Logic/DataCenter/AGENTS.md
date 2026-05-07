# Logic/DataCenter — 数据中心域

数据中心域负责 FE 全局物品库存、玩家背包访问和把原版物品消耗/查询重定向到数据中心的 patch。

## Structure

```
DataCenter/
├── DataCenterInventory.cs      # centerItemCount/Inc、手动上传/提取统计、存档
├── PlayerInventoryAccess.cs    # 玩家背包 add/take/count、TakeItemWithTip
├── PackageAccessRules.cs       # ArchitectMode、科技开关、访问判断
└── Patches/                    # 物品计数/提取/建造/制造/排序等数据中心访问 patch
```

## Rules

- 数据中心库存状态只放 `DataCenterInventory`。
- 玩家包裹和物流包裹访问只放 `PlayerInventoryAccess`。
- `Patches` 只放让原版/外部系统从数据中心读取、写入或统计物品的 patch。
- 纯 UI 防御、战斗显示修复、引擎安全兜底不放 `DataCenter/Patches`，应归属到对应功能域或 `EnginePatches`。
- `Utils/PackageUtils.cs` 只保留共享 flag 和翻译入口。
