# Logic/Progression — 进度域

进度域负责 FE 隐藏载体科技、原版堆叠科技投影、启动套件、教程和恢复指引元数据。

## Structure

```
Progression/
├── TechManager/          # 隐藏科技注册、矩阵进度、恢复状态和运行解锁 patch
├── Tutorials/            # G 键教程翻译、注册和窗口 patch
└── Stacking/             # 堆叠状态及分拣器、实验室、流速监控兼容 patch
    ├── StackingManager.cs
    ├── InserterStackPatch.cs
    ├── LabStackPatch.cs
    └── MonitorStackPatch.cs
```

## Rules

- 隐藏 `TechProto` 只作为 DSP 解锁、发放和兼容载体，不显示为普通主脑科技。
- 科技位置、启动套件和配方解锁载体放 `TechManager/Techs.cs`。
- 文明协议、检索和远古科技树业务放 `Logic/Civilization`，不得塞进隐藏科技管理器。
- 矩阵层研究进度与原版配方开放判断放 `TechManager/MatrixProgress.cs`。
- 教程正文翻译放 `Tutorials/TutorialTexts.cs`。
- 教程窗口 patch 放 `Tutorials/TutorialWindowPatch.cs`。
- 建筑堆叠能力只读取原版堆叠科技，不恢复 FE 旧建筑等级。
- 所有由 `StackingManager.CurrentMaxStack` 驱动的游戏组件兼容补丁统一放 `Stacking/`；不要重新建立无领域归属的 `Logic/Manager`。
