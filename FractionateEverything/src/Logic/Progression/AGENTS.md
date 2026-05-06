# Logic/Progression — 进度域

进度域负责科技、教程、指引和成就相关元数据。

## Structure

```
Progression/
├── TechManager/       # 科技注册、矩阵进度、配方基线、运行解锁 patch
└── TutorialManager/   # G 键指引教程元数据、翻译、注册、布局 patch、阅读进度
```

## Rules

- 科技树位置和科技奖励放 `TechManager/Techs.cs`。
- 矩阵层研究进度和原版配方增强开放判断放 `TechManager/MatrixProgress.cs`。
- 教程正文翻译放 `TutorialManager/Translations.cs`。
- 教程窗口 patch 放 `TutorialManager/LayoutPatch.cs`。
