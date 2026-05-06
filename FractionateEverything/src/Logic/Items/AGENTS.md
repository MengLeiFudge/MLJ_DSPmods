# Logic/Items — 物品域

物品域负责 FE 物品原型、物品价值、矩阵阶段分类，以及与物品展示相关的表现层 patch。

## Structure

```
Items/
├── ItemManager.cs          # AddTranslations、AddCoreItemsAndPrototypes、itemValue、矩阵阶段 helper
└── Presentation/           # IconSet、SignalPicker、SignalTagPicker patch
```

## Rules

- 新增 FE 物品原型注册放 `ItemManager.cs`。
- 物品价值或矩阵阶段分类放 `ItemManager.cs`，不要写到 UI 页面。
- 图标/信号选择窗口 patch 放 `Presentation`，不要放 `UI/Patches`。
- 数据中心库存不属于 Items，放 `Logic/DataCenter`。
