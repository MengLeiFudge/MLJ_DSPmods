# Logic — FE 业务逻辑层

`Logic` 承载 FE 的生产规则、状态与游戏补丁；UI 只读取快照和发起明确操作，不拥有业务真源。

## Domain Map

```
Logic/
├── Civilization/   # 具体内容探索、共享解析槽、成长科技、首遗物和跨存档完成标记
├── DataCenter/     # 分馏亚空间库存、容量、背包访问和库存重定向
├── Enhancements/   # 全局堆叠与原版配方耗时投影
├── EnginePatches/  # 独立引擎/数据加载安全补丁
├── Fractionation/  # 五塔原型、基础/高级运行、稳定配方和塔窗口表现
├── Items/          # Frag、Memory、通用原胚、价值与物品表现
└── Station/        # 物流交互站定义、槽位模式、传输和窗口补丁
```

## Ownership

- `Civilization` 决定具体内容由哪个物品承载、解析是否完成、探索点是否存在及成长科技效果；它可读取配方目录并向亚空间发放奖励。
- `Fractionation` 决定塔如何收料、结算、输出、保存实例状态；它只通过 Civilization 的公开查询/通知接口读取研究和成长状态。
- `DataCenter` 是亚空间数量与增产点的唯一真源；任何实体上传都通过 `DataCenterUploadRouter`。
- `Station` 只决定物流槽如何访问亚空间，不复制库存状态。
- `Items` 负责原型与静态分类，不拥有研究、探索或库存进度。
- `Enhancements` 把已完成成长科技投影到原版系统，不建立第二套科技状态。

## Lifecycle

- 原型和翻译由 `FeatureBootstrap` 按现有加载阶段注册。
- 存档顺序只由 `Lifecycle/FeatureSaveRegistry.cs` 维护。
- 新存档、载入和切档必须经过各域 `IntoOtherSave`，不得依赖静态字段自然归零。
- 跨域 tick 由 `CivilizationModule.Tick` 聚合；高频分馏路径仍归塔运行器。

## Rules

- 持久化身份使用稳定块名、Proto ID、配方 Stable/Persistent ID 和固定枚举值，不使用类名或显示文本。
- Nebula 主机执行研究、探索、亚空间和高级塔结算；客户端状态是主机快照投影。
- 不把协议、成就、远古科技点、定向原胚、主路锁定或副产物弃置重新放入生产路径。
- 新 Harmony patch 先确认没有现有 owner 或公开入口可扩展；表现 patch 归对应域的 `Presentation` 或窗口文件。
- 跨域调用不得在持有一个域的锁时进入另一个会反向调用的域；需要时记录待办状态并在主线程锁外执行。
