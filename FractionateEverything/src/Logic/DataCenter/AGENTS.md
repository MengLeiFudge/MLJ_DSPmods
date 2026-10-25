# Logic/DataCenter — 分馏亚空间域

本域负责宇宙共享亚空间的真实库存、单物品容量、手动背包访问，以及把原版物品查询/扣取重定向到亚空间的补丁。`DataCenter` 是稳定内部名称；玩家可见名称统一为“分馏亚空间”。

## Structure

```
DataCenter/
├── DataCenterInventory.cs      # long 数量/增产点、容量、部分接收、奖励超容、存档
├── SubspaceExpansion.cs        # 全局矩阵供料、永久容量倍率、按秒结算和扩容子块
├── DataCenterUploadRouter.cs   # 实体上传、基础塔整批预检
├── PlayerInventoryAccess.cs    # 玩家背包 add/take/count 和提示
├── PackageAccessRules.cs       # 通信门禁、建筑师/沙盒临时背包边界
└── Patches/                    # 计数、扣取、临时背包、排序和丢弃回收重定向
```

## Rules

- `centerItemCount` / `centerItemInc` 是库存唯一真源，长度和元素类型保持 `long[12000]`。
- 每种物品独立按统一价值计算基础容量，建筑保留按堆叠计算的底线；已注册物品读取Items启动时完成的统一正估值。SubspaceExpansion拥有六档独立永久增量、供料及累计溢出价值；同档多塔不叠加速率，不同档同时生效。终极仓储仍受数量与增产点安全界限约束。
- Expansion子块随DataCenter存档、联机快照与切档清理；仅主机结算游戏秒，停供不缩水、不补算离线时间。库存锁可进入扩容锁，扩容锁内不得反向查询库存或探索。
- `AddExplorationReward` 允许超容量，只供首遗物和探索奖励调用。
- 实体上传统一走 `DataCenterUploadRouter`；高级塔按原物品、数量和增产点进入普通库存，并在同类库存首次达到 1000 座时通知研究域永久开放方向；献祭只在基础解析塔发生。
- StorageSource区分手动、基础塔、有限/无限交互站。手动允许超存实体；基础塔和无限上传的超额物料不可逆折为扩容价值；有限上传同时受容量与设置目标约束。StorageReceipt保证入库/折算/拒收及增产点守恒，只有实际入库触发取得事件。
- 基础塔正面上传在同一库存锁内检查整堆、取货和分配去向；按统一最终估值结算实体与溢出折算，未注册或非法价值不能折算。可上传判断用ItemManager按ID索引表。
- 持有库存锁时只用 `AddStoredItem` 写库存；研究通知和手动入库的联机包由 `NotifyItemAdded` / `OnItemCountIncreased` 在释放锁后处理。
- 下载按真实数量拆分增产点；增产点始终满足非负且不超过数量的 10 倍。
- 实际库存变化设置 `snapshotDirty`，由主机文明快照广播；UI 刷新不得直接改数组。
- 通信门禁使用 `CivilizationRecoveryManager.HasDataCenterCommunication`。
- 所有玩家物品消耗（包括任务及兼容模组）通过`PlayerInventoryAccess`查询和扣除，遵守`ModSettings.TakeItemPriority`。联机重放复用`TakeItemFromInventories`，已核销额度只重放不重复扣除；不得在业务层另写顺序或直接扣单一库存。玩家包裹访问只放 `PlayerInventoryAccess`；库存重定向 patch 只做边界接线，不复制容量或业务判断。
