# Logic/DataCenter — 数据中心域

数据中心域负责 FE 全局物品库存、玩家背包访问和把原版物品消耗/查询重定向到数据中心的 patch。

## Structure

```
DataCenter/
├── DataCenterInventory.cs      # centerItemCount/Inc、手动上传/提取统计、存档
├── PlayerInventoryAccess.cs    # 玩家背包 add/take/count、TakeItemWithTip
├── PackageAccessRules.cs       # ArchitectMode、科技开关、访问判断
└── Patches/                    # 物品计数/提取/建造/制造/排序/弹药安全 patch
```

## Rules

- 数据中心库存状态只放 `DataCenterInventory`。
- 玩家包裹和物流包裹访问只放 `PlayerInventoryAccess`。
- Harmony patch 按场景放 `Patches`，不要再放回 `Utils`。
- `Utils/PackageUtils.cs` 只保留共享 flag 和翻译入口。
