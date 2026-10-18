# Logic/Fractionation — 五塔与配方域

本域拥有五座分馏塔、基础/高级运行路径、稳定配方目录、实例持久化、蓝图配方选择和分馏塔窗口表现。

## Structure

```
Fractionation/
├── Fractionators/
│   ├── Definitions/                 # 两座基础塔、三座高级塔原型
│   ├── FractionatorTowerCatalog.cs  # 五塔稳定目录与 ERecipe 映射
│   ├── FractionatorRuntime.cs       # 基础塔单输入、输出、正面上传与共享参数
│   ├── VanillaFractionatorOutput.cs # 原版塔流动物和产物补放
│   ├── FractionatorOutputState.cs   # 基础塔扩展缓存、存档与拆除清理
│   ├── FractionatorBlueprintParameters.cs # 共用蓝图状态块读写
│   ├── AdvancedFractionatorRuntime.cs # 多输入/输出、食物、营养、耐久、状态
│   ├── AdvancedFractionatorPersistence.cs # 高级塔存档及全量/增量快照
│   ├── AdvancedRecipeSelectionSync.cs # 复制粘贴、蓝图和联机配方 ID
│   ├── LivingTowerPatches.cs        # 拆除/损毁、维修和运行接线
│   ├── FractionatorManager.cs       # 原型入口与 Building 存档聚合
│   └── Presentation/                # 原版分馏窗口与 brief info
└── Recipes/
    ├── Definitions/BaseRecipe.cs
    ├── Definitions/WorldviewRecipe.cs
    ├── Models/                       # ERecipe、输出模型
    ├── RecipeManager.cs              # 目录、索引、稳定 ID、已解析配方缓存
    └── RecipeRuntime.cs              # 基础配方可用性投影
```

## Stable Identities

- 活动塔目录固定为基础矿物、基础解析、高级矿物、高级交互、高级转化，共 5 项。
- `ERecipe` 数值保持：高级交互 1、基础解析 2、基础矿物 3、高级转化 4、高级矿物 5。
- 物理 Item/Model/Recipe ID、`WorldviewRecipe.StableId` / `PersistentId`、蓝图 magic/version 和存档块名不得因源码重命名改变。
- 旧中文塔常量只能作为同 ID 兼容别名，不能注册旧塔或建立第二套目录。

## Runtime Rules

- 基础塔输入使用 `ProcessManager.BufferCapacity`（20×当前全局堆叠件），高级主料使用40×当前全局堆叠件；货物计数参与速度，不替代这两条准入策略。
- 所有塔的产物与流动输出阈值统一读取 `ProcessManager.OutputCapacity`（5×当前全局堆叠件）。按本批开始前数量准入，整批结果允许越过阈值；禁止以最坏产量预留空间或按单轮产量扩大阈值。
- 原版分馏塔输入缓存保持 `FractionatorManager.VanillaFluidInputCargoMax`（40）货物堆；更新后由 `ProcessManager.TopUpVanillaOutputs` 把流动输出和产物叠到输出带头部。已放置塔的原版缓存字段由 `FractionatorManager.SetFractionatorCacheSize` 与 `FactorySystem.Import` 补丁同步。
- 基础塔按带尾真实stack做整批准入；基础矿物失败回流，解析按研究域的价值概率区分成功、损毁和流回，具体公式见GAME_DESIGN_SPEC。
- 高级塔先验证已研究且塔型匹配的稳定配方 ID，再接收多输入和矩阵食物。
- 主料与辅料使用40×当前集装层数件准入阈值；消耗概率低于10%的催化剂至少保留5件或一轮需求。备用食物硬容量5件，消化物与备用同种，二者均空才可切换。配方矩阵优先作为反应物，仅空腹且无备用时先取一件点火。旧档按消化剩余比例迁移，异种备用空间不足时保留待排。
- 旧/无关物优先排出；产物达到阈值时停止反应，将现有及新到普通输入改走流动输出，保留数量、增产点和真实货物计数。正常流动物与产物等待当前全局满堆，缺料或堵塞时释放尾料，残留催化剂不能阻止输出；多个产物按实际积压轮转。
- 高级结算采用 `floor(expected + U)` 的批量期望取整；基础成功率乘以共享Memory分钟献祭加成。可执行轮数随真实输入货物数量增长；扣任何主料前，用所有辅料实际库存与特质后的消耗概率限制成功数，再统一结算回流、产物和营养，不以截断扣料掩盖欠账。
- 高级塔由当前消化矩阵提供速度与能量，不耗电；`MatrixNutrition` 根据统一价值建立食物目录，接纳 Matrix 类型及黑雾矩阵5201。成功轮按真实主料件数耗能，待机仍耗能，断粮按耐久降速并损坏。具体公式见GAME_DESIGN_SPEC第3章。不使用 `hpIncoming`，原版维修不能恢复生命塔。
- 基础解析配方为显式研究样本、Memory、Frag、高级塔和六种主线矩阵建立；矩阵先转入DataCenter的扩容供料，未接收部分带原增产点回流，不再同时进入样本研究。塔体有已探索配方/特质时先解析，无候选且献祭已开放时才以2%成功率产出Memory。培育用原胚，繁育用对应塔体，不通过输入输出推测样本。
- 主动拆除返还所有真实字典缓存；损毁只移除状态。原版单槽镜像只供显示，不能用来返还。

## State And UI

- `AdvancedFractionatorRuntime` 是高级塔缓存、营养和选定配方的唯一真源；原版字段只镜像当前配方第一输入/第一产物。
- 导入将外部字节视为不可信，限制状态数、字典项、ID、数量、营养和增产点。
- 蓝图/复制粘贴只保存选定配方 ID，不复制缓存或营养。
- 高级窗口固定几何，展示配方/速率、食物单格和输入/流出/产物真实缓存；零库存配方项保留位置，额外残料独立滚动。禁止恢复主路锁定、副产物弃置；不在窗口提供详细接线动作提示。
- 表现代码不得直接写研究状态；选择操作统一走 `AdvancedRecipeSelectionSync`。
