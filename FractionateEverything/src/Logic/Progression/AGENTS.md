# Logic/Progression — 进度域

进度域负责 FE 隐藏载体科技、原版堆叠科技投影、启动套件、教程和恢复指引元数据。

## Structure

```
Progression/
├── TechManager/          # 隐藏科技注册、矩阵进度、恢复状态和运行解锁 patch
├── Tutorials/            # G 键教程翻译、注册和窗口 patch
├── StackingManager.cs    # 原版堆叠科技到 FE 建筑运行上限的投影
└── InserterStackPatch.cs # 分拣器堆叠兼容 patch
```

## Rules

- 隐藏 `TechProto` 只作为 DSP 解锁、发放和兼容载体，不显示为普通主脑科技。
- 科技位置、启动套件和配方解锁载体放 `TechManager/Techs.cs`。
- 文明协议、检索和远古科技树业务放 `Logic/Civilization`，不得塞进隐藏科技管理器。
- 矩阵层研究进度与原版配方开放判断放 `TechManager/MatrixProgress.cs`。
- 教程正文翻译放 `Tutorials/TutorialTexts.cs`。
- 教程窗口 patch 放 `Tutorials/TutorialWindowPatch.cs`。
- 建筑堆叠能力只读取原版堆叠科技，不恢复 FE 旧建筑等级。
