# Logic/Manager — 共享 Manager

`Manager` 不再是新增功能的默认位置。当前只保留尚未归域的共享 manager；不要新增迁移期门面或只负责转发旧调用面的 facade。

## Current Files

- `LabManager.cs`：实验室相关共享逻辑，暂未单独成域。
- `MonitorManager.cs`：监控/统计相关共享逻辑，引用建筑定义但不拥有建筑域。

## Rule

新增功能先判断归属：

- 建筑：`Logic/Buildings`
- 分馏配方/运行/状态/表现层：`Logic/Fractionation`
- 物流交互站：`Logic/Station`
- 数据中心库存/背包访问：`Logic/DataCenter`
- 物品原型/价值/矩阵阶段：`Logic/Items`
- 抽取：`Logic/Gacha`
- 经济：`Logic/Economy`
- 科技/教程：`Logic/Progression`
- 黑雾：`Logic/DarkFog`

只有确实跨域且暂时没有更清晰归属的共享入口，才允许放在本目录。
