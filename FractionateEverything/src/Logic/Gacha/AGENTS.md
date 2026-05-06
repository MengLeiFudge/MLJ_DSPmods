# Logic/Gacha — 抽取域

抽取域负责抽卡状态、卡池、抽取执行、成长报价和图鉴加成。UI 页面只调用本域 API，不承载概率/保底规则。

## Structure

```
Gacha/
├── GachaManager.cs              # 保底、积分、UP 组轮换、状态持久化
├── GachaPool.cs                 # 池 ID、概率池、随机选择模型
├── GachaService/                # 构池、抽取、奖励、聚焦、成长报价、展示文本
└── GachaGalleryBonusManager.cs  # 图鉴完成度加成缓存
```

## Files

- `GachaManager.cs`：保底、积分、UP 组轮换、状态持久化。
- `GachaPool.cs`：池 ID、概率池、随机选择模型。
- `GachaService/`：构池、抽取、奖励、聚焦、成长报价、展示文本。
- `GachaGalleryBonusManager.cs`：图鉴完成度加成缓存。

## Rules

- 概率、保底、奖励解析只放 `GachaService`/`GachaManager`。
- UI 只能展示结果和触发命令。
- 新池 ID 必须先经过 `GachaPool.IsValidPoolId` 约束。
