# Logic/Buildings — 建筑聚合与迁移域

本目录负责跨功能域聚合 FE 建筑原型注册、材质/能耗刷新、固定运行属性、建筑实例状态存档和 FE 2.x Proto 迁移。具体塔定义仍归各自功能域。

## Structure

```
Buildings/
├── BuildingManager.cs       # 跨域注册、固定属性、缓存上限、实例状态存档聚合
└── Migration/
    └── LegacyProtoMigration.cs # FE 2.x 现役 Proto ID 和持久状态迁移
```

## Definition Ownership

- `Logic/Fractionation/Fractionators/InteractionTower.cs`：交互塔原型。
- `Logic/Fractionation/Fractionators/RectificationTower.cs`：解析塔原型。
- `Logic/Fractionation/Fractionators/MineralReplicationTower.cs`：资源塔原型。
- `Logic/Fractionation/Fractionators/ConversionTower.cs`：转化塔原型。
- `Logic/Station/Definitions/PlanetaryInteractionStation.cs`：行星内物流交互站原型。
- `Logic/Station/Definitions/InterstellarInteractionStation.cs`：星际物流交互站原型。

## Registration Flow

`BuildingManager.AddTranslations()` 聚合各建筑翻译。
`BuildingManager.AddFractionators()` 聚合各建筑 `Create()`。
`BuildingManager.SetFractionatorMaterial()` 聚合各建筑 `SetMaterial()`。
`BuildingManager.UpdateHpAndEnergy()` 聚合各建筑 `UpdateHpAndEnergy()`。

## Rules

- 建筑不再拥有等级、经验、突破、献祭、裂变池或共鸣状态；塔型能力来自文明科技投影、协议状态或明确的单塔实例状态。
- 新增建筑实例状态必须接入 `BuildingManager.Import/Export/IntoOtherSave`，并评估复制粘贴、蓝图和联机同步。
- FE 2.x 迁移只映射现役对应物，不把废弃内容映射为新内容。
- `BuildingManager` 只做跨域聚合和通用固定属性，不把分馏热路径或物流站运行逻辑搬入本目录。

## Anti-Patterns

- 为了集中注册而把具体塔定义移入 `Logic/Buildings`，破坏功能域所有权。
- 在建筑定义里处理主面板 UI；主面板页面应进 `UI/MainPanel`。
- 恢复旧建筑成长字段或用隐藏等级替代文明科技许可。
