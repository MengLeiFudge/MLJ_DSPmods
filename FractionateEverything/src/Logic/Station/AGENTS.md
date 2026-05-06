# Logic/Station — 物流交互站域

本目录负责物流交互站与数据中心之间的运行交互，以及物流站窗口/总控面板相关 patch。

## Structure

```
Station/
├── StationManager/
│   ├── StationManager.cs   # 翻译入口、顶层说明、共享常量
│   ├── Runtime.cs          # 上传/下载同步、目标数量、电力消耗、运行 patch
│   ├── ModeState.cs        # 传输/容量模式状态和归一化
│   ├── StationWindow.cs    # 独立物流站窗口按钮和槽位交互
│   ├── ControlPanel.cs     # 总控面板窗口、槽位刷新、弹窗选项
│   ├── ControlPanel/
│   │   └── Inspector.cs    # 总控面板检查器布局和集装滑块
│   ├── OutputStackPatch.cs # 物流站输出堆叠上限 transpiler
│   ├── Save.cs             # 交互站模式存档读写
│   └── Shared.cs           # 按钮布局和共享 UI helper
└── ProliferatorPool.cs      # 交互站自动增产点池
```

## Files

- `ProliferatorPool.cs`：交互站自动增产点池。

## Rules

- 站点运行语义放 `Runtime`，不要写进 UI 文件。
- 独立物流站窗口和总控面板 patch 分别维护，除共享 helper 外不要互相塞逻辑。
- 数据中心库存数量本身归 `Logic/DataCenter`，本站只负责交互站如何访问它。
