# Logic/Station — 物流交互站域

本域负责行星/星际物流交互站原型、槽位模式、亚空间传输、建筑参数和原版物流站界面补丁。

## Structure

```
Station/
├── Definitions/             # 行星与星际物流交互站
├── StationManager.cs        # 翻译、配置、槽位模式存档聚合
├── Runtime.cs               # 阈值、部分传输、耗能与库存接线
├── ModeState.cs             # Sync/Upload/Download 和容量模式
├── StationParameterPatch.cs # Q 复制、复制粘贴、蓝图参数
├── WindowPatch.cs           # 单站窗口
├── ControlPanelPatch.cs     # 物流总控面板
└── OutputStackPatch.cs      # 输出堆叠投影
```

## Rules

- 站点运行语义只放 `Runtime`；窗口文件只读写公开模式/设置接口。
- 亚空间数量与增产点归 `DataCenterInventory`，本站不得维护镜像余额。
- 上传使用实际接收数扣槽位；容量竞争拒收的数量与增产点留在站内。
- 下载保留亚空间中的真实增产点，不创建自动喷涂池。
- 每槽按分配预算和单件耗能裁剪传输数量，最终只按实际搬运件数扣站内电量。
- 亚空间能效倍率由 `GrowthTechnologyManager` 提供，最低 0.5；本站不另存科技等级。
- 槽位传输模式、容量模式和参数格式必须继续支持 Q 键复制、复制粘贴、蓝图与旧物流站升级路径。
- 输出堆叠使用全局 `StackingManager` 状态，不维护站点等级或单站强化。
- 玩家可见文本使用“亚空间”；内部 `DataCenter` 标识可保留以维持稳定合同。
