# Logic/DataCenter — 分馏亚空间域

本域负责宇宙共享亚空间的真实库存、单物品容量、手动背包访问，以及把原版物品查询/扣取重定向到亚空间的补丁。`DataCenter` 是稳定内部名称；玩家可见名称统一为“分馏亚空间”。

## Structure

```
DataCenter/
├── DataCenterInventory.cs      # long 数量/增产点、容量、部分接收、奖励超容、存档
├── DataCenterUploadRouter.cs   # 实体上传、基础塔整批预检、旧包裹规范化
├── PlayerInventoryAccess.cs    # 玩家背包 add/take/count 和提示
├── PackageAccessRules.cs       # 通信门禁、建筑师/沙盒临时背包边界
└── Patches/                    # 计数、扣取、临时背包、排序和丢弃回收重定向
```

## Rules

- `centerItemCount` / `centerItemInc` 是库存唯一真源，长度和元素类型保持 `long[12000]`。
- 每种物品容量独立计算；普通 `AddItemToModData` 只返回实际接收量，调用方必须保留或返还未接收部分。
- `AddExplorationReward` 允许超容量，只供首遗物和探索奖励调用。
- 实体上传统一走 `DataCenterUploadRouter`；高级塔按原物品、数量和增产点进入普通库存，并在同类库存首次达到 1000 座时通知研究域永久开放方向；献祭只在基础解析塔发生。
- 基础塔正面上传必须在取带尾货物前检查真实整堆可接收，不拆分、不先取后丢。
- 下载按真实数量拆分增产点；增产点始终满足非负且不超过数量的 10 倍。
- 实际库存变化设置 `snapshotDirty`，由主机文明快照广播；UI 刷新不得直接改数组。
- 通信门禁使用 `CivilizationRecoveryManager.HasDataCenterCommunication`。
- 旧 Frag/Memory 包裹 ID 只用于导入历史误入槽位时规范化，不是正式物品或上传协议。
- 玩家包裹访问只放 `PlayerInventoryAccess`；库存重定向 patch 只做边界接线，不复制容量或业务判断。
