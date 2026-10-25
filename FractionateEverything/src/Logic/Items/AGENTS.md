# Logic/Items — 物品域

本域负责 FE 物品原型、物品价值、矩阵阶段分类、基础塔上传候选，以及物品选择器/图标相关表现补丁。

## Structure

```
Items/
├── ItemManager.cs    # Frag、Memory、通用原胚、统一价值、矩阵和needs
├── MineralValues.cs  # 主题矿脉、气体及海洋的自然来源估值，不生成银河
├── MatrixNutrition.cs # 统一价值驱动的矩阵食物目录、营养与速度
├── ItemAvailability.cs # 最终原型的合法来源闭包及外部来源声明
└── Presentation/     # 图标集、物品/信号选择器与标签选择器 patch
```

## Rules

- 当前核心物品只有 Frag、Memory 和通用原胚；五座塔与物流交互站原型由各自建筑域注册。
- Frag 是黑雾掉落并由基础解析塔转换为探索机会；Memory 来自高级塔献祭；通用原胚来自首遗物、探索、黑雾和高级交互繁育。
- 不注册黑雾样本、定向原胚或 Frag/Memory 面额包裹，不保留旧包裹折算。
- `itemValue` 是容量、物流耗能、研究与矩阵供养共享的唯一价值表。启动时在最终原型加载后计算一次，天然来源与必要基础值先行，再沿包括隐藏配方在内的普通生产路径推导，外部未解项最后统一取1。FE新增物品必须有独立基础价值或制造投入，不使用1兜底；丰碑弹药按最终投入定价。maxValue仅用于计算期间的未解项及不存在ID，运行使用方不自设另一套默认价格；配方资格及自动整理/回收使用显式物品类别。业务状态不得写入Items。
- `itemToMatrix` / 矩阵阶段 helper 只表达原版进度分类，不决定随机研究是否已解锁。
- `needs` 覆盖全部已注册实体物品，用于基础塔正面上传候选。
- 图标和选择窗口 patch 放 `Presentation`；亚空间库存放 `Logic/DataCenter`，具体内容探索与解析状态放 `Logic/Civilization`。
