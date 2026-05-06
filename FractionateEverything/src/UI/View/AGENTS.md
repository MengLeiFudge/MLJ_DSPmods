# UI/View — 功能面板层

当前 48 个 `.cs` 文件：根层 `MainWindow`/`MainWindowPageRegistry` + 6 个业务子域。核心变化：**双主面板架构**、**抽取成长页重构**，且符文页已移除。

## 入口与路由

- `MainWindow.cs`：
  - 面板状态机（`SelectedMainPanelType` / `OpenedMainPanelType`）
  - Legacy 与 Analysis 的打开/关闭/切换
  - 保存块 `MainPanelSelection`
  - 跨页导航 `NavigateToPage(category, index)`
- `MainWindowPageRegistry.cs`：
  - 分类顺序 `categoryOrder`
  - 页面注册 `allPages`
  - 面板过滤 `IsEnabledFor(panelType, sandboxMode)`

## 子目录索引（仅列变化敏感域）

| Dir | 文件数 | 关注点 |
|---|---:|---|
| `DrawGrowth/` | 7 | 抽取成长主域：抽奖、成长规划、聚焦、资源统筹与表现组件 |
| `CoreOperate/` | 9 | 配方/建筑操作主面板；`FracRecipeOperate*.cs` 按翻译、配置/存档、布局、刷新、等级列、产物展示拆分 |
| `ProgressTask/` | 8 | 主线/循环任务、成就系统；`Achievements*.cs` 按定义、翻译、状态、UI、奖励拆分 |
| `Archive/` | 8 | 统计、图鉴、开发日志；开发日志拆为目录、文本注册、状态、UI、存档 partial |
| `ResourceInteraction/` | 2 | 数据中心物品上传/下载与重要物品 |
| `Setting/` | 3 | 面板风格切换按钮与沙盒开关 |

## 新约定（必须）

1. 页面可见性由 `MainWindowPageRegistry` 控制，避免在 `MainWindow` 分散 if/else。
2. 跨“抽奖↔商店”跳转统一使用 `MainWindow.NavigateToPage`。
3. 需要跨面板共享的数据（如抽卡总次数）统一进 `MainWindow.SharedPanelState`。
4. `UpdateUI` 必须先判断页面可见性 + 当前主面板类型。
5. `UI/View/*` 颜色文本禁止硬编码，统一 `RichTextUtils`。
6. 成就页逻辑按 `Achievements*.cs` partial 边界维护：成就条件放 `Definitions`，翻译放 `Translations`，配置/存档放 `State`，布局刷新放 `UI`，奖励文本与对外加成查询放 `Rewards`。
7. 分馏配方页逻辑按 `FracRecipeOperate*.cs` partial 边界维护：文本注册放 `Translations`，配置/存档放 `State`，布局节点放 `Layout`，页面刷新入口放 `Refresh`，右侧等级列放 `LevelColumn`，产物/等效处理行放 `Products`。

## 反模式

- 继续把新页面写成 `MainWindow` 内硬编码注册（应放 `MainWindowPageRegistry`）
- 在子页面直接访问另一窗口实例（应通过 `MainWindow` 导航/状态接口）
- 在离屏页面持续刷新 UI（性能浪费 + 状态错乱）
