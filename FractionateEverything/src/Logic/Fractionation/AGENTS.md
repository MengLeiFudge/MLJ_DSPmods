# Logic/Fractionation — 五塔与配方域

本域拥有五座分馏塔、基础/高级运行路径、稳定配方目录、实例持久化、蓝图配方选择和分馏塔窗口表现。

## Structure

```
Fractionation/
├── Fractionators/
│   ├── Definitions/                 # 两座基础塔、三座高级塔原型
│   ├── FractionatorTowerCatalog.cs  # 五塔稳定目录与 ERecipe 映射
│   ├── FractionatorRuntime.cs       # 基础塔单输入、输出和正面上传
│   ├── AdvancedFractionatorRuntime.cs # 多输入/输出、食物、营养、耐久、状态
│   ├── AdvancedRecipeSelectionSync.cs # 复制粘贴、蓝图和联机配方 ID
│   ├── LivingTowerPatches.cs        # 拆除/损毁、维修和运行接线
│   ├── FractionatorManager.cs       # 原型入口与 Building 存档聚合
│   └── Presentation/                # 原版分馏窗口与 brief info
└── Recipes/
    ├── Definitions/BaseRecipe.cs
    ├── Definitions/WorldviewRecipe.cs
    ├── Models/                       # ERecipe、输出模型
    ├── RecipeManager.cs              # 目录、索引、稳定 ID、统计存档
    └── RecipeRuntime.cs              # 基础配方可用性投影
```

## Stable Identities

- 活动塔目录固定为基础矿物、基础解析、高级矿物、高级交互、高级转化，共 5 项。
- `ERecipe` 数值保持：高级交互 1、基础解析 2、基础矿物 3、高级转化 4、高级矿物 5。
- 物理 Item/Model/Recipe ID、`WorldviewRecipe.StableId` / `PersistentId`、蓝图 magic/version 和存档块名不得因源码重命名改变。
- 旧中文塔常量只能作为同 ID 兼容别名，不能注册旧塔或建立第二套目录。

## Runtime Rules

- 基础塔单输入自动识别；输入、流动输出、产物缓存均为 20 件，不随全局堆叠放大。
- 基础塔按带尾真实 stack 做整批准入；失败物进入流动输出，`DestroyRatio` 为 0。
- 高级塔先验证已研究且塔型匹配的稳定配方 ID，再接收多输入和矩阵食物。
- 高级每种输入、食物、产物和流动物缓存上限为 20；优先食物、两轮底料、流出、多余输入。
- 高级结算采用 `floor(expected + U)` 的批量期望取整；基础成功率乘以同种塔献祭加成，可执行轮数随真实输入货物数量增长，普通输入失败回流，低概率输入只在成功轮结算。
- 旧/无关物最高排空优先；正常流动物与产物等待当前全局满堆，输入耗尽后释放尾料；多个产物按实际积压轮转。
- 高级塔有营养时全速运行；六档矩阵速度为 `1.0..1.5`，每等效轮消耗 0.02 营养，待机每秒消耗 0.002。营养为零时按耐久比例降速并持续损坏。不耗电、不使用 `hpIncoming`，原版维修不能恢复生命塔。
- 基础解析配方只为有研究池的物品、Frag 和高级塔建立；高级塔只有在库存门槛已开放且有候选时解析，否则献祭并以 2% 成功率产出 Memory。
- 主动拆除返还所有真实字典缓存；损毁只移除状态。原版单槽镜像只供显示，不能用来返还。

## State And UI

- `AdvancedFractionatorRuntime` 是高级塔缓存、营养和选定配方的唯一真源；原版字段只镜像当前配方第一输入/第一产物。
- 导入将外部字节视为不可信，限制状态数、字典项、ID、数量、营养和增产点。
- 蓝图/复制粘贴只保存选定配方 ID，不复制缓存或营养。
- 高级窗口只增加配方选择与当前营养；不得恢复主路锁定、副产物弃置或制造台式多缓存面板。
- 表现代码不得直接写研究状态；选择操作统一走 `AdvancedRecipeSelectionSync`。
