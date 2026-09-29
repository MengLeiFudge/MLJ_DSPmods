# Compatibility — 外部模组与旧引用兼容

兼容层负责外部模组检测、专用补丁、Nebula 传输和 FE 2.x 物理 Proto 引用迁移。它不拥有 FE 研究、探索、亚空间或高级塔业务状态。

## Structure

```
Compatibility/
├── CheckPlugins.cs     # 依赖声明、启用标记和启动说明
├── LegacySave/         # FE 2.x 原版持久容器物理引用迁移
├── Mods/               # 每个外部模组的专用兼容
└── Nebula/             # packet、processor、主机快照和动作请求
```

## Detection

- `CheckPlugins.Check()` 在启动阶段一次性设置各兼容类 `Enable` 标记。
- 业务热路径读取标记，不重复查询 `Chainloader.PluginInfos`。
- 普通第三方兼容每个模组集中在一个文件；不要把 FE 专用规则写入外部模组或 qqbot。

## Legacy Save

- `LegacyProtoMigration` 只处理原版持久容器中 FE 2.x Item/Model/Recipe 的物理 ID 引用。
- 迁移门禁是 FE 存档版本大于 0 且小于 10；版本 10 不执行 2.x 迁移，保存为 11 后不重复。
- FE 旧自有协议、成就、塔等级、科技点和库存语义不映射为当前状态。
- 旧常量可作为同一物理 ID 的源码别名，但不能重新注册旧塔、旧物品或旧玩法入口。

## Nebula

- 主机统一结算研究、探索、亚空间与高级塔；客户端只提交受限请求并应用主机状态。
- 文明快照包含 `Civilization`、`Recipes`、`DataCenter` 和 `Process`，并同步全局堆叠。
- 高级塔快照包含多缓存、食物、营养和选定配方；主机每秒广播，并在玩家加入时补发。
- 探索请求只携带 `markId`；主机从连接对应 `IPlayerData.UPosition` 读取宇宙坐标。
- `BuildingChangePacket` 类型 2 只表示高级塔稳定配方 ID；校验失败不得转发。
- 新 packet 字段和块名视为联机合同，修改时同时考虑旧客户端拒绝、版本一致性和中途加入。

## Mod Boundaries

- Genesis Book / Orbital Ring 可调整科技位置、原型语义和教程解析，但不改变五塔与成长真源。
- They Come From Void 兼容只处理外部模组接口；FE 自身 Frag/原胚掉落归 `Logic/Civilization/FragDropPatch.cs`。
- PackageLogistic、UXAssist 等只通过现有亚空间/背包边界接线，不直接写文明状态。
