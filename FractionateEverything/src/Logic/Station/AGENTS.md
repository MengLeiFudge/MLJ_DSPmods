# Logic/Station — 物流交互站域

本目录负责物流交互站与数据中心之间的运行交互，以及物流站窗口/总控面板相关 patch。

## Structure

```
Station/
├── Definitions/             # 物流交互站建筑定义
├── StationManager.cs        # 翻译入口、共享常量、存档读写
├── Runtime.cs               # 上传/下载同步、目标数量、电力消耗、运行 patch
├── ModeState.cs             # 传输/容量模式状态和归一化
├── WindowPatch.cs           # 独立物流站窗口按钮和槽位交互
├── ControlPanelPatch.cs     # 总控面板窗口、槽位刷新、检查器布局
└── OutputStackPatch.cs      # 物流站输出堆叠上限 transpiler
```

## Rules

- 站点运行语义放 `Runtime`，不要写进 UI 文件。
- 独立物流站窗口和总控面板 patch 分别维护，除共享 helper 外不要互相塞逻辑。
- 数据中心库存数量本身归 `Logic/DataCenter`，本站只负责交互站如何访问它。
- 物流交互站不维护建筑等级或自动增产点池；下载物品保留数据中心库存中原有的增产点。
