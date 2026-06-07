# Save Data Exporter 存档数据导出

按配置的快捷键导出当前已载入存档中的星系/星球维度生产统计数据。

## 使用方式

1. 安装并启用本模组。
2. 进入游戏设置的按键绑定页面，为“导出存档统计”绑定快捷键。默认无按键，不会占用任何键位。
3. 载入一个存档后按下快捷键。主页或未载入存档时不会导出。
4. 默认输出目录是 `BepInEx/config/SaveDataExporter/`，文件名形如 `SaveDataExporter_<存档名>_yyyyMMdd_HHmmss.xlsx`。
5. 可在游戏设置的“杂项”页面切换输出文件命名模式。

## 配置项

- `TargetItems`：导出的目标物品，支持物品 ID 或物品名，用逗号分隔。默认 `1143,6006`，即增产剂 Mk.III 和宇宙矩阵。
- `TimeLevel`：统计周期，`0=1分钟`、`1=10分钟`、`2=1小时`、`3=10小时`、`4=100小时`。默认 `1`。
- `OutputDirectory`：输出目录。留空时使用默认目录。
- `OutputFileNameMode`：输出文件命名模式，可在游戏设置的“杂项”页面下拉切换。默认 `TimestampedNewFile`，每次导出生成 `SaveDataExporter_<存档名>_yyyyMMdd_HHmmss.xlsx`；可改为 `SaveNameOverwrite`，固定生成 `SaveDataExporter_<存档名>.xlsx`，同名文件会被覆盖替换。

## 导出内容

导出的 xlsx 包含两个 sheet：

- `星球信息导出模板1`：以恒星为行、每颗星球占一组列，适合单个目标物品快速查看。
- `星球信息导出模板2`：每行一颗星球，按目标物品展开实际产量、理论产量、实际消耗、理论消耗。

非“总计”统计周期按游戏统计面板口径导出每分钟速率；未建厂星球保留行但数值为 0。
