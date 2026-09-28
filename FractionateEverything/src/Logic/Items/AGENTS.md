# Logic/Items — 物品域

本域负责 FE 物品原型、物品价值、矩阵阶段分类、基础塔上传候选，以及物品选择器/图标相关表现补丁。

## Structure

```
Items/
├── ItemManager.cs    # Frag、Memory、通用原胚、价值、矩阵和 needs
└── Presentation/     # 图标集、物品/信号选择器与标签选择器 patch
```

## Rules

- 3.0 正式核心物品只有 Frag、Memory 和通用原胚；五座塔与物流交互站原型由各自建筑域注册。
- Frag 是黑雾掉落并由基础解析塔转换为探索机会；Memory 来自高级塔献祭；通用原胚来自首遗物、探索、黑雾和高级交互繁育。
- 不注册黑雾样本、定向原胚或 Frag/Memory 面额包裹；旧包裹常量只供兼容折算。
- `itemValue` 是物流交互耗能、兼容路径和候选过滤的共享静态数据；业务状态不得写入 Items。
- `itemToMatrix` / 矩阵阶段 helper 只表达原版进度分类，不决定随机研究是否已解锁。
- `needs` 覆盖全部已注册实体物品并排除旧货币包裹，用于基础塔正面上传候选。
- 图标和选择窗口 patch 放 `Presentation`；亚空间库存放 `Logic/DataCenter`，研究资格放 `Logic/Civilization`。
