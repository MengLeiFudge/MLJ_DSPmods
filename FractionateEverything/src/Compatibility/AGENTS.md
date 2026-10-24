# Compatibility — 外部模组兼容

兼容层负责外部模组检测、专用补丁与Nebula传输，不拥有研究、探索、亚空间或高级塔业务状态。

## Structure

```
Compatibility/
├── CheckPlugins.cs     # 依赖声明、启用标记和启动说明
├── LegacySave/         # FE 2.2 原版持久容器物理引用转换
├── Mods/               # 每个外部模组的专用兼容
└── Nebula/             # packet、processor、主机快照和动作请求
```

## Detection

- `CheckPlugins.Check()` 在启动阶段一次性设置各兼容类 `Enable` 标记。
- 业务热路径读取标记，不重复查询 `Chainloader.PluginInfos`。
- 普通第三方兼容每个模组集中在一个文件；不要把 FE 专用规则写入外部模组或 qqbot。

## 存档边界

- 顶层保存版本固定为10；增删业务块与字段沿SaveUtils的标签和长度隔离。LegacyProtoMigration显式注册并只将格式1/2识别为2.2，转换原版容器中的物理引用。历史3.0不按顶层版本拒绝，恢复可读取的块，未知块跳过，失败块记录警告并隔离；不恢复废弃玩法或补偿旧进度。
- 2.2无当前对应物的实体由原版移除流程清理，不恢复旧原型与业务进度；外部模组兼容不受此存档边界影响。

## Nebula

- 主机统一结算研究、探索、亚空间与高级塔；客户端只提交受限请求并应用主机状态。
- 文明快照包含 `Civilization` 和 `DataCenter`，并同步全局堆叠；不读写废弃的配方统计块。
- 高级塔快照包含多缓存、食物、营养和选定配方；主机每秒只广播变化实例与删除身份，玩家加入时补发全量。营养每帧消耗只在显示精度 0.1 变化或归零时置脏，避免增量退化为全量。客户端原位更新实例，全量才移除未出现的身份；状态字段复用存档序列，存档格式不因增量包改变。
- 探索认领与终极源点交付请求只携带 `markId`；主机从连接对应 `IPlayerData` 读取位置与星球，再按统一空间距离与剩余需求结算；不采信FE请求中的位置声明。
- `ExplorationRecoveryPacket` 只传已经结算的点位类型、内容身份、物品/剧情编号、数量与质量；客户端校验后按本端语言生成标题和正文，不依赖快照先到，不从提示包发物品。
- `BuildingChangePacket` 类型 2 只表示高级塔稳定配方 ID；校验失败不得转发。
- 新 packet 字段和块名视为联机合同，修改时同时考虑旧客户端拒绝、版本一致性和中途加入。

## Mod Boundaries

- Genesis Book / Orbital Ring 可调整科技位置、原型语义和教程解析，但不改变五塔与成长真源。
- They Come From Void 兼容只处理外部模组接口；FE 自身 Frag/原胚掉落归 `Logic/Civilization/FragDropPatch.cs`。
- PackageLogistic、UXAssist 等只通过现有亚空间/背包边界接线，不直接写文明状态。
